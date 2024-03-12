// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace BinaryToolKit;

public class BinaryTool
{
    public async Task ExecuteAsync(
        string targetDirectory,
        string outputReportDirectory,
        string? allowedBinariesFile,
        string? disallowedSbBinariesFile,
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
        disallowedSbBinariesFile = GetAndValidateFullPath(
            "DisallowedSbBinariesFile",
            disallowedSbBinariesFile,
            isDirectory: false,
            createIfNotExist: false,
            isRequired: false);

        // Run the tooling
        var detectedBinaries = await DetectBinaries.ExecuteAsync(targetDirectory);

        var comparedBinaries = CompareBinariesAgainstBaselines
            .Execute(
                detectedBinaries,
                allowedBinariesFile,
                disallowedSbBinariesFile,
                outputReportDirectory,
                targetDirectory,
                mode);

        if (mode.HasFlag(Modes.Clean))
        {
            RemoveBinaries.Execute(comparedBinaries, targetDirectory);
        }

        Log.LogInformation("Finished all binary tasks. Took " + (DateTime.Now - startTime).TotalSeconds + " seconds.");
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
}