// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.Tools.Test;

namespace Microsoft.DotNet.Cli
{
    internal sealed class TestApplication : IDisposable
    {
        private readonly Module _module;
        private readonly List<string> _args;

        private readonly List<string> _outputData = [];
        private readonly List<string> _errorData = [];
        private readonly PipeNameDescription _pipeNameDescription = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"));
        private readonly CancellationTokenSource _cancellationToken = new();

        private Task _testAppPipeConnectionLoop;
        private readonly List<NamedPipeServer> _testAppPipeConnections = new();
        private ConcurrentDictionary<string, string> _executionIds = [];

        public event EventHandler<HandshakeArgs> HandshakeReceived;
        public event EventHandler<HelpEventArgs> HelpRequested;
        public event EventHandler<DiscoveredTestEventArgs> DiscoveredTestsReceived;
        public event EventHandler<TestResultEventArgs> TestResultsReceived;
        public event EventHandler<FileArtifactEventArgs> FileArtifactsReceived;
        public event EventHandler<SessionEventArgs> SessionEventReceived;
        public event EventHandler<ErrorEventArgs> ErrorReceived;
        public event EventHandler<TestProcessExitEventArgs> TestProcessExited;
        public event EventHandler<ExecutionEventArgs> ExecutionIdReceived;


        public Module Module => _module;

        public TestApplication(Module module, List<string> args)
        {
            _module = module;
            _args = args;
        }

        public void AddExecutionId(string executionId)
        {
            _ = _executionIds.GetOrAdd(executionId, _ => string.Empty);
        }

        public async Task<int> RunAsync(bool hasFilterMode, bool enableHelp, BuildConfigurationOptions buildConfigurationOptions)
        {
            if (hasFilterMode && !ModulePathExists())
            {
                return 1;
            }

            bool isDll = _module.DllOrExePath.HasExtension(CliConstants.DLLExtension);
            var processStartInfo = CreateProcessStartInfo(hasFilterMode, isDll, buildConfigurationOptions, enableHelp);

            _testAppPipeConnectionLoop = Task.Run(async () => await WaitConnectionAsync(_cancellationToken.Token), _cancellationToken.Token);
            var testProcessResult = await StartProcess(processStartInfo);

            WaitOnTestApplicationPipeConnectionLoop();

            return testProcessResult;
        }


        private ProcessStartInfo CreateProcessStartInfo(bool hasFilterMode, bool isDll, BuildConfigurationOptions buildConfigurationOptions, bool enableHelp)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = hasFilterMode ? (isDll ? Environment.ProcessPath : _module.DllOrExePath) : Environment.ProcessPath,
                Arguments = hasFilterMode ? BuildArgs(isDll) : BuildArgsWithDotnetRun(enableHelp, buildConfigurationOptions),
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            if (!string.IsNullOrEmpty(_module.RunSettingsFilePath))
            {
                processStartInfo.EnvironmentVariables.Add(CliConstants.TestingPlatformVsTestBridgeRunSettingsFileEnvVar, _module.RunSettingsFilePath);
            }

            return processStartInfo;
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
            catch (OperationCanceledException ex) when (ex.CancellationToken == token)
            {
                // We are exiting
            }
            catch (Exception ex)
            {
                if (VSTestTrace.TraceEnabled)
                {
                    VSTestTrace.SafeWriteTrace(() => ex.ToString());
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
                        if (VSTestTrace.TraceEnabled)
                        {
                            VSTestTrace.SafeWriteTrace(() => $"Request '{request.GetType()}' with Serializer ID = {unknownMessage.SerializerId} is unsupported.");
                        }
                        return Task.FromResult((IResponse)VoidResponse.CachedInstance);

                    default:
                        // If it doesn't match any of the above, throw an exception
                        throw new NotSupportedException(string.Format(LocalizableStrings.CmdUnsupportedMessageRequestTypeException, request.GetType()));
                }
            }
            catch (Exception ex)
            {
                if (VSTestTrace.TraceEnabled)
                {
                    VSTestTrace.SafeWriteTrace(() => ex.ToString());
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
            if (VSTestTrace.TraceEnabled)
            {
                VSTestTrace.SafeWriteTrace(() => $"Test application arguments: {processStartInfo.Arguments}");
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
            if (!File.Exists(_module.DllOrExePath))
            {
                ErrorReceived.Invoke(this, new ErrorEventArgs { ErrorMessage = $"Test module '{_module.DllOrExePath}' not found. Build the test application before or run 'dotnet test'." });
                return false;
            }
            return true;
        }

        private string BuildArgsWithDotnetRun(bool hasHelp, BuildConfigurationOptions buildConfigurationOptions)
        {
            StringBuilder builder = new();

            builder.Append($"{CliConstants.DotnetRunCommand} {TestingPlatformOptions.ProjectOption.Name} \"{_module.ProjectPath}\"");

            // Because we restored and built before in MSHandler, we will skip those with dotnet run
            builder.Append($" {TestingPlatformOptions.NoRestoreOption.Name}");
            builder.Append($" {TestingPlatformOptions.NoBuildOption.Name}");

            if (buildConfigurationOptions.HasListTests)
            {
                builder.Append($" {TestingPlatformOptions.ListTestsOption.Name}");
            }

            if (!string.IsNullOrEmpty(buildConfigurationOptions.Architecture))
            {
                builder.Append($" {TestingPlatformOptions.ArchitectureOption.Name} {buildConfigurationOptions.Architecture}");
            }

            if (!string.IsNullOrEmpty(buildConfigurationOptions.Configuration))
            {
                builder.Append($" {TestingPlatformOptions.ConfigurationOption.Name} {buildConfigurationOptions.Configuration}");
            }

            if (!string.IsNullOrEmpty(_module.TargetFramework))
            {
                builder.Append($" {CliConstants.FrameworkOptionKey} {_module.TargetFramework}");
            }

            builder.Append($" {CliConstants.ParametersSeparator} ");

            if (hasHelp)
            {
                builder.Append($" {CliConstants.HelpOptionKey} ");
            }

            builder.Append(_args.Count != 0
                ? _args.Aggregate((a, b) => $"{a} {b}")
                : string.Empty);

            builder.Append($" {CliConstants.ServerOptionKey} {CliConstants.ServerOptionValue} {CliConstants.DotNetTestPipeOptionKey} {_pipeNameDescription.Name}");

            return builder.ToString();
        }

        private string BuildArgs(bool isDll)
        {
            StringBuilder builder = new();

            if (isDll)
            {
                builder.Append($"exec {_module.DllOrExePath} ");
            }

            builder.Append(_args.Count != 0
                ? _args.Aggregate((a, b) => $"{a} {b}")
                : string.Empty);

            builder.Append($" {CliConstants.ServerOptionKey} {CliConstants.ServerOptionValue} {CliConstants.DotNetTestPipeOptionKey} {_pipeNameDescription.Name}");

            return builder.ToString();
        }

        public void OnHandshakeMessage(HandshakeMessage handshakeMessage)
        {
            if (handshakeMessage.Properties.TryGetValue(HandshakeMessagePropertyNames.ExecutionId, out string executionId))
            {
                AddExecutionId(executionId);
                ExecutionIdReceived?.Invoke(this, new ExecutionEventArgs { ModulePath = _module.DllOrExePath, ExecutionId = executionId });
            }
            HandshakeReceived?.Invoke(this, new HandshakeArgs { Handshake = new Handshake(handshakeMessage.Properties) });
        }

        public void OnCommandLineOptionMessages(CommandLineOptionMessages commandLineOptionMessages)
        {
            HelpRequested?.Invoke(this, new HelpEventArgs { ModulePath = commandLineOptionMessages.ModulePath, CommandLineOptions = commandLineOptionMessages.CommandLineOptionMessageList.Select(message => new CommandLineOption(message.Name, message.Description, message.IsHidden, message.IsBuiltIn)).ToArray() });
        }

        internal void OnDiscoveredTestMessages(DiscoveredTestMessages discoveredTestMessages)
        {
            DiscoveredTestsReceived?.Invoke(this, new DiscoveredTestEventArgs
            {
                ExecutionId = discoveredTestMessages.ExecutionId,
                DiscoveredTests = discoveredTestMessages.DiscoveredMessages.Select(message => new DiscoveredTest(message.Uid, message.DisplayName)).ToArray()
            });
        }

        internal void OnTestResultMessages(TestResultMessages testResultMessage)
        {
            TestResultsReceived?.Invoke(this, new TestResultEventArgs
            {
                ExecutionId = testResultMessage.ExecutionId,
                SuccessfulTestResults = testResultMessage.SuccessfulTestMessages.Select(message => new SuccessfulTestResult(message.Uid, message.DisplayName, message.State, message.Duration, message.Reason, message.StandardOutput, message.ErrorOutput, message.SessionUid)).ToArray(),
                FailedTestResults = testResultMessage.FailedTestMessages.Select(message => new FailedTestResult(message.Uid, message.DisplayName, message.State, message.Duration, message.Reason, message.Exceptions.Select(e => new FlatException(e.ErrorMessage, e.ErrorType, e.StackTrace)).ToArray(), message.StandardOutput, message.ErrorOutput, message.SessionUid)).ToArray()
            });
        }

        internal void OnFileArtifactMessages(FileArtifactMessages fileArtifactMessages)
        {
            FileArtifactsReceived?.Invoke(this, new FileArtifactEventArgs { FileArtifacts = fileArtifactMessages.FileArtifacts.Select(message => new FileArtifact(message.FullPath, message.DisplayName, message.Description, message.TestUid, message.TestDisplayName, message.SessionUid)).ToArray() });
        }

        internal void OnSessionEvent(TestSessionEvent sessionEvent)
        {
            SessionEventReceived?.Invoke(this, new SessionEventArgs { SessionEvent = new TestSession(sessionEvent.SessionType, sessionEvent.SessionUid, sessionEvent.ExecutionId) });
        }

        public override string ToString()
        {
            StringBuilder builder = new();

            if (!string.IsNullOrEmpty(_module.DllOrExePath))
            {
                builder.Append($"DLL: {_module.DllOrExePath}");
            }

            if (!string.IsNullOrEmpty(_module.ProjectPath))
            {
                builder.Append($"Project: {_module.ProjectPath}");
            }
            ;

            if (!string.IsNullOrEmpty(_module.TargetFramework))
            {
                builder.Append($"Target Framework: {_module.TargetFramework}");
            }
            ;

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
}
