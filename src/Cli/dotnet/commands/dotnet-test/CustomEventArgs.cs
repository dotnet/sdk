// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli
{
    internal class HandshakeArgs : EventArgs
    {
        public Handshake Handshake { get; set; }
    }

    internal class HelpEventArgs : EventArgs
    {
        public string ModulePath { get; set; }

        public CommandLineOption[] CommandLineOptions { get; set; }
    }

    internal class DiscoveredTestEventArgs : EventArgs
    {
        public string ExecutionId { get; set; }

        public DiscoveredTest[] DiscoveredTests { get; set; }
    }

    internal class TestResultEventArgs : EventArgs
    {
        public string ExecutionId { get; set; }

        public SuccessfulTestResult[] SuccessfulTestResults { get; set; }

        public FailedTestResult[] FailedTestResults { get; set; }
    }

    internal class FileArtifactEventArgs : EventArgs
    {
        public string ExecutionId { get; set; }

        public FileArtifact[] FileArtifacts { get; set; }
    }

    internal class SessionEventArgs : EventArgs
    {
        public TestSession SessionEvent { get; set; }
    }

    internal class ErrorEventArgs : EventArgs
    {
        public string ErrorMessage { get; set; }
    }

    internal class TestProcessExitEventArgs : EventArgs
    {
        public List<string> OutputData { get; set; }
        public List<string> ErrorData { get; set; }
        public int ExitCode { get; set; }
    }

    internal class ExecutionEventArgs : EventArgs
    {
        public string ModulePath { get; set; }
        public string ExecutionId { get; set; }
    }
}
