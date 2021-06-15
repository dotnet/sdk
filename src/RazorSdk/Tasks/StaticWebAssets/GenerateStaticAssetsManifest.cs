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
            var assets = Assets.OrderBy(a => a.GetMetadata("FullPath")).Select(StaticWebAsset.FromTaskItem).ToArray();
            var relatedManifests = RelatedManifests.OrderBy(a => a.GetMetadata("FullPath")).Select(ComputeManifestReference).ToArray();
            var discoveryPatterns = DiscoveryPatterns.OrderBy(a => a.ItemSpec).Select(ComputeDiscoveryPattern).ToArray();

            var manifest = new StaticWebAssetsManifest(Source, BasePath, Mode, ManifestType, relatedManifests, discoveryPatterns, assets);
            PersistManifest(manifest);

            return true;
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

            if (!File.Exists(identity))
            {
                throw new InvalidOperationException($"Manifest '{identity}' doesn't exist.");
            }

            var relatedManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(identity));

            return new StaticWebAssetsManifest.ManifestReference(identity, relatedManifest.Hash, relatedManifest.Source, relatedManifest.ManifestType);
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
