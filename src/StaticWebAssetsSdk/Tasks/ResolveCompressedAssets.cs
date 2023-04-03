// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.NET.Sdk.WebAssembly;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class ResolveCompressedAssets : Task
{
    public ITaskItem[] CandidateAssets { get; set; }

    public ITaskItem[] CompressionConfigurations { get; set; }

    public ITaskItem[] ExplicitAssets { get; set; }

    [Required]
    public string OutputBasePath { get; set; }

    [Output]
    public ITaskItem[] AssetsToCompress { get; set; }

    public override bool Execute()
    {
        if (CandidateAssets is null)
        {
            Log.LogMessage(
                MessageImportance.Low,
                "Skipping task '{0}' because no candidate assets for compression were specified.",
                nameof(ResolveCompressedAssets));
            return true;
        }

        if (CompressionConfigurations is null)
        {
            Log.LogMessage(
                MessageImportance.Low,
                "Skipping task '{0}' because no compression configurations were specified.",
                nameof(ResolveCompressedAssets));
            return true;
        }

        // Scan the provided candidate assets and determine which ones have already been compressed and in which formats.
        var existingCompressionFormatsByAssetItemSpec = new Dictionary<string, HashSet<string>>();
        foreach (var asset in CandidateAssets)
        {
            if (IsCompressedAsset(asset))
            {
                var relatedAssetItemSpec = asset.GetMetadata("RelatedAsset");

                if (string.IsNullOrEmpty(relatedAssetItemSpec))
                {
                    Log.LogError(
                        "The asset '{0}' was detected as compressed but didn't specify a related asset.",
                        asset.ItemSpec);
                    continue;
                }

                if (!existingCompressionFormatsByAssetItemSpec.TryGetValue(relatedAssetItemSpec, out var existingFormats))
                {
                    existingFormats = new();
                    existingCompressionFormatsByAssetItemSpec.Add(relatedAssetItemSpec, existingFormats);
                }

                var compressionFormat = asset.GetMetadata("AssetTraitValue");

                if (!CompressionFormat.IsValidCompressionFormat(compressionFormat))
                {
                    Log.LogError(
                        "The asset '{0}' has an unknown compression format '{1}'.",
                        asset.ItemSpec,
                        compressionFormat);
                }

                existingFormats.Add(compressionFormat);
            }
        }

        // Generate internal representations of each compression configuration.
        var compressionConfigurations = CompressionConfigurations
            .Select(CompressionConfiguration.FromTaskItem)
            .ToArray();
        var candidateAssetsByConfigurationName = compressionConfigurations
            .ToDictionary(cc => cc.ItemSpec, _ => new List<ITaskItem>());

        // Add each explicitly-provided asset as a candidate asset for its specified compression configuration.
        if (ExplicitAssets is not null)
        {
            foreach (var asset in ExplicitAssets)
            {
                var configurationName = asset.GetMetadata("ConfigurationName");
                if (candidateAssetsByConfigurationName.TryGetValue(configurationName, out var candidateAssets))
                {
                    candidateAssets.Add(asset);
                    Log.LogMessage(
                        "Explicitly-specified compressed asset '{0}' matches known compression configuration '{1}'.",
                        asset.ItemSpec,
                        configurationName);
                }
                else
                {
                    Log.LogError(
                        "Explicitly-specified compressed asset '{0}' has an unknown compression configuration '{1}'.",
                        asset.ItemSpec,
                        configurationName);
                }
            }
        }

        // Add each candidate asset to each compression configuration with a matching pattern.
        foreach (var asset in CandidateAssets)
        {
            if (IsCompressedAsset(asset))
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    "Ignoring asset '{0}' for compression because it is already compressed.",
                    asset.ItemSpec);
                continue;
            }

            var relativePath = asset.GetMetadata("RelativePath");

            foreach (var configuration in compressionConfigurations)
            {
                if (configuration.Matches(relativePath))
                {
                    var candidateAssets = candidateAssetsByConfigurationName[configuration.ItemSpec];
                    candidateAssets.Add(asset);

                    Log.LogMessage(
                        MessageImportance.Low,
                        "Asset '{0}' with relative path '{1}' matched compression configuration '{2}' with include pattern '{3}' and exclude pattern '{4}'.",
                        asset.ItemSpec,
                        relativePath,
                        configuration.ItemSpec,
                        configuration.IncludePattern,
                        configuration.ExcludePattern);
                }
                else
                {
                    Log.LogMessage(
                        MessageImportance.Low,
                        "Asset '{0}' with relative path '{1}' did not match compression configuration '{2}' with include pattern '{3}' and exclude pattern '{4}'.",
                        asset.ItemSpec,
                        relativePath,
                        configuration.ItemSpec,
                        configuration.IncludePattern,
                        configuration.ExcludePattern);
                }
            }
        }

        // Process the final set of candidate assets, deduplicating assets compressed in the same format multiple times and
        // generating new a static web asset definition for each compressed item.
        var compressedAssets = new List<ITaskItem>();
        foreach (var configuration in compressionConfigurations)
        {
            var candidateAssets = candidateAssetsByConfigurationName[configuration.ItemSpec];
            var targetDirectory = configuration.ComputeOutputPath(OutputBasePath);

            foreach (var asset in candidateAssets)
            {
                var itemSpec = asset.ItemSpec;
                if (!existingCompressionFormatsByAssetItemSpec.TryGetValue(itemSpec, out var alreadyCompressedFormats))
                {
                    alreadyCompressedFormats = new();
                }

                var format = configuration.Format;
                if (alreadyCompressedFormats.Contains(format))
                {
                    Log.LogMessage(
                        "Ignoring asset '{0}' because it was already compressed with format '{1}'.",
                        itemSpec,
                        format);
                    continue;
                }

                if (TryCreateCompressedAsset(asset, targetDirectory, configuration.Format, out var compressedAsset))
                {
                    compressedAssets.Add(compressedAsset);
                    alreadyCompressedFormats.Add(format);

                    Log.LogMessage(
                        "Created compressed asset '{0}'.",
                        compressedAsset.ItemSpec);
                }
                else
                {
                    Log.LogError(
                        "Could not create compressed asset for original asset '{0}'.",
                        itemSpec);
                }
            }
        }

        AssetsToCompress = compressedAssets.ToArray();

        return !Log.HasLoggedErrors;
    }

    private static bool IsCompressedAsset(ITaskItem asset)
        => string.Equals("Content-Encoding", asset.GetMetadata("AssetTraitName"));

    private bool TryCreateCompressedAsset(ITaskItem asset, string targetDirectory, string format, out TaskItem result)
    {
        string fileExtension;
        string assetTraitValue;

        if (CompressionFormat.IsGzip(format))
        {
            fileExtension = ".gz";
            assetTraitValue = "gzip";
        }
        else if (CompressionFormat.IsBrotli(format))
        {
            fileExtension = ".br";
            assetTraitValue = "br";
        }
        else
        {
            Log.LogError($"Unknown compression format '{format}' for '{asset.ItemSpec}'.");
            result = null;
            return false;
        }

        var originalItemSpec = asset.GetMetadata("OriginalItemSpec");
        var relativePath = asset.GetMetadata("RelativePath");

        var fileName = FileHasher.GetFileHash(originalItemSpec) + fileExtension;
        var outputRelativePath = Path.Combine(targetDirectory, fileName);

        result = new TaskItem(outputRelativePath, asset.CloneCustomMetadata());

        result.SetMetadata("RelativePath", relativePath + fileExtension);
        result.SetMetadata("RelatedAsset", asset.ItemSpec);
        result.SetMetadata("OriginalItemSpec", asset.ItemSpec);
        result.SetMetadata("AssetRole", "Alternative");
        result.SetMetadata("AssetTraitName", "Content-Encoding");
        result.SetMetadata("AssetTraitValue", assetTraitValue);
        result.SetMetadata("TargetDirectory", targetDirectory);

        return true;
    }

    private static class BuildStage
    {
        public const string Build = nameof(Build);
        public const string Publish = nameof(Publish);

        public static bool IsBuild(string buildStage) => string.Equals(Build, buildStage, StringComparison.OrdinalIgnoreCase);
        public static bool IsPublish(string buildStage) => string.Equals(Publish, buildStage, StringComparison.OrdinalIgnoreCase);
        public static bool IsValidBuildStage(string buildStage)
            => IsBuild(buildStage)
            || IsPublish(buildStage);
    }

    private static class CompressionFormat
    {
        public const string Gzip = nameof(Gzip);
        public const string Brotli = nameof(Brotli);

        public static bool IsGzip(string format) => string.Equals(Gzip, format, StringComparison.OrdinalIgnoreCase);
        public static bool IsBrotli(string format) => string.Equals(Brotli, format, StringComparison.OrdinalIgnoreCase);
        public static bool IsValidCompressionFormat(string format)
            => IsGzip(format)
            || IsBrotli(format);
    }

    private sealed class CompressionConfiguration
    {
        private readonly Matcher _matcher = new();

        public string ItemSpec { get; set; }

        public string IncludePattern { get; set; }

        public string ExcludePattern { get; set; }

        public string Format { get; }

        public string Stage { get; }

        public static CompressionConfiguration FromTaskItem(ITaskItem taskItem)
        {
            var itemSpec = taskItem.ItemSpec;
            var includePattern = taskItem.GetMetadata("IncludePattern");
            var excludePattern = taskItem.GetMetadata("ExcludePattern");
            var format = taskItem.GetMetadata("Format");
            var stage = taskItem.GetMetadata("Stage");

            if (!CompressionFormat.IsValidCompressionFormat(format))
            {
                throw new InvalidOperationException($"Unknown compression format '{format}' for the compression configuration '{itemSpec}'.");
            }

            if (!BuildStage.IsValidBuildStage(stage))
            {
                throw new InvalidOperationException($"Unknown build stage '{stage}' for the compression configuration '{itemSpec}'.");
            }

            return new(itemSpec, includePattern, excludePattern, format, stage);
        }

        private CompressionConfiguration(string itemSpec, string includePattern, string excludePattern, string format, string stage)
        {
            ItemSpec = itemSpec;
            IncludePattern = includePattern;
            ExcludePattern = excludePattern;
            Format = format;
            Stage = stage;

            var includePatterns = SplitPattern(includePattern);
            var excludePatterns = SplitPattern(excludePattern);
            _matcher.AddIncludePatterns(includePatterns);
            _matcher.AddExcludePatterns(excludePatterns);

            static string[] SplitPattern(string pattern)
                => pattern
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .ToArray();
        }

        public string ComputeOutputPath(string outputBasePath)
        {
            if (ComputeOutputSubdirectory() is { } outputSubdirectory)
            {
                return Path.Combine(outputBasePath, outputSubdirectory);
            }

            throw new InvalidOperationException($"Could not compute the output subdirectory for compression configuration '{ItemSpec}'.");

            string ComputeOutputSubdirectory()
            {
                // TODO: Let's change the output directory to be compressed\[publish]\
                if (BuildStage.IsBuild(Stage))
                {
                    if (CompressionFormat.IsGzip(Format))
                    {
                        return "build-gz";
                    }

                    if (CompressionFormat.IsBrotli(Format))
                    {
                        return "build-br";
                    }

                    return null;
                }

                if (BuildStage.IsPublish(Stage))
                {
                    return "compress";
                }

                return null;
            }
        }

        public bool Matches(string relativePath)
            => _matcher.Match(relativePath).HasMatches;
    }
}
