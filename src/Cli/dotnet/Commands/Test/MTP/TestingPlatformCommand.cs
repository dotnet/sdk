// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal partial class TestingPlatformCommand : Command, ICustomHelp
{
    private TerminalTestReporter _output;
    private TestApplicationActionQueue _actionQueue;

    private byte _cancelled;
    private bool _isDiscovery;
    private bool _isRetry;

    public TestingPlatformCommand(string name, string description = null) : base(name, description)
    {
        TreatUnmatchedTokensAsErrors = false;
    }

    public int Run(ParseResult parseResult)
    {
        int? exitCode = null;
        try
        {
            exitCode = RunInternal(parseResult);
            return exitCode.Value;
        }
        finally
        {
            CompleteRun(exitCode);
        }
    }

    private int RunInternal(ParseResult parseResult)
    {
        ValidationUtility.ValidateMutuallyExclusiveOptions(parseResult);
        ValidationUtility.ValidateSolutionOrProjectOrDirectoryOrModulesArePassedCorrectly(parseResult);

        PrepareEnvironment(parseResult, out TestOptions testOptions, out int degreeOfParallelism);

        InitializeOutput(degreeOfParallelism, parseResult, testOptions.IsHelp);

        BuildOptions buildOptions = MSBuildUtility.GetBuildOptions(parseResult, degreeOfParallelism);

        InitializeActionQueue(degreeOfParallelism, testOptions, buildOptions);

        var msBuildHandler = new MSBuildHandler(buildOptions, _actionQueue, _output);
        TestModulesFilterHandler testModulesFilterHandler = new(_actionQueue, _output);

        if (testOptions.HasFilterMode)
        {
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

        _actionQueue.EnqueueCompleted();
        // Don't inline exitCode variable. We want to always call WaitAllActions first.
        var exitCode = _actionQueue.WaitAllActions();
        exitCode = _output.HasHandshakeFailure ? ExitCode.GenericFailure : exitCode;
        if (exitCode == ExitCode.Success &&
            parseResult.HasOption(TestingPlatformOptions.MinimumExpectedTestsOption) &&
            parseResult.GetValue(TestingPlatformOptions.MinimumExpectedTestsOption) is { } minimumExpectedTests &&
            _output.TotalTests < minimumExpectedTests)
        {
            exitCode = ExitCode.MinimumExpectedTestsPolicyViolation;
        }

        return exitCode;
    }

    private void PrepareEnvironment(ParseResult parseResult, out TestOptions testOptions, out int degreeOfParallelism)
    {
        SetupCancelKeyPressHandler();

        degreeOfParallelism = GetDegreeOfParallelism(parseResult);

        bool filterModeEnabled = parseResult.HasOption(TestingPlatformOptions.TestModulesFilterOption);

        var arguments = parseResult.GetArguments();
        testOptions = GetTestOptions(filterModeEnabled, isHelp: ContainsHelpOption(arguments));

        _isDiscovery = ContainsListTestsOption(arguments);

        // This is ugly, and we need to replace it by passing out some info from testing platform to inform us that some process level retry plugin is active.
        _isRetry = arguments.Contains("--retry-failed-tests");
    }

    private void InitializeActionQueue(int degreeOfParallelism, TestOptions testOptions, BuildOptions buildOptions)
    {
        _actionQueue = new TestApplicationActionQueue(degreeOfParallelism, buildOptions, testOptions, _output, async (TestApplication testApp) =>
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

    private void InitializeOutput(int degreeOfParallelism, ParseResult parseResult, bool isHelp)
    {
        var console = new SystemConsole();
        var showPassedTests = parseResult.GetValue(TestingPlatformOptions.OutputOption) == OutputOptions.Detailed;
        var noProgress = parseResult.HasOption(TestingPlatformOptions.NoProgressOption);
        var noAnsi = parseResult.HasOption(TestingPlatformOptions.NoAnsiOption);

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
            MinimumExpectedTests = parseResult.GetValue(TestingPlatformOptions.MinimumExpectedTestsOption),
        });

        _output.TestExecutionStarted(DateTimeOffset.Now, degreeOfParallelism, _isDiscovery, isHelp, _isRetry);
    }

    private static int GetDegreeOfParallelism(ParseResult parseResult)
    {
        var degreeOfParallelism = parseResult.GetValue(TestingPlatformOptions.MaxParallelTestModulesOption);
        if (degreeOfParallelism <= 0)
            degreeOfParallelism = Environment.ProcessorCount;
        return degreeOfParallelism;
    }

    private static TestOptions GetTestOptions(bool hasFilterMode, bool isHelp) =>
        new(hasFilterMode, isHelp);

    private static bool ContainsHelpOption(IEnumerable<string> args)
    {
        return args.Contains(TestingPlatformOptions.HelpOption.Name) || TestingPlatformOptions.HelpOption.Aliases.Any(alias => args.Contains(alias));
    }

    private static bool ContainsListTestsOption(IEnumerable<string> args)
    {
        return args.Contains(TestingPlatformOptions.ListTestsOption.Name);
    }

    private void CompleteRun(int? exitCode)
    {
        if (Interlocked.CompareExchange(ref _cancelled, 1, 0) == 0)
        {
            _output?.TestExecutionCompleted(DateTimeOffset.Now, exitCode);
        }
    }
}
