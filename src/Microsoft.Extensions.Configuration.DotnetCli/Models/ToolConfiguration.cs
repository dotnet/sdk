// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.Configuration.DotnetCli.Models;

/// <summary>
/// Configuration settings that control global tools behavior.
/// </summary>
public sealed class ToolConfiguration
{
    /// <summary>
    /// Gets or sets whether to allow tool manifests in the repository root.
    /// Mapped from DOTNET_TOOLS_ALLOW_MANIFEST_IN_ROOT environment variable.
    /// </summary>
    public FlexibleBool AllowManifestInRoot { get; set; } = false;
}
