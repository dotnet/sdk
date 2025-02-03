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
        private TestModulesFilterHandler _testModulesFilterHandler;
        private TerminalTestReporter _output;
        private bool _isHelp;
        private int _degreeOfParallelism;
        private TestApplicationActionQueue _actionQueue;
        private List<string> _args;
        private ConcurrentDictionary<TestApplication, (string ModulePath, string TargetFramework, string Architecture, string ExecutionId)> _executions = new();
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
                SetupCancelKeyPressHandler();

                _degreeOfParallelism = GetDegreeOfParallelism(parseResult);
                BuildConfigurationOptions buildConfigurationOptions = GetBuildConfigurationOptions(parseResult);

                _isDiscovery = parseResult.HasOption(TestingPlatformOptions.ListTestsOption);
                _args = [.. parseResult.UnmatchedTokens];
                _isHelp = ContainsHelpOption(parseResult.GetArguments());

                InitializeOutput(_degreeOfParallelism);

                bool filterModeEnabled = parseResult.HasOption(TestingPlatformOptions.TestModulesFilterOption);
                if (_isHelp)
                {
                    InitializeHelpActionQueue(_degreeOfParallelism, buildConfigurationOptions, filterModeEnabled);
                }
                else
                {
                    InitializeTestExecutionActionQueue(_degreeOfParallelism, buildConfigurationOptions, filterModeEnabled);
                }

                _msBuildHandler = new(_args, _actionQueue, _output);
                _testModulesFilterHandler = new(_args, _actionQueue);

                _eventHandlers = new TestApplicationsEventHandlers(_executions, _output);

                if (filterModeEnabled)
                {
                    if (!_testModulesFilterHandler.RunWithTestModulesFilter(parseResult))
                    {
                        return ExitCodes.GenericFailure;
                    }
                }
                else
                {
                    if (!_msBuildHandler.RunMSBuild(GetBuildOptions(parseResult)))
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

        private void SetupCancelKeyPressHandler()
        {
            Console.CancelKeyPress += (s, e) =>
            {
                _output?.StartCancelling();
                CompleteRun();
            };
        }

        private void InitializeOutput(int degreeOfParallelism)
        {
            var console = new SystemConsole();
            _output = new TerminalTestReporter(console, new TerminalTestReporterOptions()
            {
                ShowPassedTests = Environment.GetEnvironmentVariable("SHOW_PASSED") == "1" ? () => true : () => false,
                ShowProgress = () => Environment.GetEnvironmentVariable("NO_PROGRESS") != "1",
                UseAnsi = Environment.GetEnvironmentVariable("NO_ANSI") != "1",
                ShowAssembly = true,
                ShowAssemblyStartAndComplete = true,
            });

            if (!_isHelp)
            {
                _output.TestExecutionStarted(DateTimeOffset.Now, degreeOfParallelism, _isDiscovery, _isHelp);
            }
        }

        private void InitializeHelpActionQueue(int degreeOfParallelism, BuildConfigurationOptions buildConfigurationOptions, bool filterModeEnabled)
        {
            _actionQueue = new(degreeOfParallelism, async (TestApplication testApp) =>
            {
                testApp.HelpRequested += OnHelpRequested;
                testApp.ErrorReceived += _eventHandlers.OnErrorReceived;
                testApp.TestProcessExited += _eventHandlers.OnTestProcessExited;
                testApp.ExecutionIdReceived += _eventHandlers.OnExecutionIdReceived;

                return await testApp.RunAsync(filterModeEnabled, enableHelp: true, buildConfigurationOptions);
            });
        }

        private void InitializeTestExecutionActionQueue(int degreeOfParallelism, BuildConfigurationOptions buildConfigurationOptions, bool filterModeEnabled)
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

                return await testApp.RunAsync(filterModeEnabled, enableHelp: false, buildConfigurationOptions);
            });
        }

        private static int GetDegreeOfParallelism(ParseResult parseResult)
        {
            if (!int.TryParse(parseResult.GetValue(TestingPlatformOptions.MaxParallelTestModulesOption), out int degreeOfParallelism) || degreeOfParallelism <= 0)
                degreeOfParallelism = Environment.ProcessorCount;
            return degreeOfParallelism;
        }

        private static BuildConfigurationOptions GetBuildConfigurationOptions(ParseResult parseResult) =>
            new(parseResult.HasOption(TestingPlatformOptions.ListTestsOption),
                parseResult.GetValue(TestingPlatformOptions.ConfigurationOption),
                parseResult.GetValue(TestingPlatformOptions.ArchitectureOption));

        private BuildOptions GetBuildOptions(ParseResult parseResult)
        {
            bool allowBinLog = MSBuildUtility.IsBinaryLoggerEnabled([.. parseResult.UnmatchedTokens], out string binLogFileName);

            return new BuildOptions(parseResult.GetValue(TestingPlatformOptions.ProjectOption),
                parseResult.GetValue(TestingPlatformOptions.SolutionOption),
                parseResult.GetValue(TestingPlatformOptions.DirectoryOption),
                parseResult.HasOption(TestingPlatformOptions.NoRestoreOption),
                parseResult.HasOption(TestingPlatformOptions.NoBuildOption),
                parseResult.GetValue(TestingPlatformOptions.ConfigurationOption),
                parseResult.HasOption(TestingPlatformOptions.ArchitectureOption) ?
                    CommonOptions.ResolveRidShorthandOptionsToRuntimeIdentifier(string.Empty, parseResult.GetValue(TestingPlatformOptions.ArchitectureOption)) :
                    string.Empty,
                allowBinLog,
                binLogFileName,
                _degreeOfParallelism);
        }

        private static bool ContainsHelpOption(IEnumerable<string> args) => args.Contains(CliConstants.HelpOptionKey) || args.Contains(CliConstants.HelpOptionKey.Substring(0, 2));

        private void CompleteRun()
        {
            if (Interlocked.CompareExchange(ref _cancelled, 1, 0) == 0)
            {
                _output?.TestExecutionCompleted(DateTimeOffset.Now);
            }
        }

        private void CleanUp()
        {
            _msBuildHandler.Dispose();
            foreach (var execution in _executions)
            {
                execution.Key.Dispose();
            }
        }
    }
}
