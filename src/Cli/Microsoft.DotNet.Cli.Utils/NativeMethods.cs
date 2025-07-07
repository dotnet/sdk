// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Win32.SafeHandles;
#if NET
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
#endif

namespace Microsoft.DotNet.Cli.Utils;

public static partial class NativeMethods
{
    public unsafe static partial class Windows
    {
        internal enum JobObjectInfoClass : uint
        {
            JobObjectExtendedLimitInformation = 9,
        }

        [Flags]
        internal enum JobObjectLimitFlags : uint
        {
            JobObjectLimitKillOnJobClose = 0x2000,
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct JobObjectBasicLimitInformation
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public JobObjectLimitFlags LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct JobObjectExtendedLimitInformation
        {
            public JobObjectBasicLimitInformation BasicLimitInformation;
            public IoCounters IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        internal const int ProcessBasicInformation = 0;

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_BASIC_INFORMATION
        {
            public uint ExitStatus;
            public IntPtr PebBaseAddress;
            public UIntPtr AffinityMask;
            public int BasePriority;
            public UIntPtr UniqueProcessId;
            public UIntPtr InheritedFromUniqueProcessId;
        }
#if NET
        [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
#else
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
#endif
        internal static
#if NET
        partial
#else
        extern
#endif
        SafeWaitHandle CreateJobObjectW(IntPtr lpJobAttributes, string? lpName);

#if NET
        [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
#else
        [DllImport("kernel32.dll", SetLastError = true)]
#endif
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static
#if NET
        partial
#else
        extern
#endif
        bool SetInformationJobObject(IntPtr hJob, JobObjectInfoClass jobObjectInformationClass, IntPtr lpJobObjectInformation, uint cbJobObjectInformationLength);

#if NET
        [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
#else
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
#endif
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static
#if NET
        partial
#else
        extern
#endif

        bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

#if NET
        [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
#else
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
#endif
        internal static
#if NET
        partial
#else
        extern
#endif
        IntPtr GetCommandLine();

#if NET
        [LibraryImport("ntdll.dll", SetLastError = true)]
#else
        [DllImport("ntdll.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
#endif
        internal static
#if NET
        partial
#else
        extern
#endif
        uint NtQueryInformationProcess(SafeProcessHandle ProcessHandle, int ProcessInformationClass, void* ProcessInformation, uint ProcessInformationLength, out uint ReturnLength);
    }

    internal static partial class Posix
    {
#if NET
        [LibraryImport("libc", SetLastError = true)]
#else
        [DllImport("libc", SetLastError = true)]
#endif
        internal static
#if NET
        partial
#else
        extern
#endif
        int kill(int pid, int sig);

        internal const int SIGINT = 2;
        internal const int SIGTERM = 15;
    }
}
