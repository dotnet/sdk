// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

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
    private static readonly string ToolPathSentinelFileName = $"{Product.Version}.toolpath.sentinel";

    public static ITelemetry TelemetryClient;
    public static int Main(string[] args)
    {
        using AutomaticEncodingRestorer _ = new();

        // Setting output encoding is not available on those platforms
        if (UILanguageOverride.OperatingSystemSupportsUtf8())
        {
            Console.OutputEncoding = Encoding.UTF8;
        }

        DebugHelper.HandleDebugSwitch(ref args);

        // Capture the current timestamp to calculate the host overhead.
        DateTime mainTimeStamp = DateTime.Now;
        TimeSpan startupTime = mainTimeStamp - Process.GetCurrentProcess().StartTime;

        bool perfLogEnabled = Env.GetEnvironmentVariableAsBool("DOTNET_CLI_PERF_LOG", false);

        if (string.IsNullOrEmpty(Env.GetEnvironmentVariable("MSBUILDFAILONDRIVEENUMERATINGWILDCARD")))
        {
            Environment.SetEnvironmentVariable("MSBUILDFAILONDRIVEENUMERATINGWILDCARD", "1");
        }

        // Avoid create temp directory with root permission and later prevent access in non sudo
        if (SudoEnvironmentDirectoryOverride.IsRunningUnderSudo())
        {
            perfLogEnabled = false;
        }

        PerformanceLogStartupInformation startupInfo = null;
        if (perfLogEnabled)
        {
            startupInfo = new PerformanceLogStartupInformation(mainTimeStamp);
            PerformanceLogManager.InitializeAndStartCleanup(FileSystemWrapper.Default);
        }

        PerformanceLogEventListener perLogEventListener = null;
        try
        {
            if (perfLogEnabled)
            {
                perLogEventListener = PerformanceLogEventListener.Create(FileSystemWrapper.Default, PerformanceLogManager.Instance.CurrentLogDirectory);
            }

            PerformanceLogEventSource.Log.LogStartUpInformation(startupInfo);
            PerformanceLogEventSource.Log.CLIStart();

            InitializeProcess();

            try
            {
                return ProcessArgs(args, startupTime);
            }
            catch (Exception e) when (e.ShouldBeDisplayedAsError())
            {
                Reporter.Error.WriteLine(CommandLoggingContext.IsVerbose
                    ? e.ToString().Red().Bold()
                    : e.Message.Red().Bold());

                var commandParsingException = e as CommandParsingException;
                if (commandParsingException != null && commandParsingException.ParseResult != null)
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
                PerformanceLogEventSource.Log.CLIStop();
            }
        }
        finally
        {
            if (perLogEventListener != null)
            {
                perLogEventListener.Dispose();
            }
        }
    }

    internal static int ProcessArgs(string[] args)
    {
        return ProcessArgs(args, new TimeSpan(0));
    }

    internal static int ProcessArgs(string[] args, TimeSpan startupTime)
    {
        Dictionary<string, double> performanceData = [];

        PerformanceLogEventSource.Log.BuiltInCommandParserStart();
        ParseResult parseResult;
        using (new PerformanceMeasurement(performanceData, "Parse Time"))
        {
            parseResult = Parser.Parse(args);

            // Avoid create temp directory with root permission and later prevent access in non sudo
            // This method need to be run very early before temp folder get created
            // https://github.com/dotnet/sdk/issues/20195
            SudoEnvironmentDirectoryOverride.OverrideEnvironmentVariableToTmp(parseResult);
        }
        PerformanceLogEventSource.Log.BuiltInCommandParserStop();

        using (IFirstTimeUseNoticeSentinel disposableFirstTimeUseNoticeSentinel = new FirstTimeUseNoticeSentinel())
        {
            IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel = disposableFirstTimeUseNoticeSentinel;
            IAspNetCertificateSentinel aspNetCertificateSentinel = new AspNetCertificateSentinel();
            IFileSentinel toolPathSentinel = new FileSentinel(new FilePath(Path.Combine(CliFolderPathCalculator.DotnetUserProfileFolderPath, ToolPathSentinelFileName)));

            PerformanceLogEventSource.Log.TelemetryRegistrationStart();

            TelemetryClient ??= new Telemetry.Telemetry(firstTimeUseNoticeSentinel);
            TelemetryEventEntry.Subscribe(TelemetryClient.TrackEvent);
            TelemetryEventEntry.TelemetryFilter = new TelemetryFilter(Sha256Hasher.HashWithNormalizedCasing);

            PerformanceLogEventSource.Log.TelemetryRegistrationStop();

            if (parseResult.GetValue(Parser.DiagOption) && parseResult.IsDotnetBuiltInCommand())
            {
                // We found --diagnostic or -d, but we still need to determine whether the option should
                // be attached to the dotnet command or the subcommand.
                if (args.DiagOptionPrecedesSubcommand(parseResult.RootSubCommandResult()))
                {
                    Environment.SetEnvironmentVariable(CommandLoggingContext.Variables.Verbose, bool.TrueString);
                    CommandLoggingContext.SetVerbose(true);
                    Reporter.Reset();
                }
            }
            if (parseResult.HasOption(Parser.VersionOption) && parseResult.IsTopLevelDotnetCommand())
            {
                CommandLineInfo.PrintVersion();
                return 0;
            }
            else if (parseResult.HasOption(Parser.InfoOption) && parseResult.IsTopLevelDotnetCommand())
            {
                CommandLineInfo.PrintInfo();
                return 0;
            }
            else
            {
                PerformanceLogEventSource.Log.FirstTimeConfigurationStart();

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
        }

        if (CommandLoggingContext.IsVerbose)
        {
            Console.WriteLine($"Telemetry is: {(TelemetryClient.Enabled ? "Enabled" : "Disabled")}");
        }
        PerformanceLogEventSource.Log.TelemetrySaveIfEnabledStart();
        performanceData.Add("Startup Time", startupTime.TotalMilliseconds);

        string globalJsonState = string.Empty;
        if (TelemetryClient.Enabled)
        {
            // Get the global.json state to report in telemetry along with this command invocation.
            // We don't care about the actual SDK resolution, just the global.json information,
            // so just pass empty string as executable directory for resolution.
            NativeWrapper.SdkResolutionResult result = NativeWrapper.NETCoreSdkResolverNativeWrapper.ResolveSdk(string.Empty, Environment.CurrentDirectory);
            globalJsonState = result.GlobalJsonState;
        }

        TelemetryEventEntry.SendFiltered(Tuple.Create(parseResult, performanceData, globalJsonState));
        PerformanceLogEventSource.Log.TelemetrySaveIfEnabledStop();

        int exitCode;
        if (parseResult.CanBeInvoked())
        {
            InvokeBuiltInCommand(parseResult, out exitCode);
        }
        else
        {
            PerformanceLogEventSource.Log.ExtensibleCommandResolverStart();
            try
            {
                string commandName = "dotnet-" + parseResult.GetValue(Parser.DotnetSubCommand);
                var resolvedCommandSpec = CommandResolver.TryResolveCommandSpec(
                    new DefaultCommandResolverPolicy(),
                    commandName,
                    args.GetSubArguments(),
                    FrameworkConstants.CommonFrameworks.NetStandardApp15);

                if (resolvedCommandSpec is null && TryRunFileBasedApp(parseResult) is { } fileBasedAppExitCode)
                {
                    exitCode = fileBasedAppExitCode;
                }
                else
                {
                    var resolvedCommand = CommandFactoryUsingResolver.CreateOrThrow(commandName, resolvedCommandSpec);
                    PerformanceLogEventSource.Log.ExtensibleCommandResolverStop();

                    PerformanceLogEventSource.Log.ExtensibleCommandStart();
                    var result = resolvedCommand.Execute();
                    PerformanceLogEventSource.Log.ExtensibleCommandStop();

                    exitCode = result.ExitCode;
                }
            }
            catch (CommandUnknownException e)
            {
                Reporter.Error.WriteLine(e.Message.Red());
                Reporter.Output.WriteLine(e.InstructionMessage);
                exitCode = 1;
            }
        }

        PerformanceLogEventSource.Log.TelemetryClientFlushStart();
        TelemetryClient.Flush();
        PerformanceLogEventSource.Log.TelemetryClientFlushStop();

        TelemetryClient.Dispose();

        return exitCode;

        static int? TryRunFileBasedApp(ParseResult parseResult)
        {
            // If we didn't match any built-in commands, and a C# file path is the first argument,
            // parse as `dotnet run --file file.cs ..rest_of_args` instead.
            if (parseResult.GetValue(Parser.DotnetSubCommand) is { } unmatchedCommandOrFile
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

                parseResult = Parser.Parse(["run", "--file", unmatchedCommandOrFile, .. otherTokens]);

                InvokeBuiltInCommand(parseResult, out var exitCode);
                return exitCode;
            }

            return null;
        }

        static void InvokeBuiltInCommand(ParseResult parseResult, out int exitCode)
        {
            Debug.Assert(parseResult.CanBeInvoked());

            PerformanceLogEventSource.Log.BuiltInCommandStart();

            try
            {
                exitCode = Parser.Invoke(parseResult);
                exitCode = AdjustExitCode(parseResult, exitCode);
            }
            catch (Exception exception)
            {
                exitCode = Parser.ExceptionHandler(exception, parseResult);
            }

            PerformanceLogEventSource.Log.BuiltInCommandStop();
        }
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

        Reporter.Verbose.WriteLine(
            string.Format(
                LocalizableStrings.DotnetCliHomeUsed,
                home,
                CliFolderPathCalculator.DotnetHomeVariableName));
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
