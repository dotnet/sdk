// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Tools.Test;

namespace Microsoft.DotNet.Cli
{
    internal class TestApplication
    {
        private readonly string _modulePath;
        private readonly string _pipeName;
        private readonly string[] _args;
        private readonly List<string> _outputData = [];
        private readonly List<string> _errorData = [];

        public event EventHandler<HandshakeInfoArgs> HandshakeInfoReceived;
        public event EventHandler<HelpEventArgs> HelpRequested;
        public event EventHandler<SuccessfulTestResultEventArgs> SuccessfulTestResultReceived;
        public event EventHandler<FailedTestResultEventArgs> FailedTestResultReceived;
        public event EventHandler<FileArtifactInfoEventArgs> FileArtifactInfoReceived;
        public event EventHandler<SessionEventArgs> SessionEventReceived;
        public event EventHandler<ErrorEventArgs> ErrorReceived;
        public event EventHandler<TestProcessExitEventArgs> TestProcessExited;

        public string ModulePath => _modulePath;

        public TestApplication(string modulePath, string pipeName, string[] args)
        {
            _modulePath = modulePath;
            _pipeName = pipeName;
            _args = args;
        }

        public async Task<int> RunAsync(bool enableHelp)
        {
            if (!ModulePathExists())
            {
                return 1;
            }

            bool isDll = _modulePath.EndsWith(".dll");
            ProcessStartInfo processStartInfo = new()
            {
                FileName = isDll ?
                Environment.ProcessPath :
                _modulePath,
                Arguments = enableHelp ? BuildHelpArgs(isDll) : BuildArgs(isDll),
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            return await StartProcess(processStartInfo);
        }

        private async Task<int> StartProcess(ProcessStartInfo processStartInfo)
        {
            if (VSTestTrace.TraceEnabled)
            {
                VSTestTrace.SafeWriteTrace(() => $"Updated args: {processStartInfo.Arguments}");
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
            if (!File.Exists(_modulePath))
            {
                ErrorReceived.Invoke(this, new ErrorEventArgs { ErrorMessage = $"Test module '{_modulePath}' not found. Build the test application before or run 'dotnet test'." });
                return false;
            }
            return true;
        }

        private string BuildArgs(bool isDll)
        {
            StringBuilder builder = new();

            if (isDll)
            {
                builder.Append($"exec {_modulePath} ");
            }

            builder.Append(_args.Length != 0
                ? _args.Aggregate((a, b) => $"{a} {b}")
                : string.Empty);

            builder.Append($" {CliConstants.ServerOptionKey} {CliConstants.ServerOptionValue} {CliConstants.DotNetTestPipeOptionKey} {_pipeName}");

            return builder.ToString();
        }

        private string BuildHelpArgs(bool isDll)
        {
            StringBuilder builder = new();

            if (isDll)
            {
                builder.Append($"exec {_modulePath} ");
            }

            builder.Append($" {CliConstants.HelpOptionKey} {CliConstants.ServerOptionKey} {CliConstants.ServerOptionValue} {CliConstants.DotNetTestPipeOptionKey} {_pipeName}");

            return builder.ToString();
        }

        public void OnHandshakeInfo(HandshakeInfo handshakeInfo)
        {
            HandshakeInfoReceived?.Invoke(this, new HandshakeInfoArgs { handshakeInfo = handshakeInfo });
        }

        public void OnCommandLineOptionMessages(CommandLineOptionMessages commandLineOptionMessages)
        {
            HelpRequested?.Invoke(this, new HelpEventArgs { CommandLineOptionMessages = commandLineOptionMessages });
        }

        internal void OnSuccessfulTestResultMessage(SuccessfulTestResultMessage successfulTestResultMessage)
        {
            SuccessfulTestResultReceived?.Invoke(this, new SuccessfulTestResultEventArgs { SuccessfulTestResultMessage = successfulTestResultMessage });
        }

        internal void OnFailedTestResultMessage(FailedTestResultMessage failedTestResultMessage)
        {
            FailedTestResultReceived?.Invoke(this, new FailedTestResultEventArgs { FailedTestResultMessage = failedTestResultMessage });
        }

        internal void OnFileArtifactInfo(FileArtifactInfo fileArtifactInfo)
        {
            FileArtifactInfoReceived?.Invoke(this, new FileArtifactInfoEventArgs { FileArtifactInfo = fileArtifactInfo });
        }

        internal void OnSessionEvent(TestSessionEvent sessionEvent)
        {
            SessionEventReceived?.Invoke(this, new SessionEventArgs { SessionEvent = sessionEvent });
        }
    }
}
