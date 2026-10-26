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
    // Repository entries retain this field for compatibility, but are evaluated from GlobalJsonPath.
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

    /// <summary>The payload's product version when it differs from the tracked build identity. Use <see cref="LayoutVersion"/> for paths.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProductVersion { get; set; }

    public List<string> Subcomponents { get; set; } = [];

    /// <summary>The version folder on disk.</summary>
    [JsonIgnore]
    public string LayoutVersion => ProductVersion ?? Version;

    /// <summary>The installation's folder relative to the install root, such as <c>sdk/10.0.100</c>.</summary>
    [JsonIgnore]
    public string LayoutPath => Component.GetLayoutPath(LayoutVersion);

    /// <summary>
    /// Returns true if the spec owns this installation. Only global.json specs match the on-disk version, which the host
    /// resolves global.json against; other channels match the build version so a daily build can't satisfy channels like latest.
    /// </summary>
    public bool IsOwnedBy(InstallSpec spec)
    {
        if (spec.Component != Component)
        {
            return false;
        }

        var channel = new UpdateChannel(spec.VersionOrChannel);
        return IsSatisfiedBy(channel, Version)
            || (spec.InstallSource == InstallSource.GlobalJson && ProductVersion is not null && IsSatisfiedBy(channel, ProductVersion));
    }

    private static bool IsSatisfiedBy(UpdateChannel channel, string version) =>
        ReleaseVersion.TryParse(version, out var parsed) && channel.Matches(parsed);

    public DotnetInstall ToDotnetInstall(DotnetInstallRoot installRoot) =>
        new(installRoot, new ReleaseVersion(Version), Component,
            ProductVersion is null ? null : new ReleaseVersion(ProductVersion));

    public static Installation FromDotnetInstall(DotnetInstall install, IEnumerable<string> subcomponents) => new()
    {
        Component = install.Component,
        Version = install.Version.ToString(),
        ProductVersion = install.ProductVersion?.ToString(),
        Subcomponents = [.. subcomponents],
    };
}
