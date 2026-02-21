// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    /// On macOS: ~/Library/Application Support/dotnetup
    /// On Linux: ~/.local/share/dotnetup
    /// </summary>
    /// <remarks>
    /// Can be overridden via DOTNET_TESTHOOK_DOTNETUP_DATA_DIR environment variable.
    /// Throws if the base directory cannot be determined.
    /// </remarks>
    public static string DataDirectory
    {
        get
        {
            // Allow override for testing — avoids touching the real user profile.
            var overrideDir = Environment.GetEnvironmentVariable("DOTNET_TESTHOOK_DOTNETUP_DATA_DIR");
            if (!string.IsNullOrEmpty(overrideDir))
            {
                return overrideDir;
            }

            if (_dataDirectory is not null)
            {
                return _dataDirectory;
            }

            var baseDir = GetBaseDirectory();
            if (string.IsNullOrEmpty(baseDir))
            {
                throw new InvalidOperationException("Could not determine the local application data directory. Ensure the environment is properly configured.");
            }

            _dataDirectory = Path.Combine(baseDir, DotnetupFolderName);
            return _dataDirectory;
        }
    }

    /// <summary>
    /// Gets the path to the dotnetup manifest file.
    /// </summary>
    /// <remarks>
    /// Can be overridden via DOTNET_TESTHOOK_MANIFEST_PATH environment variable.
    /// </remarks>
    public static string ManifestPath
    {
        get
        {
            // Allow override for testing
            var overridePath = Environment.GetEnvironmentVariable("DOTNET_TESTHOOK_MANIFEST_PATH");
            if (!string.IsNullOrEmpty(overridePath))
            {
                return overridePath;
            }

            return Path.Combine(DataDirectory, ManifestFileName);
        }
    }

    /// <summary>
    /// Gets the path to the telemetry first-run sentinel file.
    /// </summary>
    public static string TelemetrySentinelPath => Path.Combine(DataDirectory, TelemetrySentinelFileName);

    /// <summary>
    /// Ensures the data directory exists, creating it if necessary.
    /// </summary>
    /// <returns>True if the directory exists or was created; false otherwise.</returns>
    public static bool EnsureDataDirectoryExists()
    {
        try
        {
            var dataDir = DataDirectory;
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
        // Use LocalApplicationData on all platforms:
        // Windows: %LOCALAPPDATA% (e.g., C:\Users\<user>\AppData\Local)
        // macOS:   ~/Library/Application Support
        // Linux:   $XDG_DATA_HOME or ~/.local/share
        return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    }
}
