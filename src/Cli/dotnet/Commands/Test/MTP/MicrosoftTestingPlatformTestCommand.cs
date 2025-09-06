// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal partial class MicrosoftTestingPlatformTestCommand : Command, ICustomHelp, ICommandDocument
{
    private TerminalTestReporter _output;
    private byte _cancelled;

    public MicrosoftTestingPlatformTestCommand(string name, string description = null) : base(name, description)
    {
        TreatUnmatchedTokensAsErrors = false;
    }

    public string DocsLink => "https://aka.ms/dotnet-test";

    public int Run(ParseResult parseResult, bool isHelp = false)
    {
        int? exitCode = null;
        try
        {
            exitCode = RunInternal(parseResult, isHelp);
            return exitCode.Value;
        }
        finally
        {
            CompleteRun(exitCode);
        }
    }

    private int RunInternal(ParseResult parseResult, bool isHelp)
    {
        ValidationUtility.ValidateMutuallyExclusiveOptions(parseResult);
        ValidationUtility.ValidateSolutionOrProjectOrDirectoryOrModulesArePassedCorrectly(parseResult);

        int degreeOfParallelism = GetDegreeOfParallelism(parseResult);
        bool filterModeEnabled = parseResult.HasOption(MicrosoftTestingPlatformOptions.TestModulesFilterOption);
        var testOptions = new TestOptions(filterModeEnabled, IsHelp: isHelp, IsDiscovery: parseResult.HasOption(MicrosoftTestingPlatformOptions.ListTestsOption));

        InitializeOutput(degreeOfParallelism, parseResult, testOptions);

        SetupCancelKeyPressHandler();

        BuildOptions buildOptions = MSBuildUtility.GetBuildOptions(parseResult, degreeOfParallelism);

        var actionQueue = InitializeActionQueue(degreeOfParallelism, testOptions, buildOptions);

        var msBuildHandler = new MSBuildHandler(buildOptions, actionQueue, _output);

        if (testOptions.HasFilterMode)
        {
            var testModulesFilterHandler = new TestModulesFilterHandler(actionQueue, _output);
            if (!testModulesFilterHandler.RunWithTestModulesFilter(parseResult))
            {
                return ExitCode.GenericFailure;
            }
        }
        else
        {
            if (!msBuildHandler.RunMSBuild())
            {
                return ExitCode.GenericFailure;
            }

            if (!msBuildHandler.EnqueueTestApplications())
            {
                return ExitCode.GenericFailure;
            }
        }

        actionQueue.EnqueueCompleted();
        // Don't inline exitCode variable. We want to always call WaitAllActions first.
        var exitCode = actionQueue.WaitAllActions();
        exitCode = _output.HasHandshakeFailure ? ExitCode.GenericFailure : exitCode;
        if (exitCode == ExitCode.Success &&
            parseResult.HasOption(MicrosoftTestingPlatformOptions.MinimumExpectedTestsOption) &&
            parseResult.GetValue(MicrosoftTestingPlatformOptions.MinimumExpectedTestsOption) is { } minimumExpectedTests &&
            _output.TotalTests < minimumExpectedTests)
        {
            exitCode = ExitCode.MinimumExpectedTestsPolicyViolation;
        }

        return exitCode;
    }

    private TestApplicationActionQueue InitializeActionQueue(int degreeOfParallelism, TestOptions testOptions, BuildOptions buildOptions)
    {
        return new TestApplicationActionQueue(degreeOfParallelism, buildOptions, testOptions, _output, async (TestApplication testApp) =>
        {
            testApp.HelpRequested += OnHelpRequested;
            return await testApp.RunAsync();
        });
    }

    private void SetupCancelKeyPressHandler()
    {
        Console.CancelKeyPress += (s, e) =>
        {
            _output?.StartCancelling();
            // We are not sure what the exit code will be, there might be an exception.
            CompleteRun(exitCode: null);
        };
    }

    private void InitializeOutput(int degreeOfParallelism, ParseResult parseResult, TestOptions testOptions)
    {
        var console = new SystemConsole();
        var showPassedTests = parseResult.GetValue(MicrosoftTestingPlatformOptions.OutputOption) == OutputOptions.Detailed;
        var noProgress = parseResult.HasOption(MicrosoftTestingPlatformOptions.NoProgressOption);
        var noAnsi = parseResult.HasOption(MicrosoftTestingPlatformOptions.NoAnsiOption);

        // TODO: Replace this with proper CI detection that we already have in telemetry. https://github.com/microsoft/testfx/issues/5533#issuecomment-2838893327
        bool inCI = string.Equals(Environment.GetEnvironmentVariable("TF_BUILD"), "true", StringComparison.OrdinalIgnoreCase) || string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);

        _output = new TerminalTestReporter(console, new TerminalTestReporterOptions()
        {
            ShowPassedTests = () => showPassedTests,
            ShowProgress = () => !noProgress,
            UseAnsi = !noAnsi,
            UseCIAnsi = inCI,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = true,
            MinimumExpectedTests = parseResult.GetValue(MicrosoftTestingPlatformOptions.MinimumExpectedTestsOption),
        });

        // This is ugly, and we need to replace it by passing out some info from testing platform to inform us that some process level retry plugin is active.
        var isRetry = parseResult.GetArguments().Contains("--retry-failed-tests");

        _output.TestExecutionStarted(DateTimeOffset.Now, degreeOfParallelism, testOptions.IsDiscovery, testOptions.IsHelp, isRetry);
    }

    private static int GetDegreeOfParallelism(ParseResult parseResult)
    {
        var degreeOfParallelism = parseResult.GetValue(MicrosoftTestingPlatformOptions.MaxParallelTestModulesOption);
        if (degreeOfParallelism <= 0)
            degreeOfParallelism = Environment.ProcessorCount;
        return degreeOfParallelism;
    }

    private void CompleteRun(int? exitCode)
    {
        if (Interlocked.CompareExchange(ref _cancelled, 1, 0) == 0)
        {
            _output?.TestExecutionCompleted(DateTimeOffset.Now, exitCode);
        }
    }
}
