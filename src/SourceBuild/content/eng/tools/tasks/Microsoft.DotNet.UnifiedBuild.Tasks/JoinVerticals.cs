// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.Build.Framework;
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
    /// Location of sownloaded artifacts from the main vertical
    /// </summary>
    [Required]
    public required string MainVerticalArtifactsFolder { get; init; }

    /// <summary>
    /// Folder where packages and assets will be stored
    /// </summary>
    [Required]
    public required string OutputFolder { get; init; }

    private const string _packageElementName = "Package";
    private const string _blobElementName = "Blob";
    private const string _idAttribute = "Id";
    private const string _visibilityAttribute = "Visibility";
    private const string _externalVisibility = "External";
    private const string _internalVisibility = "Internal";
    private const string _verticalVisibility = "Vertical";
    private const string _verticalNameAttribute = "VerticalName";
    private const string _artifactNameSuffix = "_Artifacts";
    private const string _assetsFolderName = "assets";
    private const string _packagesFolderName = "packages";
    private const int _retryCount = 10;

    private Dictionary<string, List<string>> duplicatedItems = new();

    public override bool Execute()
    {
        ExecuteAsync().GetAwaiter().GetResult();
        return !Log.HasLoggedErrors;
    }

    private async Task ExecuteAsync()
    {
        List<XDocument> verticalManifests = VerticalManifest.Select(xmlPath => XDocument.Load(xmlPath.ItemSpec)).ToList();

        XDocument mainVerticalManifest = verticalManifests.FirstOrDefault(manifest => GetRequiredRootAttribute(manifest, _verticalNameAttribute) == MainVertical)
            ?? throw new ArgumentException($"Couldn't find main vertical manifest {MainVertical} in vertical manifest list");

        if (!Directory.Exists(MainVerticalArtifactsFolder))
        {
            throw new ArgumentException($"Main vertical artifacts directory {MainVerticalArtifactsFolder} not found.");
        }

        string mainVerticalName = GetRequiredRootAttribute(mainVerticalManifest, _verticalNameAttribute);

        Dictionary<string, AddedElement> packageElements = [];
        Dictionary<string, AddedElement> blobElements = [];

        List<string> addedPackageIds = AddMissingElements(packageElements, mainVerticalManifest, _packageElementName);
        List<string> addedBlobIds = AddMissingElements(blobElements, mainVerticalManifest, _blobElementName);

        string packagesOutputDirectory = Path.Combine(OutputFolder, _packagesFolderName);
        string blobsOutputDirectory = Path.Combine(OutputFolder, _assetsFolderName);

        CopyMainVerticalAssets(Path.Combine(MainVerticalArtifactsFolder, _packagesFolderName), packagesOutputDirectory);
        CopyMainVerticalAssets(Path.Combine(MainVerticalArtifactsFolder, _assetsFolderName), blobsOutputDirectory);

        using var clientThrottle = new SemaphoreSlim(16, 16);
        List<Task> downloadTasks = new();

        foreach (XDocument verticalManifest in verticalManifests)
        {
            string verticalName = GetRequiredRootAttribute(verticalManifest, _verticalNameAttribute);

            // We already processed the main vertical
            if (verticalName == MainVertical)
            {
                continue;
            }

            addedPackageIds = AddMissingElements(packageElements, verticalManifest, _packageElementName);
            addedBlobIds = AddMissingElements(blobElements, verticalManifest, _blobElementName);

            if (addedPackageIds.Count > 0)
            {
                downloadTasks.Add(
                    DownloadArtifactFiles(
                        BuildId,
                        $"{verticalName}{_artifactNameSuffix}",
                        addedPackageIds,
                        packagesOutputDirectory,
                        clientThrottle));
            }

            if (addedBlobIds.Count > 0)
            {
                downloadTasks.Add(
                    DownloadArtifactFiles(
                        BuildId,
                        $"{verticalName}{_artifactNameSuffix}",
                        addedBlobIds,
                        blobsOutputDirectory,
                        clientThrottle));
            }
        }

        await Task.WhenAll(downloadTasks);

        // Create MergedManifest.xml
        // taking the attributes from the main manifest
        XElement mainManifestRoot = verticalManifests.First().Root
            ?? throw new ArgumentException("The root element of the vertical manifest is null.");
        mainManifestRoot.Attribute(_verticalNameAttribute)!.Remove();

        string manifestOutputPath = Path.Combine(OutputFolder, "MergedManifest.xml");
        XDocument mergedManifest = new(new XElement(
            mainManifestRoot.Name,
            mainManifestRoot.Attributes(),
            packageElements.Values.Select(v => v.Element).OrderBy(elem => elem.Attribute(_idAttribute)?.Value),
            blobElements.Values.Select(v => v.Element).OrderBy(elem => elem.Attribute(_idAttribute)?.Value)));

        File.WriteAllText(manifestOutputPath, mergedManifest.ToString());

        Log.LogMessage(MessageImportance.High, $"### Duplicate items found in the following verticals: ###");

        foreach (var item in duplicatedItems)
        {
            Log.LogMessage(MessageImportance.High, $"Item: {item.Key} -- Produced by: {string.Join(", ", item.Value)}");
        }
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

    /// <summary>
    /// Copy all files from the source directory to the destination directory,
    /// in a flat layout
    /// </summary>
    private void CopyMainVerticalAssets(string sourceDirectory, string destinationDirectory)
    {
        var sourceFiles = Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories);

        if (!Directory.Exists(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        foreach (var sourceFile in sourceFiles)
        {
            string destinationFilePath = Path.Combine(destinationDirectory, Path.GetFileName(sourceFile));

            Log.LogMessage(MessageImportance.High, $"Copying {sourceFile} to {destinationFilePath}");
            File.Copy(sourceFile, destinationFilePath, true);
        }
    }

    /// <summary>
    /// Find the artifacts from the vertical manifest that are not already in the dictionary and add them
    /// Return a list of the added artifact ids
    /// </summary>
    private List<string> AddMissingElements(Dictionary<string, AddedElement> addedArtifacts, XDocument verticalManifest, string elementName)
    {
        List<string> addedFiles = [];

        string verticalName = verticalManifest.Root!.Attribute(_verticalNameAttribute)!.Value;

        foreach (XElement artifactElement in verticalManifest.Descendants(elementName))
        {
            string elementId = artifactElement.Attribute(_idAttribute)?.Value
                ?? throw new ArgumentException($"Required attribute '{_idAttribute}' not found in {elementName} element.");

            // Filter out artifacts that are not "External" visibility.
            // Artifacts of "Vertical" visibility should have been filtered out in each individual vertical,
            // but artifacts of "Internal" visibility would have been included in each vertical's manifest (to enable feeding into join verticals).
            // We need to remove them here so they don't get included in the final merged manifest.
            // As we're in the final join, there should be no jobs after us. Therefore, we can also skip uploading them to the final artifacts folders
            // as no job should run after this job that would consume them.
            string? visibility = artifactElement.Attribute(_visibilityAttribute)?.Value;
            
            if (visibility == _verticalVisibility)
            {
                Log.LogError($"Artifact {elementId} has 'Vertical' visibility and should not be present in a vertical manifest.");
                continue;
            }
            else if (visibility == _internalVisibility)
            {
                Log.LogMessage(MessageImportance.High, $"Artifact {elementId} has 'Internal' visibility and will not be included in the final merged manifest.");
                continue;
            }
            else if (visibility is not (null or "" or _externalVisibility))
            {
                Log.LogError($"Artifact {elementId} has unknown visibility: '{visibility}'");
                continue;
            }

            if (addedArtifacts.TryAdd(elementId, new AddedElement(verticalName, artifactElement)))
            {
                if (elementName == _packageElementName)
                {
                    string version = artifactElement.Attribute("Version")?.Value
                        ?? throw new ArgumentException($"Required attribute 'Version' not found in {elementName} element.");

                    elementId += $".{version}.nupkg";
                }

                addedFiles.Add(elementId);
                Log.LogMessage(MessageImportance.High, $"Taking {elementName} '{elementId}' from '{verticalName}'");
            }
            else
            {
                AddedElement previouslyAddedArtifact = addedArtifacts[elementId];
                if (previouslyAddedArtifact.VerticalName != MainVertical)
                {
                    if (!duplicatedItems.TryAdd(elementId, new List<string> { verticalName, previouslyAddedArtifact.VerticalName }))
                    {
                        duplicatedItems[elementId].Add(verticalName);
                    }
                }
            }
        }

        return addedFiles;
    }

    private static string GetRequiredRootAttribute(XDocument document, string attributeName)
    {
        return document.Root?.Attribute(attributeName)?.Value
            ?? throw new ArgumentException($"Required attribute '{attributeName}' not found in root element.");
    }

    private record AddedElement(string VerticalName, XElement Element);
}
