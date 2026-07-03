// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Win32.SafeHandles;
#if !TARGET_WINDOWS && NET
using System.ComponentModel;
#endif
#if TARGET_WINDOWS
using Windows.Win32.System.JobObjects;
#endif

namespace Microsoft.DotNet.Cli.Utils;

/// <summary>
///  Responsible for reaping a target process if the current process terminates.
/// </summary>
/// <remarks>
///  <para>
///   On Windows, a job object will be used to ensure the termination of the target
///   process (and its tree) even if the current process is rudely terminated.
///  </para>
///  <para>
///   On POSIX systems, the reaper will handle SIGTERM and attempt to forward the
///   signal to the target process only.
///  </para>
///  <para>
///   The reaper also suppresses SIGINT in the current process to allow the target
///   process to handle the signal.
///  </para>
/// </remarks>
internal class ProcessReaper : IDisposable
{
    private readonly Process _process;

#if TARGET_WINDOWS
    private sealed class WindowsProcessReaper : ProcessReaper
    {
        public WindowsProcessReaper(Process process) : base(process)
        {
            // Ensure Ctrl+C handling is enabled in this process.
            //
            // When a parent process (e.g. dotnet-watch) launches us with CREATE_NEW_PROCESS_GROUP,
            // Ctrl+C handlers are disabled in the new process group. We re-enable them so that
            // HandleCancelKeyPress fires when the parent sends CTRL_C_EVENT.
            // This is safe to call unconditionally — it's a no-op if Ctrl+C is already enabled.
            //
            // See https://learn.microsoft.com/windows/console/setconsolectrlhandler
            EnableWindowsCtrlCHandling();
        }

        private unsafe static void EnableWindowsCtrlCHandling()
        {
            PInvoke.SetConsoleCtrlHandler(null, false);
        }

        private SafeWaitHandle? _job;

        public override void NotifyProcessStarted()
        {
            // Limit the use of job objects to versions of Windows that support nested jobs (i.e. Windows 8/2012 or later).
            // Ideally, we would check for some new API export or OS feature instead of the OS version,
            // but nested jobs are transparently implemented with respect to the Job Objects API.
            // Note: Windows 8.1 and later may report as Windows 8 (see https://docs.microsoft.com/windows/desktop/sysinfo/operating-system-version).
            //       However, for the purpose of this check that is still sufficient.
            if (Environment.OSVersion.Version.Major > 6 ||
                (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor >= 2))
            {
                _job = AssignProcessToJobObject((HANDLE)_process.Handle);
            }
        }

        private static SafeWaitHandle? AssignProcessToJobObject(HANDLE process)
        {
            HANDLE job = PInvoke.CreateJobObject(null, null);
            if (job.IsNull)
            {
                return null;
            }

            if (!SetKillOnJobClose(job, true))
            {
                PInvoke.CloseHandle(job);
                return null;
            }

            if (!PInvoke.AssignProcessToJobObject(job, process))
            {
                PInvoke.CloseHandle(job);
                return null;
            }

            return new(job, ownsHandle: true);
        }

        private static unsafe bool SetKillOnJobClose(HANDLE job, bool value)
        {
            JOBOBJECT_EXTENDED_LIMIT_INFORMATION information = new();
            if (value)
            {
                information.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
            }

            return PInvoke.SetInformationJobObject(
                job,
                JOBOBJECTINFOCLASS.JobObjectExtendedLimitInformation,
                &information,
                (uint)sizeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
        }

        public override void Dispose()
        {
            if (_job is null)
            {
                base.Dispose();
                return;
            }

            // Clear the kill on close flag because the child process terminated successfully
            // If this fails, then we have no choice but to terminate any remaining processes in the job
            SetKillOnJobClose((HANDLE)_job.DangerousGetHandle(), false);

            _job.Dispose();
            _job = null;

            base.Dispose();
        }
    }
#endif

    // The Unix reaper is only relevant to a Unix runtime, which is always a modern .NET
    // (NET) target. .NET Framework (net472) runs only on Windows and must not compile this
    // code. TARGET_WINDOWS reflects the build's TargetOS, not the runtime, and is not
    // defined when net472 is cross-compiled on a non-Windows build - hence "&& NET" here
    // rather than relying on TARGET_WINDOWS alone.
#if !TARGET_WINDOWS && NET
    private sealed class UnixProcessReaper : ProcessReaper
    {
        // Coordinates Dispose (typically the main thread) with HandleProcessExit,
        // which is raised on the AppDomain.ProcessExit thread when the CLI itself is
        // shutting down (for example when a SIGTERM handler calls Environment.Exit).
        //
        // A System.Threading.Lock is used here rather than a Mutex on purpose. The prior
        // Mutex based design disposed the mutex in Dispose while a concurrent ProcessExit
        // callback could still call WaitOne on it, throwing "Cannot access a disposed
        // object" during shutdown (dotnet/sdk#55096). A managed lock has no disposable
        // wait handle to race on, and the _shuttingDown flag closes the window so a
        // ProcessExit callback that starts after Dispose has run simply becomes a no-op.
        private readonly Lock _shutdownLock = new();
        private bool _shuttingDown;

        public UnixProcessReaper(Process process) : base(process)
        {
            AppDomain.CurrentDomain.ProcessExit += HandleProcessExit;
        }

        public override void Dispose()
        {
            AppDomain.CurrentDomain.ProcessExit -= HandleProcessExit;

            // If a ProcessExit driven shutdown is already running on another thread,
            // block here until it finishes so we don't race with the CLR shutdown from
            // the signal handler. Setting _shuttingDown also makes any ProcessExit
            // callback that starts after this point a no-op.
            lock (_shutdownLock)
            {
                _shuttingDown = true;
            }

            base.Dispose();
        }

        private void HandleProcessExit(object? sender, EventArgs args)
        {
            lock (_shutdownLock)
            {
                if (_shuttingDown)
                {
                    // Dispose has already run (or is running on another thread); there
                    // is nothing to do and the process state may already be torn down.
                    return;
                }

                _shuttingDown = true;

                try
                {
                    // If the target is still running, forward SIGTERM so it can shut
                    // down as well. Signal returns false if the process already exited,
                    // and throws Win32Exception if the signal could not be delivered.
                    if (!_process.WaitForExit(0))
                    {
                        _process.SafeHandle.Signal(PosixSignal.SIGTERM);
                    }

                    // If SIGTERM was ignored by the target, then we'll still wait.
                    _process.WaitForExit();

                    Environment.ExitCode = _process.ExitCode;
                }
                catch (InvalidOperationException)
                {
                    // The process hasn't started yet, or no exit code is available;
                    // nothing to signal or wait for.
                }
                catch (Win32Exception)
                {
                    // The signal could not be delivered (for example, insufficient
                    // permissions). Don't wait on a process we couldn't signal.
                }
            }
        }
    }
#endif

    /// <inheritdoc cref="ProcessReaper(Process)"/>
    public static ProcessReaper Create(Process process)
    {
#if TARGET_WINDOWS
        return new WindowsProcessReaper(process);
#elif NET
        return new UnixProcessReaper(process);
#else
        // .NET Framework only runs on Windows, so the Unix reaper is never relevant and
        // the CsWin32 based Windows reaper is only compiled for Windows TargetOS builds.
        // This branch is reached only when the net472 target is cross-compiled on a
        // non-Windows build (not a shipping configuration); fall back to the base reaper,
        // which still suppresses Ctrl+C so the target process can handle it.
        return new ProcessReaper(process);
#endif
    }

    /// <summary>
    ///  Creates a new process reaper.
    /// </summary>
    /// <param name="process">
    ///  The target process to reap if the current process terminates. The process should not yet be started.
    /// </param>
    private ProcessReaper(Process process)
    {
        _process = process;

        // The tests need the event handlers registered prior to spawning the child to prevent a race
        // where the child writes output the test expects before the intermediate dotnet process
        // has registered the event handlers to handle the signals the tests will generate.
        Console.CancelKeyPress += HandleCancelKeyPress;
    }

    /// <summary>
    ///  Call to notify the reaper that the process has started.
    /// </summary>
    public virtual void NotifyProcessStarted() { }

    public virtual void Dispose()
    {
        Console.CancelKeyPress -= HandleCancelKeyPress;
    }

    private void HandleCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        // Ignore SIGINT/SIGQUIT so that the process can handle the signal
        e.Cancel = true;

        // For WinExe apps (WinForms, WPF, MAUI) that don't respond to Ctrl+C,
        // CloseMainWindow() posts WM_CLOSE to gracefully shut them down.
        // For console apps this is a no-op (returns false) since they have no main window.
        try
        {
            _process.CloseMainWindow();
        }
        catch (InvalidOperationException)
        {
            // The process hasn't started yet or has already exited; nothing to signal
        }
    }
}
