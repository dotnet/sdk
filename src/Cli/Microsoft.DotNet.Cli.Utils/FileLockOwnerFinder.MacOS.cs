// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

using System.Text;

namespace Microsoft.DotNet.Cli.Utils;

internal static partial class FileLockOwnerFinder
{
    private const string LibProc = "/usr/lib/libproc.dylib";
    private const uint ProcAllPids = 1;
    private const uint ProcListPidsPathExcludeEventOnly = 2;
    private const int ProcessNameBufferSize = 1024;
    private const int ProcessListGrowth = 16;

    private static unsafe FileLockOwner? TryGetMacOSFileLockOwner(string filePath)
    {
        string targetPath = Path.GetFullPath(filePath);
        int requiredBytes = proc_listpidspath(
            ProcAllPids,
            typeInfo: 0,
            targetPath,
            ProcListPidsPathExcludeEventOnly,
            buffer: null,
            bufferSize: 0);

        if (requiredBytes <= 0)
        {
            return null;
        }

        int processCapacity = checked((requiredBytes / sizeof(int)) + ProcessListGrowth);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            int[] processIds = GC.AllocateUninitializedArray<int>(processCapacity);
            int bytesWritten;
            fixed (int* buffer = processIds)
            {
                bytesWritten = proc_listpidspath(
                    ProcAllPids,
                    typeInfo: 0,
                    targetPath,
                    ProcListPidsPathExcludeEventOnly,
                    buffer,
                    checked(processIds.Length * sizeof(int)));
            }

            if (bytesWritten <= 0)
            {
                return null;
            }

            if (bytesWritten >= processIds.Length * sizeof(int))
            {
                processCapacity = checked(processCapacity * 2);
                continue;
            }

            int processCount = Math.Min(bytesWritten / sizeof(int), processIds.Length);
            for (int index = 0; index < processCount; index++)
            {
                int processId = processIds[index];
                if (processId > 0 &&
                    TryGetMacOSProcessName(processId) is string processName)
                {
                    return new FileLockOwner(processId, processName);
                }
            }

            return null;
        }

        return null;
    }

    private static unsafe string? TryGetMacOSProcessName(int processId)
    {
        Span<byte> processNameBuffer = stackalloc byte[ProcessNameBufferSize];
        int bytesWritten;
        fixed (byte* buffer = processNameBuffer)
        {
            bytesWritten = proc_name(processId, buffer, ProcessNameBufferSize);
        }

        if (bytesWritten <= 0)
        {
            return null;
        }

        ReadOnlySpan<byte> processNameBytes = processNameBuffer[..Math.Min(bytesWritten, processNameBuffer.Length)];
        int nullTerminatorIndex = processNameBytes.IndexOf((byte)0);
        if (nullTerminatorIndex >= 0)
        {
            processNameBytes = processNameBytes[..nullTerminatorIndex];
        }

        string processName = SanitizeProcessName(Encoding.UTF8.GetString(processNameBytes));
        return string.IsNullOrWhiteSpace(processName) ? null : processName;
    }

    [LibraryImport(LibProc, EntryPoint = "proc_listpidspath", StringMarshalling = StringMarshalling.Utf8)]
    private static unsafe partial int proc_listpidspath(
        uint type,
        uint typeInfo,
        string path,
        uint pathFlags,
        void* buffer,
        int bufferSize);

    [LibraryImport(LibProc, EntryPoint = "proc_name")]
    private static unsafe partial int proc_name(int processId, byte* buffer, uint bufferSize);
}

#endif
