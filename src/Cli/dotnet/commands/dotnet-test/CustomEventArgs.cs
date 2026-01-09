// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Test;

namespace Microsoft.DotNet.Cli
{
    internal class HandshakeInfoArgs : EventArgs
    {
        public HandshakeInfo handshakeInfo { get; set; }
    }

    internal class HelpEventArgs : EventArgs
    {
        public CommandLineOptionMessages CommandLineOptionMessages { get; set; }
    }

    internal class SuccessfulTestResultEventArgs : EventArgs
    {
        public SuccessfulTestResultMessage SuccessfulTestResultMessage { get; set; }
    }

    internal class FailedTestResultEventArgs : EventArgs
    {
        public FailedTestResultMessage FailedTestResultMessage { get; set; }
    }

    internal class FileArtifactInfoEventArgs : EventArgs
    {
        public FileArtifactInfo FileArtifactInfo { get; set; }
    }

    internal class SessionEventArgs : EventArgs
    {
        public TestSessionEvent SessionEvent { get; set; }
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
}
