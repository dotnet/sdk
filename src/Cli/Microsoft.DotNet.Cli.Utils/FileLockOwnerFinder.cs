// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils;

/// <summary>
/// Identifies a process that is reported as holding a file lock.
/// </summary>
internal sealed record FileLockOwner(int ProcessId, string ProcessName);

internal static partial class FileLockOwnerFinder
{
    /// <summary>
    /// Finds a file lock owner so contention diagnostics can identify the process the user is waiting for.
    /// </summary>
    /// <remarks>
    /// Discovery is best-effort and platform-specific. Unsupported platforms, access restrictions, and process
    /// lifetime races return <see langword="null"/> rather than affecting the operation that encountered contention.
    /// </remarks>
    internal static FileLockOwner? TryGetFileLockOwner(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        try
        {
#if TARGET_WINDOWS
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return TryGetWindowsFileLockOwner(filePath);
            }
#endif
#if NET
            if (OperatingSystem.IsLinux())
            {
                return TryGetLinuxFileLockOwner(filePath);
            }

            if (OperatingSystem.IsMacOS())
            {
                return TryGetMacOSFileLockOwner(filePath);
            }
#endif
        }
        catch (Exception)
        {
            // Lock owner discovery is diagnostic only and must not affect the operation.
        }

        return null;
    }

    /// <summary>
    /// Bounds and sanitizes an OS-provided process name before including it in terminal output.
    /// </summary>
    /// <remarks>Control characters are replaced and names are limited to 256 characters.</remarks>
    private static string SanitizeProcessName(string processName) =>
        string.Concat(processName.Take(256).Select(character => char.IsControl(character) ? '?' : character));
}
