// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>Opens owned regular files without following links. Callers retain the update locks during mutations.</summary>
internal static partial class SelfUpdateFile
{
    public static FileStream Open(string path, FileAccess access = FileAccess.Read)
    {
        path = Path.GetFullPath(path);
        var directoryPath = Path.GetDirectoryName(path)!;
        using var directory = PinDirectory(directoryPath);
        ValidateOwnedDirectory(directoryPath);
        SafeFileHandle handle;
        if (OperatingSystem.IsWindows())
        {
            handle = CreateFile(path, access == FileAccess.Read ? 0x80000000u : 0xc0000000u,
                5, 0, 3, 0x00200000, 0);
            CheckHandle(handle, path);
        }
        else
        {
            handle = OpenUnix(path, directory: false, access);
        }

        try
        {
            ValidateHandle(handle, path, directory: false, requireUnique: access == FileAccess.ReadWrite);
            var stream = new FileStream(handle, access);
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    ValidateWindowsSecurity(stream.GetAccessControl());
                }
                catch
                {
                    stream.Dispose();
                    throw;
                }
            }

            return stream;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    public static void ValidateOwnedDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            ValidateWindowsSecurity(new DirectoryInfo(path).GetAccessControl());
        }
        else
        {
            using var handle = OpenUnix(path, directory: true, FileAccess.Read);
            ValidateHandle(handle, path, directory: true);
        }
    }

    public static SelfUpdateDirectory PinDirectory(string path)
    {
        var handles = new List<SafeFileHandle>();
        var lease = new SelfUpdateDirectory(handles);
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var parents = new Stack<string>();
                for (var parent = new DirectoryInfo(path); parent is not null; parent = parent.Parent)
                {
                    parents.Push(parent.FullName);
                }

                foreach (var parent in parents)
                {
                    var handle = CreateFile(parent, 0x80000000, 3, 0, 3, 0x02200000, 0);
                    CheckHandle(handle, parent);
                    handles.Add(handle);
                    ValidateHandle(handle, parent, directory: true);
                }
            }
            else
            {
                var handle = OpenUnix(path, directory: true, FileAccess.Read);
                handles.Add(handle);
                ValidateHandle(handle, path, directory: true);
            }

            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    public static bool Exists(string path)
    {
        try
        {
            _ = File.GetAttributes(path);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    public static void RequireAbsent(string path)
    {
        if (Exists(path))
        {
            throw new IOException($"Self-update will not overwrite an occupied recovery path: '{path}'.");
        }
    }

    public static void MoveUnix(SelfUpdateDirectory directory, string source, string destination)
    {
        if (RenameAt(directory.Handle, Path.GetFileName(source), directory.Handle, Path.GetFileName(destination)) != 0)
        {
            ThrowNativeError(source);
        }
    }

    public static void CreateBackupUnix(SelfUpdateDirectory directory, string source, string backup)
    {
        if (LinkAt(directory.Handle, Path.GetFileName(source), directory.Handle, Path.GetFileName(backup), 0) != 0)
        {
            ThrowNativeError(source);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ValidateWindowsSecurity(FileSystemSecurity security)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var owner = (SecurityIdentifier)security.GetOwner(typeof(SecurityIdentifier))!;
        if (!IsTrusted(owner, identity.User!))
        {
            throw new UnauthorizedAccessException("Self-update requires an installation owned by the current user or a system administrator.");
        }

        const FileSystemRights writes = FileSystemRights.Write | FileSystemRights.Delete |
            FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            if (rule.AccessControlType == AccessControlType.Allow && (rule.FileSystemRights & writes) != 0 &&
                !IsTrusted((SecurityIdentifier)rule.IdentityReference, identity.User!))
            {
                throw new UnauthorizedAccessException("Self-update refuses an installation writable by another user.");
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool IsTrusted(SecurityIdentifier owner, SecurityIdentifier currentUser) =>
        owner == currentUser || owner.IsWellKnown(WellKnownSidType.LocalSystemSid) ||
        owner.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid) || owner.IsWellKnown(WellKnownSidType.CreatorOwnerSid);

    private static SafeFileHandle OpenUnix(string path, bool directory, FileAccess access)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("Self-update supports Windows, Linux, and macOS.");
        }

        var noFollow = OperatingSystem.IsLinux() ? 0x20000 : 0x100;
        var directoryFlag = OperatingSystem.IsLinux() ? 0x10000 : 0x100000;
        var closeOnExec = OperatingSystem.IsLinux() ? 0x80000 : 0x1000000;
        var nonBlocking = OperatingSystem.IsLinux() ? 0x800 : 0x4;
        var descriptor = OpenNative("/", directoryFlag | closeOnExec);
        if (descriptor < 0)
        {
            ThrowNativeError(path);
        }

        var parent = new SafeFileHandle(descriptor, ownsHandle: true);
        try
        {
            var components = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < components.Length; index++)
            {
                var isDirectory = directory || index < components.Length - 1;
                var flags = noFollow | closeOnExec | nonBlocking | (isDirectory ? directoryFlag : access == FileAccess.Read ? 0 : 2);
                descriptor = OpenAt(parent, components[index], flags);
                if (descriptor < 0)
                {
                    ThrowNativeError(path);
                }

                var next = new SafeFileHandle(descriptor, ownsHandle: true);
                parent.Dispose();
                parent = next;
            }

            return parent;
        }
        catch
        {
            parent.Dispose();
            throw;
        }
    }

    private static unsafe void ValidateHandle(SafeFileHandle handle, string path, bool directory, bool requireUnique = false)
    {
        Span<byte> information = stackalloc byte[256];
        fixed (byte* buffer = information)
        {
            if (OperatingSystem.IsWindows())
            {
                if (GetFileType(handle) != 1 || !GetFileInformation(handle, buffer))
                {
                    throw new IOException($"Self-update requires a disk file: '{path}'.");
                }

                var attributes = (FileAttributes)BinaryPrimitives.ReadUInt32LittleEndian(information);
                if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0 ||
                    ((attributes & FileAttributes.Directory) != 0) != directory)
                {
                    throw new IOException($"Self-update refuses a non-regular file: '{path}'.");
                }

                if (requireUnique && BinaryPrimitives.ReadUInt32LittleEndian(information[40..]) != 1)
                {
                    throw new IOException("The staged executable must not have other hard links.");
                }
            }
            else
            {
                var result = OperatingSystem.IsLinux() ? Statx(handle, "", 0x1000, 0x7ff, buffer) :
                    RuntimeInformation.ProcessArchitecture == Architecture.X64 ? FStatInode64(handle, buffer) : FStat(handle, buffer);
                if (result != 0)
                {
                    ThrowNativeError(path);
                }

                var mode = BinaryPrimitives.ReadUInt16LittleEndian(information[(OperatingSystem.IsLinux() ? 28 : 4)..]);
                var owner = BinaryPrimitives.ReadUInt32LittleEndian(information[(OperatingSystem.IsLinux() ? 20 : 16)..]);
                if ((mode & 0xf000) != (directory ? 0x4000 : 0x8000) || owner != GetEffectiveUserId() || (mode & 0x12) != 0)
                {
                    throw new IOException($"Self-update requires an owned, non-shared {(directory ? "directory" : "regular file")}: '{path}'.");
                }

                var links = OperatingSystem.IsLinux() ? BinaryPrimitives.ReadUInt32LittleEndian(information[16..]) :
                    BinaryPrimitives.ReadUInt16LittleEndian(information[6..]);
                if (requireUnique && links != 1)
                {
                    throw new IOException("The staged executable must not have other hard links.");
                }
            }
        }
    }

    private static void CheckHandle(SafeFileHandle handle, string path)
    {
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            ThrowNativeError(path, error);
        }
    }

    private static void ThrowNativeError(string path, int? error = null)
    {
        var code = error ?? Marshal.GetLastPInvokeError();
        if (code == 2 || (OperatingSystem.IsWindows() && code == 3))
        {
            throw new FileNotFoundException("Self-update artifact not found.", path);
        }

        throw new IOException($"Cannot securely open self-update artifact '{path}': {new Win32Exception(code).Message}.", new Win32Exception(code));
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial SafeFileHandle CreateFile(string path, uint access, uint share, nint security, uint creation, uint flags, nint template);

    [LibraryImport("kernel32.dll", EntryPoint = "GetFileInformationByHandle", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool GetFileInformation(SafeFileHandle handle, byte* information);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetFileType(SafeFileHandle handle);

    [LibraryImport("libc", EntryPoint = "open", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int OpenNative(string path, int flags);

    [LibraryImport("libc", EntryPoint = "openat", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int OpenAt(SafeFileHandle parent, string path, int flags);

    [LibraryImport("libc", EntryPoint = "statx", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static unsafe partial int Statx(SafeFileHandle handle, string path, int flags, uint mask, byte* information);

    [LibraryImport("libc", EntryPoint = "fstat", SetLastError = true)]
    private static unsafe partial int FStat(SafeFileHandle handle, byte* information);

    [LibraryImport("libc", EntryPoint = "fstat$INODE64", SetLastError = true)]
    private static unsafe partial int FStatInode64(SafeFileHandle handle, byte* information);

    [LibraryImport("libc", EntryPoint = "geteuid")]
    private static partial uint GetEffectiveUserId();

    [LibraryImport("libc", EntryPoint = "renameat", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int RenameAt(SafeFileHandle sourceDirectory, string source, SafeFileHandle destinationDirectory, string destination);

    [LibraryImport("libc", EntryPoint = "linkat", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int LinkAt(SafeFileHandle sourceDirectory, string source, SafeFileHandle destinationDirectory, string destination, int flags);
}