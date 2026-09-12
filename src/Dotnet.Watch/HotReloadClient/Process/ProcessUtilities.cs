// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Runtime.InteropServices;

namespace Microsoft.DotNet.HotReload;

internal static class ProcessUtilities
{
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

    private static string GetLastPInvokeErrorMessage()
    {
#if NET
        var error = Marshal.GetLastPInvokeError();
        return $"{Marshal.GetPInvokeErrorMessage(error)} (code {error})";
#else
        var error = Marshal.GetLastWin32Error();
        return $"{new System.ComponentModel.Win32Exception(error).Message} (code {error})";
#endif
    }
}
