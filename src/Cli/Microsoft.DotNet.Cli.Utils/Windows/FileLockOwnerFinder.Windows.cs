// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if TARGET_WINDOWS

using Windows.Win32.System.RestartManager;

namespace Microsoft.DotNet.Cli.Utils;

internal static partial class FileLockOwnerFinder
{
    private const int RestartManagerSessionKeyLength = 33;
    private const int RestartManagerMaxRetries = 6;

    private static unsafe FileLockOwner? TryGetWindowsFileLockOwner(string filePath)
    {
        Span<char> sessionKey = stackalloc char[RestartManagerSessionKeyLength];
        WIN32_ERROR result = PInvoke.RmStartSession(out uint sessionHandle, sessionKey);
        if (result != 0)
        {
            return null;
        }

        try
        {
            result = PInvoke.RmRegisterResources(
                sessionHandle,
                [filePath],
                default(ReadOnlySpan<RM_UNIQUE_PROCESS>),
                default(ReadOnlySpan<string>));
            if (result != 0)
            {
                return null;
            }

            uint processInfoCount = 0;
            RM_PROCESS_INFO[]? processInfos = null;

            for (int retry = 0; retry < RestartManagerMaxRetries; retry++)
            {
                uint processInfoNeeded;
                uint rebootReasons;
                fixed (RM_PROCESS_INFO* affectedProcesses = processInfos)
                {
                    result = PInvoke.RmGetList(
                        sessionHandle,
                        &processInfoNeeded,
                        &processInfoCount,
                        affectedProcesses,
                        &rebootReasons);
                }

                if (result == 0)
                {
                    if (processInfoCount == 0)
                    {
                        return null;
                    }

                    for (int index = 0; index < processInfoCount; index++)
                    {
                        int processId = (int)processInfos![index].Process.dwProcessId;
                        string processName = SanitizeProcessName(processInfos[index].strAppName.ToString());
                        if (processId > 0 && !string.IsNullOrWhiteSpace(processName))
                        {
                            return new FileLockOwner(processId, processName);
                        }
                    }

                    return null;
                }

                if (result != WIN32_ERROR.ERROR_MORE_DATA)
                {
                    return null;
                }

                processInfoCount = processInfoNeeded;
                processInfos = new RM_PROCESS_INFO[processInfoCount];
            }

            return null;
        }
        finally
        {
            _ = PInvoke.RmEndSession(sessionHandle);
        }
    }
}

#endif
