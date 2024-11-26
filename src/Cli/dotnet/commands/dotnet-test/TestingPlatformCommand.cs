// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.CommandLine;
using Microsoft.DotNet.Tools.Test;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.DotNet.Cli
{
    internal partial class TestingPlatformCommand : CliCommand, ICustomHelp
    {
        private readonly ConcurrentBag<TestApplication> _testApplications = [];
        private readonly CancellationTokenSource _cancellationToken = new();

        private MSBuildConnectionHandler _msBuildConnectionHandler;
        private TestModulesFilterHandler _testModulesFilterHandler;
        private TestApplicationActionQueue _actionQueue;
        private Task _namedPipeConnectionLoop;
        private List<string> _args;

        public TestingPlatformCommand(string name, string description = null) : base(name, description)
        {
            TreatUnmatchedTokensAsErrors = false;
        }

        public int Run(ParseResult parseResult)
        {
            // User can decide what the degree of parallelism should be
            // If not specified, we will default to the number of processors
            if (!int.TryParse(parseResult.GetValue(TestingPlatformOptions.MaxParallelTestModulesOption), out int degreeOfParallelism))
                degreeOfParallelism = Environment.ProcessorCount;

            bool filterModeEnabled = parseResult.HasOption(TestingPlatformOptions.TestModulesFilterOption);

            if (filterModeEnabled && parseResult.HasOption(TestingPlatformOptions.ArchitectureOption))
            {
                VSTestTrace.SafeWriteTrace(() => $"The --arch option is not supported yet.");
            }

            BuiltInOptions builtInOptions = new(
                parseResult.HasOption(TestingPlatformOptions.NoRestoreOption),
                parseResult.HasOption(TestingPlatformOptions.NoBuildOption),
                parseResult.GetValue(TestingPlatformOptions.ConfigurationOption),
                parseResult.GetValue(TestingPlatformOptions.ArchitectureOption));

            if (ContainsHelpOption(parseResult.GetArguments()))
            {
                _actionQueue = new(degreeOfParallelism, async (TestApplication testApp) =>
                {
                    testApp.HelpRequested += OnHelpRequested;
                    testApp.ErrorReceived += OnErrorReceived;
                    testApp.TestProcessExited += OnTestProcessExited;
                    testApp.Run += OnTestApplicationRun;
                    testApp.ExecutionIdReceived += OnExecutionIdReceived;

                    return await testApp.RunAsync(filterModeEnabled, enableHelp: true, builtInOptions);
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

            _args = new List<string>(parseResult.UnmatchedTokens);
            _msBuildConnectionHandler = new(_args, _actionQueue);
            _testModulesFilterHandler = new(_args, _actionQueue);
            _namedPipeConnectionLoop = Task.Run(async () => await _msBuildConnectionHandler.WaitConnectionAsync(_cancellationToken.Token));

            if (parseResult.HasOption(TestingPlatformOptions.TestModulesFilterOption))
            {
                if (!_testModulesFilterHandler.RunWithTestModulesFilter(parseResult))
                {
                    return ExitCodes.GenericFailure;
                }
            }
            else
            {
                // If no filter was provided, MSBuild will get the test project paths
                var msbuildResult = _msBuildConnectionHandler.RunWithMSBuild(parseResult);
                if (msbuildResult != 0)
                {
                    VSTestTrace.SafeWriteTrace(() => $"MSBuild task _GetTestsProject didn't execute properly with exit code: {msbuildResult}.");
                    return ExitCodes.GenericFailure;
                }

                // If not all test projects have IsTestingPlatformApplication set to true, we will simply return
                if (!_msBuildConnectionHandler.EnqueueTestApplications())
                {
                    VSTestTrace.SafeWriteTrace(() => LocalizableStrings.CmdUnsupportedVSTestTestApplicationsDescription);
                    return ExitCodes.GenericFailure;
                }
            }


            _actionQueue.EnqueueCompleted();
            var hasFailed = _actionQueue.WaitAllActions();

            // Above line will block till we have all connections and all GetTestsProject msbuild task complete.
            _cancellationToken.Cancel();
            _namedPipeConnectionLoop.Wait();

            // Clean up everything
            CleanUp();

            return hasFailed ? ExitCodes.GenericFailure : ExitCodes.Success;
        }

        private void CleanUp()
        {
            _msBuildConnectionHandler.Dispose();
            foreach (var testApplication in _testApplications)
            {
                testApplication.Dispose();
            }
        }

        private void OnHandshakeReceived(object sender, HandshakeArgs args)
        {
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
                $"{failedTestResult.State}, {failedTestResult.Duration}, {failedTestResult.Reason}, {failedTestResult.ErrorMessage}," +
                $"{failedTestResult.ErrorStackTrace}, {failedTestResult.StandardOutput}, {failedTestResult.ErrorOutput}, {failedTestResult.SessionUid}");
            }
        }

        private void OnFileArtifactsReceived(object sender, FileArtifactEventArgs args)
        {
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
            if (!VSTestTrace.TraceEnabled)
            {
                return;
            }

            if (args.ExitCode != 0)
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
