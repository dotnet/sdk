// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Represents a .NET installation with a fully specified version.
/// The MuxerDirectory is the directory of the corresponding .NET host that has visibility into this .NET installation.
/// </summary>
internal record DotnetInstall(
    DotnetInstallRoot InstallRoot,
    ReleaseVersion Version,
    InstallComponent Component);

/// <summary>
/// A lightweight specification for an install request before path resolution and
/// version resolution have occurred. Holds only the component type and an optional
/// version/channel string as provided by the user (e.g. "10.0", "latest", or null).
/// </summary>
internal record MinimalInstallSpec(
    InstallComponent Component,
    string? VersionOrChannel);

/// <summary>
/// Represents a request for a .NET installation.
/// <see cref="Channel"/> is the unresolved version channel string provided by the user
/// (e.g. "latest", "10.0", "9.0.3xx", or a specific version like "9.0.304").
/// It has NOT been resolved to a concrete <see cref="ReleaseVersion"/>.
/// To obtain a resolved version, create a <see cref="ResolvedInstallRequest"/>
/// by resolving this request through a <c>ChannelVersionResolver</c>.
/// </summary>
internal record DotnetInstallRequest(
    DotnetInstallRoot InstallRoot,
    UpdateChannel Channel,
    InstallComponent Component,
    InstallRequestOptions Options);

/// <summary>
/// An install request whose <see cref="DotnetInstallRequest.Channel"/> has been resolved to a concrete
/// <see cref="ResolvedVersion"/>. Created by the install workflow after version
/// resolution succeeds. The <see cref="ResolvedVersion"/> is guaranteed non-null.
/// </summary>
internal record ResolvedInstallRequest(
    DotnetInstallRequest Request,
    ReleaseVersion ResolvedVersion);

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
    public InstallSource InstallSource { get; init; } = InstallSource.Explicit;

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

    /// <summary>
    /// Controls the level of diagnostic output during installation.
    /// Corresponds to the <c>--verbosity</c> CLI option.
    /// </summary>
    public Verbosity Verbosity { get; init; }

    /// <summary>
    /// How the installation is performed. Defaults to <see cref="InstallMechanism.Archive"/>;
    /// admin mode requests use <see cref="InstallMechanism.WindowsInstallerBundle"/>.
    /// </summary>
    public InstallMechanism InstallMechanism { get; init; } = InstallMechanism.Archive;

    /// <summary>
    /// If true, the download must be covered by the signed releases.json manifest; downloads trusted
    /// only by a published hash (such as daily or blob-feed builds) are rejected. This is separate from
    /// Authenticode verification and from the machine BlockUnsignedDownloads policy. Set for admin mode installs.
    /// </summary>
    public bool RequireReleasesJsonSignature { get; init; }
}

/// <summary>
/// The mechanism used to lay down a .NET installation.
/// </summary>
internal enum InstallMechanism
{
    /// <summary>Download an archive and extract it into the install root.</summary>
    Archive = 0,

    /// <summary>Run the Windows installer bundle (.exe) into the shared system folder.</summary>
    WindowsInstallerBundle = 1,
}

/// <summary>
/// Controls the amount of output produced during installation operations.
/// Currently only <see cref="Normal"/> (default) and <see cref="Detailed"/> are
/// implemented. Future levels such as Quiet and Diagnostic can be added between
/// or after the existing values when needed.
/// </summary>
internal enum Verbosity
{
    // Future: Quiet = 0,
    Normal = 1,
    Detailed = 2,
    // Future: Diagnostic = 3,
}
