// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

/// <summary>
/// Runs a test child process and kills its entire process tree if it is still running when the run ends.
/// The one-shot <c>Process.Run*</c> APIs kill only the started process on timeout, which can leave
/// descendants holding test files, locks, or redirected pipes.
/// </summary>
internal static class ChildProcessRunner
{
    /// <summary>
    /// Captures both output streams when standard output is redirected, and writes <paramref name="standardInput"/>
    /// then closes standard input when it is redirected. A <see langword="null"/> timeout waits indefinitely.
    /// </summary>
    public static ChildProcessResult Run(ProcessStartInfo startInfo, TimeSpan? timeout = null, string? standardInput = null)
    {
        using var process = Process.Start(startInfo)!;
        using var deadline = new CancellationTokenSource(timeout ?? Timeout.InfiniteTimeSpan);
        try
        {
            // Read before writing input so a child that writes first cannot fill its output pipe.
            var output = startInfo.RedirectStandardOutput
                ? process.ReadAllTextAsync(deadline.Token)
                : Task.FromResult((string.Empty, string.Empty));
            if (startInfo.RedirectStandardInput)
            {
                process.StandardInput.Write(standardInput);
                process.StandardInput.Close();
            }

            var (standardOutput, standardError) = output.GetAwaiter().GetResult();
            process.WaitForExitAsync(deadline.Token).GetAwaiter().GetResult();
            return new ChildProcessResult(process.ExitCode, standardOutput, standardError, TimedOut: false);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            return new ChildProcessResult(ExitCode: -1, string.Empty, string.Empty, TimedOut: true);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                Assert.IsTrue(process.WaitForExit(TimeSpan.FromSeconds(10)), $"Process {process.Id} did not terminate.");
            }
        }
    }
}
