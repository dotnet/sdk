// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.UnifiedBuild.Tasks.ManifestAssets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.DotNet.UnifiedBuild.Tasks;

public class JoinVerticals : Microsoft.Build.Utilities.Task
{
    /// <summary>
    /// Paths to Verticals Manifests
    /// </summary>
    [Required]
    public required ITaskItem[] VerticalManifest { get; init; }

    /// <summary>
    /// Location of downloaded artifacts from the main vertical
    /// </summary>
    [Required]
    public required string VerticalArtifactsBaseFolder { get; init; }

    /// <summary>
    /// Folder where packages and assets will be stored
    /// </summary>
    [Required]
    public required string OutputFolder { get; init; }

    private const string _assetsFolderName = "assets";
    private const string _packagesFolderName = "packages";
    private const string _releaseFolderName = "Release";

    public override bool Execute()
    {
        List<BuildAssetsManifest> manifests = VerticalManifest.Select(xmlPath => BuildAssetsManifest.LoadFromFile(xmlPath.ItemSpec)).ToList();
        BuildAssetsManifest mainVerticalManifest = manifests
            .FirstOrDefault()
            ?? throw new ArgumentException($"Vertical manifest list was empty");

        JoinVerticalsAssetSelector joinVerticalsAssetSelector = new();

        List<AssetVerticalMatchResult> selectedVerticals = joinVerticalsAssetSelector.SelectAssetMatchingVertical(manifests).ToList();

        var notMatchedAssets = selectedVerticals.Where(o => o.MatchType == AssetVerticalMatchType.NotSpecified).ToList();
        if (notMatchedAssets.Count > 0)
        {
            Log.LogError($"### {notMatchedAssets.Count} Assets not properly matched to vertical: ###");
            foreach (var matchResult in notMatchedAssets)
            {
                Log.LogMessage(MessageImportance.High, $"Asset: {matchResult.AssetId} -- Matched to: {matchResult.VerticalName}, Other verticals: {string.Join(", ", matchResult.OtherVerticals)}");
            }
        }
        // save manifest and download all the assets
        string packagesOutputDirectory = Path.Combine(OutputFolder, _packagesFolderName);
        ForceDirectory(packagesOutputDirectory);
        string assetsOutputDirectory = Path.Combine(OutputFolder, _assetsFolderName);
        ForceDirectory(assetsOutputDirectory);

        XDocument mergedManifest = JoinVerticalsManifestExportHelper.ExportMergedManifest(mainVerticalManifest, selectedVerticals);
        string manifestOutputAssetsPath = Path.Combine(assetsOutputDirectory, "VerticalsMergeManifest.xml");
        mergedManifest.Save(manifestOutputAssetsPath);
        string manifestOutputPath = Path.Combine(OutputFolder, "MergedManifest.xml");
        mergedManifest.Save(manifestOutputPath);

        foreach (var matchResult in selectedVerticals.GroupBy(o => o.VerticalName).OrderByDescending(g => g.Count()))
        {
            string verticalName = matchResult.Key;

            // go through all verticals and assets inside and download them
            var assetListPackages = matchResult
                .Where(o => o.Asset.AssetType == ManifestAssetType.Package)
                .Select(figureOutFileName)
                .ToList();
            var assetListBlobs = matchResult
                .Where(o => o.Asset.AssetType == ManifestAssetType.Blob)
                .Select(figureOutFileName)
                .ToList();

            CopyVerticalAssets(Path.Combine(VerticalArtifactsBaseFolder, verticalName, _packagesFolderName, _releaseFolderName), packagesOutputDirectory, assetListPackages);
            CopyVerticalAssets(Path.Combine(VerticalArtifactsBaseFolder, verticalName, _assetsFolderName, _releaseFolderName), assetsOutputDirectory, assetListBlobs);
        }

        return !Log.HasLoggedErrors;


        static (AssetVerticalMatchResult matchResult, string fileName) figureOutFileName(AssetVerticalMatchResult matchResult)
        {
            string fileName = matchResult.Asset.AssetType switch
            {
                ManifestAssetType.Package => $"{matchResult.Asset.Id}.{matchResult.Asset.Version}.nupkg",
                ManifestAssetType.Blob => matchResult.Asset.Id,
                _ => throw new ArgumentException($"Unknown asset type {matchResult.Asset.AssetType}")
            };
            return (matchResult, fileName);
        }
    }

    private static void ForceDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// Copy assets for a vertical from the source directory to the destination directory in a flat layout
    /// </summary>
    private void CopyVerticalAssets(string sourceDirectory, string destinationDirectory,
        IEnumerable<(AssetVerticalMatchResult matchResult, string fileName)> assets)
    {
        foreach (var sourceFile in assets)
        {
            string destinationFilePath = Path.Combine(destinationDirectory, Path.GetFileName(sourceFile.fileName));
            Log.LogMessage(MessageImportance.High, $"Copying {sourceFile} to {destinationFilePath}");

            if (sourceFile.matchResult.Asset.AssetType == ManifestAssetType.Package)
            {
                string shippingSubdir = sourceFile.matchResult.Asset.NonShipping
                    ? "NonShipping"
                    : "Shipping";
                File.Copy(Path.Combine(sourceDirectory, shippingSubdir, sourceFile.matchResult.Asset.RepoOrigin ?? "", sourceFile.fileName), destinationFilePath, true);
            }
            else
            {
                File.Copy(Path.Combine(sourceDirectory, sourceFile.fileName), destinationFilePath, true);
            }
        }
    }
}
