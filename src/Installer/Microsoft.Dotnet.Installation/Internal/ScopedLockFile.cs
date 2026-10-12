// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Owns one runtime file-sharing lock on a permanent file in a caller-owned directory.
/// Disposing the lease closes its handle without deleting or modifying the file.
/// Cooperating callers must use compatible runtime sharing and keep the path stable.
/// </summary>
internal sealed class ScopedLockFile : IDisposable
{
    private readonly FileStream _stream;

    private ScopedLockFile(FileStream stream)
    {
        _stream = stream;
    }

    public static ScopedLockFile? TryAcquireShared(string path)
        => TryAcquire(path, FileAccess.Read, FileShare.Read);

    public static ScopedLockFile? TryAcquireExclusive(string path)
        => TryAcquire(path, FileAccess.ReadWrite, FileShare.None);

    public void Dispose() => _stream.Dispose();

    private static ScopedLockFile? TryAcquire(string path, FileAccess access, FileShare share)
    {
        try
        {
            return new ScopedLockFile(new FileStream(path, FileMode.OpenOrCreate, access, share));
        }
        catch (IOException exception) when (IsContention(exception))
        {
            return null;
        }
    }

    private static bool IsContention(IOException exception)
    {
        // .NET reports Windows ERROR_SHARING_VIOLATION as HRESULT 0x80070020 and reports
        // Unix EWOULDBLOCK using the native errno as IOException.HResult:
        // https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/System/HResults.cs
        // https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/Interop/Unix/Interop.IOErrors.cs
        // Linux defines EAGAIN as 11:
        // https://github.com/torvalds/linux/blob/master/include/uapi/asm-generic/errno-base.h
        // Darwin and FreeBSD define EAGAIN as 35:
        // https://github.com/apple-oss-distributions/xnu/blob/main/bsd/sys/errno.h
        // https://github.com/freebsd/freebsd-src/blob/main/sys/sys/errno.h
        const int WindowsSharingViolation = unchecked((int)0x80070020);
        const int LinuxWouldBlock = 11;
        const int BsdWouldBlock = 35;

        return OperatingSystem.IsWindows() ? exception.HResult == WindowsSharingViolation
            : OperatingSystem.IsLinux() ? exception.HResult == LinuxWouldBlock
            : (OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD()) && exception.HResult == BsdWouldBlock;
    }
}