// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class GenerateStaticWebAssetsManifest : Task
    {
        [Required]
        public string Source { get; set; }

        [Required]
        public string BasePath { get; set; }

        [Required]
        public string Mode { get; set; }

        [Required]
        public string ManifestType { get; set; }

        [Required]
        public ITaskItem[] RelatedManifests { get; set; }

        [Required]
        public ITaskItem[] DiscoveryPatterns { get; set; }

        [Required]
        public ITaskItem[] Assets { get; set; }

        [Required]
        public string ManifestPath { get; set; }

        public override bool Execute()
        {
            try
            {
                var assets = Assets.OrderBy(a => a.GetMetadata("FullPath")).Select(StaticWebAsset.FromTaskItem).ToArray();
                var relatedManifests = RelatedManifests.OrderBy(a => a.GetMetadata("FullPath"))
                    .Select(ComputeManifestReference)
                    .Where(r => r != null)
                    .ToArray();

                if (Log.HasLoggedErrors)
                {
                    return false;
                }

                var discoveryPatterns = DiscoveryPatterns.OrderBy(a => a.ItemSpec).Select(ComputeDiscoveryPattern).ToArray();

                var manifest = new StaticWebAssetsManifest(Source, BasePath, Mode, ManifestType, relatedManifests, discoveryPatterns, assets);
                PersistManifest(manifest);
            }
            catch (Exception ex)
            {
                Log.LogError(ex.ToString());
                Log.LogErrorFromException(ex);
            }
            return !Log.HasLoggedErrors;
        }

        private StaticWebAssetsManifest.DiscoveryPattern ComputeDiscoveryPattern(ITaskItem pattern)
        {
            var name = pattern.ItemSpec;
            var contentRoot = pattern.GetMetadata("ContentRoot");
            var basePath = pattern.GetMetadata("BasePath");
            var glob = pattern.GetMetadata("Pattern");

            return new StaticWebAssetsManifest.DiscoveryPattern(name, contentRoot, basePath, glob);
        }

        private StaticWebAssetsManifest.ManifestReference ComputeManifestReference(ITaskItem reference)
        {
            var identity = reference.GetMetadata("FullPath");
            var source = reference.GetMetadata("Source");
            var manifestType = reference.GetMetadata("ManifestType");

            if (!File.Exists(identity))
            {
                if (!string.Equals(manifestType, StaticWebAssetsManifest.ManifestTypes.Publish, StringComparison.OrdinalIgnoreCase))
                {
                    Log.LogError("Manifest '{0}' for project '{1}' with type '{2}' does not exist.", identity, source, manifestType);
                }

                return null;
            }

            var relatedManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(identity));

            return new StaticWebAssetsManifest.ManifestReference(identity, source, manifestType, relatedManifest.Hash);
        }

        private void PersistManifest(StaticWebAssetsManifest manifest)
        {
            var data = JsonSerializer.SerializeToUtf8Bytes(manifest);
            var fileExists = File.Exists(ManifestPath);
            var existingManifestHash = fileExists ? StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(ManifestPath)).Hash : "";

            if (!fileExists)
            {
                Log.LogMessage($"Creating manifest because manifest file '{ManifestPath}' does not exist.");
                File.WriteAllBytes(ManifestPath, data);
            }
            else if (!string.Equals(manifest.Hash, existingManifestHash, StringComparison.Ordinal))
            {
                Log.LogMessage($"Updating manifest because manifest version '{manifest.Hash}' is different from existing manifest hash '{existingManifestHash}'.");
                File.WriteAllBytes(ManifestPath, data);
            }
            else
            {
                Log.LogMessage($"Skipping manifest updated because manifest version '{manifest.Hash}' has not changed.");
            }
        }
    }
}
