// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

/// <summary>
/// Represents the JSON manifest for static web asset package data.
/// Contains both assets and endpoints in a single file.
/// </summary>
public class StaticWebAssetPackageManifest
{
    public int Version { get; set; } = 1;

    public string ManifestType { get; set; } = "Package";

    public PackageManifestAsset[] Assets { get; set; } = [];

    public StaticWebAssetEndpoint[] Endpoints { get; set; } = [];
}

/// <summary>
/// Represents a static web asset entry in the package manifest.
/// Paths are package-relative (resolved at consumer read time using PackageRoot).
/// </summary>
public class PackageManifestAsset
{
    /// <summary>
    /// Package-relative path to the asset file on disk (e.g., "staticwebassets/css/site.css").
    /// This is the resolved path (tokens replaced) computed by ComputeTargetPath at pack time.
    /// Resolved against PackageRoot at consumer read time to produce the asset's Identity.
    /// </summary>
    public string PackagePath { get; set; }

    /// <summary>
    /// Relative path with token expressions preserved (e.g., "css/site#[.{fingerprint}]?.css").
    /// Used as the RelativePath metadata on the emitted MSBuild item.
    /// </summary>
    public string RelativePath { get; set; }

    /// <summary>
    /// URL prefix under which the asset is served (e.g., "_content/MyLib").
    /// </summary>
    public string BasePath { get; set; }

    /// <summary>
    /// How the asset was discovered: "Package" or "Framework".
    /// </summary>
    public string SourceType { get; set; }

    public string AssetKind { get; set; }
    public string AssetMode { get; set; }
    public string AssetRole { get; set; }

    /// <summary>
    /// Package-relative path to the related primary asset (for Alternative/Related roles).
    /// Empty string for Primary assets.
    /// </summary>
    public string RelatedAsset { get; set; }

    public string AssetTraitName { get; set; }
    public string AssetTraitValue { get; set; }

    /// <summary>
    /// Semicolon-delimited group requirements (e.g., "BootstrapVersion=V5").
    /// Empty string for ungrouped assets.
    /// </summary>
    public string AssetGroups { get; set; }

    public string Fingerprint { get; set; }
    public string Integrity { get; set; }

    public string CopyToOutputDirectory { get; set; }
    public string CopyToPublishDirectory { get; set; }

    public string FileLength { get; set; }
    public string LastWriteTime { get; set; }
}
