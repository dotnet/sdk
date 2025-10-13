// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Microsoft.DotNet.Cli;

/// <summary>
/// Detects mixed installation scenarios where the dotnet muxer on PATH
/// is from a global install but DOTNET_ROOT points to a different location.
/// </summary>
internal static class MixedInstallationDetector
{
    /// <summary>
    /// Gets the global installation root path for the current platform.
    /// Based on https://github.com/dotnet/designs/blob/main/accepted/2020/install-locations.md
    /// and https://github.com/dotnet/designs/blob/main/accepted/2021/install-location-per-architecture.md
    /// </summary>
    private static string? GetGlobalInstallRoot()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows: Read from registry HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\<arch>\InstallLocation
            // Use 32-bit registry view as specified in the spec
            try
            {
                string arch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                using (var key = hklm.OpenSubKey($@"SOFTWARE\dotnet\Setup\InstalledVersions\{arch}"))
                {
                    if (key != null)
                    {
                        var installLocation = key.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrEmpty(installLocation))
                        {
                            return installLocation;
                        }
                    }
                }
            }
            catch
            {
                // If registry reading fails, return null
            }
        }
        else
        {
            // Linux/macOS: Read from /etc/dotnet/install_location or /etc/dotnet/install_location_<arch>
            try
            {
                string arch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
                string archSpecificPath = $"/etc/dotnet/install_location_{arch}";
                string defaultPath = "/etc/dotnet/install_location";

                // Try arch-specific location first
                if (File.Exists(archSpecificPath))
                {
                    string location = File.ReadAllText(archSpecificPath).Trim();
                    if (!string.IsNullOrEmpty(location))
                    {
                        return location;
                    }
                }

                // Fall back to default location
                if (File.Exists(defaultPath))
                {
                    string location = File.ReadAllText(defaultPath).Trim();
                    if (!string.IsNullOrEmpty(location))
                    {
                        return location;
                    }
                }
            }
            catch
            {
                // If file reading fails, return null
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves a path to its full path and follows symlinks to the target.
    /// </summary>
    private static string ResolvePathAndLinks(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        // First expand to full path
        string fullPath = Path.GetFullPath(path);

        try
        {
            // Try to resolve symlinks
            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Exists && fileInfo.LinkTarget != null)
            {
                // Follow the symlink
                fullPath = Path.GetFullPath(fileInfo.LinkTarget, Path.GetDirectoryName(fullPath) ?? string.Empty);
            }
            else
            {
                var dirInfo = new DirectoryInfo(fullPath);
                if (dirInfo.Exists && dirInfo.LinkTarget != null)
                {
                    // Follow the symlink
                    fullPath = Path.GetFullPath(dirInfo.LinkTarget, Path.GetDirectoryName(fullPath) ?? string.Empty);
                }
            }
        }
        catch
        {
            // If we can't resolve links, just use the full path
        }

        return fullPath;
    }

    /// <summary>
    /// Detects if the current installation is a mixed installation scenario.
    /// </summary>
    /// <param name="muxerPath">The path to the current dotnet muxer executable</param>
    /// <param name="dotnetRoot">The value of the DOTNET_ROOT environment variable (can be null)</param>
    /// <returns>True if a mixed installation is detected, false otherwise</returns>
    public static bool IsMixedInstallation(string muxerPath, string? dotnetRoot)
    {
        if (string.IsNullOrEmpty(muxerPath) || string.IsNullOrEmpty(dotnetRoot))
        {
            return false;
        }

        // Get the registered global install location
        string? globalInstallRoot = GetGlobalInstallRoot();
        if (string.IsNullOrEmpty(globalInstallRoot))
        {
            // No global install registered, cannot detect mixed installation
            return false;
        }

        // Normalize paths and resolve symlinks for comparison
        string normalizedMuxerPath = ResolvePathAndLinks(muxerPath);
        string normalizedDotnetRoot = ResolvePathAndLinks(dotnetRoot);
        string normalizedGlobalRoot = ResolvePathAndLinks(globalInstallRoot);

        // Check if the muxer is in the global install root
        if (!normalizedMuxerPath.StartsWith(normalizedGlobalRoot, GetStringComparison()))
        {
            // Muxer is not in the global install root, no mixed installation
            return false;
        }

        // Check if DOTNET_ROOT points to a different location than the global install root
        bool isDifferentRoot = !normalizedDotnetRoot.StartsWith(normalizedGlobalRoot, GetStringComparison());

        return isDifferentRoot;
    }

    /// <summary>
    /// Gets the appropriate string comparison for the current platform.
    /// </summary>
    private static StringComparison GetStringComparison()
    {
        // Windows is case-insensitive for paths
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    /// <summary>
    /// Gets the documentation URL for mixed installation issues.
    /// </summary>
    public static string? GetDocumentationUrl()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "https://learn.microsoft.com/dotnet/core/install/linux-package-mixup";
        }

        return null;
    }
}
