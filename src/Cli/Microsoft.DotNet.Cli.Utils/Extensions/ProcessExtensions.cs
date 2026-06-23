// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System.Runtime.Versioning;
#endif

using Microsoft.Win32.SafeHandles;
using Windows.Win32.System.Threading;
using Windows.Wdk.System.Threading;

namespace Microsoft.DotNet.Cli.Utils.Extensions;

/// <summary>
/// Extensions methods for <see cref="Process"/> components.
/// </summary>
#if NET
[SupportedOSPlatform("windows")]
#endif
public static class ProcessExtensions
{
#pragma warning disable CA1416
    /// <summary>
    /// Returns the parent process of this process by querying the Win32_Process class.
    /// </summary>
    /// <param name="process">The process component.</param>
    /// <returns>The parent process or <see langword="null"/> if the parent process cannot be found.</returns>
    public static Process? GetParentProcess(this Process process)
    {
        int ppid = process.GetParentProcessId();

        return ppid != -1 ? Process.GetProcessById(ppid) : null;
    }

    /// <summary>
    /// Returns the parent process ID of this process by querying the Win32_Process class.
    /// </summary>
    /// <param name="process">The process component.</param>
    /// <returns>The process ID of the parent process, or -1 if the parent process could not be found.</returns>
    public static unsafe int GetParentProcessId(this Process process)
    {
        SafeProcessHandle handle = process.SafeHandle;
        PROCESS_BASIC_INFORMATION info;
        uint returnLength = 0;

        if (WDK.PInvoke.NtQueryInformationProcess(
            (HANDLE)handle.DangerousGetHandle(),
            PROCESSINFOCLASS.ProcessBasicInformation,
            &info,
            (uint)sizeof(PROCESS_BASIC_INFORMATION),
            ref returnLength) != 0)
        {
            return -1;
        }

        return (int)info.InheritedFromUniqueProcessId;
    }
#pragma warning restore CA1416
}
