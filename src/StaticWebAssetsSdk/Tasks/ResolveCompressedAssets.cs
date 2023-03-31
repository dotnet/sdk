// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.NET.Sdk.WebAssembly;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class ResolveCompressedAssets : Task
{
    private static readonly char[] s_invalidPathChars = Path.GetInvalidFileNameChars();

    public ITaskItem[] CandidateAssets { get; set; }

    public ITaskItem[] CompressionConfigurations { get; set; }

    public ITaskItem[] ExplicitAssets { get; set; }

    [Output]
    public ITaskItem[] AssetsToCompress { get; set; }

    public override bool Execute()
    {
        if (CandidateAssets is null || CompressionConfigurations is null)
        {
            return true;
        }

        var assetsToCompress = new List<AssetToCompress>();

        // Scan the candidate assets and determine which assets have already been compressed in which formats.
        var alreadyCompressedAssetFormatsByItemSpec = new Dictionary<string, HashSet<string>>();
        foreach (var asset in CandidateAssets)
        {
            if (asset.GetMetadata("AssetTraitName") == "Content-Encoding")
            {
                var assetTraitValue = asset.GetMetadata("AssetTraitValue");
                var relatedAssetItemSpec = asset.GetMetadata("RelatedAsset");

                if (!alreadyCompressedAssetFormatsByItemSpec.TryGetValue(relatedAssetItemSpec, out var formats))
                {
                    formats = new();
                    alreadyCompressedAssetFormatsByItemSpec.Add(relatedAssetItemSpec, formats);
                }

                formats.Add(assetTraitValue);
            }
        }

        // Generate internal representations of each compression configuration.
        var compressionConfigurations = CompressionConfigurations
            .Select(CompressionConfiguration.FromTaskItem)
            .ToArray();
        var candidateAssetsByConfigurationName = compressionConfigurations
            .ToDictionary(cc => cc.ItemSpec, cc => (cc, new List<AssetToCompress>()));

        // Add each explicitly-provided asset as a candidate asset for its specified compression configuration.
        if (ExplicitAssets is not null)
        {
            foreach (var asset in ExplicitAssets)
            {
                var configurationName = asset.GetMetadata("ConfigurationName");
                if (candidateAssetsByConfigurationName.TryGetValue(configurationName, out var result))
                {
                    var (configuration, candidateAssets) = result;
                    var relativePath = StaticWebAsset.ComputeAssetRelativePath(asset, out _);
                    candidateAssets.Add(new(asset, configuration, relativePath));
                }
            }
        }

        // Add each candidate asset to each compression configuration with a matching pattern.
        foreach (var asset in CandidateAssets)
        {
            if (asset.GetMetadata("AssetTraitName") == "Content-Encoding")
            {
                // This is a compressed asset - no need to compress again.
                continue;
            }

            var relativePath = StaticWebAsset.ComputeAssetRelativePath(asset, out _);

            foreach (var configuration in compressionConfigurations)
            {
                if (configuration.Matches(relativePath))
                {
                    var (_, candidateAssets) = candidateAssetsByConfigurationName[configuration.ItemSpec];
                    candidateAssets.Add(new(asset, configuration, relativePath));
                }
            }
        }

        // Process the final set of candidate assets, deduplicating assets compressed in the same format multiple times.
        foreach (var configuration in compressionConfigurations)
        {
            var (_, candidateAssets) = candidateAssetsByConfigurationName[configuration.ItemSpec];

            foreach (var asset in candidateAssets)
            {
                var itemSpec = asset.StaticWebAsset.ItemSpec;
                if (!alreadyCompressedAssetFormatsByItemSpec.TryGetValue(itemSpec, out var alreadyCompressedFormats))
                {
                    alreadyCompressedFormats = new();
                }

                var format = configuration.Format;
                if (alreadyCompressedFormats.Contains(format))
                {
                    Log.LogMessage($"Skipping '{itemSpec}' because it was already compressed with format '{format}'");
                    continue;
                }

                assetsToCompress.Add(asset);
                alreadyCompressedFormats.Add(format);
            }
        }

        // Generate new static web asset definitions for the compressed items.
        var compressedAssets = new List<ITaskItem>();

        foreach (var asset in assetsToCompress)
        {
            var staticWebAsset = asset.StaticWebAsset;
            var originalItemSpec = staticWebAsset.GetMetadata("OriginalItemSpec");
            compressedAssets.Add(CreateCompressedAsset(asset, originalItemSpec));
        }

        AssetsToCompress = compressedAssets.ToArray();

        return !Log.HasLoggedErrors;
    }

    private TaskItem CreateCompressedAsset(AssetToCompress asset, string originalItemSpec)
    {
        var originalAsset = asset.StaticWebAsset;
        var relativePath = originalAsset.GetMetadata("RelativePath");
        var targetDirectory = asset.CompressionConfiguration.TargetDirectory;
        var format = asset.CompressionConfiguration.Format;

        // TODO: Clean up this method.
        string fileExtension;
        string assetTraitValue;

        if (string.Equals("gzip", format, StringComparison.OrdinalIgnoreCase))
        {
            fileExtension = ".gz";
            assetTraitValue = "gzip";
        }
        else if (string.Equals("brotli", format, StringComparison.OrdinalIgnoreCase))
        {
            fileExtension = ".br";
            assetTraitValue = "br";
        }
        else
        {
            fileExtension = ".gz";
            assetTraitValue = "gzip";
            Log.LogError($"Unknown compression format '{format}'. Defaulting to 'gzip'.");
        }

        var fileName = FileHasher.GetFileHash(originalItemSpec) + fileExtension;
        var outputRelativePath = Path.Combine(targetDirectory, fileName);

        var result = new TaskItem(outputRelativePath, originalAsset.CloneCustomMetadata());

        result.SetMetadata("RelativePath", relativePath + fileExtension);
        result.SetMetadata("RelatedAsset", originalAsset.ItemSpec);
        result.SetMetadata("OriginalItemSpec", originalAsset.ItemSpec);
        result.SetMetadata("AssetRole", "Alternative");
        result.SetMetadata("AssetTraitName", "Content-Encoding");
        result.SetMetadata("AssetTraitValue", assetTraitValue);
        result.SetMetadata("TargetDirectory", targetDirectory);
        result.SetMetadata("Format", format);

        return result;
    }

    private sealed class CompressionConfiguration
    {
        private readonly Matcher _matcher = new();

        public string ItemSpec { get; set; }

        public string TargetDirectory { get; }

        public string Format { get; }

        public static CompressionConfiguration FromTaskItem(ITaskItem taskItem)
        {
            var itemSpec = taskItem.ItemSpec;
            var includePattern = taskItem.GetMetadata("IncludePattern");
            var excludePattern = taskItem.GetMetadata("ExcludePattern");
            var targetDirectory = taskItem.GetMetadata("TargetDirectory");
            var format = taskItem.GetMetadata("Format");

            var includePatterns = SplitPattern(includePattern);
            var excludePatterns = SplitPattern(excludePattern);

            return new(itemSpec, includePatterns, excludePatterns, targetDirectory, format);

            static string[] SplitPattern(string pattern)
                => pattern
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .ToArray();
        }

        private CompressionConfiguration(string itemSpec, string[] includePatterns, string[] excludePatterns, string targetDirectory, string format)
        {
            _matcher.AddIncludePatterns(includePatterns);
            _matcher.AddExcludePatterns(excludePatterns);

            ItemSpec = itemSpec;
            TargetDirectory = targetDirectory;
            Format = format;
        }

        public bool Matches(string relativePath)
            => _matcher.Match(relativePath).HasMatches;
    }

    private sealed class AssetToCompress
    {
        public ITaskItem StaticWebAsset { get; }

        public string RelativePath { get; }

        public CompressionConfiguration CompressionConfiguration { get; }

        public AssetToCompress(ITaskItem staticWebAsset, CompressionConfiguration compressionConfiguration, string relativePath)
        {
            StaticWebAsset = staticWebAsset;
            CompressionConfiguration = compressionConfiguration;
            RelativePath = relativePath;
        }
    }
}
