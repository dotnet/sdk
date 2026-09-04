// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HotReload;

internal sealed class ProcessState(Process process, ILogger logger, ProcessExitAction? onExit, Func<ValueTask<bool>>? terminator, bool isUserApplication) : IDisposable
{
    // Exit code used by the OS when process is terminated by an external signal.
    private static readonly int s_processTerminatedExitCode = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? unchecked((int)0xC000013A) : 137;

    public Process Process => process;
    public bool IsUserApplication => isUserApplication;
    public int ProcessId { get; private set; }
    public bool HasExited { get; private set; }

    public void Dispose()
        => Process.Dispose();

    public void Started(int processId)
        => ProcessId = processId;

    public async Task<int?> WaitForExitAsync(TimeSpan processCleanupTimeout, CancellationToken processTerminationToken)
    {
        try
        {
            try
            {
                await WaitForExitImplAsync(processTerminationToken);
            }
            catch (OperationCanceledException)
            {
                // Process termination requested via cancellation token.
                // Either Ctrl+C was pressed or the process is being restarted.

                // Non-cancellable to not leave orphaned processes around blocking resources:
                await TerminateProcessAsync(terminator, processCleanupTimeout);
            }
        }
        catch (Exception e)
        {
            if (isUserApplication)
            {
                logger.Log(LogEvents.ApplicationFailed, e.Message);
            }
        }

        HasExited = true;

        int? exitCode;

        try
        {
            exitCode = Process.ExitCode;
        }
        catch
        {
            exitCode = null;
        }

        if (isUserApplication)
        {
            if (exitCode == 0 || exitCode == s_processTerminatedExitCode)
            {
                logger.Log(LogEvents.Exited);
            }
            else if (exitCode == null)
            {
                logger.Log(LogEvents.ExitedWithUnknownErrorCode);
            }
            else
            {
                logger.Log(LogEvents.ExitedWithErrorCode, exitCode.Value);
            }
        }

        if (onExit != null)
        {
            await onExit(ProcessId, exitCode);
        }

        return exitCode;
    }

    private async ValueTask TerminateProcessAsync(Func<ValueTask<bool>>? terminator, TimeSpan processCleanupTimeout)
    {
        var forceOnly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !isUserApplication;

        if (terminator is null || await terminator() is false)
        {
            TerminateProcess(forceOnly);
        }

        if (forceOnly)
        {
            _ = await WaitForExitReportProgressAsync(timeout: null);
            return;
        }

        // Ctlr+C/SIGTERM has been sent, wait for the process to exit gracefully.
        if (processCleanupTimeout.TotalMilliseconds == 0 ||
            !await WaitForExitReportProgressAsync(processCleanupTimeout))
        {
            // Force termination if the process is still running after the timeout.
            TerminateProcess(force: true);

            _ = await WaitForExitReportProgressAsync(timeout: null);
        }
    }

    private async ValueTask<bool> WaitForExitReportProgressAsync(TimeSpan? timeout)
    {
        Task? reportingTask;

        using var cancellationSource = new CancellationTokenSource();
        if (timeout != null)
        {
            logger.Log(LogEvents.WaitingForProcessToExitWithin, ProcessId, (int)timeout.Value.TotalSeconds);
            cancellationSource.CancelAfter(timeout.Value);
            reportingTask = null;
        }
        else
        {
            // report progress if waiting without a timeout:
            reportingTask = Task.Run(async () =>
            {
                try
                {
                    var i = 1;
                    while (!cancellationSource.IsCancellationRequested)
                    {
                        logger.Log(LogEvents.WaitingForProcessToExit, ProcessId, i++);
                        await Task.Delay(TimeSpan.FromSeconds(1), cancellationSource.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });
        }

        try
        {
            return await WaitForExitImplAsync(cancellationSource.Token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            if (reportingTask != null)
            {
                cancellationSource.Cancel();
                await reportingTask;
            }
        }
    }

    /// <summary>
    /// Returns true if the process has been verified to have exited and its output has been drained, false if an error occurred.
    /// </summary>
    private async ValueTask<bool> WaitForExitImplAsync(CancellationToken cancellationToken)
    {
#if NET
        await process.WaitForExitAsync(cancellationToken);
        return true;
#else
        if (!await WaitForExitNetFrameworkAsync(cancellationToken))
        {
            return false;
        }

        // Parameterless WaitForExit drains asynchronous output and error streams after process has exited:
        return await Task.Run(() =>
        {
            try
            {
                process.WaitForExit();
                return true;
            }
            catch
            {
                return false;
            }
        });

        async ValueTask<bool> WaitForExitNetFrameworkAsync(CancellationToken cancellationToken)
        {
            var exitedSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnExited(object? sender, EventArgs e)
                => exitedSource.TrySetResult(true);

            process.Exited += OnExited;
            try
            {
                process.EnableRaisingEvents = true;

                if (process.HasExited)
                {
                    return true;
                }

                using (cancellationToken.Register(() => exitedSource.TrySetCanceled()))
                {
                    await exitedSource.Task;
                }

                return true;
            }
            catch (Exception e) when (e is InvalidOperationException or Win32Exception)
            {
                return false;
            }
            finally
            {
                process.Exited -= OnExited;
            }
        }
#endif
    }

    private void TerminateProcess(bool force)
    {
        try
        {
            if (!HasExited && !process.HasExited)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    TerminateWindowsProcess(force);
                }
                else
                {
#if NET
                    TerminateUnixProcess(force);
#else
                    throw new PlatformNotSupportedException();
#endif
                }
            }
        }
        catch (Exception e)
        {
            logger.Log(LogEvents.FailedToKillProcess, ProcessId, e.Message);
        }
    }

    private void TerminateWindowsProcess(bool force)
    {
        var signalName = force ? "Kill" : "Ctrl+C";
        logger.Log(LogEvents.TerminatingProcess, ProcessId, signalName);

        if (force)
        {
            try
            {
                process.Kill();
            }
            catch (Exception e)
            {
                logger.Log(LogEvents.FailedToSendSignalToProcess, signalName, ProcessId, e.Message);
            }
        }
        else
        {
            var error = ProcessUtilities.SendWindowsCtrlCEvent(ProcessId);
            if (error != null)
            {
                logger.Log(LogEvents.FailedToSendSignalToProcess, signalName, ProcessId, error);
            }
        }
    }

#if NET
    [UnsupportedOSPlatform("windows")]
    private void TerminateUnixProcess(bool force)
    {
        var signal = force ? PosixSignal.SIGKILL : PosixSignal.SIGTERM;
        var signalName = force ? "SIGKILL" : "SIGTERM";
        logger.Log(LogEvents.TerminatingProcess, ProcessId, signalName);

        string? error = null;
        try
        {
            process.SafeHandle.Signal(signal);
        }
        catch (Win32Exception ex)
        {
            // A process that has already exited is handled by Signal's non-exception return path.
            // This catch is for exceptional failures, such as attempting to signal a process
            // that we don't have permission to kill.
            error = ex.Message;
        }

        if (error != null)
        {
            logger.Log(LogEvents.FailedToSendSignalToProcess, signalName, ProcessId, error);
        }
    }
#endif
}
