// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch;

internal static class ProcessUtilities
{
    public static string? SendWindowsCtrlBreakEvent(int processId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);
        const uint CTRL_BREAK_EVENT = 1;

        // CTRL_C_EVENT succeeds without delivering a signal when a process group is specified.
        // https://learn.microsoft.com/windows/console/generateconsolectrlevent
        return GenerateConsoleCtrlEvent(CTRL_BREAK_EVENT, (uint)processId) ? null : GetLastPInvokeErrorMessage();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);
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
