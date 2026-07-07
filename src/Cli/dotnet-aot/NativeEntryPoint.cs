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
using Microsoft.DotNet.Utilities;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli;

static unsafe partial class NativeEntryPoint
{
    /// <summary>
    ///  When set by the native entry point, AOT-capable commands use this instead of
    ///  discovering the dotnet root via PATH / environment probing.
    /// </summary>
    internal static string? DotnetRoot { get; set; }

    /// <summary>
    ///  The versioned SDK directory (the folder containing dotnet.dll, MSBuild.dll, Sdks\, ...),
    ///  resolved from the host-provided sdk_dir or by self-locating the dotnet-aot module. Also
    ///  published as the "Microsoft.DotNet.Sdk.Root" AppContext value (SdkPaths.DataName) so
    ///  compiled-in assemblies that probe AppContext.BaseDirectory can find the SDK. See
    ///  src/Cli/dotnet-aot/SdkRootResolution.md.
    /// </summary>
    internal static string? SdkDirectory { get; set; }

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

    // Guards one-time subscription of the "classic" TelemetryEventEntry pub/sub to the AOT
    // telemetry client. ExecuteCore runs once per process in production; the guard keeps repeated
    // in-process invocations (e.g. unit tests) from attaching duplicate handlers.
    private static bool s_telemetryEventsSubscribed;

    /// <summary>
    ///  Core execution logic, separated from native marshalling for testability.
    /// </summary>
    internal static int ExecuteCore(
        string hostPath, string dotnetRoot, string sdkDir,
        string hostfxrPath, string[] args)
    {
        // Publish the versioned SDK directory as the "Microsoft.DotNet.Sdk.Root" AppContext value
        // (SdkPaths.DataName) for the assemblies compiled into the AOT host (MSBuild, NuGet, the command
        // resolvers, ...) that otherwise probe AppContext.BaseDirectory - which under the NativeAOT muxer
        // is the install root, not the versioned SDK directory. Unlike an environment variable an
        // AppContext value is process-local and is not inherited by child processes. See
        // src/Cli/dotnet-aot/SdkRootResolution.md.
        //
        // Honor a value a caller already provided (e.g. a runtimeconfig configProperties entry): it is
        // authoritative, but it must point to a real directory - fail fast rather than handing the
        // compiled-in assemblies a bogus SDK root. Otherwise resolve it from the host-provided sdk_dir
        // (the muxer already resolved it to locate dotnet-aot), else self-locate the dotnet-aot module.
        string? sdkDirectory = AppContext.GetData(SdkPaths.DataName) as string;
        if (!string.IsNullOrEmpty(sdkDirectory))
        {
            if (!Directory.Exists(sdkDirectory))
            {
                Console.Error.WriteLine(string.Format(CliStrings.SdkRootDirectoryDoesNotExist, sdkDirectory, SdkPaths.DataName));
                return 1;
            }
        }
        else
        {
            sdkDirectory = SdkRootLocator.Resolve(sdkDir);
            if (!string.IsNullOrEmpty(sdkDirectory))
            {
                AppContext.SetData(SdkPaths.DataName, sdkDirectory);
            }
            else
            {
                Console.Error.WriteLine(CliStrings.SdkDirectoryCouldNotBeDetermined);
            }
        }

        SdkDirectory = string.IsNullOrEmpty(sdkDirectory) ? null : sdkDirectory;

        // Telemetry is best-effort and must never prevent the CLI from running. Initializing
        // it can fail on some layouts (e.g. the NativeAOT muxer cannot resolve the crypto
        // native library used to hash telemetry properties on macOS - see dotnet/sdk#54544),
        // so swallow any failure here and continue to the actual command.
        Activity? mainActivity = null;
        string? globalJsonState = null;
        try
        {
            DateTime preTelemetry = DateTime.UtcNow;
            // Initialize OTel telemetry (mirrors managed Program.cs setup). The client is stored in
            // the TelemetryClient property so AOT command actions (e.g. --cli-schema) can reuse it.
            TelemetryClient = new Telemetry.TelemetryClient(sessionId: null);

            // Route the "classic" hashed telemetry events (toplevelparser/command,
            // commandresolution/commandresolved, ...) into the AOT telemetry client, mirroring the
            // managed Program.cs setup. Without a subscriber these events - including the one
            // CompositeCommandResolver already raises during external command resolution - are
            // silently dropped on the AOT path. Subscribe at most once per process; the handler
            // routes to whichever TelemetryClient is current.
            if (!s_telemetryEventsSubscribed)
            {
                s_telemetryEventsSubscribed = true;
                TelemetryEventEntry.Subscribe((eventName, properties) => TelemetryClient?.TrackEvent(eventName, properties));
                TelemetryEventEntry.TelemetryFilter = new TelemetryFilter(Sha256Hasher.HashWithNormalizedCasing);
            }

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
                globalJsonState = NativeWrapper.NETCoreSdkResolverNativeWrapper.GetGlobalJsonState(Environment.CurrentDirectory);
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

            // Surface the host-provided dotnet root so AOT-capable commands (e.g. `sdk check`)
            // can use it instead of re-probing PATH / environment for the dotnet installation.
            DotnetRoot = string.IsNullOrEmpty(dotnetRoot) ? null : dotnetRoot;

            // Try the AOT-compiled path for supported commands (if enabled)
            if (EnvironmentVariableParser.ParseBool(Environment.GetEnvironmentVariable(EnvironmentVariableNames.DOTNET_CLI_ENABLEAOT), defaultValue: false))
            {
                ParseResult? parseResult = null;
                using (var parse = Activities.Source.StartActivity("parse"))
                {
                    parseResult = Parser.Parse(args);
                    mainActivity?.SetDisplayName(parseResult);
                }

                // Run the cross-cutting first-run experience (first-time-use notice, telemetry message,
                // ASP.NET dev cert, global-tools PATH, workload integrity check) before invoking the command
                // in-process, mirroring the managed CLI's Program.ProcessArgsAndExecute. On the AOT path this
                // returns false when first-run is still pending (the crypto/NuGet/MSBuild-dependent parts are
                // unavailable here), signalling that this invocation must be deferred to the managed CLI, which
                // performs the complete first-run exactly once. A failure is treated the same way - defer to
                // the managed CLI, which reproduces the exact behavior (and error reporting).
                bool firstRunCompleted;
                try
                {
                    firstRunCompleted = FirstRunExperience.Setup(parseResult);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"First-run setup failed in the AOT entry point; deferring to the managed CLI: {ex}");
                    firstRunCompleted = false;
                }

                if (firstRunCompleted)
                {
                    if (parseResult.CanBeInvoked())
                    {
                        try
                        {
                            // Invoke the built-in command in-process using the shared CommandInvocation
                            // helper, identical to the managed CLI: same exit-code handling, including the
                            // "new" command's 127 adjustment and Parser.ExceptionHandler. This keeps the
                            // two entry points in parity.
                            exitCode = CommandInvocation.ExecuteInternalCommand(parseResult);
                            success = true;
                            // The built-in command ran in-process (no managed fallback), so emit the same
                            // top-level parser telemetry the managed CLI sends from Program.ProcessArgsAndExecute.
                            SendAotParserTelemetry(parseResult, globalJsonState);
                            return exitCode;
                        }
                        catch (CommandNotAvailableInAotException)
                        {
                            // The parsed command requires the managed CLI — fall through to the managed fallback below.
                        }
                    }
                    // An unrecognized top-level token is either an external command (`dotnet ef`, a global
                    // or local tool, a command on the PATH, ...) or an implicit file-based app (`dotnet app.cs`).
                    // Resolve and invoke external commands in AOT when possible; defer file-based apps, legacy
                    // project tools, and anything that does not resolve to the managed CLI.
                    else if (parseResult is not null && TryInvokeExternalCommand(parseResult, args, sdkDirectory, mainActivity, globalJsonState, out exitCode, out success))
                    {
                        return exitCode;
                    }
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

            string dotnetDll = Path.Join(sdkDirectory, "dotnet.dll");
            string runtimeConfig = Path.Join(sdkDirectory, "dotnet.runtimeconfig.json");

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
            Console.Error.WriteLine(string.Format(CliStrings.ManagedFallbackCouldNotBeLocated, dotnetDll, runtimeConfig));
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
    private static bool TryInvokeExternalCommand(ParseResult parseResult, string[] args, string sdkDir, Activity? mainActivity, string? globalJsonState, out int exitCode, out bool success)
    {
        exitCode = 1;
        success = false;

        // File-based apps (`dotnet app.cs`) are re-dispatched by the managed CLI as `dotnet run --file`,
        // which requires the managed `run` command. Defer them to the managed CLI.
        if (parseResult.GetFileBasedAppEntryPointToken() is not null)
        {
            return false;
        }

        string? subCommandToken = parseResult.GetValue(Parser.RootCommand.DotnetSubCommand);
        string commandName = "dotnet-" + subCommandToken;
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

        // The parser only matched the top-level `dotnet` command, so the root span was named just
        // "dotnet". Now that we have committed to resolving and invoking this external command in the
        // AOT bubble, name the root span after the full command (e.g. "dotnet dev-certs") to match the
        // managed fallback path, which derives the same name from the first argument.
        string externalDisplayName = "dotnet " + subCommandToken;
        if (mainActivity is not null)
        {
            mainActivity.DisplayName = externalDisplayName;
            mainActivity.SetTag("command.name", externalDisplayName);
        }

        // Resolution succeeded, so this command will be invoked in-process and will not fall back to
        // the managed CLI. Emit the top-level parser telemetry now - before launching the (often
        // long-running) external process - so the async telemetry uploader has the child's entire
        // lifetime to flush, rather than racing process exit. The commandresolution/commandresolved
        // event was already raised during the resolution above.
        SendAotParserTelemetry(parseResult, globalJsonState);

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

    /// <summary>
    ///  Emits the "classic" filtered parser telemetry (e.g. <c>toplevelparser/command</c>) for a command
    ///  the AOT bridge handled in-process, mirroring the managed CLI's
    ///  <c>TelemetryEventEntry.SendFiltered(...)</c> call in <c>Program.ProcessArgsAndExecute</c>. Only call
    ///  this once the AOT path has committed to running the command itself: when the bridge instead falls
    ///  back to the managed CLI, that process emits these events, so emitting them here would double-count.
    /// </summary>
    private static void SendAotParserTelemetry(ParseResult parseResult, string? globalJsonState)
    {
        try
        {
            TelemetryEventEntry.SendFiltered(new ParseResultWithGlobalJsonState(parseResult, globalJsonState));
        }
        catch (Exception ex)
        {
            // Telemetry is best-effort and must never affect command execution or the exit code.
            Debug.WriteLine($"Failed to emit AOT parser telemetry: {ex}");
        }
    }
}
