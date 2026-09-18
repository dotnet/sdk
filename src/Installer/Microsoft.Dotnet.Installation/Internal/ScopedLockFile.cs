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
        const int WindowsSharingViolation = unchecked((int)0x80070020);
        const int LinuxWouldBlock = 11;
        const int BsdWouldBlock = 35;

        return OperatingSystem.IsWindows() ? exception.HResult == WindowsSharingViolation
            : OperatingSystem.IsLinux() ? exception.HResult == LinuxWouldBlock
            : (OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD()) && exception.HResult == BsdWouldBlock;
    }
}