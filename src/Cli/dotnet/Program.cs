// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.CommandFactory;
using Microsoft.DotNet.Cli.CommandFactory.CommandResolution;
using Microsoft.DotNet.Cli.CommandLine;
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

public static class Activities
{
    public static ActivitySource s_source = new("dotnet-cli", Product.Version);
}

public class Program
{
    private static readonly string ToolPathSentinelFileName = $"{Product.Version}.toolpath.sentinel";

    public static ITelemetry TelemetryClient;
    public static int Main(string[] args)
    {
        // Register a handler for SIGTERM to allow graceful shutdown of the application on Unix.
        // See https://github.com/dotnet/docs/issues/46226.
        using var termSignalRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => Environment.Exit(0));
        
        // capture the time to we can compute muxer/host startup overhead
        DateTime mainTimeStamp = DateTime.Now;
        using var _mainActivity = Activities.s_source.StartActivity("main");
        _mainActivity.AddTag("process.pid", Process.GetCurrentProcess().Id);
        _mainActivity.AddTag("process.executable.name", "dotnet");
        using AutomaticEncodingRestorer _encodingRestorer = new();

        // Setting output encoding is not available on those platforms
        if (UILanguageOverride.OperatingSystemSupportsUtf8())
        {
            Console.OutputEncoding = Encoding.UTF8;
        }

        DebugHelper.HandleDebugSwitch(ref args);

        TrackHostStartup(mainTimeStamp);

        SetupMSBuildEnvironmentInvariants();

        try
        {
            InitializeProcess();

            try
            {
                var exitCode = ProcessArgs(args);
                _mainActivity.AddTag("process.exit.code", exitCode);
                _mainActivity.SetStatus(ActivityStatusCode.Ok);
                return exitCode;
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
                _mainActivity.AddTag("process.exit.code", exitCode);
                _mainActivity.SetStatus(ActivityStatusCode.Error);
                return 1;
            }
            catch (Exception e) when (!e.ShouldBeDisplayedAsError())
            {
                // If telemetry object has not been initialized yet. It cannot be collected
                TelemetryEventEntry.SendFiltered(e);
                Reporter.Error.WriteLine(e.ToString().Red().Bold());
                _mainActivity.AddTag("process.exit.code", exitCode);
                _mainActivity.SetStatus(ActivityStatusCode.Error);
                return 1;
            }
            finally
            {
                PerformanceLogEventSource.Log.CLIStop();
            }
        }
        finally
        {
            Activities.s_source.Dispose();
        }
    }

    private static void TrackHostStartup(DateTime mainTimeStamp)
    {
        using var hostStartupActivity = Activities.s_source.CreateActivity("host-startup", ActivityKind.Server);
        hostStartupActivity?.SetStartTime(Process.GetCurrentProcess().StartTime);
        if (TelemetryClient.Enabled)
        {
            // Get the global.json state to report in telemetry along with this command invocation.
            if (NativeWrapper.NETCoreSdkResolverNativeWrapper.GetGlobalJsonState(Environment.CurrentDirectory) is string globalJsonState)
            {
                hostStartupActivity?.AddTag("dotnet.globalJson", globalJsonState);
            }
        }
        hostStartupActivity?.SetEndTime(mainTimeStamp);
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

    internal static int ProcessArgs(string[] args)
    {
        ParseResult parseResult;
        using (var _parseActivity = Activities.s_source.StartActivity("parse"))
        {
            parseResult = Parser.Parse(args);

            // Avoid create temp directory with root permission and later prevent access in non sudo
            // This method need to be run very early before temp folder get created
            // https://github.com/dotnet/sdk/issues/20195
            SudoEnvironmentDirectoryOverride.OverrideEnvironmentVariableToTmp(parseResult);
        }

        using (var _firstTimeUseActivity = Activities.s_source.StartActivity("first-time-use"))
        {
            IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel = new FirstTimeUseNoticeSentinel();

            IAspNetCertificateSentinel aspNetCertificateSentinel = new AspNetCertificateSentinel();
            IFileSentinel toolPathSentinel = new FileSentinel(
                new FilePath(
                    Path.Combine(
                        CliFolderPathCalculator.DotnetUserProfileFolderPath,
                        ToolPathSentinelFileName)));

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
                skipFirstTimeUseCheck: getStarOptionPassed);
        }

        var telemetryClient = new Telemetry.Telemetry();
        TelemetryEventEntry.Subscribe(telemetryClient.TrackEvent);
        TelemetryEventEntry.TelemetryFilter = new TelemetryFilter(Sha256Hasher.HashWithNormalizedCasing);

        if (CommandLoggingContext.IsVerbose)
        {
            Console.WriteLine($"Telemetry is: {(TelemetryClient.Enabled ? "Enabled" : "Disabled")}");
        }

        int exitCode;
        if (parseResult.CanBeInvoked())
        {
            InvokeBuiltInCommand(parseResult, out exitCode);
        }
        else
        {
            try
            {
                var _lookupExternalCommandActivity = Activities.s_source.StartActivity("lookup-external-command");
                string commandName = "dotnet-" + parseResult.GetValue(Parser.DotnetSubCommand);
                var resolvedCommandSpec = CommandResolver.TryResolveCommandSpec(
                    new DefaultCommandResolverPolicy(),
                    commandName,
                    args.GetSubArguments(),
                    FrameworkConstants.CommonFrameworks.NetStandardApp15);
                _lookupExternalCommandActivity?.Dispose();

                if (resolvedCommandSpec is null && TryRunFileBasedApp(parseResult) is { } fileBasedAppExitCode)
                {
                    exitCode = fileBasedAppExitCode;
                }
                else
                {
                    var _executionActivity = Activities.s_source.StartActivity("execute-extensible-command");
                    var resolvedCommand = CommandFactoryUsingResolver.CreateOrThrow(commandName, resolvedCommandSpec);
                    var result = resolvedCommand.Execute();
                    _executionActivity?.Dispose();
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
            using var _invocationActivity = Activities.s_source.StartActivity("invocation");
            try
            {
                exitCode = Parser.Invoke(parseResult);
                exitCode = AdjustExitCode(parseResult, exitCode);
            }
            catch (Exception exception)
            {
                exitCode = Parser.ExceptionHandler(exception, parseResult);
            }
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
