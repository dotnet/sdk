// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Represents a .NET installation with a fully specified version.
/// The MuxerDirectory is the directory of the corresponding .NET host that has visibility into this .NET installation.
/// </summary>
public record DotnetInstall(
    DotnetInstallRoot InstallRoot,
    ReleaseVersion Version,
    InstallComponent Component);

/// <summary>
/// Represents a request for a .NET installation with a channel version that will get resolved into a fully specified version.
/// </summary>
internal record DotnetInstallRequest(
    DotnetInstallRoot InstallRoot,
    UpdateChannel Channel,
    InstallComponent Component,
    InstallRequestOptions Options)
{
    /// <summary>
    /// Optional pre-resolved version. When set, the orchestrator uses this directly
    /// instead of resolving the channel again. The Channel is still needed for
    /// recording the install spec in the manifest.
    /// </summary>
    public ReleaseVersion? ResolvedVersion { get; init; }
}

internal record InstallRequestOptions()
{
    // Include options such as the custom feed, manifest path, etc.
    public string? ManifestPath { get; init; }

    /// <summary>
    /// If true, the installation will fail if the muxer (dotnet executable) cannot be updated.
    /// If false (default), a warning is displayed but installation continues.
    /// </summary>
    public bool RequireMuxerUpdate { get; init; }

    /// <summary>
    /// The source of this install request.
    /// Used when recording install specs in the manifest.
    /// </summary>
    public InstallRequestSource InstallSource { get; init; } = InstallRequestSource.Explicit;

    /// <summary>
    /// The path to the global.json file that triggered this install, if applicable.
    /// </summary>
    public string? GlobalJsonPath { get; init; }

    /// <summary>
    /// If true, the installation will not be recorded in the dotnetup manifest
    /// and the untracked-installation guard will be bypassed.
    /// </summary>
    public bool Untracked { get; init; }

    /// <summary>
    /// If true, the install spec will not be recorded in the manifest.
    /// The installation itself is still recorded. Use this when the install spec
    /// already exists (e.g., during updates) to avoid creating duplicates.
    /// </summary>
    public bool SkipInstallSpecRecording { get; init; }
}

/// <summary>
/// The source that triggered an install request.
/// </summary>
internal enum InstallRequestSource
{
    Explicit,
    GlobalJson,
}
