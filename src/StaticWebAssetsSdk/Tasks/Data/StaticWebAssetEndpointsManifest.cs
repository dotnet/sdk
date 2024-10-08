// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.StaticWebAssets.Tasks;

public class StaticWebAssetEndpointsManifest()
{
    public int Version { get; set; }

    public string ManifestType { get; set; }

    public StaticWebAssetEndpoint[] Endpoints { get; set; }
}
