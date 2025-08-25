// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Base record for .NET installation information with common properties.
/// </summary>
internal record DotnetInstallBase(
    string ResolvedDirectory,
    InstallType Type,
    InstallMode Mode,
    InstallArchitecture Architecture)
{
    public Guid Id { get; } = Guid.NewGuid();
}

/// <summary>
/// Represents a .NET installation with a fully specified version.
/// </summary>
internal record DotnetInstall(
    string FullySpecifiedVersion,
    string ResolvedDirectory,
    InstallType Type,
    InstallMode Mode,
    InstallArchitecture Architecture) : DotnetInstallBase(ResolvedDirectory, Type, Mode, Architecture);

/// <summary>
/// Represents a request for a .NET installation with a channel version.
/// </summary>
internal record DotnetInstallRequest(
    string ChannelVersion,
    string ResolvedDirectory,
    InstallType Type,
    InstallMode Mode,
    InstallArchitecture Architecture) : DotnetInstallBase(ResolvedDirectory, Type, Mode, Architecture);
