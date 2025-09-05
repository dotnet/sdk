// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Base record for .NET installation information with common properties.
/// </summary>
public record DotnetInstallBase(
    string ResolvedDirectory,
    InstallType Type,
    InstallMode Mode,
    InstallArchitecture Architecture)
{
    public Guid Id { get; } = Guid.NewGuid();
}

internal record InstallRequestOptions()
{
    // Include things such as the custom feed here.
}

/// <summary>
/// Represents a .NET installation with a fully specified version.
/// The MuxerDirectory is the directory of the corresponding .NET host that has visibility into this .NET installation.
/// </summary>
public record DotnetInstall(
    DotnetVersion FullySpecifiedVersion,
    string MuxerDirectory,
    InstallType Type,
    InstallMode Mode,
    InstallArchitecture Architecture,
    ManagementCadence Cadence) : DotnetInstallBase(MuxerDirectory, Type, Mode, Architecture);

/// <summary>
/// Represents a request for a .NET installation with a channel version that will get resolved into a fully specified version.
/// </summary>
internal record DotnetInstallRequest(
    string ChannelVersion,
    string TargetDirectory,
    InstallType Type,
    InstallMode Mode,
    InstallArchitecture Architecture,
    ManagementCadence Cadence,
    InstallRequestOptions Options) : DotnetInstallBase(Path.Combine(TargetDirectory, DnupUtilities.GetDotnetExeName()), Type, Mode, Architecture);
