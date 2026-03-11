// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class StaticWebAssetPackageManifest
{
    public int Version { get; set; } = 1;

    public string ManifestType { get; set; } = "Package";

    public PackageManifestAsset[] Assets { get; set; } = [];

    public StaticWebAssetEndpoint[] Endpoints { get; set; } = [];
}

public class PackageManifestAsset
{
    public string PackagePath { get; set; }

    public string RelativePath { get; set; }

    public string BasePath { get; set; }

    public string SourceType { get; set; }

    public string AssetKind { get; set; }

    public string AssetMode { get; set; }

    public string AssetRole { get; set; }

    public string RelatedAsset { get; set; }

    public string AssetTraitName { get; set; }

    public string AssetTraitValue { get; set; }

    public string AssetGroups { get; set; }

    public string Fingerprint { get; set; }

    public string Integrity { get; set; }

    public string CopyToOutputDirectory { get; set; }

    public string CopyToPublishDirectory { get; set; }

    public string FileLength { get; set; }

    public string LastWriteTime { get; set; }
}
