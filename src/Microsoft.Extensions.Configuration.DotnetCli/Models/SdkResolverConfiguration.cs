// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.Configuration.DotnetCli.Models;

/// <summary>
/// Configuration settings that control SDK resolution and discovery.
/// </summary>
public sealed class SdkResolverConfiguration
{
    /// <summary>
    /// Gets or sets whether to enable SDK resolver logging.
    /// Mapped from DOTNET_MSBUILD_SDK_RESOLVER_ENABLE_LOG environment variable.
    /// </summary>
    public bool EnableLog { get; set; } = false;

    /// <summary>
    /// Gets or sets the directory containing SDKs.
    /// Mapped from DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR environment variable.
    /// </summary>
    public string? SdksDirectory { get; set; }

    /// <summary>
    /// Gets or sets the SDK version to use.
    /// Mapped from DOTNET_MSBUILD_SDK_RESOLVER_SDKS_VER environment variable.
    /// </summary>
    public string? SdksVersion { get; set; }

    /// <summary>
    /// Gets or sets the CLI directory for SDK resolution.
    /// Mapped from DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR environment variable.
    /// </summary>
    public string? CliDirectory { get; set; }
}
