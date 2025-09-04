// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Cli.Commands.Test;

internal sealed class HandshakeArgs : EventArgs
{
    public Handshake Handshake { get; set; }
    public bool GotSupportedVersion { get; set; }
}

internal sealed class HelpEventArgs : EventArgs
{
    public string ModulePath { get; set; }

    public CommandLineOption[] CommandLineOptions { get; set; }
}

internal sealed class DiscoveredTestEventArgs : EventArgs
{
    public string ExecutionId { get; set; }

    public string InstanceId { get; set; }

    public DiscoveredTest[] DiscoveredTests { get; set; }
}

internal sealed class TestResultEventArgs : EventArgs
{
    public string ExecutionId { get; set; }

    public string InstanceId { get; set; }

    public SuccessfulTestResult[] SuccessfulTestResults { get; set; }

    public FailedTestResult[] FailedTestResults { get; set; }
}

internal sealed class FileArtifactEventArgs : EventArgs
{
    public string ExecutionId { get; set; }

    public string InstanceId { get; set; }

    public FileArtifact[] FileArtifacts { get; set; }
}

internal sealed class SessionEventArgs : EventArgs
{
    public TestSession SessionEvent { get; set; }
}

internal sealed class ErrorEventArgs : EventArgs
{
    public string ErrorMessage { get; set; }
}

internal sealed class TestProcessExitEventArgs : EventArgs
{
    public List<string> OutputData { get; set; }
    public List<string> ErrorData { get; set; }
    public int ExitCode { get; set; }
}
