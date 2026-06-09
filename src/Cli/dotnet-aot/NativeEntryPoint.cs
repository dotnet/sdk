// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.DotNet.Cli.Telemetry;
using System.CommandLine;
using System.Diagnostics;
using Microsoft.DotNet.NativeWrapper;

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
            mainActivity = Activities.Source.StartActivity("native-entrypoint", Telemetry.TelemetryClient.ActivityKind, Telemetry.TelemetryClient.ParentActivityContext);

            // Backdate the activity start to process start time for accurate timing.
            if (mainActivity is not null)
            {
                mainActivity.SetStartTime(Process.GetCurrentProcess().StartTime.ToUniversalTime());
                using var telemetryActivity = Activities.Source.StartActivity("aot-telemetry-setup");
                telemetryActivity?.SetStartTime(preTelemetry);
                telemetryActivity?.SetEndTime(postTelemetry);
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
                using (var parse = Activities.Source.StartActivity("aot-parsing"))
                {
                    try
                    {
                        parseResult = Parser.Parse(args);
                        mainActivity?.SetDisplayName(parseResult);
                    }
                    catch (Exception ex)
                    {
                        // The full command tree is shared with the managed CLI, so a command-specific parser or
                        // validator may run during Parse that is only fully supported there. Rather than surface
                        // an AOT failure, treat any unexpected error while parsing as a signal to fall back to the
                        // managed CLI, which will re-parse and handle the command (or report the error). This catch
                        // is intentionally scoped to Parse only — invocation failures must not be masked here, since
                        // doing so could re-execute a command that already ran (and had side effects) in AOT.
                        parse?.SetStatus(ActivityStatusCode.Error);
                        parse?.AddException(ex);
                        parseResult = null;
                    }
                }

                if (parseResult is not null
                    && parseResult.Errors.Count == 0
                    // An implicit file-based app invocation (e.g. `dotnet app.cs`) is run by the managed
                    // CLI's run pipeline. The shared parser only sees the path as an unmatched root
                    // argument, so defer to the managed fallback below instead of printing root usage.
                    && parseResult.GetFileBasedAppEntryPointToken() is null)
                {
                    using var invoke = Activities.Source.StartActivity("aot-invocation");
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
            }

            // Fall back to the fully managed dotnet CLI by hosting .NET
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
}
