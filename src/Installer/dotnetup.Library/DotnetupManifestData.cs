// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Top-level manifest data structure for dotnetup installation tracking.
/// </summary>
internal class DotnetupManifestData
{
    public string SchemaVersion { get; set; } = "1";
    public List<DotnetRootEntry> DotnetRoots { get; set; } = [];
}

/// <summary>
/// Represents a dotnet installation root directory with its install specs and installations.
/// </summary>
internal class DotnetRootEntry
{
    public string Path { get; set; } = string.Empty;
    public InstallArchitecture Architecture { get; set; }
    public List<InstallSpec> InstallSpecs { get; set; } = [];
    public List<Installation> Installations { get; set; } = [];
}

/// <summary>
/// Represents what the user requested to be installed (the "install spec").
/// </summary>
internal class InstallSpec
{
    public InstallComponent Component { get; set; }
    public string VersionOrChannel { get; set; } = string.Empty;
    public InstallSource InstallSource { get; set; }
    public string? GlobalJsonPath { get; set; }
}

/// <summary>
/// Represents an actual installed component with its exact version and subcomponents.
/// </summary>
internal class Installation
{
    public InstallComponent Component { get; set; }
    public string Version { get; set; } = string.Empty;

    /// <summary>The payload's on-disk version when it differs from the tracked build identity.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProductVersion { get; set; }

    public List<string> Subcomponents { get; set; } = [];

    /// <summary>The version folder on disk.</summary>
    [JsonIgnore]
    public string LayoutVersion => ProductVersion ?? Version;

    /// <summary>
    /// Returns true if the spec owns this installation. Only global.json specs match the on-disk version, which the host
    /// resolves global.json against; other channels match the build version so a daily build can't satisfy channels like latest.
    /// </summary>
    public bool IsOwnedBy(InstallSpec spec) =>
        spec.Component == Component && IsOwnedBy(Version, ProductVersion, spec.InstallSource, spec.VersionOrChannel);

    internal static bool IsOwnedBy(string version, string? productVersion, InstallSource source, string versionOrChannel)
    {
        var channel = new UpdateChannel(versionOrChannel);
        return IsSatisfiedBy(channel, version)
            || (source == InstallSource.GlobalJson && productVersion is not null && IsSatisfiedBy(channel, productVersion));
    }

    private static bool IsSatisfiedBy(UpdateChannel channel, string version) =>
        ReleaseVersion.TryParse(version, out var parsed) && channel.Matches(parsed);

    public DotnetInstall ToDotnetInstall(DotnetInstallRoot installRoot) =>
        new(installRoot, new ReleaseVersion(Version), Component,
            ProductVersion is null ? null : new ReleaseVersion(ProductVersion));
}

/// <summary>
/// The source of an install spec.
/// </summary>
internal enum InstallSource
{
    Explicit,
    GlobalJson,
    All,
}
