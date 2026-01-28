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
    /// Gets the display name for the component as shown by dotnet --list-runtimes.
    /// </summary>
    public static string GetDisplayName(this InstallComponent component) => component switch
    {
        InstallComponent.SDK => ".NET SDK",
        InstallComponent.Runtime => "Microsoft.NETCore.App",
        InstallComponent.ASPNETCore => "Microsoft.AspNetCore.App",
        InstallComponent.WindowsDesktop => "Microsoft.WindowsDesktop.App",
        _ => component.ToString()
    };
}
