// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation;

public enum InstallComponent
{
    SDK,
    Runtime,
    ASPNETCore,
    WindowsDesktop
}

public static class InstallComponentExtensions
{
    /// <summary>
    /// The longest display name across all components. Used to pad shorter names
    /// so progress rows align when multiple component types are shown together.
    /// </summary>
    private static readonly int s_maxDisplayNameLength =
        Enum.GetValues<InstallComponent>().Max(c => c.GetDisplayName().Length);

    /// <summary>
    /// Gets the human-readable display name for the component.
    /// Uses shorter, punchier names rather than the full Microsoft.* framework names.
    /// </summary>
    public static string GetDisplayName(this InstallComponent component) => component switch
    {
        InstallComponent.SDK => ".NET SDK",
        InstallComponent.Runtime => "dotnet (runtime)",
        InstallComponent.ASPNETCore => "aspnet (runtime)",
        InstallComponent.WindowsDesktop => "windowsdesktop (runtime)",
        _ => component.ToString()
    };

    /// <summary>
    /// Gets the display name right-padded to the length of the longest component name,
    /// so that version numbers align when multiple component types appear together.
    /// </summary>
    public static string GetPaddedDisplayName(this InstallComponent component) =>
        component.GetDisplayName().PadRight(s_maxDisplayNameLength);

    /// <summary>
    /// Formats a version string to a fixed display width so progress rows align.
    /// Short versions like "9.0.312" are left-padded; long versions like
    /// "11.0.100-preview.2.26159.112" are truncated to "..59.112".
    /// Target width matches the common format "10.0.201" (8 chars).
    /// </summary>
    public static string FormatVersionForDisplay(string version)
    {
        const int targetWidth = 8;
        if (version.Length <= targetWidth)
        {
            return version.PadLeft(targetWidth);
        }

        return ".." + version[^(targetWidth - 2)..];
    }

    /// <summary>
    /// Width of the " (nnn.n MB / nnn.n MB)" suffix appended during download progress.
    /// Used to pad non-download descriptions (e.g. "Installing") to the same total width
    /// so all progress rows stay aligned.
    /// </summary>
    public const int DownloadSuffixWidth = 22;

    /// <summary>
    /// Builds a progress-bar description such as "Downloading aspnet (runtime)         9.0.312".
    /// Component names and versions are padded so all rows align vertically.
    /// </summary>
    public static string FormatProgressDescription(string action, InstallComponent component, string version) =>
        $"{action} {component.GetPaddedDisplayName()} {FormatVersionForDisplay(version)}";

    /// <summary>
    /// Gets the official framework name for the component (e.g., "Microsoft.NETCore.App").
    /// Used for JSON/machine-readable output.
    /// </summary>
    public static string GetFrameworkName(this InstallComponent component) => component switch
    {
        InstallComponent.SDK => ".NET SDK",
        InstallComponent.Runtime => "Microsoft.NETCore.App",
        InstallComponent.ASPNETCore => "Microsoft.AspNetCore.App",
        InstallComponent.WindowsDesktop => "Microsoft.WindowsDesktop.App",
        _ => component.ToString()
    };

    /// <summary>
    /// Resolves a framework name (e.g. "Microsoft.NETCore.App") to the corresponding <see cref="InstallComponent"/>.
    /// Returns null when the name is not recognized.
    /// </summary>
    public static InstallComponent? FromFrameworkName(string frameworkName) => frameworkName switch
    {
        "Microsoft.NETCore.App" => InstallComponent.Runtime,
        "Microsoft.AspNetCore.App" => InstallComponent.ASPNETCore,
        "Microsoft.WindowsDesktop.App" => InstallComponent.WindowsDesktop,
        _ => null
    };
}
