// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using Microsoft.DotNet.Cli.CommandFactory;
using Microsoft.DotNet.Cli.CommandFactory.CommandResolution;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Workload;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.ShellShim;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.DotNet.Configurer;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Frameworks;
using CommandResult = System.CommandLine.Parsing.CommandResult;

namespace Microsoft.DotNet.Cli;

public class Program
{
    private static readonly string s_toolPathSentinelFileName = $"{Product.Version}.toolpath.sentinel";
    public static ITelemetry TelemetryClient { get; }
    internal static PerformanceLogEventListener? performanceLogEventListener;
    private static Dictionary<string, double> performanceData = [];
    private static readonly Activity? s_mainActivity;
    private static readonly DateTime s_mainTimeStamp;

    static Program()
    {
        s_mainTimeStamp = DateTime.Now;
        s_mainActivity = Activities.Source.CreateActivity("main", kind: ActivityKind.Internal);
        s_mainActivity?.SetStartTime(Process.GetCurrentProcess().StartTime);
        TrackHostStartup(s_mainTimeStamp);
        SetupMSBuildEnvironmentInvariants();
        bool perfLogEnabled = Env.GetEnvironmentVariableAsBool("DOTNET_CLI_PERF_LOG", false);
        // Avoid create temp directory with root permission and later prevent access in non sudo
        if (SudoEnvironmentDirectoryOverride.IsRunningUnderSudo())
        {
            perfLogEnabled = false;
        }
        if (perfLogEnabled)
        {
            PerformanceLogManager.InitializeAndStartCleanup(FileSystemWrapper.Default);
            performanceLogEventListener = PerformanceLogEventListener.Create(FileSystemWrapper.Default, PerformanceLogManager.Instance.CurrentLogDirectory);
        }
        else
        {
            performanceLogEventListener = null;
        }
        TelemetryClient = InitializeTelemetry();

    }

    public static int Main(string[] args)
    {
        using AutomaticEncodingRestorer _encodingRestorer = new();
        TimeSpan startupTime = s_mainTimeStamp - Process.GetCurrentProcess().StartTime;
        performanceData.Add("Startup Time", startupTime.TotalMilliseconds);

        // Setting output encoding is not available on those platforms
        if (UILanguageOverride.OperatingSystemSupportsUtf8())
        {
            Console.OutputEncoding = Encoding.UTF8;
        }

        DebugHelper.HandleDebugSwitch(ref args);
        PerformanceLogStartupInformation? startupInfo = null;
        if (performanceLogEventListener != null)
        {
            startupInfo = new PerformanceLogStartupInformation(s_mainTimeStamp);
        }
        try
        {
            PerformanceLogEventSource.Log.LogStartUpInformation(startupInfo);
            PerformanceLogEventSource.Log.CLIStart();
            InitializeProcess();
            return ProcessArgs(args);
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

            return 1;
        }
        catch (Exception e) when (!e.ShouldBeDisplayedAsError())
        {
            // If telemetry object has not been initialized yet. It cannot be collected
            TelemetryEventEntry.SendFiltered(e);
            Reporter.Error.WriteLine(e.ToString().Red().Bold());

            return 1;
        }
        finally
        {
            PerformanceLogEventSource.Log.TelemetryClientFlushStart();
            TelemetryClient.Flush();
            PerformanceLogEventSource.Log.TelemetryClientFlushStop();
            PerformanceLogEventSource.Log.CLIStop();
            Shutdown();
        }
    }

    public static void Shutdown()
    {
        s_mainActivity?.Stop();
        performanceLogEventListener?.Dispose();
        Activities.Source.Dispose();
    }

    private static void TrackHostStartup(DateTime mainTimeStamp)
    {
        using var hostStartupActivity = Activities.Source.StartActivity("host-startup");
        hostStartupActivity?.SetStartTime(Process.GetCurrentProcess().StartTime);
        hostStartupActivity?.SetEndTime(mainTimeStamp);
        hostStartupActivity?.SetStatus(ActivityStatusCode.Ok);
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

    private static string GetCommandName(ParseResult r)
    {
        if (r.Action is Parser.PrintVersionAction)
        {
            // If the action is PrintVersionAction, we return the command name as "dotnet --version"
            return "dotnet --version";
        }
        else if (r.Action is Parser.PrintInfoAction)
        {
            // If the action is PrintHelpAction, we return the command name as "dotnet --help"
            return "dotnet --info";
        }

        // Walk the parent command tree to find the top-level command name and get the full command name for this parseresult.
        List<string> parentNames = [r.CommandResult.Command.Name];
        var current = r.CommandResult.Parent;
        while (current is CommandResult parentCommandResult)
        {
            parentNames.Add(parentCommandResult.Command.Name);
            current = parentCommandResult.Parent;
        }
        parentNames.Reverse();
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

        if (parseResult.CanBeInvoked())
        {
            PerformanceLogEventSource.Log.TelemetrySaveIfEnabledStart();
            TelemetryEventEntry.SendFiltered(Tuple.Create(parseResult, performanceData, GetGlobalJsonState()));
            PerformanceLogEventSource.Log.TelemetrySaveIfEnabledStop();
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

    private static string? GetGlobalJsonState()
    {
        if (TelemetryClient.Enabled)
        {
            // Get the global.json state to report in telemetry along with this command invocation.
            // We don't care about the actual SDK resolution, just the global.json information,
            // so just pass empty string as executable directory for resolution.
            NativeWrapper.SdkResolutionResult result = NativeWrapper.NETCoreSdkResolverNativeWrapper.ResolveSdk(string.Empty, Environment.CurrentDirectory);
            return result.GlobalJsonState;
        }

        return null;
    }

    private static int LookupAndExecuteCommand(string[] args, ParseResult parseResult)
    {
        var lookupExternalCommandActivity = Activities.Source.StartActivity("lookup-external-command");
        PerformanceLogEventSource.Log.ExtensibleCommandResolverStart();
        string commandName = "dotnet-" + parseResult.GetValue(Parser.DotnetSubCommand);
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
            PerformanceLogEventSource.Log.ExtensibleCommandResolverStop();
            lookupExternalCommandActivity?.Dispose();

            PerformanceLogEventSource.Log.ExtensibleCommandStart();
            using var _executionActivity = Activities.Source.StartActivity("execute-extensible-command");
            var result = resolvedCommand.Execute();
            PerformanceLogEventSource.Log.ExtensibleCommandStop();
            return result.ExitCode;
        }
    }

    private static void InvokeBuiltInCommand(ParseResult parseResult, out int exitCode)
    {
        Debug.Assert(parseResult.CanBeInvoked());
        using var _invocationActivity = Activities.Source.StartActivity("invocation");
        PerformanceLogEventSource.Log.TelemetrySaveIfEnabledStart();
        TelemetryEventEntry.SendFiltered(Tuple.Create(parseResult, performanceData, GetGlobalJsonState()));
        PerformanceLogEventSource.Log.TelemetrySaveIfEnabledStop();
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
            && parseResult.GetValue(Parser.DotnetSubCommand) is { } unmatchedCommandOrFile
            && VirtualProjectBuildingCommand.IsValidEntryPointPath(unmatchedCommandOrFile))
        {
            List<string> otherTokens = new(parseResult.Tokens.Count - 1);
            foreach (var token in parseResult.Tokens)
            {
                if (token.Type != TokenType.Argument || token.Value != unmatchedCommandOrFile)
                {
                    otherTokens.Add(token.Value);
                }
            }
            parseResult = Parser.Parse(["run", unmatchedCommandOrFile, .. otherTokens]);

            InvokeBuiltInCommand(parseResult, out var exitCode);
            return exitCode;
        }

        return null;
    }

    private static ITelemetry InitializeTelemetry()
    {
        PerformanceLogEventSource.Log.TelemetryRegistrationStart();
        var telemetryClient = new Telemetry.Telemetry();
        TelemetryEventEntry.Subscribe(telemetryClient.TrackEvent);
        TelemetryEventEntry.TelemetryFilter = new TelemetryFilter(Sha256Hasher.HashWithNormalizedCasing);
        PerformanceLogEventSource.Log.TelemetryRegistrationStop();

        if (CommandLoggingContext.IsVerbose)
        {
            Console.WriteLine($"Telemetry is: {(telemetryClient.Enabled ? "Enabled" : "Disabled")}");
        }

        return telemetryClient;
    }

    private static ParseResult ParseArgs(string[] args)
    {
        ParseResult parseResult;
        using (new PerformanceMeasurement(performanceData, "Parse Time"))
        using (var _parseActivity = Activities.Source.StartActivity("parse"))
        {
            parseResult = Parser.Parse(args);

            // Avoid create temp directory with root permission and later prevent access in non sudo
            // This method need to be run very early before temp folder get created
            // https://github.com/dotnet/sdk/issues/20195
            SudoEnvironmentDirectoryOverride.OverrideEnvironmentVariableToTmp(parseResult);
        }
        PerformanceLogEventSource.Log.BuiltInCommandParserStop();
        SetDisplayName(s_mainActivity, parseResult);
        return parseResult;
    }

    private static void SetupDotnetFirstRun(ParseResult parseResult)
    {
        PerformanceLogEventSource.Log.FirstTimeConfigurationStart();
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
        if (parseResult.CommandResult.Command.Name.Equals(Parser.InstallSuccessCommand.Name))
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
            performanceData,
            skipFirstTimeUseCheck: getStarOptionPassed);
        PerformanceLogEventSource.Log.FirstTimeConfigurationStop();
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
       Dictionary<string, double> performanceMeasurements,
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
            performanceMeasurements,
            skipFirstTimeUseCheck: skipFirstTimeUseCheck);

        dotnetConfigurer.Configure();

        if (isDotnetBeingInvokedFromNativeInstaller && OperatingSystem.IsWindows())
        {
            DotDefaultPathCorrector.Correct();
        }

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
        // by default, .NET Core doesn't have all code pages needed for Console apps.
        // see the .NET Core Notes in https://docs.microsoft.com/dotnet/api/system.diagnostics.process#-notes
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        UILanguageOverride.Setup();
    }
}
