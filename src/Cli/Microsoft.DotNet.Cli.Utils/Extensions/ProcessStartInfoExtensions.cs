// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Cli.Utils.Extensions;

internal static class ProcessStartInfoExtensions
{
    public static int Execute(this ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        using Process process = new()
        {
            StartInfo = startInfo
        };

#if NET
        if (!startInfo.UseShellExecute)
        {
            startInfo.InheritedHandles ??= [];
        }
#endif
        using (var reaper = ProcessReaper.Create(process))
        {
            process.Start();
            reaper.NotifyProcessStarted();
#if NET
            return process.WaitForExitStatus().ExitCode;
#else
            process.WaitForExit();
            return process.ExitCode;
#endif
        }
    }

    public static int ExecuteAndCaptureOutput(this ProcessStartInfo startInfo, out string? stdOut, out string? stdErr)
    {
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        using Process process = new()
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

#if NET
        if (!startInfo.UseShellExecute)
        {
            startInfo.InheritedHandles ??= [];
        }
#endif
        using (var reaper = ProcessReaper.Create(process))
        {
            process.Start();
            reaper.NotifyProcessStarted();

#if NET
            // Preserve the captured text as-is; callers do not require StreamForwarder's newline normalization.
            (stdOut, stdErr) = process.ReadAllText();
            return process.WaitForExitStatus().ExitCode;
#else
            StreamForwarder outStream = new StreamForwarder().Capture();
            StreamForwarder errStream = new StreamForwarder().Capture();
            var taskOut = outStream.BeginRead(process.StandardOutput);
            var taskErr = errStream.BeginRead(process.StandardError);

            process.WaitForExit();

            taskOut.Wait();
            taskErr.Wait();
            stdOut = outStream.CapturedOutput;
            stdErr = errStream.CapturedOutput;
            return process.ExitCode;
#endif
        }

    }
}
