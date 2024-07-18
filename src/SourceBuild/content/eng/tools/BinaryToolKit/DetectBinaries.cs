// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Extensions.FileSystemGlobbing;
using System.Text.RegularExpressions;

namespace BinaryToolKit;

public static class DetectBinaries
{
    private const string Utf16Marker = "UTF-16";
    private const int ChunkSize = 4096;
    private static readonly Regex GitCleanRegex = new Regex(@"Would (remove|skip)( repository)? (.*)");

    public static async Task<List<string>> ExecuteAsync(
        string targetDirectory,
        string outputReportDirectory,
        string? allowedBinariesFile)
    {
        Log.LogInformation($"Detecting binaries in '{targetDirectory}' not listed in '{allowedBinariesFile}'...");

        var matcher = new Matcher(StringComparison.Ordinal);
        matcher.AddInclude("**/*");

        IEnumerable<string> matchingFiles = matcher.GetResultsInFullPath(targetDirectory);

        var tasks = matchingFiles
            .Select(async file =>
            {
                return await IsBinaryAsync(file) ? file.Substring(targetDirectory.Length + 1) : null;
            });

        var binaryFiles = (await Task.WhenAll(tasks)).OfType<string>();

        var unmatchedBinaryFiles = GetUnmatchedBinaries(
            binaryFiles,
            allowedBinariesFile,
            outputReportDirectory,
            targetDirectory).ToList();

        Log.LogInformation($"Finished binary detection.");

        return unmatchedBinaryFiles;
    }

    private static async Task<bool> IsBinaryAsync(string filePath)
    {
        // Using the GNU diff heuristic to determine if a file is binary or not.
        // For more details, refer to the GNU diff manual: 
        // https://www.gnu.org/software/diffutils/manual/html_node/Binary.html

        using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        using (BinaryReader br = new BinaryReader(fs))
        {
            byte[] buffer = new byte[ChunkSize];
            int bytesRead = br.Read(buffer, 0, ChunkSize);
            for (int i = 0; i < bytesRead; i++)
            {
                if (buffer[i] == 0)
                {
                    // Need to check that the file is not UTF-16 encoded
                    // because heuristic can return false positives
                    return await IsNotUTF16Async(filePath);
                }
            }
        }
        return false;
    }

    private static async Task<bool> IsNotUTF16Async(string file)
    {
        if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            string output = await ExecuteProcessAsync("file",  $"\"{file}\"");
            output = output.Split(":")[1].Trim();

            if (output.Contains(Utf16Marker))
            {
                return false;
            }
        }
        return true;
    }

    private static async Task<string> ExecuteProcessAsync(string executable, string arguments)
    {
        ProcessStartInfo psi = new ()
        {
            FileName = executable,
            Arguments = arguments,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var proc = Process.Start(psi)!;

        string output = await proc.StandardOutput.ReadToEndAsync();
        string error = await proc.StandardError.ReadToEndAsync();

        await proc.WaitForExitAsync();

        if (!string.IsNullOrEmpty(error))
        {
            Log.LogError(error);
        }

        return output;
    }

    private static IEnumerable<string> GetUnmatchedBinaries(
        IEnumerable<string> searchFiles,
        string? allowedBinariesFile,
        string outputReportDirectory,
        string targetDirectory)
    {
        HashSet<string> unmatchedFiles = new HashSet<string>(searchFiles);

        var filesToPatterns = new Dictionary<string, HashSet<string>>();
        ParseAllowedBinariesFile(allowedBinariesFile, ref filesToPatterns);

        foreach (var fileToPatterns in filesToPatterns)
        {
            var patterns = fileToPatterns.Value;
            HashSet<string> unusedPatterns = new HashSet<string>(patterns);

            foreach (string pattern in patterns)
            {
                Matcher matcher = new Matcher(StringComparison.Ordinal);
                matcher.AddInclude(pattern);
                
                var matches = matcher.Match(targetDirectory, searchFiles);
                if (matches.HasMatches)
                {
                    unusedPatterns.Remove(pattern);
                    unmatchedFiles.ExceptWith(matches.Files.Select(file => file.Path));
                }
            }

            UpdateAllowedBinariesFile(fileToPatterns.Key, outputReportDirectory, unusedPatterns);
        }

        return unmatchedFiles;
    }

    private static void ParseAllowedBinariesFile(string? file, ref Dictionary<string, HashSet<string>> result)
    {
        if (!File.Exists(file))
        {
            return;
        }

        if (!result.ContainsKey(file))
        {
            result[file] = new HashSet<string>();
        }

        foreach (var line in File.ReadLines(file))
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
            {
                continue;
            }

            if (trimmedLine.StartsWith("import:"))
            {
                var importFile = trimmedLine.Substring("import:".Length).Trim();
                if (!Path.IsPathFullyQualified(importFile))
                {
                    var currentDirectory = Path.GetDirectoryName(file)!;
                    importFile = Path.Combine(currentDirectory, importFile);
                }
                if (result.ContainsKey(importFile))
                {
                    Log.LogWarning($"    Duplicate import {importFile}. Skipping.");
                    continue;
                }

                ParseAllowedBinariesFile(importFile, ref result);
            }
            else
            {
                result[file].Add(trimmedLine.Split('#')[0].Trim());
            }
        }
    }

    private static void UpdateAllowedBinariesFile(string? file, string outputReportDirectory, HashSet<string> unusedPatterns)
    {
        if(File.Exists(file) && unusedPatterns.Any())
        {
            var lines = File.ReadAllLines(file);
            var newLines = lines.Where(line => !unusedPatterns.Contains(line)).ToList();

            string updatedFile = Path.Combine(outputReportDirectory, "Updated" + Path.GetFileName(file));

            File.WriteAllLines(updatedFile, newLines);

            Log.LogInformation($"    Updated allowed binaries file '{Path.GetFileName(file)}' written to '{updatedFile}'");
        }
    }
}