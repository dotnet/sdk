// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice.Terminal;
using Microsoft.Testing.TestInfrastructure;

namespace Microsoft.DotNet.Cli
{
    internal partial class TestingPlatformCommand : CliCommand, ICustomHelp
    {
        private readonly ConcurrentDictionary<string, TestApplication> _testApplications = [];
        private readonly CancellationTokenSource _cancellationToken = new();

        private MSBuildConnectionHandler _msBuildConnectionHandler;
        private TestModulesFilterHandler _testModulesFilterHandler;
        private TerminalTestReporter _output;
        private TestApplicationActionQueue _actionQueue;
        private Task _namedPipeConnectionLoop;
        private string[] _args;
        private Dictionary<TestApplication, (string ModulePath, string TargetFramework, string Architecture, string ExecutionId)> _executions = new();

        public TestingPlatformCommand(string name, string description = null) : base(name, description)
        {
            TreatUnmatchedTokensAsErrors = false;
        }

        public int Run(ParseResult parseResult)
        {
            if (Environment.GetEnvironmentVariable("Debug") == "1")
            {
                DebuggerUtility.AttachCurrentProcessToParentVSProcess();
            }

            if (parseResult.HasOption(TestingPlatformOptions.ArchitectureOption))
            {
                VSTestTrace.SafeWriteTrace(() => $"The --arch option is not yet supported.");
                return ExitCodes.GenericFailure;
            }

            // User can decide what the degree of parallelism should be
            // If not specified, we will default to the number of processors
            if (!int.TryParse(parseResult.GetValue(TestingPlatformOptions.MaxParallelTestModulesOption), out int degreeOfParallelism))
                degreeOfParallelism = Environment.ProcessorCount;

            var console = new SystemConsole();
            var output = new TerminalTestReporter(console, new TerminalTestReporterOptions()
            {
                UseAnsi = true,
                ShowAssembly = true,
                ShowAssemblyStartAndComplete = true,
            });
            _output = output;
            _output.TestExecutionStarted(DateTimeOffset.Now, degreeOfParallelism);

            if (ContainsHelpOption(parseResult.GetArguments()))
            {
                _actionQueue = new(degreeOfParallelism, async (TestApplication testApp) =>
                {
                    testApp.HelpRequested += OnHelpRequested;
                    testApp.ErrorReceived += OnErrorReceived;
                    testApp.TestProcessExited += OnTestProcessExited;
                    testApp.Created += OnTestApplicationCreated;
                    testApp.ExecutionIdReceived += OnExecutionIdReceived;

                    var result = await testApp.RunAsync(enableHelp: true);
                    _output.TestExecutionCompleted(DateTimeOffset.Now);
                    return result;
                });
            }
            else
            {
                _actionQueue = new(degreeOfParallelism, async (TestApplication testApp) =>
                {
                    testApp.HandshakeInfoReceived += OnHandshakeInfoReceived;
                    testApp.DiscoveredTestReceived += OnDiscoveredTestReceived;
                    testApp.SuccessfulTestResultReceived += OnTestResultReceived;
                    testApp.FailedTestResultReceived += OnTestResultReceived;
                    testApp.FileArtifactInfoReceived += OnFileArtifactInfoReceived;
                    testApp.SessionEventReceived += OnSessionEventReceived;
                    testApp.ErrorReceived += OnErrorReceived;
                    testApp.TestProcessExited += OnTestProcessExited;
                    testApp.Created += OnTestApplicationCreated;
                    testApp.ExecutionIdReceived += OnExecutionIdReceived;

                    var result = await testApp.RunAsync(enableHelp: false);
                    return result;
                });
            }

            _args = [.. parseResult.UnmatchedTokens];
            _msBuildConnectionHandler = new(_args, _actionQueue);
            _testModulesFilterHandler = new(_args, _actionQueue);
            _namedPipeConnectionLoop = Task.Run(async () => await _msBuildConnectionHandler.WaitConnectionAsync(_cancellationToken.Token));

            if (parseResult.HasOption(TestingPlatformOptions.TestModulesFilterOption))
            {
                if (!_testModulesFilterHandler.RunWithTestModulesFilter(parseResult))
                {
                    _output.TestExecutionCompleted(DateTimeOffset.Now);
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
                    _output.TestExecutionCompleted(DateTimeOffset.Now);
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

            _output.TestExecutionCompleted(DateTimeOffset.Now);
            return hasFailed ? ExitCodes.GenericFailure : ExitCodes.Success;
        }

        private void CleanUp()
        {
            _msBuildConnectionHandler.Dispose();
            foreach (var testApplication in _testApplications.Values)
            {
                testApplication.Dispose();
            }
        }

        private void OnHandshakeInfoReceived(object sender, HandshakeInfoArgs args)
        {
            var testApplication = (TestApplication)sender;
            var executionId = args.HandshakeInfo.Properties[HandshakeInfoPropertyNames.ExecutionId];
            var arch = args.HandshakeInfo.Properties[HandshakeInfoPropertyNames.Architecture];
            var tfm = args.HandshakeInfo.Properties[HandshakeInfoPropertyNames.Framework];
            (string ModulePath, string TargetFramework, string Architecture, string ExecutionId) appInfo = new(testApplication.ModulePath, tfm, arch, executionId);
            _executions[testApplication] = appInfo;
            _output.AssemblyRunStarted(appInfo.ModulePath, appInfo.TargetFramework, appInfo.Architecture, appInfo.ExecutionId);

            if (!VSTestTrace.TraceEnabled)
            {
                return;
            }

            var handshakeInfo = args.HandshakeInfo;

            foreach (var property in handshakeInfo.Properties)
            {
                VSTestTrace.SafeWriteTrace(() => $"{property.Key}: {property.Value}");
            }
        }

        private void OnDiscoveredTestReceived(object sender, DiscoveredTestEventArgs args)
        {
            if (!VSTestTrace.TraceEnabled)
            {
                return;
            }

            var successfulTestResultMessage = args.DiscoveredTestMessage;
            VSTestTrace.SafeWriteTrace(() => $"DiscoveredTestMessage: {successfulTestResultMessage.Uid}, {successfulTestResultMessage.DisplayName}, {successfulTestResultMessage.ExecutionId}");
        }

        private void OnTestResultReceived(object sender, EventArgs args)
        {
            if (args is SuccessfulTestResultEventArgs success)
            {
                var testApp = (TestApplication)sender;
                var appInfo = _executions[testApp];
                // TODO: timespan for duration
                _output.TestCompleted(appInfo.ModulePath, appInfo.TargetFramework, appInfo.Architecture, appInfo.ExecutionId,
                    success.SuccessfulTestResultMessage.DisplayName,
                    TestOutcome.Passed,
                    TimeSpan.FromSeconds(1),
                    errorMessage: null,
                    errorStackTrace: null,
                    expected: null,
                    actual: null);
            }
            else if (args is FailedTestResultEventArgs failed)
            {
                var testApp = (TestApplication)sender;
                // TODO: timespan for duration
                // TODO: expected
                // TODO: actual
                var appInfo = _executions[testApp];
                _output.TestCompleted(appInfo.ModulePath, appInfo.TargetFramework, appInfo.Architecture, appInfo.ExecutionId,
                    failed.FailedTestResultMessage.DisplayName,
                    TestOutcome.Fail,
                    TimeSpan.FromSeconds(1),
                    errorMessage: failed.FailedTestResultMessage.ErrorMessage,
                    errorStackTrace: failed.FailedTestResultMessage.ErrorStackTrace,
                    expected: null, actual: null);
            }


            if (!VSTestTrace.TraceEnabled)
            {
                return;
            }

            if (args is SuccessfulTestResultEventArgs successfulTestResultEventArgs)
            {
                var successfulTestResultMessage = successfulTestResultEventArgs.SuccessfulTestResultMessage;
                VSTestTrace.SafeWriteTrace(() => $"TestResultMessage: {successfulTestResultMessage.Uid}, {successfulTestResultMessage.DisplayName}, " +
                $"{successfulTestResultMessage.State}, {successfulTestResultMessage.Reason}, {successfulTestResultMessage.SessionUid}, {successfulTestResultMessage.ExecutionId}");
            }
            else if (args is FailedTestResultEventArgs failedTestResultEventArgs)
            {
                var failedTestResultMessage = failedTestResultEventArgs.FailedTestResultMessage;
                VSTestTrace.SafeWriteTrace(() => $"TestResultMessage: {failedTestResultMessage.Uid}, {failedTestResultMessage.DisplayName}, " +
                $"{failedTestResultMessage.State}, {failedTestResultMessage.Reason}, {failedTestResultMessage.ErrorMessage}," +
                $" {failedTestResultMessage.ErrorStackTrace}, {failedTestResultMessage.SessionUid}, {failedTestResultMessage.ExecutionId}");
            }
        }

        private void OnFileArtifactInfoReceived(object sender, FileArtifactInfoEventArgs args)
        {
            if (!VSTestTrace.TraceEnabled)
            {
                return;
            }

            var fileArtifactInfo = args.FileArtifactInfo;
            VSTestTrace.SafeWriteTrace(() => $"FileArtifactInfo: {fileArtifactInfo.FullPath}, {fileArtifactInfo.DisplayName}, " +
                $"{fileArtifactInfo.Description}, {fileArtifactInfo.TestUid}, {fileArtifactInfo.TestDisplayName}, " +
                $"{fileArtifactInfo.SessionUid}, {fileArtifactInfo.ExecutionId}");
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

            var appInfo = _executions[testApplication];
            _output.AssemblyRunCompleted(appInfo.ModulePath, appInfo.TargetFramework, appInfo.Architecture, appInfo.ExecutionId);

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

        private void OnTestApplicationCreated(object sender, EventArgs args)
        {
            TestApplication testApp = sender as TestApplication;
            _testApplications[testApp.ModulePath] = testApp;

            VSTestTrace.SafeWriteTrace(() => $"Created {testApp.ModulePath}");


        }

        private void OnExecutionIdReceived(object sender, ExecutionEventArgs args)
        {
            if (_testApplications.TryGetValue(args.ModulePath, out var testApp))
            {
                VSTestTrace.SafeWriteTrace(() => $"id {args.ModulePath}");

                testApp.AddExecutionId(args.ExecutionId);
            }
        }

        private static bool ContainsHelpOption(IEnumerable<string> args) => args.Contains(CliConstants.HelpOptionKey) || args.Contains(CliConstants.HelpOptionKey.Substring(0, 2));
    }
}


