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

/// <summary>
/// Extension methods for InstallComponent.
/// </summary>
public static class InstallComponentExtensions
{
    /// <summary>
    /// Gets a user-friendly description for the install component type.
    /// </summary>
    public static string GetDescription(this InstallComponent component) => component switch
    {
        InstallComponent.SDK => ".NET SDK",
        InstallComponent.Runtime => ".NET Runtime",
        InstallComponent.ASPNETCore => "ASP.NET Core Runtime",
        InstallComponent.WindowsDesktop => "Windows Desktop Runtime",
        _ => ".NET"
    };
}
