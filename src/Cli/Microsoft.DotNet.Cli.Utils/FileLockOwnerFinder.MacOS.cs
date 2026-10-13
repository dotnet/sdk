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
    private const int ProcPidListFileDescriptors = 1;
    private const int ProcPidFileDescriptorVnodePathInfo = 2;
    private const uint ProcFileDescriptorTypeVnode = 1;
    // proc_fileinfo.fi_openflags mirrors XNU fileglob flags; sys_flock sets FWASLOCKED after successful acquisition.
    private const uint FileWasLocked = 0x00004000;
    private const int MaxPathLength = 1024;
    private const int ProcessNameBufferSize = 1024;
    private const int ListGrowth = 16;

    /// <summary>
    /// Identifies a unique macOS process whose target descriptor has successfully used advisory locking.
    /// </summary>
    /// <remarks>
    /// XNU exposes a sticky <c>FWASLOCKED</c> bit after successful <c>flock</c> acquisition. Because it records
    /// "has or has had" a lock, multiple matching processes are considered ambiguous and retain the generic message.
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

        int processCapacity = checked((requiredBytes / sizeof(int)) + ListGrowth);
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

            FileLockOwner? owner = null;
            var inspectedProcessIds = new HashSet<int>();
            int processCount = Math.Min(bytesWritten / sizeof(int), processIds.Length);
            for (int index = 0; index < processCount; index++)
            {
                int processId = processIds[index];
                if (processId <= 0 || !inspectedProcessIds.Add(processId))
                {
                    continue;
                }

                bool? hasLockedTargetDescriptor = ProcessHasLockedTargetDescriptor(processId, targetPath);
                if (hasLockedTargetDescriptor is null)
                {
                    return null;
                }

                if (hasLockedTargetDescriptor is false)
                {
                    continue;
                }

                if (TryGetMacOSProcessName(processId) is not string processName)
                {
                    return null;
                }

                if (owner is not null)
                {
                    return null;
                }

                owner = new FileLockOwner(processId, processName);
            }

            return owner;
        }

        return null;
    }

    /// <summary>
    /// Confirms that a process has a descriptor for the target path that previously acquired an advisory lock.
    /// </summary>
    /// <remarks>
    /// The lock bit remains set after an explicit unlock, so this is used only with unique-process attribution.
    /// Descriptor and process lifetime races are treated as an unknown owner.
    /// </remarks>
    private static unsafe bool? ProcessHasLockedTargetDescriptor(int processId, string targetPath)
    {
        int requiredBytes = proc_pidinfo(
            processId,
            ProcPidListFileDescriptors,
            argument: 0,
            buffer: null,
            bufferSize: 0);

        if (requiredBytes <= 0)
        {
            return null;
        }

        int descriptorCapacity = checked((requiredBytes / sizeof(ProcFileDescriptorInfo)) + ListGrowth);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            ProcFileDescriptorInfo[] descriptors =
                GC.AllocateUninitializedArray<ProcFileDescriptorInfo>(descriptorCapacity);
            int bytesWritten;
            fixed (ProcFileDescriptorInfo* buffer = descriptors)
            {
                bytesWritten = proc_pidinfo(
                    processId,
                    ProcPidListFileDescriptors,
                    argument: 0,
                    buffer,
                    checked(descriptors.Length * sizeof(ProcFileDescriptorInfo)));
            }

            if (bytesWritten <= 0)
            {
                return null;
            }

            if (bytesWritten >= descriptors.Length * sizeof(ProcFileDescriptorInfo))
            {
                descriptorCapacity = checked(descriptorCapacity * 2);
                continue;
            }

            int descriptorCount = Math.Min(
                bytesWritten / sizeof(ProcFileDescriptorInfo),
                descriptors.Length);
            bool descriptorInspectionFailed = false;
            for (int index = 0; index < descriptorCount; index++)
            {
                ProcFileDescriptorInfo descriptor = descriptors[index];
                if (descriptor.Type != ProcFileDescriptorTypeVnode)
                {
                    continue;
                }

                VnodeFileDescriptorInfoWithPath descriptorInfo = default;
                int descriptorInfoBytes = proc_pidfdinfo(
                    processId,
                    descriptor.FileDescriptor,
                    ProcPidFileDescriptorVnodePathInfo,
                    &descriptorInfo,
                    sizeof(VnodeFileDescriptorInfoWithPath));

                if (descriptorInfoBytes < sizeof(VnodeFileDescriptorInfoWithPath))
                {
                    descriptorInspectionFailed = true;
                    continue;
                }

                if ((descriptorInfo.FileInfo.OpenFlags & FileWasLocked) == 0)
                {
                    continue;
                }

                byte* pathBuffer = descriptorInfo.VnodeInfo.Path;
                ReadOnlySpan<byte> pathBytes = new(pathBuffer, MaxPathLength);
                int nullTerminatorIndex = pathBytes.IndexOf((byte)0);
                if (nullTerminatorIndex >= 0)
                {
                    pathBytes = pathBytes[..nullTerminatorIndex];
                }

                if (string.Equals(
                    Encoding.UTF8.GetString(pathBytes),
                    targetPath,
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return descriptorInspectionFailed ? null : false;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcFileDescriptorInfo
    {
        internal int FileDescriptor;
        internal uint Type;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcFileInfo
    {
        internal uint OpenFlags;
        internal uint Status;
        internal long Offset;
        internal int Type;
        internal uint GuardFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct VnodeStat
    {
        internal uint Device;
        internal ushort Mode;
        internal ushort LinkCount;
        internal ulong Inode;
        internal uint UserId;
        internal uint GroupId;
        internal long AccessTimeSeconds;
        internal long AccessTimeNanoseconds;
        internal long ModificationTimeSeconds;
        internal long ModificationTimeNanoseconds;
        internal long ChangeTimeSeconds;
        internal long ChangeTimeNanoseconds;
        internal long BirthTimeSeconds;
        internal long BirthTimeNanoseconds;
        internal long Size;
        internal long Blocks;
        internal int BlockSize;
        internal uint Flags;
        internal uint Generation;
        internal uint DeviceType;
        internal long Spare0;
        internal long Spare1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct VnodeInfo
    {
        internal VnodeStat Stat;
        internal int Type;
        internal int Padding;
        internal int FileSystemId0;
        internal int FileSystemId1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct VnodeInfoWithPath
    {
        internal VnodeInfo Info;
        internal fixed byte Path[MaxPathLength];
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct VnodeFileDescriptorInfoWithPath
    {
        internal ProcFileInfo FileInfo;
        internal VnodeInfoWithPath VnodeInfo;
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
    /// Calls <c>libproc</c> to enumerate a process's file descriptors.
    /// </summary>
    [LibraryImport(LibProc, EntryPoint = "proc_pidinfo")]
    private static unsafe partial int proc_pidinfo(
        int processId,
        int flavor,
        ulong argument,
        void* buffer,
        int bufferSize);

    /// <summary>
    /// Calls <c>libproc</c> to inspect one process file descriptor.
    /// </summary>
    [LibraryImport(LibProc, EntryPoint = "proc_pidfdinfo")]
    private static unsafe partial int proc_pidfdinfo(
        int processId,
        int fileDescriptor,
        int flavor,
        void* buffer,
        int bufferSize);

    /// <summary>
    /// Calls <c>libproc</c> to retrieve a process name without constructing a managed <see cref="Process"/>.
    /// </summary>
    [LibraryImport(LibProc, EntryPoint = "proc_name")]
    private static unsafe partial int proc_name(int processId, byte* buffer, uint bufferSize);
}

#endif
