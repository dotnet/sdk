// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.Configuration.DotnetCli.Models;

/// <summary>
/// Configuration settings that control first-time user experience setup.
/// </summary>
public sealed class FirstTimeUseConfiguration
{
    /// <summary>
    /// Gets or sets whether to generate ASP.NET Core HTTPS development certificates.
    /// Mapped from DOTNET_GENERATE_ASPNET_CERTIFICATE environment variable.
    /// </summary>
    public bool GenerateAspNetCertificate { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to add global tools to the PATH.
    /// Mapped from DOTNET_ADD_GLOBAL_TOOLS_TO_PATH environment variable.
    /// </summary>
    public bool AddGlobalToolsToPath { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to skip the first-time experience setup.
    /// Mapped from DOTNET_SKIP_FIRST_TIME_EXPERIENCE environment variable.
    /// </summary>
    public bool SkipFirstTimeExperience { get; set; } = false;
}
