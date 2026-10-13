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

    /// <summary>
    /// Uses macOS process APIs to identify a process with the contended lock file open.
    /// </summary>
    /// <remarks>
    /// <c>proc_listpidspath</c> avoids reproducing private descriptor structure layouts. Event-only descriptors are
    /// excluded, and the process list is retried when it grows between the sizing and retrieval calls.
    /// </remarks>
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

    /// <summary>
    /// Gets the short macOS process name used in the contention message.
    /// </summary>
    /// <remarks><c>proc_name</c> returns a UTF-8, null-terminated name that may be shorter than the supplied buffer.</remarks>
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

    /// <summary>
    /// Calls <c>libproc</c> to list processes that hold an open reference to a path.
    /// </summary>
    /// <remarks>A null buffer requests the minimum buffer size in bytes.</remarks>
    [LibraryImport(LibProc, EntryPoint = "proc_listpidspath", StringMarshalling = StringMarshalling.Utf8)]
    private static unsafe partial int proc_listpidspath(
        uint type,
        uint typeInfo,
        string path,
        uint pathFlags,
        void* buffer,
        int bufferSize);

    /// <summary>
    /// Calls <c>libproc</c> to retrieve a process name without constructing a managed <see cref="Process"/>.
    /// </summary>
    [LibraryImport(LibProc, EntryPoint = "proc_name")]
    private static unsafe partial int proc_name(int processId, byte* buffer, uint bufferSize);
}

#endif
