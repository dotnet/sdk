// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

/// <summary>
/// Runs a test child process to completion and terminates its entire process tree if it is still running
/// when the run ends early (timeout, cancellation, or failure). The one-shot <c>Process.Run*</c> APIs kill
/// only the started process, which can leave descendants holding test files, locks, or redirected pipes.
/// </summary>
internal static class ChildProcessRunner
{
    private static readonly TimeSpan s_terminationTimeout = TimeSpan.FromSeconds(10);

    public static ChildProcessResult Run(ProcessStartInfo startInfo, TimeSpan? timeout = null, string? standardInput = null)
        => RunAsync(startInfo, timeout, standardInput).GetAwaiter().GetResult();

    /// <summary>
    /// Captures both output streams when standard output is redirected; writes <paramref name="standardInput"/>
    /// and then closes standard input when it is redirected. A <see langword="null"/> timeout waits indefinitely.
    /// </summary>
    public static async Task<ChildProcessResult> RunAsync(ProcessStartInfo startInfo, TimeSpan? timeout = null,
        string? standardInput = null, CancellationToken cancellationToken = default)
    {
        if (standardInput is not null && !startInfo.RedirectStandardInput)
        {
            throw new ArgumentException("Standard input must be redirected to supply input.", nameof(standardInput));
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start '{startInfo.FileName}'.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout ?? Timeout.InfiniteTimeSpan);

        string standardOutput = string.Empty;
        string standardError = string.Empty;
        bool timedOut = false;
        try
        {
            // Start reading before writing input so a child that writes first cannot fill its output pipe.
            var output = startInfo.RedirectStandardOutput
                ? process.ReadAllTextAsync(deadline.Token)
                : Task.FromResult((string.Empty, string.Empty));
            if (startInfo.RedirectStandardInput)
            {
                await WriteInputAsync(process, standardInput, deadline.Token).ConfigureAwait(false);
            }

            (standardOutput, standardError) = await output.ConfigureAwait(false);
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
        }
        finally
        {
            TerminateTree(process);
        }

        return new ChildProcessResult(process.ExitCode, standardOutput, standardError, timedOut);
    }

    private static async Task WriteInputAsync(Process process, string? standardInput, CancellationToken cancellationToken)
    {
        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        // EOF prevents a child from waiting on input that will never arrive.
        process.StandardInput.Close();
    }

    private static void TerminateTree(Process process)
    {
        if (process.HasExited)
        {
            return;
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
        }

        if (!process.WaitForExit(s_terminationTimeout))
        {
            throw new TimeoutException($"Process {process.Id} did not terminate within {s_terminationTimeout}.");
        }
    }
}
