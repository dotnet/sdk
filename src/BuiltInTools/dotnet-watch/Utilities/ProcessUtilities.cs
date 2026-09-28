// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Watch;

internal static class ProcessUtilities
{
    public const int SIGKILL = 9;
    public const int SIGTERM = 15;

    /// <summary>
    /// Enables handling of Ctrl+C in a process where it was disabled.
    /// 
    /// If a process is launched with CREATE_NEW_PROCESS_GROUP flag
    /// it allows the parent process to send Ctrl+C event to the child process,
    /// but also disables Ctrl+C handlers.
    /// </summary>
    public static void EnableWindowsCtrlCHandling(Action<string> log)
    {
        Debug.Assert(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        // "If the HandlerRoutine parameter is NULL, a TRUE value causes the calling process to ignore CTRL+C input,
        // and a FALSE value restores normal processing of CTRL+C input.
        // This attribute of ignoring or processing CTRL+C is inherited by child processes."

        if (SetConsoleCtrlHandler(null, false))
        {
            log("Windows Ctrl+C handling enabled.");
        }
        else
        {
            log($"Failed to enable Ctrl+C handling: {GetLastPInvokeErrorMessage()}");
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetConsoleCtrlHandler(Delegate? handler, bool add);
    }
    
    public static string? SendWindowsCtrlCEvent(int processId)
    {
        const uint CTRL_C_EVENT = 0;

        // Doc:
        // "The process identifier of the new process is also the process group identifier of a new process group.
        //
        // The process group includes all processes that are descendants of the root process.
        // Only those processes in the group that share the same console as the calling process receive the signal.
        // In other words, if a process in the group creates a new console, that process does not receive the signal,
        // nor do its descendants.
        //
        // If this parameter is zero, the signal is generated in all processes that share the console of the calling process."
        return GenerateConsoleCtrlEvent(CTRL_C_EVENT, (uint)processId) ? null : GetLastPInvokeErrorMessage();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);
    }

    public static string? SendPosixSignal(int processId, int signal)
    {
        return sys_kill(processId, signal) == 0 ? null : GetLastPInvokeErrorMessage();

        [DllImport("libc", SetLastError = true, EntryPoint = "kill")]
        static extern int sys_kill(int pid, int sig);
    }

    private static string GetLastPInvokeErrorMessage()
    {
        var error = Marshal.GetLastPInvokeError();
#if NET10_0_OR_GREATER
        return $"{Marshal.GetPInvokeErrorMessage(error)} (code {error})";
#else
        return $"error code {error}";
#endif
    }
}
