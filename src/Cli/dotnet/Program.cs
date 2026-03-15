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
using CommandResult = System.CommandLine.Parsing.CommandResult;

namespace Microsoft.DotNet.Cli;

public class Program
{
    private static readonly string s_toolPathSentinelFileName = $"{Product.Version}.toolpath.sentinel";

    private static readonly Activity? s_mainActivity;
    private static readonly PosixSignalRegistration s_sigIntRegistration;
    private static readonly PosixSignalRegistration s_sigQuitRegistration;
    private static readonly PosixSignalRegistration s_sigTermRegistration;
    private static readonly string? s_globalJsonState;

    public static ITelemetryClient TelemetryInstance { get; private set; }

    static Program()
    {
        var mainTimeStamp = DateTime.Now;
        s_sigIntRegistration = PosixSignalRegistration.Create(PosixSignal.SIGINT, Shutdown);
        s_sigQuitRegistration = PosixSignalRegistration.Create(PosixSignal.SIGQUIT, Shutdown);
        s_sigTermRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, Shutdown);

        // Note: This TelemetryClient instance needs to be created prior to calculating ActivityKind and ParentActivityContext,
        // used in the main activity creation below.
        TelemetryInstance = new TelemetryClient();
        TelemetryEventEntry.Subscribe(TelemetryInstance.TrackEvent);
        TelemetryEventEntry.TelemetryFilter = new TelemetryFilter(Sha256Hasher.HashWithNormalizedCasing);

        s_mainActivity = Activities.Source.CreateActivity("main", TelemetryClient.ActivityKind, TelemetryClient.ParentActivityContext);
        s_mainActivity
            ?.Start()
            ?.SetStartTime(Process.GetCurrentProcess().StartTime)
            ?.AddTag("process.pid", Process.GetCurrentProcess().Id)
            ?.AddTag("process.executable.name", "dotnet");

        if (CommandLoggingContext.IsVerbose)
        {
            Console.WriteLine($"Telemetry is: {(TelemetryInstance.Enabled ? "Enabled" : "Disabled")}");
        }

        // Creates a host-startup activity which includes the global.json state.
        var hostStartupActivity = Activities.Source.StartActivity("host-startup")
                ?.SetStartTime(Process.GetCurrentProcess().StartTime);
        if (TelemetryInstance.Enabled && hostStartupActivity is not null)
        {
            // Get the global.json state to report in telemetry along with this command invocation.
            s_globalJsonState = NativeWrapper.NETCoreSdkResolverNativeWrapper.GetGlobalJsonState(Environment.CurrentDirectory);
            hostStartupActivity?.AddTag("dotnet.globalJson", s_globalJsonState);
        }
        hostStartupActivity?.SetEndTime(mainTimeStamp)
            ?.SetStatus(ActivityStatusCode.Ok)
            ?.Dispose();

        // We have some behaviors in MSBuild that we want to enforce (either when using MSBuild API or by shelling out to it),
        // so we set those ASAP as globally as possible.
        if (string.IsNullOrEmpty(Env.GetEnvironmentVariable("MSBUILDFAILONDRIVEENUMERATINGWILDCARD")))
        {
            Environment.SetEnvironmentVariable("MSBUILDFAILONDRIVEENUMERATINGWILDCARD", "1");
        }
    }

    public static int Main(string[] args)
    {
        // Register a handler for SIGTERM to allow graceful shutdown of the application on Unix.
        // See https://github.com/dotnet/docs/issues/46226.
        using var termSignalRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => Environment.Exit(0));

        using AutomaticEncodingRestorer _ = new();

        if (Env.GetEnvironmentVariable(EnvironmentVariableNames.DOTNET_CLI_CONSOLE_USE_DEFAULT_ENCODING) != "1"
            // Setting output encoding is not available on those platforms
            && UILanguageOverride.OperatingSystemSupportsUtf8())
        {
            Console.OutputEncoding = Encoding.UTF8;
        }

        DebugHelper.HandleDebugSwitch(ref args);
        // By default, .NET Core doesn't have all code pages needed for Console apps.
        // See the .NET Core Notes: https://docs.microsoft.com/dotnet/api/system.diagnostics.process#-notes
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        UILanguageOverride.Setup();

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

            if (e is CommandParsingException { ParseResult: {} exceptionParseResult } )
            {
                exceptionParseResult.ShowHelp();
            }
            s_mainActivity?.AddTag("process.exit.code", exitCode)?.SetStatus(ActivityStatusCode.Error);
            return exitCode;
        }
        catch (Exception e) when (!e.ShouldBeDisplayedAsError())
        {
            TelemetryEventEntry.SendFiltered(e);
            Reporter.Error.WriteLine(e.ToString().Red().Bold());
            s_mainActivity?.AddTag("process.exit.code", exitCode)?.SetStatus(ActivityStatusCode.Error);
            return exitCode;
        }
        finally
        {
            TelemetryInstance.TrackEvent("command/finish", new Dictionary<string, string?> { { "exitCode", exitCode.ToString() } });

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

    internal static int ProcessArgs(string[] args)
    {
        ParseResult parseResult = ParseArgs(args);
        // Options that perform terminating actions are considered to essentially be subcommands. These are special as they should not run the first-run setup.
        // Example: dotnet --version
        if (!(parseResult.Action is InvocableOptionAction { Terminating: true }))
        {
            SetupDotnetFirstRun(parseResult);
        }

        TelemetryEventEntry.SendFiltered(new ParseResultWithGlobalJsonState(parseResult, s_globalJsonState));

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
            exitCode = Parser.Invoke(parseResult);
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
        if (parseResult.GetResult(Parser.RootCommand.DotnetSubCommand) is { Tokens: [{ Type: TokenType.Argument, Value: { } } unmatchedCommandOrFile] }
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

        s_mainActivity.SetDisplayName(parseResult);

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

        var isDotnetBeingInvokedFromNativeInstaller = false;
        // TODO: This should not be special cased like this. Determine if we can skip first run setup entirely for this command.
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
}
