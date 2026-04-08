// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation;

/// <summary>
/// Formatting utilities for progress bar descriptions. These constants and methods
/// ensure consistent alignment across download, install, and completion progress rows.
/// </summary>
public static class ProgressFormatting
{
    /// <summary>
    /// Width of the " (nnn.n MB / nnn.n MB)" suffix appended during download progress.
    /// Used to pad non-download descriptions (e.g. "Installing") to the same total width
    /// so all progress rows stay aligned.
    /// </summary>
    public const int DownloadSuffixWidth = 22;

    /// <summary>
    /// Localized action verb for the "downloading" state.
    /// </summary>
    public static string ActionDownloading => Strings.ProgressActionDownloading;

    /// <summary>
    /// Localized action verb for the "downloaded" state.
    /// </summary>
    public static string ActionDownloaded => Strings.ProgressActionDownloaded;

    /// <summary>
    /// Localized action verb for the "installing" state.
    /// </summary>
    public static string ActionInstalling => Strings.ProgressActionInstalling;

    /// <summary>
    /// Localized action verb for the "installed" state.
    /// </summary>
    public static string ActionInstalled => Strings.ProgressActionInstalled;

    /// <summary>
    /// Fixed width for action verbs in progress descriptions. Computed from the localized
    /// verb lengths so all progress rows stay aligned regardless of locale.
    /// </summary>
    private static readonly int ActionWidth = Math.Max(
        Math.Max(ActionDownloading.Length, ActionDownloaded.Length),
        Math.Max(ActionInstalling.Length, ActionInstalled.Length));

    /// <summary>
    /// Builds a progress-bar description such as "Downloading aspnet (runtime)         9.0.312".
    /// Component names and versions are padded so all rows align vertically.
    /// </summary>
    public static string FormatProgressDescription(string action, InstallComponent component, string version) =>
        $"{action.PadRight(ActionWidth)} {component.GetPaddedDisplayName()} {InstallComponentExtensions.FormatVersionForDisplay(version)}";

    /// <summary>
    /// Formats bytes as MB, right-aligned to 8 characters (e.g. "  0.7 MB", "290.4 MB").
    /// Always uses MB so the unit width is consistent across all progress rows.
    /// </summary>
    public static string FormatMB(long bytes) =>
        FormattableString.Invariant($"{bytes / (1024.0 * 1024.0),5:F1} MB");
}
