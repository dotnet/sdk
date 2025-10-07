// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.DotNet.Tools.Bootstrapper;

public record DotnetInstallRoot(
    string? Path,
    InstallType Type,
    InstallArchitecture Architecture)
{
    //  Do we need a GUID for the ID here?
}

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
public record DotnetInstallRequest(
    DotnetInstallRoot InstallRoot,
    UpdateChannel Channel,
    InstallComponent Component,
    InstallRequestOptions Options);

public record InstallRequestOptions()
{
    // Include things such as the custom feed here.
}
