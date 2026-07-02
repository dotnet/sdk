// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli.CommandFactory;
using Microsoft.DotNet.Cli.CommandFactory.CommandResolution;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.DotNet.ProjectTools;
using Microsoft.DotNet.Utilities;
using NuGet.Frameworks;
using CommandResult = System.CommandLine.Parsing.CommandResult;

namespace Microsoft.DotNet.Cli;

public class Program
{
    private static readonly Activity? s_mainActivity;
    private static readonly PosixSignalRegistration s_sigIntRegistration;
    private static readonly PosixSignalRegistration s_sigQuitRegistration;
    private static readonly PosixSignalRegistration s_sigTermRegistration;
    private static readonly string? s_globalJsonState;

    public static ITelemetryClient TelemetryInstance { get; private set; }

    static Program()
    {
        var preTelemetry = DateTime.UtcNow;
        s_sigIntRegistration = PosixSignalRegistration.Create(PosixSignal.SIGINT, Shutdown);
        s_sigQuitRegistration = PosixSignalRegistration.Create(PosixSignal.SIGQUIT, Shutdown);
        s_sigTermRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, Shutdown);

        // Note: This TelemetryClient instance needs to be created prior to calculating ActivityKind and ParentActivityContext,
        // used in the main activity creation below.
        TelemetryInstance = new TelemetryClient();
        TelemetryEventEntry.Subscribe(TelemetryInstance.TrackEvent);
        TelemetryEventEntry.TelemetryFilter = new TelemetryFilter(Sha256Hasher.HashWithNormalizedCasing);
        var postTelemetry = DateTime.UtcNow;

        s_mainActivity = Activities.Source.CreateActivity("main", TelemetryClient.ActivityKind, TelemetryClient.ParentActivityContext)
            ?.Start()
            ?.SetStartTime(Process.GetCurrentProcess().StartTime)
            ?.AddTag("process.pid", Process.GetCurrentProcess().Id)
            ?.AddTag("process.executable.name", "dotnet")
            ?.AddTag("cli.runtime", "managed");

        using (var telemetrySetupActivity = Activities.Source.StartActivity("telemetry-setup"))
        {
            telemetrySetupActivity?.SetStartTime(preTelemetry);
            telemetrySetupActivity?.SetEndTime(postTelemetry);
        }

        if (CommandLoggingContext.IsVerbose)
        {
            Reporter.Verbose.WriteLine($"Telemetry is: {(TelemetryInstance.Enabled ? "Enabled" : "Disabled")}");
        }

        if (TelemetryInstance.Enabled && s_mainActivity is not null)
        {
            s_globalJsonState = NativeWrapper.NETCoreSdkResolverNativeWrapper.GetGlobalJsonState(Environment.CurrentDirectory);
            s_mainActivity.AddTag("dotnet.globalJson", s_globalJsonState);
        }

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
            exitCode = ProcessArgsAndExecute(args);
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

    internal static int ProcessArgsAndExecute(string[] args)
    {
        ParseResult parseResult = ParseArgs(args);
        // Run the cross-cutting first-run experience (first-time-use notice, telemetry message,
        // ASP.NET dev cert, global-tools PATH, workload integrity check). Terminating options such as
        // "dotnet --version" are skipped inside Setup.
        FirstRunExperience.Setup(parseResult);

        TelemetryEventEntry.SendFiltered(new ParseResultWithGlobalJsonState(parseResult, s_globalJsonState));
        if (parseResult.CanBeInvoked())
        {
            return CommandInvocation.ExecuteInternalCommand(parseResult);
        }

        try
        {
            return ExecuteExternalCommand(args, parseResult);
        }
        catch (CommandUnknownException e)
        {
            Reporter.Error.WriteLine(e.Message.Red());
            Reporter.Output.WriteLine(e.InstructionMessage);
            return 1;
        }

        static ParseResult ParseArgs(string[] args)
        {
            ParseResult parseResult;
            using (var parseActivity = Activities.Source.StartActivity("parse"))
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
    }

    private static int ExecuteExternalCommand(string[] args, ParseResult parseResult)
    {
        string commandName = "dotnet-" + parseResult.GetValue(Parser.RootCommand.DotnetSubCommand);
        CommandSpec? resolvedCommandSpec = null;
        using (var lookupActivity = Activities.Source.StartActivity("lookup-external-command"))
        {
            lookupActivity?.AddTag("command.name", commandName);
            resolvedCommandSpec = CommandResolver.TryResolveCommandSpec(
                new DefaultCommandResolverPolicy(),
                commandName,
                args.GetSubArguments(),
                FrameworkConstants.CommonFrameworks.NetStandardApp15);
        }

        if (resolvedCommandSpec is null && TryRunFileBasedApp(parseResult) is { } fileBasedAppExitCode)
        {
            return fileBasedAppExitCode;
        }

        var resolvedCommand = CommandFactoryUsingResolver.CreateOrThrow(commandName, resolvedCommandSpec);
        using var __ = Activities.Source.StartActivity("execute-extensible-command");
        return resolvedCommand.Execute().ExitCode;
    }

    private static int? TryRunFileBasedApp(ParseResult parseResult)
    {
        // If we didn't match any built-in commands, and a C# file path is the first argument,
        // parse as `dotnet run file.cs ..rest_of_args` instead.
        if (parseResult.GetFileBasedAppEntryPointToken() is { } unmatchedCommandOrFile)
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
            return CommandInvocation.ExecuteInternalCommand(parseResult);
        }

        return null;
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
}
