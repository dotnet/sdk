// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

/// <summary>
/// Manages the first-run telemetry notice for dotnetup.
/// Displays a brief notice on first use and creates a sentinel file to prevent repeat notices.
/// </summary>
internal static class FirstRunNotice
{
    private const string SentinelFileName = ".dotnetup-telemetry-notice";
    private const string TelemetryDocsUrl = "https://aka.ms/dotnetup-telemetry";

    /// <summary>
    /// Shows the first-run telemetry notice if this is the first time dotnetup is run
    /// and telemetry is enabled. Creates a sentinel file to prevent future notices.
    /// </summary>
    /// <param name="telemetryEnabled">Whether telemetry is currently enabled.</param>
    public static void ShowIfFirstRun(bool telemetryEnabled)
    {
        // Don't show notice if telemetry is disabled - user has already opted out
        if (!telemetryEnabled)
        {
            return;
        }

        var sentinelPath = GetSentinelPath();
        if (string.IsNullOrEmpty(sentinelPath))
        {
            return;
        }

        // Check if we've already shown the notice
        if (File.Exists(sentinelPath))
        {
            return;
        }

        // Show the notice
        ShowNotice();

        // Create the sentinel file to prevent future notices
        CreateSentinel(sentinelPath);
    }

    /// <summary>
    /// Checks if this is the first run (sentinel doesn't exist).
    /// </summary>
    public static bool IsFirstRun()
    {
        var sentinelPath = GetSentinelPath();
        return !string.IsNullOrEmpty(sentinelPath) && !File.Exists(sentinelPath);
    }

    private static void ShowNotice()
    {
        // Keep it brief - link to docs for full details
        Console.WriteLine();
        Console.WriteLine(Strings.TelemetryNotice);
        Console.WriteLine();
    }

    private static string? GetSentinelPath()
    {
        try
        {
            // Use the same location as dotnetup's data directory
            var baseDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (string.IsNullOrEmpty(baseDir))
            {
                return null;
            }

            var dotnetupDir = Path.Combine(baseDir, ".dotnetup");
            return Path.Combine(dotnetupDir, SentinelFileName);
        }
        catch
        {
            // If we can't determine the path, skip the notice
            return null;
        }
    }

    private static void CreateSentinel(string sentinelPath)
    {
        try
        {
            var directory = Path.GetDirectoryName(sentinelPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write version info to the sentinel for debugging purposes
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
            File.WriteAllText(sentinelPath, $"dotnetup telemetry notice shown: {DateTime.UtcNow:O}\nVersion: {version}\n");
        }
        catch
        {
            // If we can't create the sentinel, the notice will show again next time
            // This is acceptable - better than crashing
        }
    }
}
