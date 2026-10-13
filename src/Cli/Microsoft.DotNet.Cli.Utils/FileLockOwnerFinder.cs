// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils;

internal sealed record FileLockOwner(int ProcessId, string ProcessName);

internal static partial class FileLockOwnerFinder
{
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

    private static string SanitizeProcessName(string processName) =>
        string.Concat(processName.Take(256).Select(character => char.IsControl(character) ? '?' : character));
}
