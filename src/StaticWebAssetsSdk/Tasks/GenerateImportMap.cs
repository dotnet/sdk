// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Sdk.StaticWebAssets;

public class GenerateImportMap : Task
{
    [Required]
    public string ManifestPath { get; set; } = string.Empty;

    [Output]
    public string ImportMap { get; set; } = string.Empty;

    public override bool Execute()
    {
        var manifest = StaticAssetsManifest.Parse(ManifestPath);
        Log.LogMessage($"Manifest parsed 'v{manifest.Version}' endpoints '{manifest.Endpoints.Count}'");

        var resources = ResourceCollectionResolver.ResolveResourceCollection(manifest);
        var importMap = ImportMapDefinition.FromResourceCollection(resources);

        ImportMap = importMap.ToJson();

        return true;
    }
}
