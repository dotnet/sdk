// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace BinaryToolKit;

public class BinaryTool
{
    public async Task<int> ExecuteAsync(
        string targetDirectory,
        string outputReportDirectory,
        string? allowedBinariesFile,
        Modes mode)
    {
        DateTime startTime = DateTime.Now;

        Log.LogInformation($"Starting binary tool at {startTime} in {mode} mode");

        // Parse args
        targetDirectory = GetAndValidateFullPath(
            "TargetDirectory",
            targetDirectory,
            isDirectory: true,
            createIfNotExist: false,
            isRequired: true)!;
        outputReportDirectory = GetAndValidateFullPath(
            "OutputReportDirectory",
            outputReportDirectory,
            isDirectory: true,
            createIfNotExist: true,
            isRequired: true)!;
        allowedBinariesFile = GetAndValidateFullPath(
            "AllowedBinariesFile",
            allowedBinariesFile,
            isDirectory: false,
            createIfNotExist: false,
            isRequired: false);

        // Run the tooling
        var detectedBinaries = await DetectBinaries.ExecuteAsync(targetDirectory, outputReportDirectory, allowedBinariesFile);

        if (mode == Modes.Validate)
        {
            ValidateBinaries(detectedBinaries, outputReportDirectory);
        }

        else if (mode == Modes.Clean)
        {
            RemoveBinaries(detectedBinaries, targetDirectory);
        }

        Log.LogInformation("Finished all binary tasks. Took " + (DateTime.Now - startTime).TotalSeconds + " seconds.");

        return Log.GetExitCode();
    }

    private string? GetAndValidateFullPath(
        string parameterName,
        string? path,
        bool isDirectory,
        bool createIfNotExist,
        bool isRequired)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            if (isRequired)
            {
                Log.LogError($"Required path for '{parameterName}' is empty or contains whitespace.");
                Environment.Exit(1);
            }
            return null;
        }

        string fullPath = Path.GetFullPath(path);
        bool exists = isDirectory ? Directory.Exists(fullPath) : File.Exists(fullPath);

        if (!exists)
        {
            if (createIfNotExist && isDirectory)
            {
                Log.LogInformation($"Creating directory '{fullPath}' for '{parameterName}'.");
                Directory.CreateDirectory(fullPath);
            }
            else
            {
                Log.LogError($"{(isDirectory ? "Directory" : "File")} '{fullPath}' for '{parameterName}' does not exist.");
                Environment.Exit(1);
            }
        }
        return fullPath;
    }

    private static void ValidateBinaries(IEnumerable<string> newBinaries, string outputReportDirectory)
    {
        if (newBinaries.Any())
        {
            string newBinariesFile = Path.Combine(outputReportDirectory, "NewBinaries.txt");

            Log.LogDebug("New binaries:");

            File.WriteAllLines(newBinariesFile, newBinaries);

            foreach (var binary in newBinaries)
            {
                Log.LogDebug($"    {binary}");
            }

            Log.LogError($"ERROR: {newBinaries.Count()} new binaries. Check '{newBinariesFile}' for details.");
        }
    }

    private static void RemoveBinaries(IEnumerable<string> binariesToRemove, string targetDirectory)
    {
        Log.LogInformation($"Removing binaries from '{targetDirectory}'...");
        
        foreach (var binary in binariesToRemove)
        {
            File.Delete(Path.Combine(targetDirectory, binary));
            Log.LogDebug($"    {binary}");
        }

        Log.LogInformation($"Finished binary removal. Removed {binariesToRemove.Count()} binaries.");
    }
}
