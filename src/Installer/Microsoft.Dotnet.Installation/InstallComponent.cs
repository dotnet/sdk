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
    // ── Framework-name constants (shared by GetFrameworkName / FromFrameworkName) ──
    public const string RuntimeFrameworkName = "Microsoft.NETCore.App";
    public const string AspNetCoreFrameworkName = "Microsoft.AspNetCore.App";
    public const string WindowsDesktopFrameworkName = "Microsoft.WindowsDesktop.App";

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
    /// Gets the official framework name for the component (e.g., "Microsoft.NETCore.App").
    /// Used for JSON/machine-readable output.
    /// </summary>
    public static string GetFrameworkName(this InstallComponent component) => component switch
    {
        InstallComponent.SDK => ".NET SDK",
        InstallComponent.Runtime => RuntimeFrameworkName,
        InstallComponent.ASPNETCore => AspNetCoreFrameworkName,
        InstallComponent.WindowsDesktop => WindowsDesktopFrameworkName,
        _ => component.ToString()
    };

    /// <summary>
    /// Resolves a framework name (e.g. "Microsoft.NETCore.App") to the corresponding <see cref="InstallComponent"/>.
    /// Returns null when the name is not recognized.
    /// </summary>
    public static InstallComponent? FromFrameworkName(string frameworkName) => frameworkName switch
    {
        RuntimeFrameworkName => InstallComponent.Runtime,
        AspNetCoreFrameworkName => InstallComponent.ASPNETCore,
        WindowsDesktopFrameworkName => InstallComponent.WindowsDesktop,
        _ => null
    };
}
