// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class ComputeReferencedProjectAssets : Task
    {
        [Required]
        public ITaskItem[] Manifests { get; set; }


        [Required]
        public string AssetKind { get; set; }

        [Output]
        public ITaskItem[] StaticWebAssets { get; set; }

        public override bool Execute()
        {
            try
            {
                var manifests = new StaticWebAssetsManifest[Manifests.Length];
                for (var i = 0; i < Manifests.Length; i++)
                {
                    var manifest = Manifests[i];
                    if (!File.Exists(manifest.ItemSpec))
                    {
                        Log.LogError($"Manifest file '{manifest.ItemSpec}' does not exist.");
                        break;
                    }

                    manifests[i] = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(manifest.ItemSpec));
                }

                var staticWebAssets = new Dictionary<string, StaticWebAsset>();

                foreach (var manifest in manifests)
                {
                    foreach (var asset in manifest.Assets)
                    {
                        if (asset.SourceType == StaticWebAsset.SourceTypes.Discovered ||
                            asset.SourceType == StaticWebAsset.SourceTypes.Computed)
                        {
                            asset.SourceType = StaticWebAsset.SourceTypes.Project;
                        }

                        if (staticWebAssets.TryGetValue(asset.Identity, out var existing))
                        {
                            if(!asset.Equals(existing))
                            {
                                throw new InvalidOperationException($"Found conflicting definitions for asset {asset.Identity}");
                            }
                            else
                            {
                                break;
                            }
                        }

                        staticWebAssets.Add(asset.Identity, asset);
                    }
                }

                StaticWebAssets = staticWebAssets.Select(a => a.Value.ToTaskItem()).ToArray();
            }
            catch (Exception ex)
            {
                Log.LogError(ex.Message);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
