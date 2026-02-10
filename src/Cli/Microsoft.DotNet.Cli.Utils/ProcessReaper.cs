// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Win32.SafeHandles;
#if !DOT_NET_BUILD_FROM_SOURCE
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

    private sealed class WindowsProcessReaper(Process process) : ProcessReaper(process)
    {
#if !DOTNET_BUILDSOURCEONLY
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
#endif
    }

    private sealed class UnixProcessReaper : ProcessReaper
    {
        private readonly Mutex _shutdownMutex;

        public UnixProcessReaper(Process process) : base(process)
        {
            _shutdownMutex = new Mutex();
            AppDomain.CurrentDomain.ProcessExit += HandleProcessExit;
        }

        public override void Dispose()
        {
            AppDomain.CurrentDomain.ProcessExit -= HandleProcessExit;

            // If there's been a shutdown via the process exit handler,
            // this will block the current thread so we don't race with the CLR shutdown
            // from the signal handler.
            _shutdownMutex.WaitOne();
            _shutdownMutex.ReleaseMutex();
            _shutdownMutex.Dispose();

            base.Dispose();
        }

        private void HandleProcessExit(object? sender, EventArgs args)
        {
            int processId;
            try
            {
                processId = _process.Id;
            }
            catch (InvalidOperationException)
            {
                // The process hasn't started yet; nothing to signal
                return;
            }

            // Take ownership of the shutdown mutex; this will ensure that the other
            // thread also waiting on the process to exit won't complete CLR shutdown before
            // this one does.
            _shutdownMutex?.WaitOne();

#if NET
            if (!_process.WaitForExit(0) && NativeMethods.Posix.kill(processId, NativeMethods.Posix.SIGTERM) != 0)
            {
                // Couldn't send the signal, don't wait
                return;
            }
#endif

            // If SIGTERM was ignored by the target, then we'll still wait
            _process.WaitForExit();

            Environment.ExitCode = _process.ExitCode;
        }
    }

    /// <inheritdoc cref="ProcessReaper(Process)"/>
    public static ProcessReaper Create(Process process)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsProcessReaper(process);
        }
        else
        {
            return new UnixProcessReaper(process);
        }
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

    private static void HandleCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        // Ignore SIGINT/SIGQUIT so that the process can handle the signal
        e.Cancel = true;
    }
}
