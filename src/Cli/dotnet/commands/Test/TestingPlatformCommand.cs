// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.DotNet.Cli;

internal partial class TestingPlatformCommand : CliCommand, ICustomHelp
{
    private MSBuildHandler _msBuildHandler;
    private TerminalTestReporter _output;
    private TestApplicationActionQueue _actionQueue;
    private TestApplicationsEventHandlers _eventHandlers;

    private byte _cancelled;
    private bool _isDiscovery;

    public TestingPlatformCommand(string name, string description = null) : base(name, description)
    {
        TreatUnmatchedTokensAsErrors = false;
    }

    public int Run(ParseResult parseResult)
    {
        int exitCode = ExitCode.Success;
        try
        {
            ValidationUtility.ValidateMutuallyExclusiveOptions(parseResult);

            PrepareEnvironment(parseResult, out TestOptions testOptions, out int degreeOfParallelism);

            InitializeOutput(degreeOfParallelism, parseResult, testOptions.IsHelp);

            InitializeActionQueue(degreeOfParallelism, testOptions, testOptions.IsHelp);

            BuildOptions buildOptions = MSBuildUtility.GetBuildOptions(parseResult, degreeOfParallelism);
            _msBuildHandler = new(buildOptions, _actionQueue, _output);
            TestModulesFilterHandler testModulesFilterHandler = new(_actionQueue, _output);

            _eventHandlers = new TestApplicationsEventHandlers(_output);

            if (testOptions.HasFilterMode)
            {
                if (!testModulesFilterHandler.RunWithTestModulesFilter(parseResult, buildOptions))
                {
                    return ExitCode.GenericFailure;
                }
            }
            else
            {
                if (!_msBuildHandler.RunMSBuild())
                {
                    return ExitCode.GenericFailure;
                }

                if (!_msBuildHandler.EnqueueTestApplications())
                {
                    return ExitCode.GenericFailure;
                }
            }

            _actionQueue.EnqueueCompleted();
            exitCode = _actionQueue.WaitAllActions();
        }
        finally
        {
            CompleteRun();
            CleanUp();
        }

        return exitCode;
    }

    private void PrepareEnvironment(ParseResult parseResult, out TestOptions testOptions, out int degreeOfParallelism)
    {
        SetupCancelKeyPressHandler();

        degreeOfParallelism = GetDegreeOfParallelism(parseResult);

        bool filterModeEnabled = parseResult.HasOption(TestingPlatformOptions.TestModulesFilterOption);

        var arguments = parseResult.GetArguments();
        testOptions = GetTestOptions(parseResult, filterModeEnabled, isHelp: ContainsHelpOption(arguments));

        _isDiscovery = ContainsListTestsOption(arguments);
    }

    private void InitializeActionQueue(int degreeOfParallelism, TestOptions testOptions, bool isHelp)
    {
        if (isHelp)
        {
            InitializeHelpActionQueue(degreeOfParallelism, testOptions);
        }
        else
        {
            InitializeTestExecutionActionQueue(degreeOfParallelism, testOptions);
        }
    }

    private void SetupCancelKeyPressHandler()
    {
        Console.CancelKeyPress += (s, e) =>
        {
            _output?.StartCancelling();
            CompleteRun();
        };
    }

    private void InitializeOutput(int degreeOfParallelism, ParseResult parseResult, bool isHelp)
    {
        var console = new SystemConsole();
        var showPassedTests = parseResult.GetValue(TestingPlatformOptions.OutputOption) == OutputOptions.Detailed;
        var noProgress = parseResult.HasOption(TestingPlatformOptions.NoProgressOption);
        var noAnsi = parseResult.HasOption(TestingPlatformOptions.NoAnsiOption);
        _output = new TerminalTestReporter(console, new TerminalTestReporterOptions()
        {
            ShowPassedTests = () => showPassedTests,
            ShowProgress = () => !noProgress,
            UseAnsi = !noAnsi,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = true,
        });

        _output.TestExecutionStarted(DateTimeOffset.Now, degreeOfParallelism, _isDiscovery, isHelp);
    }

    private void InitializeHelpActionQueue(int degreeOfParallelism, TestOptions testOptions)
    {
        _actionQueue = new(degreeOfParallelism, async (TestApplication testApp) =>
        {
            testApp.HelpRequested += OnHelpRequested;
            testApp.ErrorReceived += _eventHandlers.OnErrorReceived;
            testApp.TestProcessExited += _eventHandlers.OnTestProcessExited;

            return await testApp.RunAsync(testOptions);
        });
    }

    private void InitializeTestExecutionActionQueue(int degreeOfParallelism, TestOptions testOptions)
    {
        _actionQueue = new(degreeOfParallelism, async (TestApplication testApp) =>
        {
            testApp.HandshakeReceived += _eventHandlers.OnHandshakeReceived;
            testApp.DiscoveredTestsReceived += _eventHandlers.OnDiscoveredTestsReceived;
            testApp.TestResultsReceived += _eventHandlers.OnTestResultsReceived;
            testApp.FileArtifactsReceived += _eventHandlers.OnFileArtifactsReceived;
            testApp.SessionEventReceived += _eventHandlers.OnSessionEventReceived;
            testApp.ErrorReceived += _eventHandlers.OnErrorReceived;
            testApp.TestProcessExited += _eventHandlers.OnTestProcessExited;

            return await testApp.RunAsync(testOptions);
        });
    }

    private static int GetDegreeOfParallelism(ParseResult parseResult)
    {
        if (!int.TryParse(parseResult.GetValue(TestingPlatformOptions.MaxParallelTestModulesOption), out int degreeOfParallelism) || degreeOfParallelism <= 0)
            degreeOfParallelism = Environment.ProcessorCount;
        return degreeOfParallelism;
    }

    private static TestOptions GetTestOptions(ParseResult parseResult, bool hasFilterMode, bool isHelp) =>
        new(parseResult.GetValue(CommonOptions.ArchitectureOption),
            hasFilterMode,
            isHelp);

    private static bool ContainsHelpOption(IEnumerable<string> args)
    {
        return args.Contains(TestingPlatformOptions.HelpOption.Name) || TestingPlatformOptions.HelpOption.Aliases.Any(alias => args.Contains(alias));
    }

    private static bool ContainsListTestsOption(IEnumerable<string> args)
    {
        return args.Contains(TestingPlatformOptions.ListTestsOption.Name);
    }

    private void CompleteRun()
    {
        if (Interlocked.CompareExchange(ref _cancelled, 1, 0) == 0)
        {
            _output?.TestExecutionCompleted(DateTimeOffset.Now);
        }
    }

    private void CleanUp()
    {
        _msBuildHandler?.Dispose();
        _eventHandlers?.Dispose();
    }
}
