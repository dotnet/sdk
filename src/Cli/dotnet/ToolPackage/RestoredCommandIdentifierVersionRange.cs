// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.ToolPackage;

/// <summary>
///     A range of RestoredCommandIdentifier that is only different in the Version field.
/// </summary>
internal class RestoredCommandIdentifierVersionRange(
    PackageId packageId,
    VersionRange versionRange,
    NuGetFramework targetFramework,
    string runtimeIdentifier,
    ToolCommandName commandName)
{
    public PackageId PackageId { get; } = packageId;
    public VersionRange VersionRange { get; } = versionRange ?? throw new ArgumentException(nameof(versionRange));
    public NuGetFramework TargetFramework { get; } = targetFramework ?? throw new ArgumentException(nameof(targetFramework));
    public string RuntimeIdentifier { get; } = runtimeIdentifier ?? throw new ArgumentException(nameof(runtimeIdentifier));
    public ToolCommandName CommandName { get; } = commandName;

    public RestoredCommandIdentifier WithVersion(NuGetVersion version)
    {
        return new RestoredCommandIdentifier(PackageId, version, TargetFramework, RuntimeIdentifier, CommandName);
    }
}
