// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using Microsoft.DotNet.Cli.Commands.Test.IPC;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal sealed class TestApplication(
    TestModule module,
    BuildOptions buildOptions,
    TestOptions testOptions,
    TerminalTestReporter output) : IDisposable
{
    private readonly BuildOptions _buildOptions = buildOptions;
    private readonly TerminalTestReporter _output = output;

    private (string TargetFramework, string Architecture, string ExecutionId)? _handshakeInfo;

    private readonly List<string> _outputData = [];
    private readonly List<string> _errorData = [];
    private readonly PipeNameDescription _pipeNameDescription = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));
    private readonly CancellationTokenSource _cancellationToken = new();

    private Task _testAppPipeConnectionLoop;
    private readonly List<NamedPipeServer> _testAppPipeConnections = [];
    private readonly Dictionary<NamedPipeServer, HandshakeMessage> _handshakes = new();

    public event EventHandler<HelpEventArgs> HelpRequested;

    public TestModule Module { get; } = module;
    public TestOptions TestOptions { get; } = testOptions;

    public bool HasFailureDuringDispose { get; private set; }

    public async Task<int> RunAsync()
    {
        // TODO: RunAsync is probably expected to be executed exactly once on each TestApplication instance.
        // Consider throwing an exception if it's called more than once.
        if (TestOptions.HasFilterMode && !ModulePathExists())
        {
            return ExitCode.GenericFailure;
        }

        var processStartInfo = CreateProcessStartInfo();

        _testAppPipeConnectionLoop = Task.Run(async () => await WaitConnectionAsync(_cancellationToken.Token), _cancellationToken.Token);
        var testProcessResult = await StartProcess(processStartInfo);

        WaitOnTestApplicationPipeConnectionLoop();

        return testProcessResult;
    }

    private ProcessStartInfo CreateProcessStartInfo()
    {
        var processStartInfo = new ProcessStartInfo
        {
            // We should get correct RunProperties right away.
            // For the case of dotnet test --test-modules path/to/dll, the TestModulesFilterHandler is responsible
            // for providing the dotnet muxer as RunCommand, and `exec "path/to/dll"` as RunArguments.
            FileName = Module.RunProperties.Command,
            Arguments = GetArguments(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        if (!string.IsNullOrEmpty(Module.RunProperties.WorkingDirectory))
        {
            processStartInfo.WorkingDirectory = Module.RunProperties.WorkingDirectory;
        }

        if (Module.LaunchSettings is not null)
        {
            foreach (var entry in Module.LaunchSettings.EnvironmentVariables)
            {
                string value = Environment.ExpandEnvironmentVariables(entry.Value);
                processStartInfo.Environment[entry.Key] = value;
            }

            if (!_buildOptions.NoLaunchProfileArguments &&
                !string.IsNullOrEmpty(Module.LaunchSettings.CommandLineArgs))
            {
                processStartInfo.Arguments = $"{processStartInfo.Arguments} {Module.LaunchSettings.CommandLineArgs}";
            }
        }

        if (Module.DotnetRootArchVariableName is not null)
        {
            processStartInfo.Environment[Module.DotnetRootArchVariableName] = Path.GetDirectoryName(new Muxer().MuxerPath);
        }

        return processStartInfo;
    }

    private string GetArguments()
    {
        // Keep RunArguments first.
        // In the case of UseAppHost=false, RunArguments is set to `exec $(TargetPath)`:
        // https://github.com/dotnet/sdk/blob/333388c31d811701e3b6be74b5434359151424dc/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Sdk.targets#L1411
        // So, we keep that first always.
        // RunArguments is intentionally not escaped. It can contain multiple arguments and spaces there shouldn't cause the whole
        // value to be wrapped in double quotes. This matches dotnet run behavior.
        // In short, it's expected to already be escaped properly.
        StringBuilder builder = new(Module.RunProperties.Arguments);

        if (TestOptions.IsHelp)
        {
            builder.Append($" {TestingPlatformOptions.HelpOption.Name}");
        }

        if (_buildOptions.PathOptions.ResultsDirectoryPath is { } resultsDirectoryPath)
        {
            builder.Append($" {TestingPlatformOptions.ResultsDirectoryOption.Name} {ArgumentEscaper.EscapeSingleArg(resultsDirectoryPath)}");
        }

        if (_buildOptions.PathOptions.ConfigFilePath is { } configFilePath)
        {
            builder.Append($" {TestingPlatformOptions.ConfigFileOption.Name} {ArgumentEscaper.EscapeSingleArg(configFilePath)}");
        }

        if (_buildOptions.PathOptions.DiagnosticOutputDirectoryPath is { } diagnosticOutputDirectoryPath)
        {
            builder.Append($" {TestingPlatformOptions.DiagnosticOutputDirectoryOption.Name} {ArgumentEscaper.EscapeSingleArg(diagnosticOutputDirectoryPath)}");
        }

        foreach (var arg in _buildOptions.UnmatchedTokens)
        {
            builder.Append($" {ArgumentEscaper.EscapeSingleArg(arg)}");
        }

        builder.Append($" {CliConstants.ServerOptionKey} {CliConstants.ServerOptionValue} {CliConstants.DotNetTestPipeOptionKey} {ArgumentEscaper.EscapeSingleArg(_pipeNameDescription.Name)}");

        return builder.ToString();
    }

    private void WaitOnTestApplicationPipeConnectionLoop()
    {
        _cancellationToken.Cancel();
        _testAppPipeConnectionLoop?.Wait((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
    }

    private async Task WaitConnectionAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var pipeConnection = new NamedPipeServer(_pipeNameDescription, OnRequest, NamedPipeServerStream.MaxAllowedServerInstances, token, skipUnknownMessages: true);
                pipeConnection.RegisterAllSerializers();

                await pipeConnection.WaitConnectionAsync(token);
                _testAppPipeConnections.Add(pipeConnection);
            }
        }
        catch (OperationCanceledException ex)
        {
            // We are exiting
            if (Logger.TraceEnabled)
            {
                string tokenType = ex.CancellationToken == token ? "internal token" : "external token";
                Logger.LogTrace(() => $"WaitConnectionAsync() throws OperationCanceledException with {tokenType}");
            }
        }
        catch (Exception ex)
        {
            if (Logger.TraceEnabled)
            {
                Logger.LogTrace(() => ex.ToString());
            }

            Environment.FailFast(ex.ToString());
        }
    }

    private Task<IResponse> OnRequest(NamedPipeServer server, IRequest request)
    {
        try
        {
            switch (request)
            {
                case HandshakeMessage handshakeMessage:
                    _handshakes.Add(server, handshakeMessage);
                    string negotiatedVersion = GetSupportedProtocolVersion(handshakeMessage);
                    OnHandshakeMessage(handshakeMessage, negotiatedVersion.Length > 0);
                    return Task.FromResult((IResponse)CreateHandshakeMessage(negotiatedVersion));

                case CommandLineOptionMessages commandLineOptionMessages:
                    OnCommandLineOptionMessages(commandLineOptionMessages);
                    break;

                case DiscoveredTestMessages discoveredTestMessages:
                    OnDiscoveredTestMessages(discoveredTestMessages);
                    break;

                case TestResultMessages testResultMessages:
                    OnTestResultMessages(testResultMessages);
                    break;

                case FileArtifactMessages fileArtifactMessages:
                    OnFileArtifactMessages(fileArtifactMessages);
                    break;

                case TestSessionEvent sessionEvent:
                    OnSessionEvent(sessionEvent);
                    break;

                // If we don't recognize the message, log and skip it
                case UnknownMessage unknownMessage:
                    if (Logger.TraceEnabled)
                    {
                        Logger.LogTrace(() => $"Request '{request.GetType()}' with Serializer ID = {unknownMessage.SerializerId} is unsupported.");
                    }
                    return Task.FromResult((IResponse)VoidResponse.CachedInstance);

                default:
                    // If it doesn't match any of the above, throw an exception
                    throw new NotSupportedException(string.Format(CliCommandStrings.CmdUnsupportedMessageRequestTypeException, request.GetType()));
            }
        }
        catch (Exception ex)
        {
            if (Logger.TraceEnabled)
            {
                Logger.LogTrace(() => ex.ToString());
            }

            Environment.FailFast(ex.ToString());
        }

        return Task.FromResult((IResponse)VoidResponse.CachedInstance);
    }

    private static string GetSupportedProtocolVersion(HandshakeMessage handshakeMessage)
    {
        if (!handshakeMessage.Properties.TryGetValue(HandshakeMessagePropertyNames.SupportedProtocolVersions, out string protocolVersions) ||
            protocolVersions is null)
        {
            // It's not expected we hit this.
            // TODO: Maybe we should fail more hard?
            return string.Empty;
        }

        // NOTE: Today, ProtocolConstants.Version is only 1.0.0 (i.e, SDK supports only a single version).
        // Whenever we support multiple versions in SDK, we should do intersection
        // between protocolVersions given by MTP, and the versions supported by SDK.
        // Then we return the "highest" version from the intersection.
        // The current logic **assumes** that ProtocolConstants.SupportedVersions is a single version.
        if (protocolVersions.Split(";").Contains(ProtocolConstants.SupportedVersions))
        {
            return ProtocolConstants.SupportedVersions;
        }

        // The version given by MTP is not supported by SDK.
        return string.Empty;
    }

    private static HandshakeMessage CreateHandshakeMessage(string version) =>
        new(new Dictionary<byte, string>(capacity: 5)
        {
            { HandshakeMessagePropertyNames.PID, Environment.ProcessId.ToString(CultureInfo.InvariantCulture) },
            { HandshakeMessagePropertyNames.Architecture, RuntimeInformation.ProcessArchitecture.ToString() },
            { HandshakeMessagePropertyNames.Framework, RuntimeInformation.FrameworkDescription },
            { HandshakeMessagePropertyNames.OS, RuntimeInformation.OSDescription },
            { HandshakeMessagePropertyNames.SupportedProtocolVersions, version }
        });

    private async Task<int> StartProcess(ProcessStartInfo processStartInfo)
    {
        if (Logger.TraceEnabled)
        {
            Logger.LogTrace(() => $"Test application arguments: {processStartInfo.Arguments}");
        }

        using var process = Process.Start(processStartInfo);
        StoreOutputAndErrorData(process);
        await process.WaitForExitAsync();

        string outputData = string.Join(Environment.NewLine, _outputData);
        string errorData = string.Join(Environment.NewLine, _errorData);
        if (_handshakeInfo.HasValue)
        {
            _output.AssemblyRunCompleted(_handshakeInfo.Value.ExecutionId, process.ExitCode, outputData, errorData);
        }
        else
        {
            _output.HandshakeFailure(Module.TargetPath ?? Module.ProjectFullPath, Module.TargetFramework, process.ExitCode, outputData, errorData);
        }

        LogTestProcessExit(process.ExitCode, outputData, errorData);

        return process.ExitCode;
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

        Logger.LogTrace(() => logMessageBuilder.ToString());
    }

    private void StoreOutputAndErrorData(Process process)
    {
        process.EnableRaisingEvents = true;

        process.OutputDataReceived += (sender, e) =>
        {
            if (string.IsNullOrEmpty(e.Data))
                return;

            _outputData.Add(e.Data);
        };
        process.ErrorDataReceived += (sender, e) =>
        {
            if (string.IsNullOrEmpty(e.Data))
                return;

            _errorData.Add(e.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    private bool ModulePathExists()
    {
        if (!File.Exists(Module.RunProperties.Command))
        {
            // TODO: The error should be shown to the user, not just logged to trace.
            Logger.LogTrace(() => $"Test module '{Module.RunProperties.Command}' not found. Build the test application before or run 'dotnet test'.");

            return false;
        }
        return true;
    }

    public void OnHandshakeMessage(HandshakeMessage handshakeMessage, bool gotSupportedVersion)
    {
        if (!gotSupportedVersion)
        {
            _output.HandshakeFailure(
                Module.TargetPath,
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
            _output.AssemblyRunStarted(Module.TargetPath, handshakeInfo.TargetFramework, handshakeInfo.Architecture, handshakeInfo.ExecutionId, instanceId);
        }

        LogHandshake(handshakeMessage);
    }

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

        Logger.LogTrace(() => logMessageBuilder.ToString());
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

    private void OnCommandLineOptionMessages(CommandLineOptionMessages commandLineOptionMessages)
    {
        HelpRequested?.Invoke(this, new HelpEventArgs { ModulePath = commandLineOptionMessages.ModulePath, CommandLineOptions = [.. commandLineOptionMessages.CommandLineOptionMessageList.Select(message => new CommandLineOption(message.Name, message.Description, message.IsHidden, message.IsBuiltIn))] });
    }

    private void OnDiscoveredTestMessages(DiscoveredTestMessages discoveredTestMessages)
    {
        if (TestOptions.IsHelp)
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

        Logger.LogTrace(() => logMessageBuilder.ToString());
    }

    private void OnTestResultMessages(TestResultMessages testResultMessage)
    {
        if (TestOptions.IsHelp)
        {
            // TODO: Better to throw exception?
            return;
        }

        var handshakeInfo = _handshakeInfo.Value;
        foreach (var testResult in testResultMessage.SuccessfulTestMessages)
        {
            _output.TestCompleted(Module.TargetPath, handshakeInfo.TargetFramework, handshakeInfo.Architecture, handshakeInfo.ExecutionId,
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
            _output.TestCompleted(Module.TargetPath, handshakeInfo.TargetFramework, handshakeInfo.Architecture, handshakeInfo.ExecutionId, testResultMessage.InstanceId,
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

        Logger.LogTrace(() => logMessageBuilder.ToString());
    }

    internal void OnFileArtifactMessages(FileArtifactMessages fileArtifactMessages)
    {
        if (TestOptions.IsHelp)
        {
            // TODO: Better to throw exception?
            return;
        }

        var handshakeInfo = _handshakeInfo.Value;
        foreach (var artifact in fileArtifactMessages.FileArtifacts)
        {
            _output.ArtifactAdded(
                outOfProcess: false,
                Module.TargetPath, handshakeInfo.TargetFramework, handshakeInfo.Architecture, handshakeInfo.ExecutionId,
                artifact.TestDisplayName, artifact.FullPath);
        }

        LogFileArtifacts(fileArtifactMessages);
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

        Logger.LogTrace(() => logMessageBuilder.ToString());
    }

    private void OnSessionEvent(TestSessionEvent sessionEvent)
    {
        // TODO: We shouldn't only log here!
        // We should use it in a more meaningful way. e.g, ensure we received session start/end events.
        if (!Logger.TraceEnabled) return;

        Logger.LogTrace(() => $"TestSessionEvent: {sessionEvent.SessionType}, {sessionEvent.SessionUid}, {sessionEvent.ExecutionId}");
    }

    public override string ToString()
    {
        StringBuilder builder = new();

        if (!string.IsNullOrEmpty(Module.RunProperties.Command))
        {
            builder.Append($"{ProjectProperties.RunCommand}: {Module.RunProperties.Command}");
        }

        if (!string.IsNullOrEmpty(Module.RunProperties.Arguments))
        {
            builder.Append($"{ProjectProperties.RunArguments}: {Module.RunProperties.Arguments}");
        }

        if (!string.IsNullOrEmpty(Module.RunProperties.WorkingDirectory))
        {
            builder.Append($"{ProjectProperties.RunWorkingDirectory}: {Module.RunProperties.WorkingDirectory}");
        }

        if (!string.IsNullOrEmpty(Module.ProjectFullPath))
        {
            builder.Append($"{ProjectProperties.ProjectFullPath}: {Module.ProjectFullPath}");
        }

        if (!string.IsNullOrEmpty(Module.TargetFramework))
        {
            builder.Append($"{ProjectProperties.TargetFramework} : {Module.TargetFramework}");
        }

        return builder.ToString();
    }

    public void Dispose()
    {
        foreach (var namedPipeServer in _testAppPipeConnections)
        {
            try
            {
                namedPipeServer.Dispose();
            }
            catch (Exception ex)
            {
                StringBuilder messageBuilder;
                if (_handshakes.TryGetValue(namedPipeServer, out var handshake))
                {
                    messageBuilder = new StringBuilder(CliCommandStrings.DotnetTestPipeFailureHasHandshake);
                    messageBuilder.AppendLine();
                    foreach (var kvp in handshake.Properties)
                    {
                        messageBuilder.AppendLine($"{kvp.Key}: {kvp.Value}");
                    }                    
                }
                else
                {
                    messageBuilder = new StringBuilder(CliCommandStrings.DotnetTestPipeFailureWithoutHandshake);
                    messageBuilder.AppendLine();
                }

                messageBuilder.AppendLine($"RunCommand: {Module.RunProperties.Command}");
                messageBuilder.AppendLine($"RunArguments: {Module.RunProperties.Arguments}");
                messageBuilder.AppendLine(ex.ToString());

                HasFailureDuringDispose = true;
                Reporter.Error.WriteLine(messageBuilder.ToString());
            }
        }

        WaitOnTestApplicationPipeConnectionLoop();
    }
}
