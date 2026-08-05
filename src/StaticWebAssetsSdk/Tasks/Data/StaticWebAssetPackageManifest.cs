// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class StaticWebAssetPackageManifest
{
    public const int CurrentVersion = 1;
    public const string PackageManifestType = "Package";

    public int Version { get; set; } = CurrentVersion;

    public string ManifestType { get; set; } = PackageManifestType;

    // Key: package-relative path (for example, staticwebassets/css/site.css)
    // Value: static web asset metadata.
    public Dictionary<string, StaticWebAsset> Assets { get; set; } = [];

    public StaticWebAssetEndpoint[] Endpoints { get; set; } = [];
}
