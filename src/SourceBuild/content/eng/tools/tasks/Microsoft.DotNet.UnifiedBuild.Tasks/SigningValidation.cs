// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Xml.Linq;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.DotNet.UnifiedBuild.Tasks;

public class SigningValidation : Microsoft.Build.Utilities.Task
{
    /// <summary>
    /// Directory where the blobs and packages were downloaded to
    /// </summary>
    [Required]
    public required string ArtifactDownloadDirectory { get; init; }

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

    private readonly string _signCheckFilesDirectory = Path.Combine(Path.GetTempPath(), "SignCheckFiles");

    private const string _signCheckStdoutLogFileName = "signcheck.log";
    private const string _signCheckStderrLogFileName = "signcheck.error.log";
    private const string _signCheckResultsXmlFileName = "signcheck.xml";
    private const int _signCheckTimeout = 60 * 60 * 1000 * 2; // 2 hours

    public override bool Execute()
    {
        try
        {
            PrepareFilesToSignCheck();

            RunSignCheck();

            ProcessSignCheckResults();

            Log.LogMessage(MessageImportance.High, "Signing validation completed.");
        }
        catch (Exception ex)
        {
            Log.LogError($"Signing validation failed: {ex.Message}");
        }
        finally
        {
            if (Directory.Exists(_signCheckFilesDirectory))
            {
                Directory.Delete(_signCheckFilesDirectory, true);
            }
        }

        return !Log.HasLoggedErrors;
    }

    /// <summary>
    /// Gets the list of files to sign check from the merged manifest
    /// and copies them to the sign check directory.
    /// </summary>
    private void PrepareFilesToSignCheck()
    {
        Log.LogMessage(MessageImportance.High, "Preparing files to sign check...");

        IEnumerable<string> blobsToSignCheck = Enumerable.Empty<string>();
        IEnumerable<string> packagesToSignCheck = Enumerable.Empty<string>();

        using (Stream xmlStream = File.OpenRead(MergedManifest))
        {
            XDocument doc = XDocument.Load(xmlStream);

            // Extract blobs
            blobsToSignCheck = doc.Descendants("Blob")
                .Where(blob => IsReleaseShipping(blob))
                .Select(blob =>
                {
                    string id = ExtractAttribute(blob, "Id");
                    string filename = Path.GetFileName(id);
                    return !string.IsNullOrEmpty(filename) ? filename : string.Empty;
                })
                .Where(blob => !string.IsNullOrEmpty(blob));

            // Extract packages
            packagesToSignCheck = doc.Descendants("Package")
                .Where(pkg => IsReleaseShipping(pkg))
                .Select(pkg =>
                {
                    string id = ExtractAttribute(pkg, "Id");
                    string version = ExtractAttribute(pkg, "Version");

                    return !string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(version)
                        ? $"{id}.{version}.nupkg"
                        : string.Empty;
                })
                .Where(pkg => !string.IsNullOrEmpty(pkg));
        }

        ForceDirectory(_signCheckFilesDirectory);

        // Copy the shipping blobs and packages from the download directory to the signcheck directory
        foreach (string file in blobsToSignCheck.Concat(packagesToSignCheck))
        {
            string? sourcePath = Directory.GetFiles(ArtifactDownloadDirectory, file, SearchOption.AllDirectories).FirstOrDefault();
            string destinationPath = Path.Combine(_signCheckFilesDirectory, file);

            if (!string.IsNullOrEmpty(sourcePath))
            {
                if (File.Exists(destinationPath))
                {
                    Log.LogWarning($"File {file} already exists in {_signCheckFilesDirectory}, skipping copy.");
                }
                else
                {
                    File.Copy(sourcePath, destinationPath, true);
                }
            }
            else
            {
                Log.LogWarning($"File {file} not found in {ArtifactDownloadDirectory}");
            }
        }
    }

    /// <summary>
    /// Runs the signcheck task on the specified package base path
    /// </summary>
    private void RunSignCheck()
    {
        using (var process = new Process())
        {
            (string command, string arguments) = GetSignCheckCommandAndArguments();

            process.StartInfo = new ProcessStartInfo()
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            // SignCheck writes console output to log files and the output stream.
            // To avoid cluttering the console, redirect the output to empty handlers.
            process.OutputDataReceived += (sender, args) => {  };
            process.ErrorDataReceived += (sender, args) => { };

            Log.LogMessage(MessageImportance.High, $"Running SignCheck...");

            ForceDirectory(OutputLogsDirectory);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit(_signCheckTimeout);

            if (!process.HasExited)
            {
                process.Kill();
                process.WaitForExit();
                throw new TimeoutException($"SignCheck timed out after {_signCheckTimeout / 1000} seconds.");
            }

            string errorLog = GetLogPath(_signCheckStderrLogFileName);
            string errorLogContent = File.Exists(errorLog) ? File.ReadAllText(errorLog).Trim() : string.Empty;
            if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(errorLogContent))
            {
                // We don't want to throw here because SignCheck will fail for unsigned files
                Log.LogError($"SignCheck failed with exit code {process.ExitCode}: {errorLogContent}");
            }

            Log.LogMessage(MessageImportance.High, $"SignCheck completed.");
        }
    }

    private void ProcessSignCheckResults()
    {
        string resultsXml = GetLogPath(_signCheckResultsXmlFileName);
        if (!File.Exists(resultsXml))
        {
            throw new FileNotFoundException($"SignCheck results XML file not found: {resultsXml}");
        }

        IEnumerable<string> unsignedResults = XDocument.Load(resultsXml).Descendants("File")
            .Where(result => ExtractAttribute(result, "Outcome") == "Unsigned")
            .Select(unsignedResult =>
            {
                string fileName = ExtractAttribute(unsignedResult, "Name");

                if (string.IsNullOrEmpty(fileName))
                {
                    return string.Empty;
                }

                string otherAttributes = string.Join(" ", unsignedResult.Attributes().Where(a => a.Name != "Name").Select(a => $"{a.Name}=\"{a.Value}\""));
                return $"{fileName}: {otherAttributes}";
            })
            .Where(unsignedResult => !string.IsNullOrEmpty(unsignedResult));

        if (unsignedResults.Any())
        {
            Log.LogWarning($"There are {unsignedResults.Count()} unsigned files.");
            foreach (string result in unsignedResults)
            {
                Log.LogMessage(MessageImportance.High, $"   {result}");
            }

            throw new Exception($"SignCheck detected unsigned files.");
        }
    }

    /// <summary>
    /// Gets the command and arguments to run signcheck
    /// </summary>
    private (string command, string arguments) GetSignCheckCommandAndArguments()
    {
        string exclusionsFile = Path.Combine(DotNetRootDirectory, "eng", "SignCheckExclusionsFile.txt");
        string sdkTaskScript = Path.Combine(DotNetRootDirectory, "eng", "common", "sdk-task");

        string argumentsTemplate =
            $"'{sdkTaskScript}.$scriptExtension$' " +
            $"$argumentPrefix$task SigningValidation " +
            $"$argumentPrefix$restore " +
            $"/p:PackageBasePath='{_signCheckFilesDirectory}' " +
            $"/p:EnableStrongNameCheck=true " +
            $"/p:SignCheckLog='{GetLogPath(_signCheckStdoutLogFileName)}' " +
            $"/p:SignCheckErrorLog='{GetLogPath(_signCheckStderrLogFileName)}' " +
            $"/p:SignCheckResultsXmlFile='{GetLogPath(_signCheckResultsXmlFileName)}' " +
            $"/p:SignCheckExclusionsFile='{exclusionsFile}' " +
            $"$additionalArgs$";

        string command = string.Empty;
        string arguments = string.Empty;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            command = "powershell.exe";
            string formattedArguments = argumentsTemplate
                .Replace("$scriptExtension$", "ps1")
                .Replace("$argumentPrefix$", "-")
                .Replace("$additionalArgs$", "-msbuildEngine vs");
            arguments = $"& \"{formattedArguments}\"";
        }
        else
        {
            command = "/bin/bash";
            string formattedArguments = argumentsTemplate
                .Replace("$scriptExtension$", "sh")
                .Replace("$argumentPrefix$", "--")
                .Replace("$additionalArgs$", string.Empty);
            arguments = $"-c \"{formattedArguments}\"";
        }

        return (command, arguments);
    }

    /// <summary>
    /// Extracts the value of the specified attribute from the element and logs an error if it's missing or empty.
    /// </summary>
    private string ExtractAttribute(XElement element, string attributeName)
    {
        string? value = element.Attribute(attributeName)?.Value;
        if (string.IsNullOrEmpty(value))
        {
            Log.LogError($"{attributeName} is null or empty in element: {element}");
        }
        return value ?? string.Empty;
    }

    /// <summary>
    /// Gets the path to the log file in the output logs directory.
    /// </summary>
    private string GetLogPath(string fileName)
        => Path.Combine(OutputLogsDirectory, fileName);

    /// <summary>
    /// Checks if the element has the "DotNetReleaseShipping" attribute set to "true".
    /// </summary>
    private static bool IsReleaseShipping(XElement element)
        => element.Attribute("DotNetReleaseShipping")?.Value == "true";

    /// <summary>
    /// Creates the directory if it does not exist
    /// </summary>
    /// <param name="directory">The directory to create</param>
    private static void ForceDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
