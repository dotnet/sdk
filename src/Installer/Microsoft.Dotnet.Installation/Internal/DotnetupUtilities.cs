// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.Dotnet.Installation.Internal;

internal static class DotnetupUtilities
{
    public static string ExeSuffix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";

    public static string GetDotnetExeName()
    {
        return "dotnet" + ExeSuffix;
    }

    public static bool PathsEqual(string? a, string? b)
    {
        if (a == null && b == null)
        {
            return true;
        }
        else if (a == null || b == null)
        {
            return false;
        }

        return string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                                Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                                StringComparison.OrdinalIgnoreCase);
    }

    public static void ForceReplaceFile(string sourcePath, string destPath)
    {
        File.Copy(sourcePath, destPath, overwrite: true);

        // Copy file attributes
        var srcInfo = new FileInfo(sourcePath);
        _ = new FileInfo(destPath)
        {
            CreationTimeUtc = srcInfo.CreationTimeUtc,
            LastWriteTimeUtc = srcInfo.LastWriteTimeUtc,
            LastAccessTimeUtc = srcInfo.LastAccessTimeUtc,
            Attributes = srcInfo.Attributes
        };
    }

    public static string GetRuntimeIdentifier(InstallArchitecture architecture)
    {
        var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" :
                 RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" :
                 RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" : "unknown";

        var arch = architecture switch
        {
            InstallArchitecture.x64 => "x64",
            InstallArchitecture.x86 => "x86",
            InstallArchitecture.arm64 => "arm64",
            _ => "x64" // Default fallback
        };

        return $"{os}-{arch}";
    }

    public static string GetArchiveFileExtensionForPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ".zip"; // Windows typically uses zip archives
        }
        else
        {
            return ".tar.gz"; // Unix-like systems use tar.gz
        }
    }

    /// <summary>
    /// Attempts to find running dotnet processes and returns a formatted string with their PIDs.
    /// Returns an empty string if no processes are found or an error occurs.
    /// </summary>
    public static string GetDotnetProcessPidInfo()
    {
        try
        {
            var processes = Process.GetProcessesByName("dotnet");
            if (processes.Length == 0)
            {
                return string.Empty;
            }

            var pids = new int[processes.Length];
            for (int i = 0; i < processes.Length; i++)
            {
                pids[i] = processes[i].Id;
                processes[i].Dispose();
            }

            return $" (dotnet process PIDs: {string.Join(", ", pids)})";
        }
        catch
        {
            return string.Empty;
        }
    }
}
