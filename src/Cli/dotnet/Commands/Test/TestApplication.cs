﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.DotNet.Cli.Commands.Test.IPC;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal sealed class TestApplication(TestModule module, BuildOptions buildOptions) : IDisposable
{
    private readonly BuildOptions _buildOptions = buildOptions;

    private readonly List<string> _outputData = [];
    private readonly List<string> _errorData = [];
    private readonly PipeNameDescription _pipeNameDescription = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));
    private readonly CancellationTokenSource _cancellationToken = new();

    private Task _testAppPipeConnectionLoop;
    private readonly List<NamedPipeServer> _testAppPipeConnections = [];

    public event EventHandler<HandshakeArgs> HandshakeReceived;
    public event EventHandler<HelpEventArgs> HelpRequested;
    public event EventHandler<DiscoveredTestEventArgs> DiscoveredTestsReceived;
    public event EventHandler<TestResultEventArgs> TestResultsReceived;
    public event EventHandler<FileArtifactEventArgs> FileArtifactsReceived;
    public event EventHandler<SessionEventArgs> SessionEventReceived;
    public event EventHandler<ErrorEventArgs> ErrorReceived;
    public event EventHandler<TestProcessExitEventArgs> TestProcessExited;

    public TestModule Module { get; } = module;

    public async Task<int> RunAsync(TestOptions testOptions)
    {
        if (testOptions.HasFilterMode && !ModulePathExists())
        {
            return ExitCode.GenericFailure;
        }

        var processStartInfo = CreateProcessStartInfo(testOptions);

        _testAppPipeConnectionLoop = Task.Run(async () => await WaitConnectionAsync(_cancellationToken.Token), _cancellationToken.Token);
        var testProcessResult = await StartProcess(processStartInfo);

        WaitOnTestApplicationPipeConnectionLoop();

        return testProcessResult;
    }

    private ProcessStartInfo CreateProcessStartInfo(TestOptions testOptions)
    {
        bool isDll = Module.RunProperties.RunCommand.HasExtension(CliConstants.DLLExtension);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = GetFileName(testOptions, isDll),
            Arguments = GetArguments(testOptions, isDll),
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (!string.IsNullOrEmpty(Module.RunProperties.RunWorkingDirectory))
        {
            processStartInfo.WorkingDirectory = Module.RunProperties.RunWorkingDirectory;
        }

        if (Module.LaunchSettings is not null)
        {
            foreach (var entry in Module.LaunchSettings.EnvironmentVariables)
            {
                string value = Environment.ExpandEnvironmentVariables(entry.Value);
                processStartInfo.EnvironmentVariables[entry.Key] = value;
            }

            // TODO: Support --no-launch-profile-arguments
            if (!string.IsNullOrEmpty(Module.LaunchSettings.CommandLineArgs))
            {
                processStartInfo.Arguments = $"{processStartInfo.Arguments} {Module.LaunchSettings.CommandLineArgs}";
            }
        }

        return processStartInfo;
    }

    private string GetFileName(TestOptions testOptions, bool isDll)
        => isDll ? Environment.ProcessPath : Module.RunProperties.RunCommand;

    private string GetArguments(TestOptions testOptions, bool isDll)
    {
        if (testOptions.HasFilterMode || !isDll || !IsArchitectureSpecified(testOptions))
        {
            return BuildArgs(testOptions, isDll);
        }

        // We fallback to dotnet run only when we have a dll and an architecture is specified.
        // TODO: Is this a valid case?
        return BuildArgsWithDotnetRun(testOptions);
    }

    private static bool IsArchitectureSpecified(TestOptions testOptions)
    {
        return !string.IsNullOrEmpty(testOptions.Architecture);
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
                NamedPipeServer pipeConnection = new(_pipeNameDescription, OnRequest, NamedPipeServerStream.MaxAllowedServerInstances, token, skipUnknownMessages: true);
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

    private Task<IResponse> OnRequest(IRequest request)
    {
        try
        {
            switch (request)
            {
                case HandshakeMessage handshakeMessage:
                    if (handshakeMessage.Properties.TryGetValue(HandshakeMessagePropertyNames.ModulePath, out string value))
                    {
                        OnHandshakeMessage(handshakeMessage);

                        return Task.FromResult((IResponse)CreateHandshakeMessage(GetSupportedProtocolVersion(handshakeMessage)));
                    }
                    break;

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
        handshakeMessage.Properties.TryGetValue(HandshakeMessagePropertyNames.SupportedProtocolVersions, out string protocolVersions);

        string version = string.Empty;
        if (protocolVersions is not null && protocolVersions.Split(";").Contains(ProtocolConstants.Version))
        {
            version = ProtocolConstants.Version;
        }

        return version;
    }

    private static HandshakeMessage CreateHandshakeMessage(string version) =>
        new(new Dictionary<byte, string>
        {
            { HandshakeMessagePropertyNames.PID, Process.GetCurrentProcess().Id.ToString() },
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

        var process = Process.Start(processStartInfo);
        StoreOutputAndErrorData(process);
        await process.WaitForExitAsync();

        TestProcessExited?.Invoke(this, new TestProcessExitEventArgs { OutputData = _outputData, ErrorData = _errorData, ExitCode = process.ExitCode });

        return process.ExitCode;
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
        if (!File.Exists(Module.RunProperties.RunCommand))
        {
            ErrorReceived.Invoke(this, new ErrorEventArgs { ErrorMessage = $"Test module '{Module.RunProperties.RunCommand}' not found. Build the test application before or run 'dotnet test'." });
            return false;
        }
        return true;
    }

    private string BuildArgs(TestOptions testOptions, bool isDll)
    {
        StringBuilder builder = new();

        if (isDll)
        {
            builder.Append($"exec {Module.RunProperties.RunCommand} ");
        }

        AppendCommonArgs(builder, testOptions);

        return builder.ToString();
    }

    private string BuildArgsWithDotnetRun(TestOptions testOptions)
    {
        StringBuilder builder = new();

        builder.Append($"{CliConstants.DotnetRunCommand} {TestingPlatformOptions.ProjectOption.Name} \"{Module.ProjectFullPath}\"");

        // Because we restored and built before in MSHandler, we will skip those with dotnet run
        builder.Append($" {CommonOptions.NoRestoreOption.Name}");
        builder.Append($" {TestingPlatformOptions.NoBuildOption.Name}");

        foreach (var arg in _buildOptions.MSBuildArgs)
        {
            builder.Append($" {arg}");
        }

        if (!string.IsNullOrEmpty(Module.TargetFramework))
        {
            builder.Append($" {CliConstants.FrameworkOptionKey} {Module.TargetFramework}");
        }

        builder.Append($" {CliConstants.ParametersSeparator} ");

        AppendCommonArgs(builder, testOptions);

        return builder.ToString();
    }

    private void AppendCommonArgs(StringBuilder builder, TestOptions testOptions)
    {
        if (testOptions.IsHelp)
        {
            builder.Append($" {TestingPlatformOptions.HelpOption.Name} ");
        }

        var args = _buildOptions.UnmatchedTokens;
        builder.Append(args.Count != 0
            ? args.Aggregate((a, b) => $"{a} {b}")
            : string.Empty);

        builder.Append($" {CliConstants.ServerOptionKey} {CliConstants.ServerOptionValue} {CliConstants.DotNetTestPipeOptionKey} {_pipeNameDescription.Name} {Module.RunProperties.RunArguments}");
    }

    public void OnHandshakeMessage(HandshakeMessage handshakeMessage)
    {
        HandshakeReceived?.Invoke(this, new HandshakeArgs { Handshake = new Handshake(handshakeMessage.Properties) });
    }

    public void OnCommandLineOptionMessages(CommandLineOptionMessages commandLineOptionMessages)
    {
        HelpRequested?.Invoke(this, new HelpEventArgs { ModulePath = commandLineOptionMessages.ModulePath, CommandLineOptions = [.. commandLineOptionMessages.CommandLineOptionMessageList.Select(message => new CommandLineOption(message.Name, message.Description, message.IsHidden, message.IsBuiltIn))] });
    }

    internal void OnDiscoveredTestMessages(DiscoveredTestMessages discoveredTestMessages)
    {
        DiscoveredTestsReceived?.Invoke(this, new DiscoveredTestEventArgs
        {
            ExecutionId = discoveredTestMessages.ExecutionId,
            InstanceId = discoveredTestMessages.InstanceId,
            DiscoveredTests = [.. discoveredTestMessages.DiscoveredMessages.Select(message => new DiscoveredTest(message.Uid, message.DisplayName))]
        });
    }

    internal void OnTestResultMessages(TestResultMessages testResultMessage)
    {
        TestResultsReceived?.Invoke(this, new TestResultEventArgs
        {
            ExecutionId = testResultMessage.ExecutionId,
            InstanceId = testResultMessage.InstanceId,
            SuccessfulTestResults = [.. testResultMessage.SuccessfulTestMessages.Select(message => new SuccessfulTestResult(message.Uid, message.DisplayName, message.State, message.Duration, message.Reason, message.StandardOutput, message.ErrorOutput, message.SessionUid))],
            FailedTestResults = [.. testResultMessage.FailedTestMessages.Select(message => new FailedTestResult(message.Uid, message.DisplayName, message.State, message.Duration, message.Reason, [.. message.Exceptions.Select(e => new FlatException(e.ErrorMessage, e.ErrorType, e.StackTrace))], message.StandardOutput, message.ErrorOutput, message.SessionUid))]
        });
    }

    internal void OnFileArtifactMessages(FileArtifactMessages fileArtifactMessages)
    {
        FileArtifactsReceived?.Invoke(this, new FileArtifactEventArgs
        {
            ExecutionId = fileArtifactMessages.ExecutionId,
            InstanceId = fileArtifactMessages.InstanceId,
            FileArtifacts = [.. fileArtifactMessages.FileArtifacts.Select(message => new FileArtifact(message.FullPath, message.DisplayName, message.Description, message.TestUid, message.TestDisplayName, message.SessionUid))]
        });
    }

    internal void OnSessionEvent(TestSessionEvent sessionEvent)
    {
        SessionEventReceived?.Invoke(this, new SessionEventArgs { SessionEvent = new TestSession(sessionEvent.SessionType, sessionEvent.SessionUid, sessionEvent.ExecutionId) });
    }

    public override string ToString()
    {
        StringBuilder builder = new();

        if (!string.IsNullOrEmpty(Module.RunProperties.RunCommand))
        {
            builder.Append($"{ProjectProperties.RunCommand}: {Module.RunProperties.RunCommand}");
        }

        if (!string.IsNullOrEmpty(Module.RunProperties.RunArguments))
        {
            builder.Append($"{ProjectProperties.RunArguments}: {Module.RunProperties.RunArguments}");
        }

        if (!string.IsNullOrEmpty(Module.RunProperties.RunWorkingDirectory))
        {
            builder.Append($"{ProjectProperties.RunWorkingDirectory}: {Module.RunProperties.RunWorkingDirectory}");
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
            namedPipeServer.Dispose();
        }

        WaitOnTestApplicationPipeConnectionLoop();
    }
}
