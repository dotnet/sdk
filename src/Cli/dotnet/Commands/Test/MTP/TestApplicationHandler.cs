// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Diagnostics;
using System.Reflection;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal sealed class TestApplicationHandler
{
    private readonly TerminalTestReporter _output;
    private readonly TestModule _module;
    private readonly TestOptions _options;

    private (string TargetFramework, string Architecture, string ExecutionId)? _handshakeInfo;

    public TestApplicationHandler(TerminalTestReporter output, TestModule module, TestOptions options)
    {
        _output = output;
        _module = module;
        _options = options;
    }

    internal void OnHandshakeReceived(HandshakeMessage handshakeMessage, bool gotSupportedVersion)
    {
        if (!gotSupportedVersion)
        {
            _output.HandshakeFailure(
                _module.TargetPath,
                string.Empty,
                ExitCode.GenericFailure,
                string.Format(
                    CliCommandStrings.DotnetTestIncompatibleHandshakeVersion,
                    handshakeMessage.Properties[HandshakeMessagePropertyNames.SupportedProtocolVersions],
                    ProtocolConstants.SupportedVersions),
                string.Empty);
        }

        if (!_handshakeInfo.HasValue)
        {
            var executionId = handshakeMessage.Properties[HandshakeMessagePropertyNames.ExecutionId];
            var arch = handshakeMessage.Properties[HandshakeMessagePropertyNames.Architecture]?.ToLower();
            var tfm = TargetFrameworkParser.GetShortTargetFramework(handshakeMessage.Properties[HandshakeMessagePropertyNames.Framework]);

            _handshakeInfo = (tfm, arch, executionId);
        }
        else
        {
            // TODO: Verify we get the same info.
        }

        var hostType = handshakeMessage.Properties[HandshakeMessagePropertyNames.HostType];
        // https://github.com/microsoft/testfx/blob/2a9a353ec2bb4ce403f72e8ba1f29e01e7cf1fd4/src/Platform/Microsoft.Testing.Platform/Hosts/CommonTestHost.cs#L87-L97
        if (hostType == "TestHost")
        {
            // AssemblyRunStarted counts "retry count", and writes to terminal "(Try <number-of-try>) Running tests from <assembly>"
            // So, we want to call it only for test host, and not for test host controller (or orchestrator, if in future it will handshake as well)
            // Calling it for both test host and test host controllers means we will count retries incorrectly, and will messages twice.
            var instanceId = handshakeMessage.Properties[HandshakeMessagePropertyNames.InstanceId];
            var handshakeInfo = _handshakeInfo.Value;
            _output.AssemblyRunStarted(_module.TargetPath, handshakeInfo.TargetFramework, handshakeInfo.Architecture, handshakeInfo.ExecutionId, instanceId);
        }

        LogHandshake(handshakeMessage);
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
            HandshakeMessagePropertyNames.InstanceId => nameof(HandshakeMessagePropertyNames.InstanceId),
            _ => string.Empty,
        };

    internal void OnDiscoveredTestsReceived(DiscoveredTestMessages discoveredTestMessages)
    {
        if (_options.IsHelp)
        {
            // TODO: Better to throw exception?
            return;
        }

        foreach (var test in discoveredTestMessages.DiscoveredMessages)
        {
            _output.TestDiscovered(_handshakeInfo.Value.ExecutionId,
                test.DisplayName,
                test.Uid);
        }

        LogDiscoveredTests(discoveredTestMessages);
    }

    internal void OnTestResultsReceived(TestResultMessages testResultMessage)
    {
        if (_options.IsHelp)
        {
            // TODO: Better to throw exception?
            return;
        }

        var handshakeInfo = _handshakeInfo.Value;
        foreach (var testResult in testResultMessage.SuccessfulTestMessages)
        {
            _output.TestCompleted(_module.TargetPath, handshakeInfo.TargetFramework, handshakeInfo.Architecture, handshakeInfo.ExecutionId,
                testResultMessage.InstanceId,
                testResult.Uid,
                testResult.DisplayName,
                testResult.Reason,
                ToOutcome(testResult.State),
                TimeSpan.FromTicks(testResult.Duration ?? 0),
                exceptions: null,
                expected: null,
                actual: null,
                standardOutput: testResult.StandardOutput,
                errorOutput: testResult.ErrorOutput);
        }

        foreach (var testResult in testResultMessage.FailedTestMessages)
        {
            _output.TestCompleted(_module.TargetPath, handshakeInfo.TargetFramework, handshakeInfo.Architecture, handshakeInfo.ExecutionId, testResultMessage.InstanceId,
                testResult.Uid,
                testResult.DisplayName,
                testResult.Reason,
                ToOutcome(testResult.State),
                TimeSpan.FromTicks(testResult.Duration ?? 0),
                exceptions: [.. testResult.Exceptions.Select(fe => new Terminal.FlatException(fe.ErrorMessage, fe.ErrorType, fe.StackTrace))],
                expected: null,
                actual: null,
                standardOutput: testResult.StandardOutput,
                errorOutput: testResult.ErrorOutput);
        }

        LogTestResults(testResultMessage);
    }

    internal void OnFileArtifactsReceived(FileArtifactMessages fileArtifactMessages)
    {
        if (_options.IsHelp)
        {
            // TODO: Better to throw exception?
            return;
        }

        var handshakeInfo = _handshakeInfo.Value;
        foreach (var artifact in fileArtifactMessages.FileArtifacts)
        {
            _output.ArtifactAdded(
                outOfProcess: false,
                _module.TargetPath, handshakeInfo.TargetFramework, handshakeInfo.Architecture, handshakeInfo.ExecutionId,
                artifact.TestDisplayName, artifact.FullPath);
        }

        LogFileArtifacts(fileArtifactMessages);
    }

    internal void OnSessionEventReceived(TestSessionEvent sessionEvent)
    {
        // TODO: We shouldn't only log here!
        // We should use it in a more meaningful way. e.g, ensure we received session start/end events.
        Logger.LogTrace($"TestSessionEvent: {sessionEvent.SessionType}, {sessionEvent.SessionUid}, {sessionEvent.ExecutionId}");
    }

    internal void OnTestProcessExited(int exitCode, List<string> outputData, List<string> errorData)
    {
        string outputDataString = string.Join(Environment.NewLine, outputData);
        string errorDataString = string.Join(Environment.NewLine, errorData);
        if (_handshakeInfo.HasValue)
        {
            _output.AssemblyRunCompleted(_handshakeInfo.Value.ExecutionId, exitCode, outputDataString, errorDataString);
        }
        else
        {
            _output.HandshakeFailure(_module.TargetPath ?? _module.ProjectFullPath, _module.TargetFramework, exitCode, outputDataString, errorDataString);
        }

        LogTestProcessExit(exitCode, outputDataString, errorDataString);
    }

    private static TestOutcome ToOutcome(byte? testState) => testState switch
    {
        TestStates.Passed => TestOutcome.Passed,
        TestStates.Skipped => TestOutcome.Skipped,
        TestStates.Failed => TestOutcome.Fail,
        TestStates.Error => TestOutcome.Error,
        TestStates.Timeout => TestOutcome.Timeout,
        TestStates.Cancelled => TestOutcome.Canceled,
        _ => throw new ArgumentOutOfRangeException(nameof(testState), $"Invalid test state value {testState}")
    };

    private static void LogHandshake(HandshakeMessage handshakeMessage)
    {
        if (!Logger.TraceEnabled)
        {
            return;
        }

        var logMessageBuilder = new StringBuilder();

        foreach (var property in handshakeMessage.Properties)
        {
            logMessageBuilder.AppendLine($"{GetHandshakePropertyName(property.Key)}: {property.Value}");
        }

        Logger.LogTrace(logMessageBuilder, static logMessageBuilder => logMessageBuilder.ToString());
    }

    private static void LogDiscoveredTests(DiscoveredTestMessages discoveredTestMessages)
    {
        if (!Logger.TraceEnabled)
        {
            return;
        }

        var logMessageBuilder = new StringBuilder();

        logMessageBuilder.AppendLine($"DiscoveredTests Execution Id: {discoveredTestMessages.ExecutionId}");
        logMessageBuilder.AppendLine($"TestResults Instance Id: {discoveredTestMessages.InstanceId}");

        foreach (var discoveredTestMessage in discoveredTestMessages.DiscoveredMessages)
        {
            logMessageBuilder.AppendLine($"DiscoveredTest: {discoveredTestMessage.Uid}, {discoveredTestMessage.DisplayName}");
        }

        Logger.LogTrace(logMessageBuilder, static logMessageBuilder => logMessageBuilder.ToString());
    }

    private static void LogTestResults(TestResultMessages testResultMessages)
    {
        if (!Logger.TraceEnabled)
        {
            return;
        }

        var logMessageBuilder = new StringBuilder();

        logMessageBuilder.AppendLine($"TestResults Execution Id: {testResultMessages.ExecutionId}");
        logMessageBuilder.AppendLine($"TestResults Instance Id: {testResultMessages.InstanceId}");

        foreach (SuccessfulTestResultMessage successfulTestResult in testResultMessages.SuccessfulTestMessages)
        {
            logMessageBuilder.AppendLine($"SuccessfulTestResult: {successfulTestResult.Uid}, {successfulTestResult.DisplayName}, " +
                $"{successfulTestResult.State}, {successfulTestResult.Duration}, {successfulTestResult.Reason}, {successfulTestResult.StandardOutput}," +
                $"{successfulTestResult.ErrorOutput}, {successfulTestResult.SessionUid}");
        }

        foreach (FailedTestResultMessage failedTestResult in testResultMessages.FailedTestMessages)
        {
            logMessageBuilder.AppendLine($"FailedTestResult: {failedTestResult.Uid}, {failedTestResult.DisplayName}, " +
                $"{failedTestResult.State}, {failedTestResult.Duration}, {failedTestResult.Reason}, {string.Join(", ", failedTestResult.Exceptions?.Select(e => $"{e.ErrorMessage}, {e.ErrorType}, {e.StackTrace}"))}" +
                $"{failedTestResult.StandardOutput}, {failedTestResult.ErrorOutput}, {failedTestResult.SessionUid}");
        }

        Logger.LogTrace(logMessageBuilder, static logMessageBuilder => logMessageBuilder.ToString());
    }

    private static void LogFileArtifacts(FileArtifactMessages fileArtifactMessages)
    {
        if (!Logger.TraceEnabled)
        {
            return;
        }

        var logMessageBuilder = new StringBuilder();

        logMessageBuilder.AppendLine($"FileArtifactMessages Execution Id: {fileArtifactMessages.ExecutionId}");
        logMessageBuilder.AppendLine($"TestResults Instance Id: {fileArtifactMessages.InstanceId}");

        foreach (FileArtifactMessage fileArtifactMessage in fileArtifactMessages.FileArtifacts)
        {
            logMessageBuilder.AppendLine($"FileArtifact: {fileArtifactMessage.FullPath}, {fileArtifactMessage.DisplayName}, " +
                $"{fileArtifactMessage.Description}, {fileArtifactMessage.TestUid}, {fileArtifactMessage.TestDisplayName}, " +
                $"{fileArtifactMessage.SessionUid}");
        }

        Logger.LogTrace(logMessageBuilder, static logMessageBuilder => logMessageBuilder.ToString());
    }

    private static void LogTestProcessExit(int exitCode, string outputData, string errorData)
    {
        if (!Logger.TraceEnabled)
        {
            return;
        }

        var logMessageBuilder = new StringBuilder();

        if (exitCode != ExitCode.Success)
        {
            logMessageBuilder.AppendLine($"Test Process exited with non-zero exit code: {exitCode}");
        }

        if (!string.IsNullOrEmpty(outputData))
        {
            logMessageBuilder.AppendLine($"Output Data: {outputData}");
        }

        if (!string.IsNullOrEmpty(errorData))
        {
            logMessageBuilder.AppendLine($"Error Data: {errorData}");
        }

        Logger.LogTrace(logMessageBuilder, static logMessageBuilder => logMessageBuilder.ToString());
    }
}
