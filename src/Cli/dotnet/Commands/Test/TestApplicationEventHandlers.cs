// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using Microsoft.DotNet.Tools.Test;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.DotNet.Cli;

internal sealed class TestApplicationsEventHandlers(TerminalTestReporter output) : IDisposable
{
    private readonly ConcurrentDictionary<TestApplication, (string ModulePath, string TargetFramework, string Architecture, string ExecutionId)> _executions = new();
    private readonly TerminalTestReporter _output = output;

    public void OnHandshakeReceived(object sender, HandshakeArgs args)
    {
        var testApplication = (TestApplication)sender;
        var executionId = args.Handshake.Properties[HandshakeMessagePropertyNames.ExecutionId];
        var arch = args.Handshake.Properties[HandshakeMessagePropertyNames.Architecture]?.ToLower();
        var tfm = TargetFrameworkParser.GetShortTargetFramework(args.Handshake.Properties[HandshakeMessagePropertyNames.Framework]);
        (string ModulePath, string TargetFramework, string Architecture, string ExecutionId) appInfo = new(testApplication.Module.RunProperties.RunCommand, tfm, arch, executionId);
        _executions[testApplication] = appInfo;
        _output.AssemblyRunStarted(appInfo.ModulePath, appInfo.TargetFramework, appInfo.Architecture, appInfo.ExecutionId);

        LogHandshake(args);
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

        LogDiscoveredTests(args);
    }

    public void OnTestResultsReceived(object sender, TestResultEventArgs args)
    {
        var testApp = (TestApplication)sender;
        var appInfo = _executions[testApp];

        foreach (var testResult in args.SuccessfulTestResults)
        {
            _output.TestCompleted(appInfo.ModulePath, appInfo.TargetFramework, appInfo.Architecture, appInfo.ExecutionId,
                testResult.Uid,
                testResult.DisplayName,
                ToOutcome(testResult.State),
                TimeSpan.FromTicks(testResult.Duration ?? 0),
                exceptions: null,
                expected: null,
                actual: null,
                standardOutput: testResult.StandardOutput,
                errorOutput: testResult.ErrorOutput);
        }

        foreach (var testResult in args.FailedTestResults)
        {
            _output.TestCompleted(appInfo.ModulePath, appInfo.TargetFramework, appInfo.Architecture, appInfo.ExecutionId,
                testResult.Uid,
                testResult.DisplayName,
                ToOutcome(testResult.State),
                TimeSpan.FromTicks(testResult.Duration ?? 0),
                exceptions: testResult.Exceptions.Select(fe => new Testing.Platform.OutputDevice.Terminal.FlatException(fe.ErrorMessage, fe.ErrorType, fe.StackTrace)).ToArray(),
                expected: null,
                actual: null,
                standardOutput: testResult.StandardOutput,
                errorOutput: testResult.ErrorOutput);
        }

        LogTestResults(args);
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

        LogFileArtifacts(args);
    }

    public void OnSessionEventReceived(object sender, SessionEventArgs args)
    {
        if (!Logger.TraceEnabled) return;

        var sessionEvent = args.SessionEvent;
        Logger.LogTrace(() => $"TestSessionEvent: {sessionEvent.SessionType}, {sessionEvent.SessionUid}, {sessionEvent.ExecutionId}");
    }

    public void OnErrorReceived(object sender, ErrorEventArgs args)
    {
        if (!Logger.TraceEnabled) return;

        Logger.LogTrace(() => args.ErrorMessage);
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
            _output.AssemblyRunCompleted(testApplication.Module.RunProperties.RunCommand ?? testApplication.Module.ProjectFullPath, testApplication.Module.TargetFramework, architecture: null, null, args.ExitCode, string.Join(Environment.NewLine, args.OutputData), string.Join(Environment.NewLine, args.ErrorData));
        }

        LogTestProcessExit(args);
    }

    public static TestOutcome ToOutcome(byte? testState) => testState switch
    {
        TestStates.Passed => TestOutcome.Passed,
        TestStates.Skipped => TestOutcome.Skipped,
        TestStates.Failed => TestOutcome.Fail,
        TestStates.Error => TestOutcome.Error,
        TestStates.Timeout => TestOutcome.Timeout,
        TestStates.Cancelled => TestOutcome.Canceled,
        _ => throw new ArgumentOutOfRangeException(nameof(testState), $"Invalid test state value {testState}")
    };

    private static void LogHandshake(HandshakeArgs args)
    {
        if (!Logger.TraceEnabled)
        {
            return;
        }

        var logMessageBuilder = new StringBuilder();

        foreach (var property in args.Handshake.Properties)
        {
            logMessageBuilder.AppendLine($"{GetHandshakePropertyName(property.Key)}: {property.Value}");
        }

        Logger.LogTrace(() => logMessageBuilder.ToString());
    }

    private static void LogDiscoveredTests(DiscoveredTestEventArgs args)
    {
        if (!Logger.TraceEnabled)
        {
            return;
        }

        var logMessageBuilder = new StringBuilder();

        logMessageBuilder.AppendLine($"DiscoveredTests Execution Id: {args.ExecutionId}");
        logMessageBuilder.AppendLine($"TestResults Instance Id: {args.InstanceId}");

        foreach (var discoveredTestMessage in args.DiscoveredTests)
        {
            logMessageBuilder.AppendLine($"DiscoveredTest: {discoveredTestMessage.Uid}, {discoveredTestMessage.DisplayName}");
        }

        Logger.LogTrace(() => logMessageBuilder.ToString());
    }

    private static void LogTestResults(TestResultEventArgs args)
    {
        if (!Logger.TraceEnabled)
        {
            return;
        }

        var logMessageBuilder = new StringBuilder();

        logMessageBuilder.AppendLine($"TestResults Execution Id: {args.ExecutionId}");
        logMessageBuilder.AppendLine($"TestResults Instance Id: {args.InstanceId}");

        foreach (SuccessfulTestResult successfulTestResult in args.SuccessfulTestResults)
        {
            logMessageBuilder.AppendLine($"SuccessfulTestResult: {successfulTestResult.Uid}, {successfulTestResult.DisplayName}, " +
                $"{successfulTestResult.State}, {successfulTestResult.Duration}, {successfulTestResult.Reason}, {successfulTestResult.StandardOutput}," +
                $"{successfulTestResult.ErrorOutput}, {successfulTestResult.SessionUid}");
        }

        foreach (FailedTestResult failedTestResult in args.FailedTestResults)
        {
            logMessageBuilder.AppendLine($"FailedTestResult: {failedTestResult.Uid}, {failedTestResult.DisplayName}, " +
                $"{failedTestResult.State}, {failedTestResult.Duration}, {failedTestResult.Reason}, {string.Join(", ", failedTestResult.Exceptions?.Select(e => $"{e.ErrorMessage}, {e.ErrorType}, {e.StackTrace}"))}" +
                $"{failedTestResult.StandardOutput}, {failedTestResult.ErrorOutput}, {failedTestResult.SessionUid}");
        }

        Logger.LogTrace(() => logMessageBuilder.ToString());
    }

    private static void LogFileArtifacts(FileArtifactEventArgs args)
    {
        if (!Logger.TraceEnabled)
        {
            return;
        }

        var logMessageBuilder = new StringBuilder();

        logMessageBuilder.AppendLine($"FileArtifactMessages Execution Id: {args.ExecutionId}");
        logMessageBuilder.AppendLine($"TestResults Instance Id: {args.InstanceId}");

        foreach (FileArtifact fileArtifactMessage in args.FileArtifacts)
        {
            logMessageBuilder.AppendLine($"FileArtifact: {fileArtifactMessage.FullPath}, {fileArtifactMessage.DisplayName}, " +
                $"{fileArtifactMessage.Description}, {fileArtifactMessage.TestUid}, {fileArtifactMessage.TestDisplayName}, " +
                $"{fileArtifactMessage.SessionUid}");
        }

        Logger.LogTrace(() => logMessageBuilder.ToString());
    }

    private static void LogTestProcessExit(TestProcessExitEventArgs args)
    {
        if (!Logger.TraceEnabled)
        {
            return;
        }

        var logMessageBuilder = new StringBuilder();

        if (args.ExitCode != ExitCode.Success)
        {
            logMessageBuilder.AppendLine($"Test Process exited with non-zero exit code: {args.ExitCode}");
        }

        if (args.OutputData.Count > 0)
        {
            logMessageBuilder.AppendLine($"Output Data: {string.Join(Environment.NewLine, args.OutputData)}");
        }

        if (args.ErrorData.Count > 0)
        {
            logMessageBuilder.AppendLine($"Error Data: {string.Join(Environment.NewLine, args.ErrorData)}");
        }

        Logger.LogTrace(() => logMessageBuilder.ToString());
    }

    public void Dispose()
    {
        foreach (var execution in _executions)
        {
            execution.Key.Dispose();
        }
    }
}
