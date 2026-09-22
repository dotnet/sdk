// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Cli.Utils.Extensions;

internal static class ProcessStartInfoExtensions
{
    public static int Execute(this ProcessStartInfo startInfo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        using var process = new Process
        {
            StartInfo = startInfo
        };

        return new Command(process).Execute(cancellationToken).ExitCode;
    }

    public static int ExecuteAndCaptureOutput(
        this ProcessStartInfo startInfo,
        out string? stdOut,
        out string? stdErr,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = startInfo
        };

        CommandResult result = new Command(process)
            .CaptureStdOut()
            .CaptureStdErr()
            .Execute(cancellationToken);

        stdOut = result.StdOut;
        stdErr = result.StdErr;
        return result.ExitCode;
    }
}
