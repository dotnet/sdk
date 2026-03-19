// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Resolves the installation path for .NET components based on various inputs including
/// global.json, explicit paths, current installations, and user prompts.
/// </summary>
internal static class InstallPathClassifier
{
    /// <summary>
    /// Determines whether the given path is an admin/system-managed .NET install location.
    /// These locations are managed by system package managers or OS installers and should not
    /// be used by dotnetup for user-level installations.
    /// </summary>
    public static bool IsAdminInstallPath(string path)
    {
        var fullPath = Path.GetFullPath(path);

        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            if ((!string.IsNullOrEmpty(programFiles) && IsOrIsUnder(fullPath, Path.Combine(programFiles, "dotnet"), StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(programFilesX86) && IsOrIsUnder(fullPath, Path.Combine(programFilesX86, "dotnet"), StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }
        else
        {
            // Standard system/package-manager locations on Linux and macOS.
            // See https://github.com/dotnet/designs/blob/main/accepted/2021/install-location-per-architecture.md
            if (IsOrIsUnder(fullPath, "/usr/share/dotnet", StringComparison.Ordinal) ||
                IsOrIsUnder(fullPath, "/usr/lib/dotnet", StringComparison.Ordinal) ||
                IsOrIsUnder(fullPath, "/usr/lib64/dotnet", StringComparison.Ordinal) ||
                IsOrIsUnder(fullPath, "/usr/local/share/dotnet", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // Checks whether fullPath equals adminPath or is a child directory of it.
    // A separate equality check prevents false matches on path prefixes
    // (e.g. "C:\Program Files\dotnet is cool" matching "C:\Program Files\dotnet").
    private static bool IsOrIsUnder(string fullPath, string adminPath, StringComparison comparison)
    {
        return string.Equals(fullPath, adminPath, comparison) ||
               fullPath.StartsWith(adminPath + Path.DirectorySeparatorChar, comparison);
    }

    /// <summary>
    /// Classifies the install path for telemetry (no PII - just the type of location).
    /// When pathSource is provided, global_json paths are distinguished from other path types.
    /// </summary>
    /// <param name="path">The install path to classify.</param>
    /// <param name="pathSource">How the path was determined (e.g., "global_json", "explicit"). Null to skip source-based classification.</param>
    public static string ClassifyInstallPath(string path, PathSource? pathSource = null)
    {
        var fullPath = Path.GetFullPath(path);

        // Check for admin/system .NET paths first — these are the most important to distinguish
        if (IsAdminInstallPath(path))
        {
            return "admin";
        }

        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            if (!string.IsNullOrEmpty(programFiles) && fullPath.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase))
            {
                return "system_programfiles";
            }
            if (!string.IsNullOrEmpty(programFilesX86) && fullPath.StartsWith(programFilesX86, StringComparison.OrdinalIgnoreCase))
            {
                return "system_programfiles_x86";
            }

            // Check more-specific paths before less-specific ones:
            // LocalApplicationData (e.g., C:\Users\x\AppData\Local) is under UserProfile (C:\Users\x)
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData) && fullPath.StartsWith(localAppData, StringComparison.OrdinalIgnoreCase))
            {
                return "local_appdata";
            }

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile) && fullPath.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase))
            {
                return "user_profile";
            }
        }
        else
        {
            if (fullPath.StartsWith("/usr/", StringComparison.Ordinal) ||
                fullPath.StartsWith("/opt/", StringComparison.Ordinal))
            {
                return "system_path";
            }

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home) && fullPath.StartsWith(home, StringComparison.Ordinal))
            {
                return "user_home";
            }
        }

        // If the path was specified by global.json and doesn't match a well-known location,
        // classify it as global_json rather than generic "other"
        if (pathSource == PathSource.GlobalJson)
        {
            return "global_json";
        }

        return "other";
    }
}
