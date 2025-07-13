// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.Configuration.DotnetCli.Models;

/// <summary>
/// Configuration settings that control NuGet package management behavior.
/// </summary>
public sealed class NuGetConfiguration
{
    /// <summary>
    /// Gets or sets whether NuGet signature verification is enabled.
    /// Mapped from DOTNET_NUGET_SIGNATURE_VERIFICATION environment variable.
    /// Defaults to true on Windows and Linux, false elsewhere.
    /// </summary>
    public bool SignatureVerificationEnabled { get; set; } = OperatingSystem.IsWindows() || OperatingSystem.IsLinux();
}
