// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// Name of the main vertical that we'll take all artifacts from
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
    private const string _verticalNameAttribute = "VerticalName";
    private const string _artifactNameSuffix = "_Artifacts";
    private const string _assetsFolderName = "assets";
    private const string _packagesFolderName = "packages";
    private const int _retryCount = 10;

    private Dictionary<string, List<string>> duplicatedItems = new();

    public override bool Execute()
    {
        ExecuteAsync().Wait();
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

        CopyMainVerticalAssets(Path.Combine(MainVerticalArtifactsFolder, _packagesFolderName), Path.Combine(OutputFolder, _packagesFolderName));
        CopyMainVerticalAssets(Path.Combine(MainVerticalArtifactsFolder, _assetsFolderName), Path.Combine(OutputFolder, _assetsFolderName));

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

            if (addedPackageIds.Count > 0 || addedBlobIds.Count > 0)
            {
                await DownloadArtifactFiles(
                    BuildId,
                    $"{verticalName}{_artifactNameSuffix}",
                    addedPackageIds,
                    addedBlobIds);
            }
        }

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
    /// Downloads specified packages and symbols from a specific build artifact and stores them in an output folder, 
    /// either flat or with the same relative path as in the artifact.
    /// </summary>
    private async Task DownloadArtifactFiles(
        string buildId,
        string artifactName,
        List<string> packageFileNames,
        List<string> assetFileNames)
    {
        string packagesOutputPath = Path.Combine(OutputFolder, _packagesFolderName);
        string assetsOutputPath = Path.Combine(OutputFolder, _assetsFolderName);

        using AzureDevOpsClient azureDevOpsClient = new(AzureDevOpsToken, AzureDevOpsBaseUri, AzureDevOpsProject, Log);

        ArtifactFiles filesInformation = await azureDevOpsClient.GetArtifactFilesInformation(buildId, artifactName, _retryCount);

        foreach (var package in packageFileNames)
        {
            await DownloadFileFromArtifact(filesInformation, artifactName, azureDevOpsClient, buildId, package, packagesOutputPath);
        }

        foreach (var asset in assetFileNames)
        {
            await DownloadFileFromArtifact(filesInformation, artifactName, azureDevOpsClient, buildId, asset, assetsOutputPath);
        }
    }

    private async Task DownloadFileFromArtifact(ArtifactFiles artifactFilesMetadata, string azureDevOpsArtifact, AzureDevOpsClient azureDevOpsClient, string buildId, string manifestFile, string destinationDirectory)
    {
        ArtifactItem fileItem;

        var matchingFilePaths = artifactFilesMetadata.Items.Where(f => Path.GetFileName(f.Path) == Path.GetFileName(manifestFile));

        if (!matchingFilePaths.Any())
        {
            throw new ArgumentException($"File {manifestFile} not found in source files.");
        }

        if (matchingFilePaths.Count() > 1)
        {
            if (manifestFile.Contains("productVersion.txt"))
            {
                fileItem = matchingFilePaths.First();
            }
            else
            {
                fileItem = matchingFilePaths.SingleOrDefault(f => f.Path.Contains(manifestFile) || f.Path.Contains(manifestFile.Replace("/", @"\")))
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

        try
        {
            await azureDevOpsClient.DownloadSingleFileFromArtifact(buildId, azureDevOpsArtifact, itemId, artifactSubPath, destinationFilePath, _retryCount);
        }
        catch (Exception ex)
        {
            Log.LogError($"Failed to download file {manifestFile} from artifact {azureDevOpsArtifact}: {ex.Message}");
            throw;
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