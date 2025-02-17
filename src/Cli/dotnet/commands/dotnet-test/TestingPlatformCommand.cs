// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.CommandLine;
using Microsoft.DotNet.Tools.Test;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.DotNet.Cli
{
    internal partial class TestingPlatformCommand : CliCommand, ICustomHelp
    {
        private MSBuildHandler _msBuildHandler;
        private TerminalTestReporter _output;
        private TestApplicationActionQueue _actionQueue;
        private readonly ConcurrentDictionary<TestApplication, (string ModulePath, string TargetFramework, string Architecture, string ExecutionId)> _executions = new();
        private byte _cancelled;
        private bool _isDiscovery;
        private TestApplicationsEventHandlers _eventHandlers;

        public TestingPlatformCommand(string name, string description = null) : base(name, description)
        {
            TreatUnmatchedTokensAsErrors = false;
        }

        public int Run(ParseResult parseResult)
        {
            bool hasFailed = false;
            try
            {
                PrepareEnvironment(parseResult, out TestOptions testOptions, out int degreeOfParallelism);

                InitializeOutput(degreeOfParallelism, parseResult, testOptions.IsHelp);

                InitializeActionQueue(degreeOfParallelism, testOptions, testOptions.IsHelp);

                BuildOptions buildOptions = GetBuildOptions(parseResult, degreeOfParallelism);
                _msBuildHandler = new(buildOptions.UnmatchedTokens, _actionQueue, _output);
                TestModulesFilterHandler testModulesFilterHandler = new(buildOptions.UnmatchedTokens, _actionQueue);

                _eventHandlers = new TestApplicationsEventHandlers(_executions, _output);

                if (testOptions.HasFilterMode)
                {
                    if (!testModulesFilterHandler.RunWithTestModulesFilter(parseResult))
                    {
                        return ExitCodes.GenericFailure;
                    }
                }
                else
                {
                    if (!_msBuildHandler.RunMSBuild(buildOptions))
                    {
                        return ExitCodes.GenericFailure;
                    }

                    if (!_msBuildHandler.EnqueueTestApplications())
                    {
                        _output.WriteMessage(LocalizableStrings.CmdUnsupportedVSTestTestApplicationsDescription);
                        return ExitCodes.GenericFailure;
                    }
                }

                _actionQueue.EnqueueCompleted();
                hasFailed = _actionQueue.WaitAllActions();
            }
            finally
            {
                CompleteRun();
                CleanUp();
            }

            return hasFailed ? ExitCodes.GenericFailure : ExitCodes.Success;
        }

        private void PrepareEnvironment(ParseResult parseResult, out TestOptions testOptions, out int degreeOfParallelism)
        {
            SetupCancelKeyPressHandler();

            degreeOfParallelism = GetDegreeOfParallelism(parseResult);

            bool filterModeEnabled = parseResult.HasOption(TestingPlatformOptions.TestModulesFilterOption);
            bool isHelp = ContainsHelpOption(parseResult.GetArguments());

            testOptions = GetTestOptions(parseResult, filterModeEnabled, isHelp);

            _isDiscovery = parseResult.HasOption(TestingPlatformOptions.ListTestsOption);
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
            var showPassedTests = parseResult.GetValue<OutputOptions>(TestingPlatformOptions.OutputOption) == OutputOptions.Detailed;
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
                testApp.ExecutionIdReceived += _eventHandlers.OnExecutionIdReceived;

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
                testApp.ExecutionIdReceived += _eventHandlers.OnExecutionIdReceived;

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
            new(parseResult.HasOption(TestingPlatformOptions.ListTestsOption),
                parseResult.GetValue(TestingPlatformOptions.ConfigurationOption),
                parseResult.GetValue(TestingPlatformOptions.ArchitectureOption),
                hasFilterMode,
                isHelp);

        private static BuildOptions GetBuildOptions(ParseResult parseResult, int degreeOfParallelism)
        {
            IEnumerable<string> propertyTokens = MSBuildUtility.GetPropertyTokens(parseResult.UnmatchedTokens);
            IEnumerable<string> binaryLoggerTokens = MSBuildUtility.GetBinaryLoggerTokens(parseResult.UnmatchedTokens);

            var msbuildArgs = parseResult.OptionValuesToBeForwarded(TestCommandParser.GetCommand())
                .Concat(propertyTokens)
                .Concat(binaryLoggerTokens).ToList();

            List<string> unmatchedTokens = [.. parseResult.UnmatchedTokens];
            unmatchedTokens.RemoveAll(arg => propertyTokens.Contains(arg));
            unmatchedTokens.RemoveAll(arg => binaryLoggerTokens.Contains(arg));

            PathOptions pathOptions = new(parseResult.GetValue(TestingPlatformOptions.ProjectOption),
                parseResult.GetValue(TestingPlatformOptions.SolutionOption),
                parseResult.GetValue(TestingPlatformOptions.DirectoryOption));

            string runtimeIdentifier = parseResult.HasOption(TestingPlatformOptions.ArchitectureOption) ?
                    CommonOptions.ResolveRidShorthandOptionsToRuntimeIdentifier(string.Empty, parseResult.GetValue(TestingPlatformOptions.ArchitectureOption)) :
                    string.Empty;

            return new BuildOptions(
                pathOptions,
                parseResult.GetValue(CommonOptions.NoRestoreOption),
                parseResult.GetValue(TestingPlatformOptions.NoBuildOption),
                parseResult.GetValue(TestingPlatformOptions.ConfigurationOption),
                runtimeIdentifier,
                degreeOfParallelism,
                unmatchedTokens,
                msbuildArgs);
        }

        private static bool ContainsHelpOption(IEnumerable<string> args) => args.Contains(CliConstants.HelpOptionKey) || args.Contains(CliConstants.ShortHelpOptionKey);

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
            foreach (var execution in _executions)
            {
                execution.Key.Dispose();
            }
        }
    }
}
