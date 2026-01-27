// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;

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
/// Represents a request for a .NET installation with a channel version that will get resolved into a fully specified version.
/// </summary>
internal record DotnetInstallRequest(
    DotnetInstallRoot InstallRoot,
    UpdateChannel Channel,
    InstallComponent Component,
    InstallRequestOptions Options);

internal record InstallRequestOptions()
{
    // Include options such as the custom feed, manifest path, etc.
    public string? ManifestPath { get; init; }

    /// <summary>
    /// If true, the installation will fail if the muxer (dotnet executable) cannot be updated.
    /// If false (default), a warning is displayed but installation continues.
    /// </summary>
    public bool RequireMuxerUpdate { get; init; }
}
