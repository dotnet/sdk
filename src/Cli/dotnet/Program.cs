// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using Microsoft.DotNet.Cli.CommandFactory;
using Microsoft.DotNet.Cli.CommandFactory.CommandResolution;
using Microsoft.DotNet.Cli.Commands.Hidden.InternalReportInstallSuccess;
using Microsoft.DotNet.Cli.Commands.Workload;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.ShellShim;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.ProjectTools;
using Microsoft.DotNet.Utilities;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Frameworks;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using CommandResult = System.CommandLine.Parsing.CommandResult;

namespace Microsoft.DotNet.Cli;

public class Program
{
    // TODO: This is not used anymore, but required for the TelemetryFilter to parse the data including the globalJsonState.
    // To fix, the code and the tests for Filter in TelemetryFilter would need to be updated.
    private static readonly Dictionary<string, double> s_performanceData = [];
    private static readonly string s_toolPathSentinelFileName = $"{Product.Version}.toolpath.sentinel";

    private static readonly Activity? s_mainActivity;
    private static readonly DateTime s_mainTimeStamp;
    private static readonly PosixSignalRegistration s_sigIntRegistration;
    private static readonly PosixSignalRegistration s_sigQuitRegistration;
    private static readonly PosixSignalRegistration s_sigTermRegistration;
    private static string? s_globalJsonState;

    public static ITelemetryClient TelemetryInstance { get; private set; }

    static Program()
    {
        s_mainTimeStamp = DateTime.Now;
        s_sigIntRegistration = PosixSignalRegistration.Create(PosixSignal.SIGINT, Shutdown);
        s_sigQuitRegistration = PosixSignalRegistration.Create(PosixSignal.SIGQUIT, Shutdown);
        s_sigTermRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, Shutdown);

        (var s_parentActivityContext, var s_activityKind) = DeriveParentActivityContextFromEnv();
        s_mainActivity = Activities.Source.CreateActivity("main", s_activityKind, s_parentActivityContext);
        s_mainActivity
            ?.Start()
            ?.SetStartTime(Process.GetCurrentProcess().StartTime)
            ?.AddTag("process.pid", Process.GetCurrentProcess().Id)
            ?.AddTag("process.executable.name", "dotnet");
        TelemetryInstance = InitializeTelemetry();
        TrackHostStartup(TelemetryInstance, s_mainTimeStamp);
        SetupMSBuildEnvironmentInvariants();
    }

    public static int Main(string[] args)
    {
        // Register a handler for SIGTERM to allow graceful shutdown of the application on Unix.
        // See https://github.com/dotnet/docs/issues/46226.
        using var termSignalRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => Environment.Exit(0));

        using AutomaticEncodingRestorer _ = new();

        if (Env.GetEnvironmentVariable("DOTNET_CLI_CONSOLE_USE_DEFAULT_ENCODING") != "1")
        {
            // Setting output encoding is not available on those platforms
            if (UILanguageOverride.OperatingSystemSupportsUtf8())
            {
                Console.OutputEncoding = Encoding.UTF8;
            }
        }

        DebugHelper.HandleDebugSwitch(ref args);

        InitializeProcess();

        var exitCode = 1;
        try
        {
            exitCode = ProcessArgs(args);
            s_mainActivity?.AddTag("process.exit.code", exitCode)?.SetStatus(ActivityStatusCode.Ok);
            return exitCode;
        }
        catch (Exception e) when (e.ShouldBeDisplayedAsError())
        {
            Reporter.Error.WriteLine(CommandLoggingContext.IsVerbose
                ? e.ToString().Red().Bold()
                : e.Message.Red().Bold());

            if (e is CommandParsingException commandParsingException && commandParsingException.ParseResult != null)
            {
                commandParsingException.ParseResult.ShowHelp();
            }
            s_mainActivity?.AddTag("process.exit.code", exitCode)?.SetStatus(ActivityStatusCode.Error);
            return exitCode;
        }
        catch (Exception e) when (!e.ShouldBeDisplayedAsError())
        {
            // If telemetry object has not been initialized yet. It cannot be collected
            TelemetryEventEntry.SendFiltered(e);
            Reporter.Error.WriteLine(e.ToString().Red().Bold());
            s_mainActivity?.AddTag("process.exit.code", exitCode)?.SetStatus(ActivityStatusCode.Error);
            return exitCode;
        }
        finally
        {
            TelemetryInstance.TrackEvent("command/finish", new Dictionary<string, string?> { { "exitCode", exitCode.ToString() } }, null);

            Shutdown(default!);

            TelemetryClient.WriteLogIfNecessary();
        }
    }

    public static void Shutdown(PosixSignalContext context)
    {
        s_sigIntRegistration.Dispose();
        s_sigQuitRegistration.Dispose();
        s_sigTermRegistration.Dispose();
        s_mainActivity?.Stop();
        TelemetryClient.FlushProviders();
        Activities.Source.Dispose();
    }

    /// <summary>
    /// Uses the OpenTelemetrySDK's Propagation API to derive the parent activity context and kind
    /// from the DOTNET_CLI_TRACEPARENT and DOTNET_CLI_TRACESTATE environment variables.
    /// </summary>
    private static (System.Diagnostics.ActivityContext parentActivityContext, ActivityKind kind) DeriveParentActivityContextFromEnv()
    {
        var traceParent = Env.GetEnvironmentVariable(Activities.TRACEPARENT);
        var traceState = Env.GetEnvironmentVariable(Activities.TRACESTATE);

        if (string.IsNullOrEmpty(traceParent))
        {
            return (default, ActivityKind.Internal);
        }

        var carrierMap = new Dictionary<string, IEnumerable<string>?> { { "traceparent", [traceParent] } };
        if (!string.IsNullOrEmpty(traceState))
        {
            carrierMap.Add("tracestate", [traceState]);
        }

        // Use the OpenTelemetry Propagator to extract the parent activity context and kind. For some reason this isn't set by the OTel SDK like docs say it should be.
        Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator([
            new TraceContextPropagator(),
            new BaggagePropagator()
        ]));
        var parentActivityContext = Propagators.DefaultTextMapPropagator.Extract(default, carrierMap, GetValueFromCarrier);
        var kind = parentActivityContext.ActivityContext.IsRemote ? ActivityKind.Server : ActivityKind.Internal;

        return (parentActivityContext.ActivityContext, kind);

        static IEnumerable<string>? GetValueFromCarrier(Dictionary<string, IEnumerable<string>?> carrier, string key)
        {
            return carrier.TryGetValue(key, out var value) ? value : null;
        }
    }

    private static void TrackHostStartup(ITelemetryClient telemetryClient, DateTime mainTimeStamp)
    {
        var hostStartupActivity = Activities.Source.StartActivity("host-startup");
        hostStartupActivity?.SetStartTime(Process.GetCurrentProcess().StartTime);
        if (telemetryClient.Enabled && hostStartupActivity is not null)
        {
            // Get the global.json state to report in telemetry along with this command invocation.
            s_globalJsonState = NativeWrapper.NETCoreSdkResolverNativeWrapper.GetGlobalJsonState(Environment.CurrentDirectory);
            hostStartupActivity?.AddTag("dotnet.globalJson", s_globalJsonState);
        }
        hostStartupActivity?.SetEndTime(mainTimeStamp)
            ?.SetStatus(ActivityStatusCode.Ok)
            ?.Dispose();
    }

    /// <summary>
    /// We have some behaviors in MSBuild that we want to enforce (either when using MSBuild API or by shelling out to it),
    /// so we set those ASAP as globally as possible.
    /// </summary>
    private static void SetupMSBuildEnvironmentInvariants()
    {
        if (string.IsNullOrEmpty(Env.GetEnvironmentVariable("MSBUILDFAILONDRIVEENUMERATINGWILDCARD")))
        {
            Environment.SetEnvironmentVariable("MSBUILDFAILONDRIVEENUMERATINGWILDCARD", "1");
        }
    }

    private static string GetCommandName(ParseResult parseResult)
    {
        // Walk the parent command tree to find the top-level command name and get the full command name for this parseresult.
        List<string> parentNames = [parseResult.CommandResult.Command.Name];
        var current = parseResult.CommandResult.Parent;
        while (current is CommandResult parentCommandResult)
        {
            parentNames.Add(parentCommandResult.Command.Name);
            current = parentCommandResult.Parent;
        }
        parentNames.Reverse();

        // Options that perform terminating actions are considered part of the command name as they are essentially subcommands themselves.
        // Example: dotnet --version
        if (parseResult.Action is InvocableOptionAction { Terminating: true } optionAction)
        {
            parentNames.Add(optionAction.Option.Name);
        }

        return string.Join(' ', parentNames);
    }

    private static void SetDisplayName(Activity? activity, ParseResult parseResult)
    {
        if (activity == null)
        {
            return;
        }
        var name = GetCommandName(parseResult);

        // Set the display name to the full command name
        activity.DisplayName = name;

        // Set the command name as an attribute for better filtering in telemetry
        activity.SetTag("command.name", name);
    }

    internal static int ProcessArgs(string[] args)
    {
        ParseResult parseResult = ParseArgs(args);
        SetupDotnetFirstRun(parseResult);

        TelemetryEventEntry.SendFiltered(Tuple.Create(parseResult, s_performanceData, s_globalJsonState));

        if (parseResult.CanBeInvoked())
        {
            InvokeBuiltInCommand(parseResult, out var exitCode);
            return exitCode;
        }
        else
        {
            try
            {
                return LookupAndExecuteCommand(args, parseResult);
            }
            catch (CommandUnknownException e)
            {
                Reporter.Error.WriteLine(e.Message.Red());
                Reporter.Output.WriteLine(e.InstructionMessage);
                return 1;
            }
        }
    }

    private static int LookupAndExecuteCommand(string[] args, ParseResult parseResult)
    {
        var lookupExternalCommandActivity = Activities.Source.StartActivity("lookup-external-command");
        string commandName = "dotnet-" + parseResult.GetValue(Parser.RootCommand.DotnetSubCommand);
        var resolvedCommandSpec = CommandResolver.TryResolveCommandSpec(
            new DefaultCommandResolverPolicy(),
            commandName,
            args.GetSubArguments(),
            FrameworkConstants.CommonFrameworks.NetStandardApp15);
        lookupExternalCommandActivity?.Dispose();

        if (resolvedCommandSpec is null && TryRunFileBasedApp(parseResult) is { } fileBasedAppExitCode)
        {
            lookupExternalCommandActivity?.Dispose();
            return fileBasedAppExitCode;
        }
        else
        {
            var resolvedCommand = CommandFactoryUsingResolver.CreateOrThrow(commandName, resolvedCommandSpec);
            lookupExternalCommandActivity?.Dispose();

            using var _executionActivity = Activities.Source.StartActivity("execute-extensible-command");
            var result = resolvedCommand.Execute();
            return result.ExitCode;
        }
    }

    private static void InvokeBuiltInCommand(ParseResult parseResult, out int exitCode)
    {
        Debug.Assert(parseResult.CanBeInvoked());
        using var _invocationActivity = Activities.Source.StartActivity("invocation");
        try
        {
            exitCode = parseResult.Invoke();
            exitCode = AdjustExitCode(parseResult, exitCode);
        }
        catch (Exception exception)
        {
            exitCode = Parser.ExceptionHandler(exception, parseResult);
        }
    }

    private static int? TryRunFileBasedApp(ParseResult parseResult)
    {
        // If we didn't match any built-in commands, and a C# file path is the first argument,
        // parse as `dotnet run file.cs ..rest_of_args` instead.
        if (parseResult.CommandResult.Command is RootCommand
            && parseResult.GetResult(Parser.RootCommand.DotnetSubCommand) is { Tokens: [{ Type: TokenType.Argument, Value: { } } unmatchedCommandOrFile] }
            && VirtualProjectBuilder.IsValidEntryPointPath(unmatchedCommandOrFile.Value))
        {
            List<string> otherTokens = new(parseResult.Tokens.Count - 1);
            foreach (var token in parseResult.Tokens)
            {
                if (token.Type != TokenType.Argument || token != unmatchedCommandOrFile)
                {
                    otherTokens.Add(token.Value);
                }
            }

            parseResult = Parser.Parse(["run", "--file", unmatchedCommandOrFile.Value, .. otherTokens]);

            InvokeBuiltInCommand(parseResult, out var exitCode);
            return exitCode;
        }

        return null;
    }

    private static ITelemetryClient InitializeTelemetry()
    {
        var telemetryClient = new TelemetryClient();
        TelemetryEventEntry.Subscribe(telemetryClient.TrackEvent);
        TelemetryEventEntry.TelemetryFilter = new TelemetryFilter(Sha256Hasher.HashWithNormalizedCasing);

        if (CommandLoggingContext.IsVerbose)
        {
            Console.WriteLine($"Telemetry is: {(telemetryClient.Enabled ? "Enabled" : "Disabled")}");
        }

        return telemetryClient;
    }

    private static ParseResult ParseArgs(string[] args)
    {
        ParseResult parseResult;
        using (var _parseActivity = Activities.Source.StartActivity("parse"))
        {
            parseResult = Parser.Parse(args);

            // Avoid create temp directory with root permission and later prevent access in non sudo
            // This method need to be run very early before temp folder get created
            // https://github.com/dotnet/sdk/issues/20195
            SudoEnvironmentDirectoryOverride.OverrideEnvironmentVariableToTmp(parseResult);
        }

        SetDisplayName(s_mainActivity, parseResult);

        return parseResult;
    }

    private static void SetupDotnetFirstRun(ParseResult parseResult)
    {
        using var _ = Activities.Source.StartActivity("first-time-use");
        IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel = new FirstTimeUseNoticeSentinel();
        IAspNetCertificateSentinel aspNetCertificateSentinel = new AspNetCertificateSentinel();
        string toolPath = Path.Combine(CliFolderPathCalculator.DotnetUserProfileFolderPath, s_toolPathSentinelFileName);
        IFileSentinel toolPathSentinel = new FileSentinel(new FilePath(toolPath));

        var environmentProvider = new EnvironmentProvider();
        bool generateAspNetCertificate = environmentProvider.GetEnvironmentVariableAsBool(EnvironmentVariableNames.DOTNET_GENERATE_ASPNET_CERTIFICATE, defaultValue: true);
        bool telemetryOptout = environmentProvider.GetEnvironmentVariableAsBool(EnvironmentVariableNames.TELEMETRY_OPTOUT, defaultValue: CompileOptions.TelemetryOptOutDefault);
        bool addGlobalToolsToPath = environmentProvider.GetEnvironmentVariableAsBool(EnvironmentVariableNames.DOTNET_ADD_GLOBAL_TOOLS_TO_PATH, defaultValue: true);
        bool nologo = environmentProvider.GetEnvironmentVariableAsBool(EnvironmentVariableNames.DOTNET_NOLOGO, defaultValue: false);
        bool skipWorkloadIntegrityCheck = environmentProvider.GetEnvironmentVariableAsBool(EnvironmentVariableNames.DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK,
            // Default the workload integrity check skip to true if the command is being ran in CI. Otherwise, false.
            defaultValue: new CIEnvironmentDetectorForTelemetry().IsCIEnvironment());

        ReportDotnetHomeUsage(environmentProvider);

        var isDotnetBeingInvokedFromNativeInstaller = false;
        if (parseResult.CommandResult.Command is InternalReportInstallSuccessCommandDefinition)
        {
            aspNetCertificateSentinel = new NoOpAspNetCertificateSentinel();
            firstTimeUseNoticeSentinel = new NoOpFirstTimeUseNoticeSentinel();
            toolPathSentinel = new NoOpFileSentinel(exists: false);
            isDotnetBeingInvokedFromNativeInstaller = true;
        }

        var dotnetFirstRunConfiguration = new DotnetFirstRunConfiguration(
            generateAspNetCertificate: generateAspNetCertificate,
            telemetryOptout: telemetryOptout,
            addGlobalToolsToPath: addGlobalToolsToPath,
            nologo: nologo,
            skipWorkloadIntegrityCheck: skipWorkloadIntegrityCheck);

        string[] getStarOperators = ["getProperty", "getItem", "getTargetResult"];
        char[] switchIndicators = ['-', '/'];
        var getStarOptionPassed = parseResult.CommandResult.Tokens.Any(t =>
            getStarOperators.Any(o =>
            switchIndicators.Any(i => t.Value.StartsWith(i + o, StringComparison.OrdinalIgnoreCase))));

        ConfigureDotNetForFirstTimeUse(
            firstTimeUseNoticeSentinel,
            aspNetCertificateSentinel,
            toolPathSentinel,
            isDotnetBeingInvokedFromNativeInstaller,
            dotnetFirstRunConfiguration,
            environmentProvider,
            skipFirstTimeUseCheck: getStarOptionPassed);
    }

    private static int AdjustExitCode(ParseResult parseResult, int exitCode)
    {
        if (parseResult.Errors.Count > 0)
        {
            var commandResult = parseResult.CommandResult;

            while (commandResult is not null)
            {
                if (commandResult.Command.Name == "new")
                {
                    // default parse error exit code is 1
                    // for the "new" command and its subcommands it needs to be 127
                    return 127;
                }

                commandResult = commandResult.Parent as CommandResult;
            }
        }

        return exitCode;
    }

    private static void ReportDotnetHomeUsage(IEnvironmentProvider provider)
    {
        var home = provider.GetEnvironmentVariable(CliFolderPathCalculator.DotnetHomeVariableName);
        if (string.IsNullOrEmpty(home))
        {
            return;
        }

        Reporter.Verbose.WriteLine(string.Format(LocalizableStrings.DotnetCliHomeUsed, home, CliFolderPathCalculator.DotnetHomeVariableName));
    }

    private static void ConfigureDotNetForFirstTimeUse(
       IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel,
       IAspNetCertificateSentinel aspNetCertificateSentinel,
       IFileSentinel toolPathSentinel,
       bool isDotnetBeingInvokedFromNativeInstaller,
       DotnetFirstRunConfiguration dotnetFirstRunConfiguration,
       IEnvironmentProvider environmentProvider,
       bool skipFirstTimeUseCheck)
    {
        var isFirstTimeUse = !firstTimeUseNoticeSentinel.Exists() && !skipFirstTimeUseCheck;
        var environmentPath = EnvironmentPathFactory.CreateEnvironmentPath(isDotnetBeingInvokedFromNativeInstaller, environmentProvider);
        _ = new DotNetCommandFactory(alwaysRunOutOfProc: true);
        var aspnetCertificateGenerator = new AspNetCoreCertificateGenerator();
        var reporter = Reporter.Error;
        var dotnetConfigurer = new DotnetFirstTimeUseConfigurer(
            firstTimeUseNoticeSentinel,
            aspNetCertificateSentinel,
            aspnetCertificateGenerator,
            toolPathSentinel,
            dotnetFirstRunConfiguration,
            reporter,
            environmentPath,
            skipFirstTimeUseCheck: skipFirstTimeUseCheck);

        dotnetConfigurer.Configure();

#if !DOT_NET_BUILD_FROM_SOURCE
        if (isDotnetBeingInvokedFromNativeInstaller && OperatingSystem.IsWindows())
        {
            DotDefaultPathCorrector.Correct();
        }
#endif

        if (isFirstTimeUse && !dotnetFirstRunConfiguration.SkipWorkloadIntegrityCheck)
        {
            try
            {
                WorkloadIntegrityChecker.RunFirstUseCheck(reporter);
            }
            catch (Exception)
            {
                // If the workload check fails for any reason, we want to eat the failure and continue running the command.
                reporter.WriteLine(CliStrings.WorkloadIntegrityCheckError.Yellow());
            }
        }
    }

    private static void InitializeProcess()
    {
        // By default, .NET Core doesn't have all code pages needed for Console apps.
        // See the .NET Core Notes: https://docs.microsoft.com/dotnet/api/system.diagnostics.process#-notes
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        UILanguageOverride.Setup();
    }
}
