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
    private const string ConfigFileName = "dotnetup.config.json";
    private const string TelemetrySentinelFileName = ".dotnetup-telemetry-notice";

#pragma warning disable IDE0032 // Lazy-init cache; not convertible to auto-property
    private static string? s_dataDirectory;
#pragma warning restore IDE0032

    /// <summary>
    /// Thread-local override for the data directory, used by tests to avoid
    /// mutating process-wide environment variables. Takes precedence over
    /// the DOTNET_DOTNETUP_DATA_DIR environment variable.
    /// </summary>
    [ThreadStatic]
    private static string? s_testDataDirectoryOverride;

    /// <summary>
    /// Sets a thread-local data directory override for testing.
    /// Call <see cref="ClearTestDataDirectoryOverride"/> when done.
    /// </summary>
    public static void SetTestDataDirectoryOverride(string path) => s_testDataDirectoryOverride = path;

    /// <summary>
    /// Clears the thread-local data directory override.
    /// </summary>
    public static void ClearTestDataDirectoryOverride() => s_testDataDirectoryOverride = null;

    /// <summary>
    /// Gets the base data directory for dotnetup.
    /// On Windows: %LOCALAPPDATA%\dotnetup
    /// On macOS: ~/Library/Application Support/dotnetup
    /// On Linux: $XDG_DATA_HOME/dotnetup or ~/.local/share/dotnetup
    /// </summary>
    /// <remarks>
    /// Can be overridden via <see cref="SetTestDataDirectoryOverride"/> (thread-local, preferred for tests)
    /// or via DOTNET_DOTNETUP_DATA_DIR environment variable.
    /// Throws if the base directory cannot be determined.
    /// </remarks>
    public static string DataDirectory
    {
        get
        {
            // Thread-local test override takes highest precedence — parallel-safe.
            if (s_testDataDirectoryOverride is not null)
            {
                return s_testDataDirectoryOverride;
            }

            // Allow override for testing — avoids touching the real user profile.
            var overrideDir = Environment.GetEnvironmentVariable("DOTNET_DOTNETUP_DATA_DIR");
            if (!string.IsNullOrEmpty(overrideDir))
            {
                return overrideDir;
            }

            if (s_dataDirectory is not null)
            {
                return s_dataDirectory;
            }

            var baseDir = GetBaseDirectory();
            if (string.IsNullOrEmpty(baseDir))
            {
                throw new InvalidOperationException("Could not determine the local application data directory. Ensure the environment is properly configured.");
            }

            s_dataDirectory = Path.Combine(baseDir, DotnetupFolderName);
            return s_dataDirectory;
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
    /// Gets the path to the dotnetup configuration file.
    /// </summary>
    public static string ConfigPath => Path.Combine(DataDirectory, ConfigFileName);

    /// <summary>
    /// Gets the path to the download cache directory.
    /// </summary>
    public static string DownloadCacheDirectory => Path.Combine(DataDirectory, "downloadcache");

    /// <summary>
    /// Gets the path to the telemetry first-run sentinel file.
    /// </summary>
    public static string TelemetrySentinelPath => Path.Combine(DataDirectory, TelemetrySentinelFileName);

    /// <summary>
    /// Gets the default dotnet install path managed by dotnetup.
    /// This is the user-local dotnet root (e.g. %LOCALAPPDATA%\dotnet on Windows).
    /// </summary>
    public static string DefaultDotnetInstallPath
    {
        get
        {
            var baseDir = GetBaseDirectory();
            if (string.IsNullOrEmpty(baseDir))
            {
                throw new InvalidOperationException("Could not determine the local application data directory.");
            }

            return Path.Combine(baseDir, "dotnet");
        }
    }

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
