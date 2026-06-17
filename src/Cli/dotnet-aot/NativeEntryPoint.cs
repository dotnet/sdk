// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.CommandFactory;
using Microsoft.DotNet.Cli.CommandFactory.CommandResolution;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.DotNet.Cli.Telemetry;
using System.CommandLine;
using System.Diagnostics;
using Microsoft.DotNet.NativeWrapper;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli;

static unsafe partial class NativeEntryPoint
{
    [UnmanagedCallersOnly(EntryPoint = "dotnet_execute")]
    static int Execute(
        nint hostPathPtr,      // const char_t* host_path
        nint dotnetRootPtr,    // const char_t* dotnet_root
        nint sdkDirPtr,        // const char_t* sdk_dir
        nint hostfxrPathPtr,   // const char_t* hostfxr_path
        int argc,              // int argc (user args, no dotnet exe)
        nint argvPtr)          // const char_t** argv
    {
        string hostPath = PlatformStringMarshaller.ConvertToManaged(hostPathPtr) ?? string.Empty;
        string dotnetRoot = PlatformStringMarshaller.ConvertToManaged(dotnetRootPtr) ?? string.Empty;
        string sdkDir = PlatformStringMarshaller.ConvertToManaged(sdkDirPtr) ?? string.Empty;
        string hostfxrPath = PlatformStringMarshaller.ConvertToManaged(hostfxrPathPtr) ?? string.Empty;

        string[] args = new string[argc];
        nint* argv = (nint*)argvPtr;
        for (int i = 0; i < argc; i++)
        {
            args[i] = PlatformStringMarshaller.ConvertToManaged(argv[i]) ?? string.Empty;
        }

        return ExecuteCore(hostPath, dotnetRoot, sdkDir, hostfxrPath, args);
    }

    public static ITelemetryClient? TelemetryClient { get; private set; }

    /// <summary>
    ///  Core execution logic, separated from native marshalling for testability.
    /// </summary>
    internal static int ExecuteCore(
        string hostPath, string dotnetRoot, string sdkDir,
        string hostfxrPath, string[] args)
    {
        // Telemetry is best-effort and must never prevent the CLI from running. Initializing
        // it can fail on some layouts (e.g. the NativeAOT muxer cannot resolve the crypto
        // native library used to hash telemetry properties on macOS - see dotnet/sdk#54544),
        // so swallow any failure here and continue to the actual command.
        Activity? mainActivity = null;
        try
        {
            DateTime preTelemetry = DateTime.UtcNow;
            // Initialize OTel telemetry (mirrors managed Program.cs setup). The client is stored in
            // the TelemetryClient property so AOT command actions (e.g. --cli-schema) can reuse it.
            TelemetryClient = new Telemetry.TelemetryClient(sessionId: null);
            DateTime postTelemetry = DateTime.UtcNow;
            mainActivity = Activities.Source.StartActivity("main", Telemetry.TelemetryClient.ActivityKind, Telemetry.TelemetryClient.ParentActivityContext);

            // Backdate the activity start to process start time for accurate timing.
            if (mainActivity is not null)
            {
                mainActivity.SetStartTime(Process.GetCurrentProcess().StartTime.ToUniversalTime());
                mainActivity.AddTag("process.pid", Process.GetCurrentProcess().Id);
                mainActivity.AddTag("process.executable.name", "dotnet");
                mainActivity.AddTag("cli.runtime", "aot");
                using var telemetryActivity = Activities.Source.StartActivity("telemetry-setup");
                telemetryActivity?.SetStartTime(preTelemetry);
                telemetryActivity?.SetEndTime(postTelemetry);
            }

            // Capture global.json state for telemetry (mirrors managed Program.cs)
            if (TelemetryClient?.Enabled ?? false)
            {
                var globalJsonState = NativeWrapper.NETCoreSdkResolverNativeWrapper.GetGlobalJsonState(Environment.CurrentDirectory);
                mainActivity?.AddTag("dotnet.globalJson", globalJsonState);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize telemetry in the native entry point: {ex}");
        }

        int exitCode = 1;
        bool success = false;

        try
        {
            // Make hostfxr discoverable for NativeWrapper P/Invokes (required on non-Windows)
            if (!string.IsNullOrEmpty(hostfxrPath))
            {
                AppContext.SetData("HOSTFXR_PATH", hostfxrPath);
            }

            // Try the AOT-compiled path for supported commands (if enabled)
            if (EnvironmentVariableParser.ParseBool(Environment.GetEnvironmentVariable(EnvironmentVariableNames.DOTNET_CLI_ENABLEAOT), defaultValue: false))
            {
                ParseResult? parseResult = null;
                using (var parse = Activities.Source.StartActivity("parse"))
                {
                    parseResult = Parser.Parse(args);
                    mainActivity?.SetDisplayName(parseResult);
                }

                if (parseResult.CanBeInvoked())
                {
                    using var invoke = Activities.Source.StartActivity("invocation");
                    try
                    {
                        exitCode = Parser.Invoke(parseResult);
                        success = true;
                        return exitCode;
                    }
                    catch (CommandNotAvailableInAotException)
                    {
                        // The parsed command requires the managed CLI — fall through to the managed fallback below.
                    }
                    catch (Exception ex)
                    {
                        invoke?.SetStatus(ActivityStatusCode.Error);
                        invoke?.AddException(ex);
                        exitCode = Parser.ExceptionHandler(ex, parseResult);
                        success = false;
                        return exitCode;
                    }
                }
                // An unrecognized top-level token is either an external command (`dotnet ef`, a global
                // or local tool, a command on the PATH, ...) or an implicit file-based app (`dotnet app.cs`).
                // Resolve and invoke external commands in AOT when possible; defer file-based apps, legacy
                // project tools, and anything that does not resolve to the managed CLI.
                else if (TryInvokeExternalCommand(parseResult, args, sdkDir, out exitCode, out success))
                {
                    success = true;
                    return exitCode;
                }
            }

            // Fall back to the fully managed dotnet CLI by hosting .NET.
            // Set a best-effort display name from args when we have not done a full parse
            // (i.e. DOTNET_CLI_ENABLEAOT was not set or the command fell through without calling SetDisplayName).
            if (mainActivity is not null && mainActivity.DisplayName == "main")
            {
                var fallbackName = args.Length > 0 ? $"dotnet {args[0]}" : "dotnet";
                mainActivity.DisplayName = fallbackName;
                mainActivity.SetTag("command.name", fallbackName);
            }

            string dotnetDll = Path.Join(sdkDir, "dotnet.dll");
            string runtimeConfig = Path.Join(sdkDir, "dotnet.runtimeconfig.json");

            if (File.Exists(dotnetDll) && File.Exists(runtimeConfig))
            {
                // Use the command-line hosting path to run dotnet.dll
                string[] appArgs = new string[args.Length + 1];
                appArgs[0] = dotnetDll;
                Array.Copy(args, 0, appArgs, 1, args.Length);
                exitCode = ManagedHost.RunApp(hostPath, dotnetRoot, hostfxrPath, appArgs);
                success = true;
                return exitCode;
            }

            // No managed fallback available
            Console.Error.WriteLine($"The managed fallback could not be located. Expected '{dotnetDll}' and '{runtimeConfig}'.");
            return exitCode;
        }
        finally
        {
            mainActivity?.AddTag("process.exit.code", exitCode);
            mainActivity?.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
            mainActivity?.Stop();
            Telemetry.TelemetryClient.FlushProviders();
        }
    }

    /// <summary>
    ///  Attempts to resolve and invoke an external/tool command (e.g. <c>dotnet ef</c>, a global or
    ///  local tool, or a command on the PATH) entirely within AOT, mirroring the managed CLI's
    ///  <c>Program.ExecuteExternalCommand</c>. Returns <see langword="true"/> when the command was
    ///  resolved and executed (with its exit code in <paramref name="exitCode"/>); returns
    ///  <see langword="false"/> to signal that the invocation must be handled by the managed CLI -
    ///  file-based apps (<c>dotnet app.cs</c>), legacy project tools, commands that do not resolve in
    ///  AOT, or any resolution error.
    /// </summary>
    private static bool TryInvokeExternalCommand(ParseResult parseResult, string[] args, string sdkDir, out int exitCode, out bool success)
    {
        exitCode = 1;
        success = false;

        // File-based apps (`dotnet app.cs`) are re-dispatched by the managed CLI as `dotnet run --file`,
        // which requires the managed `run` command. Defer them to the managed CLI.
        if (parseResult.GetFileBasedAppEntryPointToken() is not null)
        {
            return false;
        }

        string commandName = "dotnet-" + parseResult.GetValue(Parser.RootCommand.DotnetSubCommand);
        CommandSpec? commandSpec;
        using (var lookupActivity = Activities.Source.StartActivity("lookup-external-command"))
        {
            lookupActivity?.AddTag("command.name", commandName);
            lookupActivity?.AddTag("sdk.root", sdkDir);
            try
            {
                commandSpec = CommandResolver.TryResolveCommandSpec(
                    new DefaultCommandResolverPolicy(),
                    commandName,
                    args.GetSubArguments(),
                    FrameworkConstants.CommonFrameworks.NetStandardApp15,
                    sdkRoot: sdkDir);
            }
            catch (Exception ex)
            {
                lookupActivity?.AddException(ex);
                // Resolution is side-effect free, so on any failure defer to the managed CLI, which has
                // the full resolver set (including project tools) and reports errors consistently.
                Debug.WriteLine($"AOT external command resolution failed; falling back to managed CLI: {ex}");
                return false;
            }
        }

        // The AOT resolver set is a subset of the managed one (it omits the MSBuild/NuGet-based project
        // tools resolver). When nothing resolves, defer to the managed CLI so it can resolve a project
        // tool or report the unknown-command error exactly as it would without the AOT fast path.
        if (commandSpec is null)
        {
            return false;
        }

        using (var invoke = Activities.Source.StartActivity("execute-extensible-command"))
        {
            try
            {
                Microsoft.DotNet.Cli.Utils.Command command = CommandFactoryUsingResolver.CreateOrThrow(commandName, commandSpec);
                exitCode = command.Execute().ExitCode;
                success = true;
                return true;
            }
            catch (Exception ex)
            {
                // The command was resolved and may have already executed, so it must not be re-run via
                // the managed CLI. Report the failure exactly as the managed invocation path would.
                invoke?.SetStatus(ActivityStatusCode.Error);
                invoke?.AddException(ex);
                exitCode = Parser.ExceptionHandler(ex, parseResult);
                success = false;
                return true;
            }
        }
    }
}
