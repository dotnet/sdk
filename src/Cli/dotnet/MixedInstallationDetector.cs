// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Cli;

/// <summary>
/// Detects mixed installation scenarios where the dotnet muxer on PATH
/// is from a global install but DOTNET_ROOT points to a different location.
/// </summary>
internal static class MixedInstallationDetector
{
    /// <summary>
    /// Gets the known global installation root paths for the current platform.
    /// Based on https://github.com/dotnet/designs/blob/main/accepted/2020/install-locations.md
    /// and https://github.com/dotnet/designs/blob/main/accepted/2021/install-location-per-architecture.md
    /// </summary>
    private static readonly string[] GlobalInstallRoots = GetGlobalInstallRoots();

    private static string[] GetGlobalInstallRoots()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows global install locations
            return new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dotnet")
            };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS global install locations
            return new[]
            {
                "/usr/local/share/dotnet"
            };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Linux global install locations (various distros use different paths)
            return new[]
            {
                "/usr/share/dotnet",
                "/usr/lib64/dotnet",
                "/usr/lib/dotnet"
            };
        }
        else
        {
            return Array.Empty<string>();
        }
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

        // Normalize paths for comparison
        string normalizedMuxerPath = Path.GetFullPath(muxerPath);
        string normalizedDotnetRoot = Path.GetFullPath(dotnetRoot);

        // Check if the muxer is in a global install root
        bool isInGlobalRoot = false;
        string? muxerRoot = null;

        foreach (var globalRoot in GlobalInstallRoots)
        {
            if (normalizedMuxerPath.StartsWith(globalRoot, GetStringComparison()))
            {
                isInGlobalRoot = true;
                muxerRoot = globalRoot;
                break;
            }
        }

        if (!isInGlobalRoot)
        {
            // Muxer is not in a global install root, no mixed installation
            return false;
        }

        // Check if DOTNET_ROOT points to a different location than the muxer's root
        bool isDifferentRoot = !normalizedDotnetRoot.StartsWith(muxerRoot!, GetStringComparison());

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
            return "https://learn.microsoft.com/en-us/dotnet/core/install/linux-package-mixup";
        }

        return null;
    }
}
