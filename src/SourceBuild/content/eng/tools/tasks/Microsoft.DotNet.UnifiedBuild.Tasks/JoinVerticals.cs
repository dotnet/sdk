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
using static Microsoft.DotNet.UnifiedBuild.Tasks.AzureDevOpsClient;
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
    /// Name of the main vertical that we'll take all artifacts from if they exist in this vertical, and at least one other vertical.
    /// </summary>
    [Required]
    public required string MainVertical { get; init; }

    /// <summary>
    /// Azure DevOps build id
    /// </summary>
    [Required]
    public required string BuildId { get; init; }

    /// <summary>
    /// Azure DevOps token, required scopes: "Build (read)", allowed to be empty when running in a public project
    /// </summary>
    public string? AzureDevOpsToken { get; set; }

    /// <summary>
    /// Azure DevOps organization
    /// </summary>
    [Required]
    public required string AzureDevOpsBaseUri { get; init; }

    /// <summary>
    /// Azure DevOps project
    /// </summary>
    [Required]
    public required string AzureDevOpsProject { get; init; }

    /// <summary>
    /// Location of downloaded artifacts from the main vertical
    /// </summary>
    [Required]
    public required string MainVerticalArtifactsFolder { get; init; }

    /// <summary>
    /// Folder where packages and assets will be stored
    /// </summary>
    [Required]
    public required string OutputFolder { get; init; }


    private const string _artifactNameSuffix = "_Artifacts";
    private const string _assetsFolderName = "assets";
    private const string _packagesFolderName = "packages";
    private const int _retryCount = 10;

    public override bool Execute()
    {
        ExecuteAsync().GetAwaiter().GetResult();
        return !Log.HasLoggedErrors;
    }

    private async Task ExecuteAsync()
    {
        List<BuildAssetsManifest> manifests = VerticalManifest.Select(xmlPath => BuildAssetsManifest.LoadFromFile(xmlPath.ItemSpec)).ToList();
        BuildAssetsManifest mainVerticalManifest = manifests
            .FirstOrDefault(manifest => StringComparer.OrdinalIgnoreCase.Equals(manifest.VerticalName, MainVertical))
            ?? throw new ArgumentException($"Couldn't find main vertical manifest {MainVertical} in vertical manifest list");

        string mainVerticalName = mainVerticalManifest.VerticalName!;

        JoinVerticalsAssetSelector joinVerticalsAssetSelector = new JoinVerticalsAssetSelector();

        List<AssetVerticalMatchResult> selectedVerticals = joinVerticalsAssetSelector.SelectAssetMatchingVertical(manifests).ToList();

        var notMatchedAssets = selectedVerticals.Where(o => o.MatchType == AssetVerticalMatchType.NotSpecified).ToList();
        Log.LogMessage(MessageImportance.High, $"### {notMatchedAssets.Count()} Assets not properly matched to vertical: ###");
        foreach (var matchResult in notMatchedAssets)
        {
            Log.LogMessage(MessageImportance.High, $"Asset: {matchResult.AssetId} -- Matched to: {matchResult.VerticalName}, Other verticals: {string.Join(", ", matchResult.OtherVerticals)}");
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

        (AssetVerticalMatchResult matchResult, string fileName) figureOutFileName(AssetVerticalMatchResult matchResult)
        {
            string fileName = matchResult.Asset.AssetType switch
            {
                ManifestAssetType.Package => $"{matchResult.Asset.Id}.{matchResult.Asset.Version}.nupkg",
                ManifestAssetType.Blob => matchResult.Asset.Id,
                _ => throw new ArgumentException($"Unknown asset type {matchResult.Asset.AssetType}")
            };
            return (matchResult, fileName);
        }

        using var clientThrottle = new SemaphoreSlim(16, 16);
        List<Task> downloadTasks = new();

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

            // copy main vertical assets from the local already downloaded artifacts
            if (StringComparer.OrdinalIgnoreCase.Equals(verticalName, mainVerticalName))
            {
                var targetFolderPackages = Path.Combine(MainVerticalArtifactsFolder, _packagesFolderName);
                downloadTasks.Add(
                    CopyMainVerticalAssets(targetFolderPackages, packagesOutputDirectory, assetListPackages)
                );
                var targetFolderBlobs = Path.Combine(MainVerticalArtifactsFolder, _assetsFolderName);
                downloadTasks.Add(
                    CopyMainVerticalAssets(targetFolderBlobs, assetsOutputDirectory, assetListPackages)
                );
                continue;
            }

            if (assetListPackages.Count > 0)
            {
                downloadTasks.Add(
                    DownloadArtifactFiles(
                        BuildId,
                        $"{verticalName}{_artifactNameSuffix}",
                        assetListPackages.Select(o => o.fileName).ToList(),
                        packagesOutputDirectory,
                        clientThrottle)
                );
            }

            if (assetListBlobs.Count > 0)
            {
                downloadTasks.Add(
                    DownloadArtifactFiles(
                        BuildId,
                        $"{verticalName}{_artifactNameSuffix}",
                        assetListBlobs.Select(o => o.fileName).ToList(),
                        assetsOutputDirectory,
                        clientThrottle)
                );
            }
        }

        await Task.WhenAll(downloadTasks);
    }

    /// <summary>
    /// Downloads specified packages and symbols from a specific build artifact and stores them in an output folder
    /// </summary>
    private async Task DownloadArtifactFiles(
        string buildId,
        string artifactName,
        List<string> fileNamesToDownload,
        string outputDirectory,
        SemaphoreSlim clientThrottle)
    {
        using AzureDevOpsClient azureDevOpsClient = new(AzureDevOpsToken, AzureDevOpsBaseUri, AzureDevOpsProject, Log);

        ArtifactFiles filesInformation = await azureDevOpsClient.GetArtifactFilesInformation(buildId, artifactName, _retryCount);

        await Task.WhenAll(fileNamesToDownload.Select(async fileName =>
            await DownloadFileFromArtifact(
                filesInformation,
                artifactName,
                azureDevOpsClient,
                buildId,
                fileName,
                outputDirectory,
                clientThrottle)));
    }

    private async Task DownloadFileFromArtifact(
        ArtifactFiles artifactFilesMetadata,
        string azureDevOpsArtifact,
        AzureDevOpsClient azureDevOpsClient,
        string buildId,
        string manifestFile,
        string destinationDirectory,
        SemaphoreSlim clientThrottle)
    {
        try
        {
            await clientThrottle.WaitAsync();

            ArtifactItem fileItem;

            var matchingFilePaths = artifactFilesMetadata.Items.Where(f => Path.GetFileName(f.Path) == Path.GetFileName(manifestFile));

            if (!matchingFilePaths.Any())
            {
                throw new ArgumentException($"File {manifestFile} not found in source files.");
            }

            if (matchingFilePaths.Count() > 1)
            {
                // Picking the first one until https://github.com/dotnet/source-build/issues/4596 is resolved
                if (manifestFile.Contains("productVersion.txt"))
                {
                    fileItem = matchingFilePaths.First();
                }
                else
                {
                    // For some files it's not enough to compare the filename because they have 2 copies in the artifact
                    // e.g. assets/Release/dotnet-sdk-*-win-x64.zip and assets/Release/Sdk/*/dotnet-sdk-*-win-x64.zip
                    // In this case take the one matching the full path from the manifest
                    fileItem = matchingFilePaths
                        .SingleOrDefault(f => f.Path.EndsWith(manifestFile) || f.Path.EndsWith(manifestFile.Replace("/", @"\")))
                        ?? throw new ArgumentException($"File {manifestFile} not found in source files.");
                }
            }
            else
            {
                fileItem = matchingFilePaths.Single();
            }

            string itemId = fileItem.Blob.Id;
            string artifactSubPath = fileItem.Path;

            string destinationFilePath = Path.Combine(destinationDirectory, Path.GetFileName(manifestFile));

            await azureDevOpsClient.DownloadSingleFileFromArtifact(buildId, azureDevOpsArtifact, itemId, artifactSubPath, destinationFilePath, _retryCount);
        }
        catch (Exception ex)
        {
            Log.LogError($"Failed to download file {manifestFile} from artifact {azureDevOpsArtifact}: {ex.Message}");
            throw;
        }
        finally
        {
            clientThrottle.Release();
        }
    }

    private void ForceDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// Copy all files from the source directory to the destination directory in a flat layout
    /// </summary>
    private async Task CopyMainVerticalAssets(string sourceDirectory, string destinationDirectory,
        IEnumerable<(AssetVerticalMatchResult matchResult, string fileName)> assets)
    {
        await Task.Yield();

        var sourceFiles = Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories);
        foreach (var sourceFile in sourceFiles)
        {
            string destinationFilePath = Path.Combine(destinationDirectory, Path.GetFileName(sourceFile));
            Log.LogMessage(MessageImportance.High, $"Copying {sourceFile} to {destinationFilePath}");
            File.Copy(sourceFile, destinationFilePath, true);
        }
    }
}
