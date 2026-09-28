// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Cli.Utils;

public readonly struct CommandResult(ProcessStartInfo startInfo, int exitCode, string? stdOut, string? stdErr)
{
    public static readonly CommandResult Empty = new();

    public ProcessStartInfo StartInfo { get; } = startInfo;
    public int ExitCode { get; } = exitCode;
    public string? StdOut { get; } = stdOut;
    public string? StdErr { get; } = stdErr;
}
