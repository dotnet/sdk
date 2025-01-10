// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.CommandLine;
using Microsoft.DotNet.Tools.Test;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.DotNet.Cli
{
    internal partial class TestingPlatformCommand : CliCommand, ICustomHelp
    {
        private readonly ConcurrentBag<TestApplication> _testApplications = [];

        private MSBuildHandler _msBuildHandler;
        private TestModulesFilterHandler _testModulesFilterHandler;
        private TerminalTestReporter _output;
        private TestApplicationActionQueue _actionQueue;
        private List<string> _args;
        private ConcurrentDictionary<TestApplication, (string ModulePath, string TargetFramework, string Architecture, string ExecutionId)> _executions = new();
        private byte _cancelled;
        private bool _isDiscovery;

        public TestingPlatformCommand(string name, string description = null) : base(name, description)
        {
            TreatUnmatchedTokensAsErrors = false;
        }

        public async Task<int> Run(ParseResult parseResult)
        {
            bool hasFailed = false;
            try
            {
                Console.CancelKeyPress += (s, e) =>
                {
                    _output?.StartCancelling();
                    CompleteRun();
                };

                // User can decide what the degree of parallelism should be
                // If not specified, we will default to the number of processors
                if (!int.TryParse(parseResult.GetValue(TestingPlatformOptions.MaxParallelTestModulesOption), out int degreeOfParallelism))
                    degreeOfParallelism = Environment.ProcessorCount;

                bool filterModeEnabled = parseResult.HasOption(TestingPlatformOptions.TestModulesFilterOption);

                if (filterModeEnabled && parseResult.HasOption(TestingPlatformOptions.ArchitectureOption))
                {
                    VSTestTrace.SafeWriteTrace(() => $"The --arch option is not supported yet.");
                }

                if (parseResult.HasOption(TestingPlatformOptions.ListTestsOption))
                {
                    _isDiscovery = true;
                }

                BuiltInOptions builtInOptions = new(
                    parseResult.HasOption(TestingPlatformOptions.NoRestoreOption),
                    parseResult.HasOption(TestingPlatformOptions.NoBuildOption),
                    parseResult.HasOption(TestingPlatformOptions.ListTestsOption),
                    parseResult.GetValue(TestingPlatformOptions.ConfigurationOption),
                    parseResult.GetValue(TestingPlatformOptions.ArchitectureOption));

                var console = new SystemConsole();
                var output = new TerminalTestReporter(console, new TerminalTestReporterOptions()
                {
                    ShowPassedTests = Environment.GetEnvironmentVariable("SHOW_PASSED") == "1" ? () => true : () => false,
                    ShowProgress = () => Environment.GetEnvironmentVariable("NO_PROGRESS") != "1",
                    UseAnsi = Environment.GetEnvironmentVariable("NO_ANSI") != "1",
                    ShowAssembly = true,
                    ShowAssemblyStartAndComplete = true,
                });
                _output = output;
                _output.TestExecutionStarted(DateTimeOffset.Now, degreeOfParallelism, _isDiscovery);

                if (ContainsHelpOption(parseResult.GetArguments()))
                {
                    _actionQueue = new(degreeOfParallelism, async (TestApplication testApp) =>
                    {
                        testApp.HelpRequested += OnHelpRequested;
                        testApp.ErrorReceived += OnErrorReceived;
                        testApp.TestProcessExited += OnTestProcessExited;
                        testApp.Run += OnTestApplicationRun;
                        testApp.ExecutionIdReceived += OnExecutionIdReceived;

                        var result = await testApp.RunAsync(filterModeEnabled, enableHelp: true, builtInOptions);
                        CompleteRun();
                        return result;
                    });
                }
                else
                {
                    _actionQueue = new(degreeOfParallelism, async (TestApplication testApp) =>
                    {
                        testApp.HandshakeReceived += OnHandshakeReceived;
                        testApp.DiscoveredTestsReceived += OnDiscoveredTestsReceived;
                        testApp.TestResultsReceived += OnTestResultsReceived;
                        testApp.FileArtifactsReceived += OnFileArtifactsReceived;
                        testApp.SessionEventReceived += OnSessionEventReceived;
                        testApp.ErrorReceived += OnErrorReceived;
                        testApp.TestProcessExited += OnTestProcessExited;
                        testApp.Run += OnTestApplicationRun;
                        testApp.ExecutionIdReceived += OnExecutionIdReceived;

                        return await testApp.RunAsync(filterModeEnabled, enableHelp: false, builtInOptions);
                    });
                }

                _args = [.. parseResult.UnmatchedTokens];
                _msBuildHandler = new(_args, _actionQueue, degreeOfParallelism);
                _testModulesFilterHandler = new(_args, _actionQueue);

                if (parseResult.HasOption(TestingPlatformOptions.TestModulesFilterOption))
                {
                    if (!_testModulesFilterHandler.RunWithTestModulesFilter(parseResult))
                    {
                        CompleteRun();
                        return ExitCodes.GenericFailure;
                    }
                }
                else
                {
                    if (!await RunMSBuild(parseResult))
                    {
                        CompleteRun();
                        return ExitCodes.GenericFailure;
                    }

                    // If not all test projects have IsTestProject and IsTestingPlatformApplication properties set to true, we will simply return
                    if (!_msBuildHandler.EnqueueTestApplications())
                    {
                        VSTestTrace.SafeWriteTrace(() => LocalizableStrings.CmdUnsupportedVSTestTestApplicationsDescription);
                        CompleteRun();
                        return ExitCodes.GenericFailure;
                    }
                }

                _actionQueue.EnqueueCompleted();
                hasFailed = _actionQueue.WaitAllActions();
                // Above line will block till we have all connections and all GetTestsProject msbuild task complete.
            }
            finally
            {
                // Clean up everything
                CleanUp();
            }

            CompleteRun();
            return hasFailed ? ExitCodes.GenericFailure : ExitCodes.Success;
        }

        private async Task<bool> RunMSBuild(ParseResult parseResult)
        {
            int msbuildExitCode;

            if (parseResult.HasOption(TestingPlatformOptions.ProjectOption))
            {
                string filePath = parseResult.GetValue(TestingPlatformOptions.ProjectOption);

                if (!File.Exists(filePath))
                {
                    VSTestTrace.SafeWriteTrace(() => string.Format(LocalizableStrings.CmdNonExistentProjectFilePathDescription, filePath));
                    return false;
                }

                msbuildExitCode = await _msBuildHandler.RunWithMSBuild(filePath);
            }
            else
            {
                // If no filter was provided neither the project using --project,
                // MSBuild will get the test project paths in the current directory
                msbuildExitCode = await _msBuildHandler.RunWithMSBuild();
            }

            if (msbuildExitCode != ExitCodes.Success)
            {
                VSTestTrace.SafeWriteTrace(() => string.Format(LocalizableStrings.CmdMSBuildProjectsPropertiesErrorMessage, msbuildExitCode));
                return false;
            }

            return true;
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
            _msBuildHandler.Dispose();
            foreach (var testApplication in _testApplications)
            {
                testApplication.Dispose();
            }
        }

        private void OnHandshakeReceived(object sender, HandshakeArgs args)
        {
            var testApplication = (TestApplication)sender;
            var executionId = args.Handshake.Properties[HandshakeMessagePropertyNames.ExecutionId];
            var arch = args.Handshake.Properties[HandshakeMessagePropertyNames.Architecture]?.ToLower();
            var tfm = TargetFrameworkParser.GetShortTargetFramework(args.Handshake.Properties[HandshakeMessagePropertyNames.Framework]);
            (string ModulePath, string TargetFramework, string Architecture, string ExecutionId) appInfo = new(testApplication.Module.DllOrExePath, tfm, arch, executionId);
            _executions[testApplication] = appInfo;
            _output.AssemblyRunStarted(appInfo.ModulePath, appInfo.TargetFramework, appInfo.Architecture, appInfo.ExecutionId);

            if (!VSTestTrace.TraceEnabled)
            {
                return;
            }

            var handshake = args.Handshake;

            foreach (var property in handshake.Properties)
            {
                VSTestTrace.SafeWriteTrace(() => $"{property.Key}: {property.Value}");
            }
        }

        private void OnDiscoveredTestsReceived(object sender, DiscoveredTestEventArgs args)
        {
            var testApp = (TestApplication)sender;
            var appInfo = _executions[testApp];

            foreach (var test in args.DiscoveredTests)
            {
                _output.TestDiscovered(appInfo.ModulePath, appInfo.TargetFramework, appInfo.Architecture, appInfo.ExecutionId,
                        test.DisplayName,
                        test.Uid);
            }

            if (!VSTestTrace.TraceEnabled)
            {
                return;
            }

            var discoveredTestMessages = args.DiscoveredTests;
            VSTestTrace.SafeWriteTrace(() => $"DiscoveredTests Execution Id: {args.ExecutionId}");
            foreach (DiscoveredTest discoveredTestMessage in discoveredTestMessages)
            {
                VSTestTrace.SafeWriteTrace(() => $"DiscoveredTest: {discoveredTestMessage.Uid}, {discoveredTestMessage.DisplayName}");
            }
        }

        private void OnTestResultsReceived(object sender, TestResultEventArgs args)
        {
            foreach (var testResult in args.SuccessfulTestResults)
            {
                var testApp = (TestApplication)sender;
                var appInfo = _executions[testApp];
                _output.TestCompleted(appInfo.ModulePath, appInfo.TargetFramework, appInfo.Architecture, appInfo.ExecutionId,
                    testResult.Uid,
                    testResult.DisplayName,
                    ToOutcome(testResult.State),
                    TimeSpan.FromTicks(testResult.Duration ?? 0),
                    exceptions: null,
                    expected: null,
                    actual: null,
                    standardOutput: null,
                    errorOutput: null);
            }

            foreach (var testResult in args.FailedTestResults)
            {
                var testApp = (TestApplication)sender;
                // TODO: expected
                // TODO: actual
                var appInfo = _executions[testApp];
                _output.TestCompleted(appInfo.ModulePath, appInfo.TargetFramework, appInfo.Architecture, appInfo.ExecutionId,
                    testResult.Uid,
                    testResult.DisplayName,
                    ToOutcome(testResult.State),
                    TimeSpan.FromTicks(testResult.Duration ?? 0),
                    exceptions: testResult.Exceptions.Select(fe => new Microsoft.Testing.Platform.OutputDevice.Terminal.FlatException(fe.ErrorMessage, fe.ErrorType, fe.StackTrace)).ToArray(),
                    expected: null,
                    actual: null,
                    standardOutput: null,
                    errorOutput: null);
            }

            if (!VSTestTrace.TraceEnabled)
            {
                return;
            }

            VSTestTrace.SafeWriteTrace(() => $"TestResults Execution Id: {args.ExecutionId}");

            foreach (SuccessfulTestResult successfulTestResult in args.SuccessfulTestResults)
            {
                VSTestTrace.SafeWriteTrace(() => $"SuccessfulTestResult: {successfulTestResult.Uid}, {successfulTestResult.DisplayName}, " +
                $"{successfulTestResult.State}, {successfulTestResult.Duration}, {successfulTestResult.Reason}, {successfulTestResult.StandardOutput}," +
                $"{successfulTestResult.ErrorOutput}, {successfulTestResult.SessionUid}");
            }

            foreach (FailedTestResult failedTestResult in args.FailedTestResults)
            {
                VSTestTrace.SafeWriteTrace(() => $"FailedTestResult: {failedTestResult.Uid}, {failedTestResult.DisplayName}, " +
                $"{failedTestResult.State}, {failedTestResult.Duration}, {failedTestResult.Reason}, {string.Join(", ", failedTestResult.Exceptions?.Select(e => $"{e.ErrorMessage}, {e.ErrorType}, {e.StackTrace}"))}" +
                $"{failedTestResult.StandardOutput}, {failedTestResult.ErrorOutput}, {failedTestResult.SessionUid}");
            }
        }

        public static TestOutcome ToOutcome(byte? testState)
        {
            return testState switch
            {
                TestStates.Passed => TestOutcome.Passed,
                TestStates.Skipped => TestOutcome.Skipped,
                TestStates.Failed => TestOutcome.Fail,
                TestStates.Error => TestOutcome.Error,
                TestStates.Timeout => TestOutcome.Timeout,
                TestStates.Cancelled => TestOutcome.Canceled,
                _ => throw new ArgumentOutOfRangeException(nameof(testState), $"Invalid test state value {testState}")
            };
        }

        private void OnFileArtifactsReceived(object sender, FileArtifactEventArgs args)
        {
            var testApp = (TestApplication)sender;
            var appInfo = _executions[testApp];

            foreach (var artifact in args.FileArtifacts)
            {
                // TODO: Is artifact out of process
                _output.ArtifactAdded(
                    outOfProcess: false,
                    appInfo.ModulePath, appInfo.TargetFramework, appInfo.Architecture, appInfo.ExecutionId,
                    artifact.TestDisplayName, artifact.FullPath);
            }

            if (!VSTestTrace.TraceEnabled)
            {
                return;
            }

            VSTestTrace.SafeWriteTrace(() => $"FileArtifactMessages Execution Id: {args.ExecutionId}");

            foreach (FileArtifact fileArtifactMessage in args.FileArtifacts)
            {
                VSTestTrace.SafeWriteTrace(() => $"FileArtifact: {fileArtifactMessage.FullPath}, {fileArtifactMessage.DisplayName}, " +
                $"{fileArtifactMessage.Description}, {fileArtifactMessage.TestUid}, {fileArtifactMessage.TestDisplayName}, " +
                $"{fileArtifactMessage.SessionUid}");
            }
        }

        private void OnSessionEventReceived(object sender, SessionEventArgs args)
        {
            if (!VSTestTrace.TraceEnabled)
            {
                return;
            }

            var sessionEvent = args.SessionEvent;
            VSTestTrace.SafeWriteTrace(() => $"TestSessionEvent: {sessionEvent.SessionType}, {sessionEvent.SessionUid}, {sessionEvent.ExecutionId}");
        }

        private void OnErrorReceived(object sender, ErrorEventArgs args)
        {
            if (!VSTestTrace.TraceEnabled)
            {
                return;
            }

            VSTestTrace.SafeWriteTrace(() => args.ErrorMessage);
        }

        private void OnTestProcessExited(object sender, TestProcessExitEventArgs args)
        {
            var testApplication = (TestApplication)sender;

            // If the application exits too early we might not start the execution,
            // e.g. if the parameter is incorrect.
            if (_executions.TryGetValue(testApplication, out var appInfo))
            {
                _output.AssemblyRunCompleted(appInfo.ModulePath, appInfo.TargetFramework, appInfo.Architecture, appInfo.ExecutionId, args.ExitCode, string.Join(Environment.NewLine, args.OutputData), string.Join(Environment.NewLine, args.ErrorData));
            }

            if (!VSTestTrace.TraceEnabled)
            {
                return;
            }

            if (args.ExitCode != ExitCodes.Success)
            {
                VSTestTrace.SafeWriteTrace(() => $"Test Process exited with non-zero exit code: {args.ExitCode}");
            }

            if (args.OutputData.Count > 0)
            {
                VSTestTrace.SafeWriteTrace(() => $"Output Data: {string.Join("\n", args.OutputData)}");
            }

            if (args.ErrorData.Count > 0)
            {
                VSTestTrace.SafeWriteTrace(() => $"Error Data: {string.Join("\n", args.ErrorData)}");
            }
        }

        private void OnTestApplicationRun(object sender, EventArgs args)
        {
            TestApplication testApp = sender as TestApplication;
            _testApplications.Add(testApp);
        }

        private void OnExecutionIdReceived(object sender, ExecutionEventArgs args)
        {
        }

        private static bool ContainsHelpOption(IEnumerable<string> args) => args.Contains(CliConstants.HelpOptionKey) || args.Contains(CliConstants.HelpOptionKey.Substring(0, 2));
    }
}
