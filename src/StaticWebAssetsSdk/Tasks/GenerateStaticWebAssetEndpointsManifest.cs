// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.NET.Sdk.StaticWebAssets.Tasks;
using Microsoft.NET.Sdk.StaticWebAssets.Utils;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class GenerateStaticWebAssetEndpointsManifest : Task
{
    [Required]
    public ITaskItem[] Endpoints { get; set; } = [];

    [Required]
    public string ManifestPath { get; set; }

    public override bool Execute()
    {
        try
        {
            if (Endpoints.Length > 0)
            {
                var manifest = new StaticWebAssetEndpointsManifest()
                {
                    Version = 1,
                    Endpoints = StaticWebAssetEndpoint.FromItemGroup(Endpoints)
                };

                this.PersistFileIfChanged(manifest, ManifestPath);
            }
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, showStackTrace: true, showDetail: true, null);
            return false;
        }

        return !Log.HasLoggedErrors;
    }
}
