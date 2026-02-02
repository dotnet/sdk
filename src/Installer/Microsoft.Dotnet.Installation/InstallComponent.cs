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
}
