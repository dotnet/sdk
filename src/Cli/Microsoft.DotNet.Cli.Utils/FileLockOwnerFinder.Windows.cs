// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace Microsoft.DotNet.Cli.Utils;

internal static partial class FileLockOwnerFinder
{
    private const int RestartManagerSessionKeyLength = 33;
    private const int RestartManagerMaxRetries = 6;
    private const int ErrorMoreData = 234;

    /// <summary>
    /// Uses Windows Restart Manager to identify the process holding the contended file open.
    /// </summary>
    /// <remarks>
    /// Restart Manager can resize its process list between calls, so retrieval is retried. Session setup, access,
    /// and process lifetime failures return an unknown owner.
    /// </remarks>
    private static FileLockOwner? TryGetWindowsFileLockOwner(string filePath)
    {
        var sessionKey = new StringBuilder(RestartManagerSessionKeyLength);
        int result = RmStartSession(out uint sessionHandle, 0, sessionKey);
        if (result != 0)
        {
            return null;
        }

        try
        {
            result = RmRegisterResources(
                sessionHandle,
                1,
                [filePath],
                0,
                null,
                0,
                null);
            if (result != 0)
            {
                return null;
            }

            uint processInfoCount = 0;
            RestartManagerProcessInfo[]? processInfos = null;

            for (int retry = 0; retry < RestartManagerMaxRetries; retry++)
            {
                uint processInfoNeeded;
                result = RmGetList(
                    sessionHandle,
                    out processInfoNeeded,
                    ref processInfoCount,
                    processInfos,
                    out _);

                if (result == 0)
                {
                    if (processInfoCount == 0)
                    {
                        return null;
                    }

                    for (int index = 0; index < processInfoCount; index++)
                    {
                        int processId = (int)processInfos![index].Process.ProcessId;
                        string processName = SanitizeProcessName(processInfos[index].ApplicationName);
                        if (processId > 0 && !string.IsNullOrWhiteSpace(processName))
                        {
                            return new FileLockOwner(processId, processName);
                        }
                    }

                    return null;
                }

                if (result != ErrorMoreData)
                {
                    return null;
                }

                processInfoCount = processInfoNeeded;
                processInfos = new RestartManagerProcessInfo[processInfoCount];
            }

            return null;
        }
        finally
        {
            _ = RmEndSession(sessionHandle);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RestartManagerUniqueProcess
    {
        public uint ProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RestartManagerProcessInfo
    {
        public RestartManagerUniqueProcess Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string ApplicationName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string ServiceShortName;
        public uint ApplicationType;
        public uint ApplicationStatus;
        public uint TerminalServicesSessionId;
        [MarshalAs(UnmanagedType.Bool)]
        public bool Restartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int RmStartSession(out uint sessionHandle, uint sessionFlags, StringBuilder sessionKey);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int RmRegisterResources(
        uint sessionHandle,
        uint fileCount,
        string[] fileNames,
        uint applicationCount,
        RestartManagerUniqueProcess[]? applications,
        uint serviceCount,
        string[]? serviceNames);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int RmGetList(
        uint sessionHandle,
        out uint processInfoNeeded,
        ref uint processInfoCount,
        [In, Out] RestartManagerProcessInfo[]? affectedProcesses,
        out uint rebootReasons);

    [DllImport("rstrtmgr.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int RmEndSession(uint sessionHandle);
}
