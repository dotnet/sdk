// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class StaticWebAssetPackageManifest
{
    public int Version { get; set; } = 1;

    public string ManifestType { get; set; } = "Package";

    // Key: package-relative path (for example, staticwebassets/css/site.css)
    // Value: static web asset metadata.
    public Dictionary<string, StaticWebAsset> Assets { get; set; } = [];

    public StaticWebAssetEndpoint[] Endpoints { get; set; } = [];
}
