// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.UnifiedBuild.Tasks.ManifestAssets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Xml.Linq;
using static Microsoft.DotNet.UnifiedBuild.Tasks.AzureDevOpsClient;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.DotNet.UnifiedBuild.Tasks;

public class SigningValidation : Microsoft.Build.Utilities.Task
{
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
    /// Where the blobs and packages are downloaded to
    /// </summary>
    [Required]
    public required string ArtifactDownloadDirectory { get; init; }

    /// <summary>
    /// Azure DevOps build id
    /// </summary>
    [Required]
    public required string BuildId { get; init; }

    /// <summary>
    /// Path to the dotnet root directory
    /// </summary>
    [Required]
    public required string DotNetRootDirectory { get; init; }

    /// <summary>
    /// Paths to the Merged Manifest
    /// </summary>
    [Required]
    public required string MergedManifest { get; init; }

    /// <summary>
    /// Path to the output logs directory
    /// </summary>
    [Required]
    public required string OutputLogsDirectory { get; init; }

    private const int _retryCount = 10;
    private const int _signCheckTimeout = 60 * 60 * 1000; // 1 hour

    public override bool Execute()
    {
        ExecuteAsync().GetAwaiter().GetResult();
        return !Log.HasLoggedErrors;
    }

    private async Task ExecuteAsync()
    {
        using var clientThrottle = new SemaphoreSlim(16, 16);
        List<string> blobsToDownload = new();
        List<string> packagesToDownload = new();

        // Read the merged manifest and extract shipping blobs and packages for downloading
        using (Stream xmlStream = File.OpenRead(MergedManifest))
        {
            XDocument doc = XDocument.Load(xmlStream);

            var blobs = doc.Descendants("Blob")
                .Where(e => e.Attribute("DotNetReleaseShipping")?.Value == "true")
                .Select(e => 
                {
                    string? id = e.Attribute("Id")?.Value;
                    if (string.IsNullOrEmpty(id))
                    {
                        Log.LogError("Blob Id is null or empty");
                        return string.Empty;
                    }
                    
                    return Path.GetFileName(id);
                })
                .Where(e => !string.IsNullOrEmpty(e))
                .ToList();

            var packages = doc.Descendants("Package")
                .Where(e => e.Attribute("DotNetReleaseShipping")?.Value == "true")
                .Select(e => 
                {
                    string? id = e.Attribute("Id")?.Value;
                    string? version = e.Attribute("Version")?.Value;

                    if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(version))
                    {
                        Log.LogError("Package Id or Version is null or empty");
                        return string.Empty;
                    }

                    return $"{id}.{version}.nupkg";
                })
                .Where(e => !string.IsNullOrEmpty(e))
                .ToList();

            blobsToDownload.AddRange(blobs);
            packagesToDownload.AddRange(packages);
        }

        // Set up download directories for blobs and packages
        string blobsDirectory = Path.Combine(ArtifactDownloadDirectory, "Blobs");
        string packagesDirectory = Path.Combine(ArtifactDownloadDirectory, "Packages");
        ForceDirectory(blobsDirectory);
        ForceDirectory(packagesDirectory);

        // Download the blobs and packages
        Task blobDownloadTask = DownloadArtifactFilesAsync(
            buildId: BuildId,
            artifactName: "BlobArtifacts",
            fileNamesToDownload: blobsToDownload,
            outputDirectory: blobsDirectory,
            clientThrottle: clientThrottle);

        Task packagesDownloadTask = DownloadArtifactFilesAsync(
            buildId: BuildId,
            artifactName: "PackageArtifacts",
            fileNamesToDownload: packagesToDownload,
            outputDirectory: packagesDirectory,
            clientThrottle: clientThrottle);

        await Task.WhenAll(blobDownloadTask, packagesDownloadTask);

        // Run signcheck on the downloaded blobs and packages
        ForceDirectory(OutputLogsDirectory);
        RunSignCheck(blobsDirectory, "Blobs");
        RunSignCheck(packagesDirectory, "Packages");

        // Process the signcheck results
        string[] signCheckResultXmls = Directory.GetFiles(OutputLogsDirectory, "*.xml", SearchOption.TopDirectoryOnly);
        List<string?> unsignedResults = new();
        foreach (string file in signCheckResultXmls)
        {
            var unsignedFiles = XDocument.Load(file).Descendants("File")
                .Where(result => result.Attribute("Outcome")?.Value == "Unsigned")
                .Select(result => result.Attribute("Name")?.Value)
                .Where(name => !string.IsNullOrEmpty(name));

            unsignedResults.AddRange(unsignedFiles);
        }

        if (unsignedResults.Any())
        {
            Log.LogWarning($"There are {unsignedResults.Count()} unsigned files:");
            foreach (string? result in unsignedResults)
            {
                Log.LogMessage(MessageImportance.High, $"   {result ?? "Unknown file"}");
            }
        }

        Log.LogMessage(MessageImportance.High, "Signing validation completed.");
    }

    /// <summary>
    /// Downloads specified packages and blobs from a specific build artifact and stores them in an output folder.
    /// If the artifact is a zip file, it will be extracted and the specified files will be copied to the output folder.
    /// </summary>
    private async Task DownloadArtifactFilesAsync(
        string buildId,
        string artifactName,
        List<string> fileNamesToDownload,
        string outputDirectory,
        SemaphoreSlim clientThrottle)
    {
        using AzureDevOpsClient azureDevOpsClient = new(AzureDevOpsToken, AzureDevOpsBaseUri, AzureDevOpsProject, Log);

        AzureDevOpsArtifactInformation artifactInformation = await azureDevOpsClient.GetArtifactInformation(buildId, artifactName, _retryCount);

        try
        {
            ArtifactFiles filesInformation = await azureDevOpsClient.GetArtifactFilesInformation(buildId, artifactName, _retryCount, artifactInformation);

            await Task.WhenAll(fileNamesToDownload.Select(async fileName =>
                await DownloadFileFromArtifactAsync(
                    filesInformation,
                    artifactName,
                    azureDevOpsClient,
                    buildId,
                    fileName,
                    outputDirectory,
                    clientThrottle)));
        }
        catch (InvalidOperationException)
        {
            // Download and extract the zip to a temporary directory
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            ForceDirectory(tempDirectory);
            string tempZipPath = Path.Combine(tempDirectory, $"{artifactName}.zip");

            try
            {
                await clientThrottle.WaitAsync();
                await azureDevOpsClient.DownloadArtifactZip(buildId, artifactName, tempZipPath, _retryCount, artifactInformation);
            }
            finally
            {
                clientThrottle.Release();
            }

            ZipFile.ExtractToDirectory(tempZipPath, tempDirectory);

            // Copy the specified files from the temporary directory to the output directory
            foreach (string fileName in fileNamesToDownload)
            {
                string sourceFilePath = Path.Combine(tempDirectory, artifactName, fileName);
                string destinationFilePath = Path.Combine(outputDirectory, fileName);

                if (File.Exists(sourceFilePath))
                {
                    Log.LogMessage(MessageImportance.High, $"Copying {fileName} from {artifactName}");
                    File.Copy(sourceFilePath, destinationFilePath, overwrite: true);
                }
                else
                {
                    Log.LogWarning($"File {fileName} not found in {artifactName}.");
                }
            }

            // Clean up the temporary directory
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    /// <summary>
    /// Downloads a single file from the artifact
    /// </summary>
    private async Task DownloadFileFromArtifactAsync(
        ArtifactFiles filesInformation,
        string azureDevOpsArtifact,
        AzureDevOpsClient azureDevOpsClient,
        string buildId,
        string fileName,
        string destinationDirectory,
        SemaphoreSlim clientThrottle)
    {
        try
        {
            await clientThrottle.WaitAsync();

            ArtifactItem fileItem;

            var matchingFilePaths = filesInformation.Items.Where(f => Path.GetFileName(f.Path) == Path.GetFileName(fileName));

            int count = matchingFilePaths.Count();
            if (count != 1)
            {
                throw new InvalidOperationException($"Expected exactly one file matching {fileName}, but found {count}");
            }
            
            fileItem = matchingFilePaths.Single();

            string itemId = fileItem.Blob.Id;
            string artifactSubPath = fileItem.Path;

            string destinationFilePath = Path.Combine(destinationDirectory, fileName);

            await azureDevOpsClient.DownloadSingleFileFromArtifact(buildId, azureDevOpsArtifact, itemId, artifactSubPath, destinationFilePath, _retryCount);
        }
        catch (Exception ex)
        {
            Log.LogError($"Failed to download file {fileName} from artifact {azureDevOpsArtifact}: {ex.Message}");
            throw;
        }
        finally
        {
            clientThrottle.Release();
        }
    }

    /// <summary>
    /// Runs the signcheck task on the specified package base path
    /// </summary>
    /// <param name="packageBasePath">The path to the package base</param>
    /// <param name="rootLogName">The name of the log file without an extension</param>
    private void RunSignCheck(string packageBasePath, string rootLogName)
    {
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        
        string sdkTaskScript = Path.Combine(DotNetRootDirectory, "eng", "common", "sdk-task");
        string argumentPrefix = isWindows ? "-" : "--";
        string baseArguments = 
            $"{argumentPrefix}task SigningValidation " +
            $"{argumentPrefix}restore " +
            $"/p:PackageBasePath='{packageBasePath}' " +
            $"/p:SignCheckLog='{GetLogPath(rootLogName + ".log")}' " +
            $"/p:SignCheckErrorLog='{GetLogPath(rootLogName + ".error.log")}' " +
            $"/p:SignCheckResultsXmlFile='{GetLogPath(rootLogName + ".xml")}' " +
            $"/p:SignCheckExclusionsFile='{Path.Combine(DotNetRootDirectory, "eng", "SignCheckExclusionsFile.txt")}'";

        string command = string.Empty;
        string arguments = string.Empty;
        if (isWindows)
        {
            command = "powershell.exe";
            arguments = $"& \"'{sdkTaskScript}.ps1' {baseArguments} -msbuildEngine vs\"";
        }
        else
        {
            command = "/bin/bash";
            arguments = $"-c \"'{sdkTaskScript}.sh' {baseArguments}\"";
        }

        using (var process = new Process())
        {
            process.StartInfo = new ProcessStartInfo()
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            Log.LogMessage(MessageImportance.High, $"Running SignCheck on {packageBasePath}");

            process.Start();
            
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit(_signCheckTimeout);
            if (!process.HasExited)
            {
                Log.LogError($"SignCheck on '{packageBasePath}' timed out. Killing process.");
                process.Kill();
                process.WaitForExit();
            }
            else if (process.ExitCode != 0)
            {
                Log.LogError($"SignCheck on '{packageBasePath}' failed with exit code {process.ExitCode}");
            }
            else
            {
                Log.LogMessage(MessageImportance.High, $"SignCheck on '{packageBasePath}' completed successfully");
            }

            WriteTextToLog(rootLogName + ".stdout.log", output);
            WriteTextToLog(rootLogName + ".stderror.log", error);
        }
    }

    /// <summary>
    /// Creates the directory if it does not exist
    /// </summary>
    /// <param name="directory">The directory to create</param>
    private void ForceDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// Gets the path to the log file
    /// </summary>
    /// <param name="logName">The name of the log file</param>
    private string GetLogPath(string logName) =>
        Path.Combine(OutputLogsDirectory, logName);

    /// <summary>
    /// Writes the output to the log file
    /// </summary>
    /// <param name="logName">The name of the log file</param>
    /// <param name="text">The text to write to the log file</param>
    private void WriteTextToLog(string logName, string text)
    {
        string trimmedText = text.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedText))
        {
            File.WriteAllText(GetLogPath(logName), trimmedText);
        }
    }
}
