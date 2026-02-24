// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

/// <summary>
/// Manages the first-run telemetry notice for dotnetup.
/// Displays a brief notice on first use and creates a sentinel file to prevent repeat notices.
/// </summary>
internal static class FirstRunNotice
{
    /// <summary>
    /// Environment variable to suppress the first-run notice (same as .NET SDK).
    /// </summary>
    private const string NoLogoEnvironmentVariable = "DOTNET_NOLOGO";

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

        // Respect DOTNET_NOLOGO to suppress notice (same behavior as .NET SDK)
        if (IsNoLogoSet())
        {
            return;
        }

        var sentinelPath = DotnetupPaths.TelemetrySentinelPath;
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
    /// Checks if DOTNET_NOLOGO is set to suppress the first-run notice.
    /// </summary>
    private static bool IsNoLogoSet()
    {
        var value = Environment.GetEnvironmentVariable(NoLogoEnvironmentVariable);
        return string.Equals(value, "1", StringComparison.Ordinal) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if this is the first run (sentinel doesn't exist).
    /// </summary>
    public static bool IsFirstRun()
    {
        var sentinelPath = DotnetupPaths.TelemetrySentinelPath;
        return !string.IsNullOrEmpty(sentinelPath) && !File.Exists(sentinelPath);
    }

    private static void ShowNotice()
    {
        // Write to stderr, consistent with .NET SDK behavior
        // See: https://learn.microsoft.com/dotnet/core/compatibility/sdk/10.0/dotnet-cli-stderr-output
        Console.Error.WriteLine();
        Console.Error.WriteLine(Strings.TelemetryNotice);
        Console.Error.WriteLine();
    }

    private static void CreateSentinel(string sentinelPath)
    {
        try
        {
            DotnetupPaths.EnsureDataDirectoryExists();

            // Write version info to the sentinel for debugging purposes
            File.WriteAllText(sentinelPath, $"dotnetup telemetry notice shown: {DateTime.UtcNow:O}\nVersion: {BuildInfo.Version}\nCommit: {BuildInfo.CommitSha}\n");
        }
        catch
        {
            // If we can't create the sentinel, the notice will show again next time
            // This is acceptable - better than crashing
        }
    }
}
