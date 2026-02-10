// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Provides canonical paths for dotnetup data directories and files.
/// Centralizes path logic to ensure consistency across the application.
/// </summary>
internal static class DotnetupPaths
{
    private const string DotnetupFolderName = "dotnetup";
    private const string ManifestFileName = "dotnetup_manifest.json";
    private const string TelemetrySentinelFileName = ".dotnetup-telemetry-notice";

    private static string? _dataDirectory;

    /// <summary>
    /// Gets the base data directory for dotnetup.
    /// On Windows: %LOCALAPPDATA%\dotnetup
    /// On Unix: ~/.dotnetup (hidden folder in user profile)
    /// </summary>
    /// <remarks>
    /// Returns null if the base directory cannot be determined.
    /// </remarks>
    public static string? DataDirectory
    {
        get
        {
            if (_dataDirectory is not null)
            {
                return _dataDirectory;
            }

            var baseDir = GetBaseDirectory();
            if (string.IsNullOrEmpty(baseDir))
            {
                return null;
            }

            _dataDirectory = Path.Combine(baseDir, DotnetupFolderName);
            return _dataDirectory;
        }
    }

    /// <summary>
    /// Gets the path to the dotnetup manifest file.
    /// </summary>
    /// <remarks>
    /// Returns null if the data directory cannot be determined.
    /// Can be overridden via DOTNET_TESTHOOK_MANIFEST_PATH environment variable.
    /// </remarks>
    public static string? ManifestPath
    {
        get
        {
            // Allow override for testing
            var overridePath = Environment.GetEnvironmentVariable("DOTNET_TESTHOOK_MANIFEST_PATH");
            if (!string.IsNullOrEmpty(overridePath))
            {
                return overridePath;
            }

            var dataDir = DataDirectory;
            return dataDir is null ? null : Path.Combine(dataDir, ManifestFileName);
        }
    }

    /// <summary>
    /// Gets the path to the telemetry first-run sentinel file.
    /// </summary>
    /// <remarks>
    /// Returns null if the data directory cannot be determined.
    /// </remarks>
    public static string? TelemetrySentinelPath
    {
        get
        {
            var dataDir = DataDirectory;
            return dataDir is null ? null : Path.Combine(dataDir, TelemetrySentinelFileName);
        }
    }

    /// <summary>
    /// Ensures the data directory exists, creating it if necessary.
    /// </summary>
    /// <returns>True if the directory exists or was created; false otherwise.</returns>
    public static bool EnsureDataDirectoryExists()
    {
        var dataDir = DataDirectory;
        if (string.IsNullOrEmpty(dataDir))
        {
            return false;
        }

        try
        {
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the base directory for dotnetup data storage.
    /// </summary>
    private static string? GetBaseDirectory()
    {
        // On Windows: use LocalApplicationData (%LOCALAPPDATA%)
        // On Unix: use UserProfile (~) - the folder name "dotnetup" will be used (not hidden)
        //          Unix convention is to use ~/.config for app data, but we use ~/dotnetup for simplicity
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
