// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal sealed class TestApplicationHandler
{
    private readonly TerminalTestReporter _output;
    private readonly TestModule _module;
    private readonly TestOptions _options;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, (int TestSessionStartCount, int TestSessionEndCount)> _testSessionEventCountPerSessionUid = new();

    private (string? TargetFramework, string? Architecture, string ExecutionId)? _handshakeInfo;
    private bool _receivedTestHostHandshake;

    public TestApplicationHandler(TerminalTestReporter output, TestModule module, TestOptions options)
    {
        _output = output;
        _module = module;
        _options = options;
    }

    /// <summary>
    /// Validates the handshake message and updates handler state. Returns <see langword="true"/> if the
    /// handshake was accepted, or <see langword="false"/> if it should be rejected (in which case the
    /// caller should return a failed handshake response to Microsoft.Testing.Platform so it stops
    /// sending further messages).
    /// </summary>
    internal bool OnHandshakeReceived(HandshakeMessage handshakeMessage, bool gotSupportedVersion)
    {
        LogHandshake(handshakeMessage);

        if (!gotSupportedVersion)
        {
            string failureMessage = handshakeMessage.Properties.TryGetValue(HandshakeMessagePropertyNames.SupportedProtocolVersions, out string? supportedProtocolVersions) && !string.IsNullOrWhiteSpace(supportedProtocolVersions)
                ? string.Format(CliCommandStrings.DotnetTestIncompatibleHandshakeVersion, supportedProtocolVersions, ProtocolConstants.SupportedVersions)
                : string.Format(CliCommandStrings.DotnetTestMissingHandshakeProtocolVersions, ProtocolConstants.SupportedVersions);

            ReportHandshakeFailure(failureMessage);
            return false;
        }

        // Validate all required handshake properties up-front. Missing values are reported via the
        // structured 'HandshakeFailure' path (consistent with how unsupported versions are reported)
        // and the caller rejects the handshake at the protocol level so MTP does not keep sending
        // further messages that would then fail validation in other handlers.
        if (!TryGetRequiredHandshakeProperty(handshakeMessage, HandshakeMessagePropertyNames.ExecutionId, out string? executionId, out string? validationError) ||
            !TryGetRequiredHandshakeProperty(handshakeMessage, HandshakeMessagePropertyNames.Architecture, out string? architecture, out validationError) ||
            !TryGetRequiredHandshakeProperty(handshakeMessage, HandshakeMessagePropertyNames.Framework, out string? framework, out validationError) ||
            !TryGetRequiredHandshakeProperty(handshakeMessage, HandshakeMessagePropertyNames.HostType, out string? hostType, out validationError))
        {
            ReportHandshakeFailure(validationError!);
            return false;
        }

        var arch = architecture!.ToLowerInvariant();
        var tfm = TargetFrameworkParser.GetShortTargetFramework(framework);
        var currentHandshakeInfo = (tfm, arch, executionId!);

        // https://github.com/microsoft/testfx/blob/2a9a353ec2bb4ce403f72e8ba1f29e01e7cf1fd4/src/Platform/Microsoft.Testing.Platform/Hosts/CommonTestHost.cs#L87-L97
        string? instanceId = null;
        if (hostType == "TestHost"
            && !TryGetRequiredHandshakeProperty(handshakeMessage, HandshakeMessagePropertyNames.InstanceId, out instanceId, out validationError))
        {
            ReportHandshakeFailure(validationError!);
            return false;
        }

        if (!_handshakeInfo.HasValue)
        {
            _handshakeInfo = currentHandshakeInfo;
        }
        else if (_handshakeInfo.Value != currentHandshakeInfo)
        {
            ReportHandshakeFailure(string.Format(CliCommandStrings.MismatchingHandshakeInfo, currentHandshakeInfo, _handshakeInfo.Value));
            return false;
        }

        if (hostType == "TestHost")
        {
            _receivedTestHostHandshake = true;
            // AssemblyRunStarted counts "retry count", and writes to terminal "(Try <number-of-try>) Running tests from <assembly>"
            // So, we want to call it only for test host, and not for test host controller (or orchestrator, if in future it will handshake as well)
            // Calling it for both test host and test host controllers means we will count retries incorrectly, and will messages twice.
            var handshakeInfo = _handshakeInfo.Value;
            _output.AssemblyRunStarted(_module.TargetPath, handshakeInfo.TargetFramework, handshakeInfo.Architecture, handshakeInfo.ExecutionId, instanceId!);
        }

        // Validate the optional ExecutionMode property last (after AssemblyRunStarted) so that any
        // diagnostic about a mismatched run/help/discover mode is associated with this assembly
        // in the terminal output.
        //
        // Older Microsoft.Testing.Platform versions don't send the ExecutionMode property at all
        // (see https://github.com/microsoft/testfx/pull/8794). In that case we keep today's
        // behavior and don't perform any validation here. If the property is present, validate it
        // even when it is empty/whitespace so protocol bugs are surfaced instead of silently accepted.
        if (handshakeMessage.Properties.TryGetValue(HandshakeMessagePropertyNames.ExecutionMode, out string? executionMode) &&
            !IsExpectedExecutionMode(executionMode, out string expectedExecutionMode))
        {
            ReportHandshakeFailure(string.Format(CliCommandStrings.MismatchingHandshakeExecutionMode, executionMode, expectedExecutionMode));
            return false;
        }

        return true;
    }

    private bool IsExpectedExecutionMode(string reportedMode, out string expectedMode)
    {
        expectedMode = _options.IsHelp
            ? HandshakeMessageExecutionModes.Help
            : _options.IsDiscovery
                ? HandshakeMessageExecutionModes.Discover
                : HandshakeMessageExecutionModes.Run;

        // If the reported mode is one of the known values, it must equal the SDK's expected mode.
        // An unknown value (e.g. a future mode added by a newer testing platform without bumping
        // the protocol version) is also rejected so we don't silently accept a message stream we
        // can't interpret.
        return reportedMode == expectedMode;
    }

    // Every caller of this helper has just decided to reject the handshake at the protocol level
    // (the caller immediately returns false from OnHandshakeReceived). We always opt out of the
    // legacy "swallow handshake failures when SDK is in help mode" workaround in
    // TerminalTestReporter.HandshakeFailure — that workaround is meant for older Microsoft.Testing.Platform
    // versions that don't handshake at all on --help and so OnTestProcessExited ends up calling
    // HandshakeFailure with no actionable context. Explicit programmatic rejections here (unsupported
    // protocol version, missing required property, mismatching handshake info, mismatching execution
    // mode) are real protocol failures and must still be surfaced even when the SDK is in help mode.
    private void ReportHandshakeFailure(string failureMessage) =>
        _output.HandshakeFailure(
            _module.TargetPath,
            string.Empty,
            ExitCode.GenericFailure,
            failureMessage,
            string.Empty,
            reportEvenWhenHelp: true);

    private static bool TryGetRequiredHandshakeProperty(HandshakeMessage handshakeMessage, byte propertyId, out string? value, out string? failureMessage)
    {
        if (handshakeMessage.Properties.TryGetValue(propertyId, out value) && !string.IsNullOrWhiteSpace(value))
        {
            failureMessage = null;
            return true;
        }

        failureMessage = string.Format(
            CliCommandStrings.DotnetTestMissingRequiredMessageProperty,
            GetHandshakePropertyName(propertyId),
            nameof(HandshakeMessage));
        return false;
    }

    private static string ValidateRequiredMessageProperty(string? value, string propertyName, string messageTypeName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(string.Format(
                CliCommandStrings.DotnetTestMissingRequiredMessageProperty,
                propertyName,
                messageTypeName));
        }

        return value;
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
            HandshakeMessagePropertyNames.IsIDE => nameof(HandshakeMessagePropertyNames.IsIDE),
            HandshakeMessagePropertyNames.ExecutionMode => nameof(HandshakeMessagePropertyNames.ExecutionMode),
            _ => string.Empty,
        };

    internal void OnDiscoveredTestsReceived(DiscoveredTestMessages discoveredTestMessages)
    {
        LogDiscoveredTests(discoveredTestMessages);

        if (_options.IsHelp)
        {
            throw new InvalidOperationException(string.Format(CliCommandStrings.UnexpectedMessageInHelpMode, nameof(DiscoveredTestMessages)));
        }

        if (!_handshakeInfo.HasValue)
        {
            throw new InvalidOperationException(string.Format(CliCommandStrings.UnexpectedMessageWithoutHandshake, nameof(DiscoveredTestMessages)));
        }

        if (!_receivedTestHostHandshake)
        {
            // The terminal reporter only registers an assembly run when the TestHost handshake completes.
            // Without it, '_assemblies[executionId]' lookups would throw a non-actionable KeyNotFoundException.
            throw new InvalidOperationException(string.Format(CliCommandStrings.UnexpectedMessageWithoutTestHostHandshake, nameof(DiscoveredTestMessages)));
        }

        string executionId = ValidateRequiredMessageProperty(
            discoveredTestMessages.ExecutionId,
            nameof(DiscoveredTestMessages.ExecutionId),
            nameof(DiscoveredTestMessages));

        if (executionId != _handshakeInfo.Value.ExecutionId)
        {
            // Received 'ExecutionId' of value '{0}' for message '{1}' while the 'ExecutionId' received of the handshake message was '{2}'.
            throw new InvalidOperationException(string.Format(CliCommandStrings.DotnetTestMismatchingExecutionId, executionId, nameof(DiscoveredTestMessages), _handshakeInfo.Value.ExecutionId));
        }

        foreach (var test in discoveredTestMessages.DiscoveredMessages)
        {
            _output.TestDiscovered(_handshakeInfo.Value.ExecutionId,
                ValidateRequiredMessageProperty(test.DisplayName, nameof(DiscoveredTestMessage.DisplayName), nameof(DiscoveredTestMessage)),
                ValidateRequiredMessageProperty(test.Uid, nameof(DiscoveredTestMessage.Uid), nameof(DiscoveredTestMessage)),
                test.FilePath,
                test.LineNumber);
        }
    }

    internal void OnTestResultsReceived(TestResultMessages testResultMessage)
    {
        LogTestResults(testResultMessage);

        if (_options.IsHelp)
        {
            throw new InvalidOperationException(string.Format(CliCommandStrings.UnexpectedMessageInHelpMode, nameof(TestResultMessages)));
        }

        if (!_handshakeInfo.HasValue)
        {
            throw new InvalidOperationException(string.Format(CliCommandStrings.UnexpectedMessageWithoutHandshake, nameof(TestResultMessages)));
        }

        if (!_receivedTestHostHandshake)
        {
            throw new InvalidOperationException(string.Format(CliCommandStrings.UnexpectedMessageWithoutTestHostHandshake, nameof(TestResultMessages)));
        }

        string messageExecutionId = ValidateRequiredMessageProperty(
            testResultMessage.ExecutionId,
            nameof(TestResultMessages.ExecutionId),
            nameof(TestResultMessages));

        if (messageExecutionId != _handshakeInfo.Value.ExecutionId)
        {
            // Received 'ExecutionId' of value '{0}' for message '{1}' while the 'ExecutionId' received of the handshake message was '{2}'.
            throw new InvalidOperationException(string.Format(CliCommandStrings.DotnetTestMismatchingExecutionId, messageExecutionId, nameof(TestResultMessages), _handshakeInfo.Value.ExecutionId));
        }

        var handshakeInfo = _handshakeInfo.Value;
        string instanceId = ValidateRequiredMessageProperty(
            testResultMessage.InstanceId,
            nameof(TestResultMessages.InstanceId),
            nameof(TestResultMessages));

        foreach (var testResult in testResultMessage.SuccessfulTestMessages)
        {
            _output.TestCompleted(_module.TargetPath, handshakeInfo.TargetFramework, handshakeInfo.Architecture, handshakeInfo.ExecutionId,
                instanceId,
                ValidateRequiredMessageProperty(testResult.Uid, nameof(SuccessfulTestResultMessage.Uid), nameof(SuccessfulTestResultMessage)),
                ValidateRequiredMessageProperty(testResult.DisplayName, nameof(SuccessfulTestResultMessage.DisplayName), nameof(SuccessfulTestResultMessage)),
                testResult.Reason,
                ToOutcome(testResult.State),
                testResult.Duration.HasValue ? TimeSpan.FromTicks(testResult.Duration.Value) : null,
                exceptions: null,
                expected: null,
                actual: null,
                standardOutput: testResult.StandardOutput,
                errorOutput: testResult.ErrorOutput);
        }

        foreach (var testResult in testResultMessage.FailedTestMessages)
        {
            _output.TestCompleted(_module.TargetPath, handshakeInfo.TargetFramework, handshakeInfo.Architecture, handshakeInfo.ExecutionId,
                instanceId,
                ValidateRequiredMessageProperty(testResult.Uid, nameof(FailedTestResultMessage.Uid), nameof(FailedTestResultMessage)),
                ValidateRequiredMessageProperty(testResult.DisplayName, nameof(FailedTestResultMessage.DisplayName), nameof(FailedTestResultMessage)),
                testResult.Reason,
                ToOutcome(testResult.State),
                testResult.Duration.HasValue ? TimeSpan.FromTicks(testResult.Duration.Value) : null,
                exceptions: [.. (testResult.Exceptions ?? []).Select(fe => new Terminal.FlatException(fe.ErrorMessage, fe.ErrorType, fe.StackTrace))],
                expected: testResult.Expected,
                actual: testResult.Actual,
                standardOutput: testResult.StandardOutput,
                errorOutput: testResult.ErrorOutput);
        }
    }

    internal void OnTestInProgressReceived(TestInProgressMessages testInProgressMessages)
    {
        LogTestInProgress(testInProgressMessages);

        if (_options.IsHelp)
        {
            throw new InvalidOperationException(string.Format(CliCommandStrings.UnexpectedMessageInHelpMode, nameof(TestInProgressMessages)));
        }

        if (!_handshakeInfo.HasValue)
        {
            throw new InvalidOperationException(string.Format(CliCommandStrings.UnexpectedMessageWithoutHandshake, nameof(TestInProgressMessages)));
        }

        if (testInProgressMessages.ExecutionId != _handshakeInfo.Value.ExecutionId)
        {
            // Received 'ExecutionId' of value '{0}' for message '{1}' while the 'ExecutionId' received of the handshake message was '{2}'.
            throw new InvalidOperationException(string.Format(CliCommandStrings.DotnetTestMismatchingExecutionId, testInProgressMessages.ExecutionId, nameof(TestInProgressMessages), _handshakeInfo.Value.ExecutionId));
        }

        var handshakeInfo = _handshakeInfo.Value;
        foreach (TestInProgressMessage inProgressMessage in testInProgressMessages.InProgressMessages)
        {
            _output.TestInProgress(
                _module.TargetPath,
                handshakeInfo.TargetFramework,
                handshakeInfo.Architecture,
                handshakeInfo.ExecutionId,
                testInProgressMessages.InstanceId!,
                inProgressMessage.Uid!,
                inProgressMessage.DisplayName!);
        }
    }

    internal void OnFileArtifactsReceived(FileArtifactMessages fileArtifactMessages)
    {
        LogFileArtifacts(fileArtifactMessages);

        if (_options.IsHelp)
        {
            throw new InvalidOperationException(string.Format(CliCommandStrings.UnexpectedMessageInHelpMode, nameof(FileArtifactMessages)));
        }

        if (!_handshakeInfo.HasValue)
        {
            throw new InvalidOperationException(string.Format(CliCommandStrings.UnexpectedMessageWithoutHandshake, nameof(FileArtifactMessages)));
        }

        if (!_receivedTestHostHandshake)
        {
            throw new InvalidOperationException(string.Format(CliCommandStrings.UnexpectedMessageWithoutTestHostHandshake, nameof(FileArtifactMessages)));
        }

        string fileArtifactExecutionId = ValidateRequiredMessageProperty(
            fileArtifactMessages.ExecutionId,
            nameof(FileArtifactMessages.ExecutionId),
            nameof(FileArtifactMessages));

        if (fileArtifactExecutionId != _handshakeInfo.Value.ExecutionId)
        {
            // Received 'ExecutionId' of value '{0}' for message '{1}' while the 'ExecutionId' received of the handshake message was '{2}'.
            throw new InvalidOperationException(string.Format(CliCommandStrings.DotnetTestMismatchingExecutionId, fileArtifactExecutionId, nameof(FileArtifactMessages), _handshakeInfo.Value.ExecutionId));
        }

        var handshakeInfo = _handshakeInfo.Value;
        foreach (var artifact in fileArtifactMessages.FileArtifacts)
        {
            string fullPath = ValidateRequiredMessageProperty(
                artifact.FullPath,
                nameof(FileArtifactMessage.FullPath),
                nameof(FileArtifactMessage));

            _output.ArtifactAdded(
                outOfProcess: false,
                _module.TargetPath, handshakeInfo.TargetFramework, handshakeInfo.Architecture, handshakeInfo.ExecutionId,
                artifact.TestDisplayName, fullPath);
        }
    }

    internal void OnSessionEventReceived(TestSessionEvent sessionEvent)
    {
        lock (_lock)
        {
            LogSessionEvent(sessionEvent);

            if (_options.IsHelp)
            {
                throw new InvalidOperationException(string.Format(CliCommandStrings.UnexpectedMessageInHelpMode, nameof(TestSessionEvent)));
            }

            if (!_handshakeInfo.HasValue)
            {
                throw new InvalidOperationException(string.Format(CliCommandStrings.UnexpectedMessageWithoutHandshake, nameof(TestSessionEvent)));
            }

            string sessionExecutionId = ValidateRequiredMessageProperty(
                sessionEvent.ExecutionId,
                nameof(TestSessionEvent.ExecutionId),
                nameof(TestSessionEvent));

            if (sessionExecutionId != _handshakeInfo.Value.ExecutionId)
            {
                // Received 'ExecutionId' of value '{0}' for message '{1}' while the 'ExecutionId' received of the handshake message was '{2}'.
                throw new InvalidOperationException(string.Format(CliCommandStrings.DotnetTestMismatchingExecutionId, sessionExecutionId, nameof(TestSessionEvent), _handshakeInfo.Value.ExecutionId));
            }

            if (sessionEvent.SessionType is null)
            {
                throw new InvalidOperationException(string.Format(
                    CliCommandStrings.DotnetTestMissingRequiredMessageProperty,
                    nameof(TestSessionEvent.SessionType),
                    nameof(TestSessionEvent)));
            }

            string sessionUid = ValidateRequiredMessageProperty(
                sessionEvent.SessionUid,
                nameof(TestSessionEvent.SessionUid),
                nameof(TestSessionEvent));

            if (sessionEvent.SessionType == SessionEventTypes.TestSessionStart)
            {
                IncreaseTestSessionStart(sessionUid);
            }
            else if (sessionEvent.SessionType == SessionEventTypes.TestSessionEnd)
            {
                var (testSessionStartCount, testSessionEndCount) = IncreaseTestSessionEnd(sessionUid);
                if (testSessionEndCount > testSessionStartCount)
                {
                    throw new InvalidOperationException(CliCommandStrings.UnexpectedTestSessionEnd);
                }
            }
            else
            {
                throw new InvalidOperationException(string.Format(CliCommandStrings.UnknownSessionEventType, sessionEvent.SessionType));
            }
        }
    }

    internal void OnAzureDevOpsLogReceived(AzureDevOpsLogMessage azureDevOpsLogMessage)
    {
        LogAzureDevOpsLog(azureDevOpsLogMessage);

        // These messages carry verbatim Azure DevOps logging commands (e.g. ##[group] / ##[endgroup] / ##vso[...])
        // that must reach the terminal unchanged so the AzDO agent can render them. They are informational and are
        // not tied to a session, so - unlike the test-result handlers - we do not require a handshake and we are
        // lenient about missing/empty content (we simply skip it) instead of failing the run.
        if (!string.IsNullOrEmpty(azureDevOpsLogMessage.LogText))
        {
            // WriteMessage appends the payload verbatim without a trailing newline (the process-output streaming
            // path supplies its own newlines). An Azure DevOps logging command must sit on its own line to be
            // recognized, so terminate the line here without otherwise altering the payload.
            _output.WriteMessage(EnsureTrailingNewLine(azureDevOpsLogMessage.LogText));
        }
    }

    internal void OnDisplayMessageReceived(DisplayMessage displayMessage)
    {
        LogDisplayMessage(displayMessage);

        // Generic host diagnostics (hang/crash dump, retry summaries, extension/framework warnings and errors)
        // forwarded outside of test results. They are informational and not tied to a session, so we do not require
        // a handshake and we stay lenient about missing content.
        if (string.IsNullOrEmpty(displayMessage.Text))
        {
            return;
        }

        switch (displayMessage.Level)
        {
            case DisplayMessageLevels.Warning:
                // WriteWarningMessage/WriteErrorMessage terminate the line themselves (AppendLine).
                _output.WriteWarningMessage(displayMessage.Text);
                break;

            case DisplayMessageLevels.Error:
                _output.WriteErrorMessage(displayMessage.Text);
                break;

            default:
                // The information path uses the verbatim WriteMessage, which does not append a newline, so
                // terminate the line here to keep it from running together with subsequent terminal output.
                _output.WriteMessage(EnsureTrailingNewLine(displayMessage.Text));
                break;
        }
    }

    // WriteMessage writes text verbatim (no trailing newline). Forwarded host messages arrive as individual
    // lines without a terminator, so ensure one is present - but leave already-terminated payloads untouched
    // to avoid introducing blank lines.
    private static string EnsureTrailingNewLine(string text)
        => text.EndsWith('\n') ? text : text + Environment.NewLine;

    private (int TestSessionStartCount, int TestSessionEndCount) IncreaseTestSessionStart(string sessionUid)
    {
        _ = _testSessionEventCountPerSessionUid.TryGetValue(sessionUid, out var count);
        count = (count.TestSessionStartCount + 1, count.TestSessionEndCount);
        _testSessionEventCountPerSessionUid[sessionUid] = count;
        return count;
    }

    private (int TestSessionStartCount, int TestSessionEndCount) IncreaseTestSessionEnd(string sessionUid)
    {
        _ = _testSessionEventCountPerSessionUid.TryGetValue(sessionUid, out var count);
        count = (count.TestSessionStartCount, count.TestSessionEndCount + 1);
        _testSessionEventCountPerSessionUid[sessionUid] = count;
        return count;
    }

    internal bool HasMismatchingTestSessionEventCount()
    {
        foreach (var (testSessionStartCount, testSessionEndCount) in _testSessionEventCountPerSessionUid.Values)
        {
            if (testSessionStartCount != testSessionEndCount)
            {
                return true;
            }
        }

        return false;
    }

    internal void WriteMessage(string? text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            _output.WriteMessage(text);
        }
    }

    internal void OnTestProcessExited(int exitCode, string outputData, string errorData)
    {
        if (_receivedTestHostHandshake && _handshakeInfo.HasValue)
        {
            // If we received a handshake from TestHostController but not from TestHost,
            // call HandshakeFailure instead of AssemblyRunCompleted
            _output.AssemblyRunCompleted(_handshakeInfo.Value.ExecutionId, exitCode, outputData, errorData);
        }
        else
        {
            _output.HandshakeFailure(_module.TargetPath ?? _module.ProjectFullPath ?? string.Empty, _module.TargetFramework, exitCode, outputData, errorData);
        }

        LogTestProcessExit(exitCode, outputData, errorData);
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
            logMessageBuilder.AppendLine($"DiscoveredTest: {discoveredTestMessage.Uid}, {discoveredTestMessage.DisplayName}, " +
                $"FilePath={discoveredTestMessage.FilePath}, LineNumber={discoveredTestMessage.LineNumber}, " +
                $"Namespace={discoveredTestMessage.Namespace}, TypeName={discoveredTestMessage.TypeName}, " +
                $"MethodName={discoveredTestMessage.MethodName}, " +
                $"ParameterTypeFullNames=[{string.Join(", ", discoveredTestMessage.ParameterTypeFullNames)}], " +
                $"Traits=[{string.Join(", ", discoveredTestMessage.Traits.Select(t => $"{t.Key}={t.Value}"))}]");
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
                $"{failedTestResult.State}, {failedTestResult.Duration}, {failedTestResult.Reason}, {string.Join(", ", (failedTestResult.Exceptions ?? Array.Empty<ExceptionMessage>()).Select(e => $"{e.ErrorMessage}, {e.ErrorType}, {e.StackTrace}"))}" +
                $"{failedTestResult.StandardOutput}, {failedTestResult.ErrorOutput}, {failedTestResult.SessionUid}");
        }

        Logger.LogTrace(logMessageBuilder, static logMessageBuilder => logMessageBuilder.ToString());
    }

    private static void LogTestInProgress(TestInProgressMessages testInProgressMessages)
    {
        if (!Logger.TraceEnabled)
        {
            return;
        }

        var logMessageBuilder = new StringBuilder();

        logMessageBuilder.AppendLine($"TestInProgress Execution Id: {testInProgressMessages.ExecutionId}");
        logMessageBuilder.AppendLine($"TestInProgress Instance Id: {testInProgressMessages.InstanceId}");

        foreach (TestInProgressMessage inProgressMessage in testInProgressMessages.InProgressMessages)
        {
            logMessageBuilder.AppendLine($"TestInProgress: {inProgressMessage.Uid}, {inProgressMessage.DisplayName}");
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

    private static void LogSessionEvent(TestSessionEvent testSessionEvent)
    {
        if (!Logger.TraceEnabled)
        {
            return;
        }

        var logMessageBuilder = new StringBuilder();

        logMessageBuilder.AppendLine($"TestSessionEvent.SessionType: {testSessionEvent.SessionType}");
        logMessageBuilder.AppendLine($"TestSessionEvent.SessionUid: {testSessionEvent.SessionUid}");
        logMessageBuilder.AppendLine($"TestSessionEvent.ExecutionId: {testSessionEvent.ExecutionId}");
        Logger.LogTrace(logMessageBuilder, static logMessageBuilder => logMessageBuilder.ToString());
    }

    private static void LogAzureDevOpsLog(AzureDevOpsLogMessage azureDevOpsLogMessage)
    {
        if (!Logger.TraceEnabled)
        {
            return;
        }

        var logMessageBuilder = new StringBuilder();

        logMessageBuilder.AppendLine($"AzureDevOpsLogMessage.ExecutionId: {azureDevOpsLogMessage.ExecutionId}");
        logMessageBuilder.AppendLine($"AzureDevOpsLogMessage.InstanceId: {azureDevOpsLogMessage.InstanceId}");
        logMessageBuilder.AppendLine($"AzureDevOpsLogMessage.LogText: {azureDevOpsLogMessage.LogText}");
        Logger.LogTrace(logMessageBuilder, static logMessageBuilder => logMessageBuilder.ToString());
    }

    private static void LogDisplayMessage(DisplayMessage displayMessage)
    {
        if (!Logger.TraceEnabled)
        {
            return;
        }

        var logMessageBuilder = new StringBuilder();

        logMessageBuilder.AppendLine($"DisplayMessage.ExecutionId: {displayMessage.ExecutionId}");
        logMessageBuilder.AppendLine($"DisplayMessage.InstanceId: {displayMessage.InstanceId}");
        logMessageBuilder.AppendLine($"DisplayMessage.Level: {displayMessage.Level}");
        logMessageBuilder.AppendLine($"DisplayMessage.Text: {displayMessage.Text}");
        Logger.LogTrace(logMessageBuilder, static logMessageBuilder => logMessageBuilder.ToString());
    }
}
