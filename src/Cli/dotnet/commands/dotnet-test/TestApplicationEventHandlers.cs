// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using Microsoft.DotNet.Tools.Test;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.DotNet.Cli
{
    internal sealed class TestApplicationsEventHandlers
    {
        private readonly ConcurrentDictionary<TestApplication, (string ModulePath, string TargetFramework, string Architecture, string ExecutionId)> _executions;
        private readonly TerminalTestReporter _output;

        public TestApplicationsEventHandlers(
            ConcurrentDictionary<TestApplication, (string ModulePath, string TargetFramework, string Architecture, string ExecutionId)> executions,
            TerminalTestReporter output)
        {
            _executions = executions;
            _output = output;
        }

        public void OnHandshakeReceived(object sender, HandshakeArgs args)
        {
            var testApplication = (TestApplication)sender;
            var executionId = args.Handshake.Properties[HandshakeMessagePropertyNames.ExecutionId];
            var arch = args.Handshake.Properties[HandshakeMessagePropertyNames.Architecture]?.ToLower();
            var tfm = TargetFrameworkParser.GetShortTargetFramework(args.Handshake.Properties[HandshakeMessagePropertyNames.Framework]);
            (string ModulePath, string TargetFramework, string Architecture, string ExecutionId) appInfo = new(testApplication.Module.DllOrExePath, tfm, arch, executionId);
            _executions[testApplication] = appInfo;
            _output.AssemblyRunStarted(appInfo.ModulePath, appInfo.TargetFramework, appInfo.Architecture, appInfo.ExecutionId);

            if (!VSTestTrace.TraceEnabled) return;

            foreach (var property in args.Handshake.Properties)
            {
                VSTestTrace.SafeWriteTrace(() => $"{GetHandshakePropertyName(property.Key)}: {property.Value}");
            }
        }

        private static string GetHandshakePropertyName(byte propertyId) =>
            propertyId switch
            {
                HandshakeMessagePropertyNames.PID => nameof(HandshakeMessagePropertyNames.PID),
                HandshakeMessagePropertyNames.Architecture => nameof(HandshakeMessagePropertyNames.Architecture),
                HandshakeMessagePropertyNames.Framework => nameof(HandshakeMessagePropertyNames.Framework),
                HandshakeMessagePropertyNames.OS => nameof(HandshakeMessagePropertyNames.OS),
                HandshakeMessagePropertyNames.SupportedProtocolVersions => nameof(HandshakeMessagePropertyNames.SupportedProtocolVersions),
                HandshakeMessagePropertyNames.HostType => nameof(HandshakeMessagePropertyNames.HostType),
                HandshakeMessagePropertyNames.ModulePath => nameof(HandshakeMessagePropertyNames.ModulePath),
                HandshakeMessagePropertyNames.ExecutionId => nameof(HandshakeMessagePropertyNames.ExecutionId),
                _ => string.Empty,
            };

        public void OnDiscoveredTestsReceived(object sender, DiscoveredTestEventArgs args)
        {
            var testApp = (TestApplication)sender;
            var appInfo = _executions[testApp];

            foreach (var test in args.DiscoveredTests)
            {
                _output.TestDiscovered(appInfo.ModulePath, appInfo.TargetFramework, appInfo.Architecture, appInfo.ExecutionId,
                        test.DisplayName,
                        test.Uid);
            }

            if (!VSTestTrace.TraceEnabled) return;

            VSTestTrace.SafeWriteTrace(() => $"DiscoveredTests Execution Id: {args.ExecutionId}");
            foreach (var discoveredTestMessage in args.DiscoveredTests)
            {
                VSTestTrace.SafeWriteTrace(() => $"DiscoveredTest: {discoveredTestMessage.Uid}, {discoveredTestMessage.DisplayName}");
            }
        }

        public void OnTestResultsReceived(object sender, TestResultEventArgs args)
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

            if (!VSTestTrace.TraceEnabled) return;

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

        public void OnFileArtifactsReceived(object sender, FileArtifactEventArgs args)
        {
            var testApp = (TestApplication)sender;
            var appInfo = _executions[testApp];

            foreach (var artifact in args.FileArtifacts)
            {
                _output.ArtifactAdded(
                    outOfProcess: false,
                    appInfo.ModulePath, appInfo.TargetFramework, appInfo.Architecture, appInfo.ExecutionId,
                    artifact.TestDisplayName, artifact.FullPath);
            }

            if (!VSTestTrace.TraceEnabled) return;

            VSTestTrace.SafeWriteTrace(() => $"FileArtifactMessages Execution Id: {args.ExecutionId}");

            foreach (FileArtifact fileArtifactMessage in args.FileArtifacts)
            {
                VSTestTrace.SafeWriteTrace(() => $"FileArtifact: {fileArtifactMessage.FullPath}, {fileArtifactMessage.DisplayName}, " +
                $"{fileArtifactMessage.Description}, {fileArtifactMessage.TestUid}, {fileArtifactMessage.TestDisplayName}, " +
                $"{fileArtifactMessage.SessionUid}");
            }
        }

        public void OnSessionEventReceived(object sender, SessionEventArgs args)
        {
            if (!VSTestTrace.TraceEnabled) return;

            var sessionEvent = args.SessionEvent;
            VSTestTrace.SafeWriteTrace(() => $"TestSessionEvent: {sessionEvent.SessionType}, {sessionEvent.SessionUid}, {sessionEvent.ExecutionId}");
        }

        public void OnErrorReceived(object sender, ErrorEventArgs args)
        {
            if (!VSTestTrace.TraceEnabled) return;

            VSTestTrace.SafeWriteTrace(() => args.ErrorMessage);
        }

        public void OnTestProcessExited(object sender, TestProcessExitEventArgs args)
        {
            var testApplication = (TestApplication)sender;

            if (_executions.TryGetValue(testApplication, out var appInfo))
            {
                _output.AssemblyRunCompleted(appInfo.ModulePath, appInfo.TargetFramework, appInfo.Architecture, appInfo.ExecutionId, args.ExitCode, string.Join(Environment.NewLine, args.OutputData), string.Join(Environment.NewLine, args.ErrorData));
            }
            else
            {
                _output.AssemblyRunCompleted(testApplication.Module.DllOrExePath ?? testApplication.Module.ProjectPath, testApplication.Module.TargetFramework, architecture: null, null, args.ExitCode, string.Join(Environment.NewLine, args.OutputData), string.Join(Environment.NewLine, args.ErrorData));
            }

            if (!VSTestTrace.TraceEnabled) return;

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

        public void OnExecutionIdReceived(object sender, ExecutionEventArgs args)
        {
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
    }
}
