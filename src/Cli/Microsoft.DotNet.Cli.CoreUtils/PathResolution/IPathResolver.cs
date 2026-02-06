// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli;

/// <summary>
/// Minimal abstraction providing the three fundamental anchor points for all path resolution.
/// All other paths are derived from these three via extension methods.
/// </summary>
/// <remarks>
/// <list type="number">
/// <item>DotnetRoot - Where dotnet.exe and shared components live</item>
/// <item>SdkRoot - Where the versioned SDK tools (MSBuild, CLI) live</item>
/// <item>DotnetExecutable - The running dotnet process executable</item>
/// </list>
///
/// All other paths (MSBuild, bundled tools, packs, manifests, etc.) are computed
/// from these three anchors via extension methods in PathResolverExtensions.
/// </remarks>
public interface IPathResolver
{
    /// <summary>
    /// Root directory where the dotnet executable and shared components are installed.
    /// </summary>
    /// <example>/usr/share/dotnet or C:\Program Files\dotnet</example>
    /// <remarks>
    /// Standard layout: Parent directory of dotnet executable
    /// Configurable via: DOTNET_ROOT environment variable
    /// </remarks>
    string DotnetRoot { get; }

    /// <summary>
    /// Current SDK tools directory (versioned) containing CLI and MSBuild.
    /// </summary>
    /// <example>/usr/share/dotnet/sdk/10.0.100</example>
    /// <remarks>
    /// Standard layout: AppContext.BaseDirectory
    /// Configurable via: DOTNET_SDK_ROOT environment variable
    /// </remarks>
    string SdkRoot { get; }

    /// <summary>
    /// Full path to the dotnet executable.
    /// </summary>
    /// <example>/usr/share/dotnet/dotnet</example>
    /// <remarks>
    /// Always determined from the running process (Environment.ProcessPath).
    /// Used for process spawning and DOTNET_HOST_PATH.
    /// </remarks>
    string DotnetExecutable { get; }
}
