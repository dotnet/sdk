// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.Configuration.DotnetCli.Models;

/// <summary>
/// Configuration settings that control .NET runtime host behavior.
/// </summary>
public sealed class RuntimeHostConfiguration
{
    /// <summary>
    /// Gets or sets the path to the .NET host executable.
    /// Mapped from DOTNET_HOST_PATH environment variable.
    /// </summary>
    public string? HostPath { get; set; }

    /// <summary>
    /// Gets or sets whether to enable multilevel lookup for shared frameworks.
    /// Mapped from DOTNET_MULTILEVEL_LOOKUP environment variable.
    /// </summary>
    public bool MultilevelLookup { get; set; } = false;

    /// <summary>
    /// Gets or sets the roll-forward policy for framework version selection.
    /// Mapped from DOTNET_ROLL_FORWARD environment variable.
    /// </summary>
    public string? RollForward { get; set; }

    /// <summary>
    /// Gets or sets the roll-forward policy when no candidate framework is found.
    /// Mapped from DOTNET_ROLL_FORWARD_ON_NO_CANDIDATE_FX environment variable.
    /// </summary>
    public string? RollForwardOnNoCandidateFx { get; set; }

    /// <summary>
    /// Gets or sets the root directory for .NET installations.
    /// Mapped from DOTNET_ROOT environment variable.
    /// </summary>
    public string? Root { get; set; }

    /// <summary>
    /// Gets or sets the root directory for x86 .NET installations.
    /// Mapped from DOTNET_ROOT(x86) environment variable.
    /// </summary>
    public string? RootX86 { get; set; }
}
