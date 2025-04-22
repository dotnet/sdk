// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.ToolManifest;

internal struct ToolManifestPackage(PackageId packagePackageId,
    NuGetVersion version,
    ToolCommandName[] toolCommandNames,
    DirectoryPath firstEffectDirectory,
    bool rollForward) : IEquatable<ToolManifestPackage>
{
    public PackageId PackageId { get; } = packagePackageId;
    public NuGetVersion Version { get; } = version ?? throw new ArgumentNullException(nameof(version));
    public ToolCommandName[] CommandNames { get; } = toolCommandNames ?? throw new ArgumentNullException(nameof(toolCommandNames));
    public bool RollForward { get; } = rollForward;
    /// <summary>
    /// The directory that will take effect first.
    /// When it is under .config directory, it is not .config directory
    /// it is .config's parent directory
    /// </summary>
    public DirectoryPath FirstEffectDirectory { get; } = firstEffectDirectory;

    public override bool Equals(object obj)
    {
        return obj is ToolManifestPackage tool &&
               Equals(tool);
    }

    public bool Equals(ToolManifestPackage other)
    {
        return PackageId.Equals(other.PackageId) &&
               EqualityComparer<NuGetVersion>.Default.Equals(Version, other.Version) &&
               CommandNamesEqual(other.CommandNames) &&
               FirstEffectDirectory.Value.TrimEnd('/', '\\')
                 .Equals(other.FirstEffectDirectory.Value.TrimEnd('/', '\\'), StringComparison.Ordinal) &&
                 RollForward.Equals(other.RollForward);
    }

    private bool CommandNamesEqual(ToolCommandName[] otherCommandNames)
    {
        if (CommandNames == null)
        {
            return otherCommandNames == null;
        }

        if (otherCommandNames == null)
        {
            return false;
        }

        return CommandNames.SequenceEqual(otherCommandNames);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(PackageId, Version, CommandNames, RollForward);
    }

    public static bool operator ==(ToolManifestPackage tool1,
        ToolManifestPackage tool2)
    {
        return tool1.Equals(tool2);
    }

    public static bool operator !=(ToolManifestPackage tool1,
        ToolManifestPackage tool2)
    {
        return !(tool1 == tool2);
    }
}
