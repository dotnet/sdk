// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    public partial class GenerateServiceWorkerAssetsManifest : Task
    {
        private static readonly JsonSerializerOptions ManifestSerializationOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        [Required]
        public ITaskItem[] Assets { get; set; }

        public string Version { get; set; }

        [Required]
        public string OutputPath { get; set; }

        [Output]
        public string CalculatedVersion { get; set; }

        public override bool Execute()
        {
            CalculatedVersion = GenerateAssetManifest();
            return !Log.HasLoggedErrors;
        }

        private string GenerateAssetManifest()
        {
            var assets = Assets.Select(a => (StaticWebAsset.FromTaskItem(a), a.GetMetadata("AssetUrl"))).ToArray();
            var entries = new ManifestEntry[Assets.Length];
            for (var i = 0; i < Assets.Length; i++)
            {
                var (asset, url) = assets[i];
                var hash = asset.Integrity;
                entries[i] = new ManifestEntry
                {
                    Hash = $"sha256-{hash}",
                    Url = url,
                };
            }

            Array.Sort(entries, (a, b) =>
            {
                int urlComparison = string.Compare(a.Url, b.Url, StringComparison.Ordinal);
                if (urlComparison != 0)
                {
                    return urlComparison;
                }
                return string.Compare(a.Hash, b.Hash, StringComparison.Ordinal);
            });
            var version = !string.IsNullOrEmpty(Version) ? Version : ComputeVersion(entries);

            var manifest = new ServiceWorkerManifest
            {
                Version = version,
                Assets = entries,
            };

            PersistManifest(manifest);
            return version;
        }

        private static string ComputeVersion(ManifestEntry [] assets)
        {
                // If a version isn't specified (which is likely the most common case), construct a Version by combining
                // the file names + hashes of all the inputs.

                var combinedHash = string.Join(
                    Environment.NewLine,
                    assets.OrderBy(f => f.Url, StringComparer.Ordinal).Select(f => f.Hash));

                using var sha = SHA256.Create();
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(combinedHash));
                var version = Convert.ToBase64String(bytes).Substring(0, 8);

            return version;
        }

        private void PersistManifest(ServiceWorkerManifest manifest)
        {
            var data = JsonSerializer.Serialize(manifest, ManifestSerializationOptions);
            var content = $"self.assetsManifest = {data};{Environment.NewLine}";
            var contentHash = ComputeFileHash(content);
            var fileExists = File.Exists(OutputPath);
            var existingManifestHash = fileExists ? ComputeFileHash(File.ReadAllText(OutputPath)) : "";

            if (!fileExists)
            {
                Log.LogMessage(MessageImportance.Low, $"Creating manifest with content hash '{contentHash}' because manifest file '{OutputPath}' does not exist.");
                File.WriteAllText(OutputPath, content);
            }
            else if (!string.Equals(contentHash, existingManifestHash, StringComparison.Ordinal))
            {
                Log.LogMessage(MessageImportance.Low, $"Updating manifest because manifest hash '{contentHash}' is different from existing manifest hash '{existingManifestHash}'.");
                File.WriteAllText(OutputPath, content);
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, $"Skipping manifest updated because manifest hash '{contentHash}' has not changed.");
            }
        }

        private string ComputeFileHash(string contents)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(contents));
            return Convert.ToBase64String(bytes);
        }

        private class ServiceWorkerManifest
        {
            public string Version { get; set; }
            public ManifestEntry[] Assets { get; set; }
        }

        private class ManifestEntry
        {
            public string Hash { get; set; }
            public string Url { get; set; }
        }
    }

}
