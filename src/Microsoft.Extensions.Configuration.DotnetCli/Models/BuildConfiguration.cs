// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.Configuration.DotnetCli.Models;

/// <summary>
/// Configuration settings that control build system behavior.
/// </summary>
public sealed class BuildConfiguration
{
    /// <summary>
    /// Gets or sets whether to run MSBuild out of process.
    /// Mapped from DOTNET_CLI_RUN_MSBUILD_OUTOFPROC environment variable.
    /// </summary>
    public bool RunMSBuildOutOfProc { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to use the MSBuild server for builds.
    /// Mapped from DOTNET_CLI_USE_MSBUILD_SERVER environment variable.
    /// </summary>
    public bool UseMSBuildServer { get; set; } = false;

    /// <summary>
    /// Gets or sets the configuration for the MSBuild terminal logger.
    /// Mapped from DOTNET_CLI_CONFIGURE_MSBUILD_TERMINAL_LOGGER environment variable.
    /// </summary>
    public string? ConfigureMSBuildTerminalLogger { get; set; }

    /// <summary>
    /// Gets or sets whether to disable publish and pack release configuration.
    /// Mapped from DOTNET_CLI_DISABLE_PUBLISH_AND_PACK_RELEASE environment variable.
    /// </summary>
    public bool DisablePublishAndPackRelease { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to enable lazy publish and pack release for solutions.
    /// Mapped from DOTNET_CLI_LAZY_PUBLISH_AND_PACK_RELEASE_FOR_SOLUTIONS environment variable.
    /// </summary>
    public bool LazyPublishAndPackReleaseForSolutions { get; set; } = false;
}
