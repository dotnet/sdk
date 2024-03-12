// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.FileSystemGlobbing;

namespace BinaryToolKit;

public static class CompareBinariesAgainstBaselines
{
    public static List<string> Execute(
        IEnumerable<string> detectedBinaries,
        string? allowedBinariesFile,
        string? disallowedSbBinariesFile,
        string outputReportDirectory,
        string targetDirectory,
        Modes mode)
    {
        Log.LogInformation("Comparing detected binaries to baseline(s).");

        var binariesToRemove = GetUnmatchedBinaries(
            detectedBinaries,
            allowedBinariesFile,
            outputReportDirectory,
            targetDirectory,
            mode).ToList();

        if (mode.HasFlag(Modes.Validate))
        {
            var nonSbBinariesToRemove = GetUnmatchedBinaries(
                detectedBinaries,
                disallowedSbBinariesFile,
                outputReportDirectory,
                targetDirectory,
                mode).ToList();
    
            var newBinaries = binariesToRemove.Intersect(nonSbBinariesToRemove);

            if (newBinaries.Any())
            {
                string newBinariesFile = Path.Combine(outputReportDirectory, "NewBinaries.txt");

                File.WriteAllLines(newBinariesFile, newBinaries);

                Log.LogWarning($"    {newBinaries.Count()} new binaries detected. Check '{newBinariesFile}' for details.");
            }
        }

        Log.LogInformation("Finished comparing binaries.");

        return binariesToRemove;
    }

    private static IEnumerable<string> GetUnmatchedBinaries(
        IEnumerable<string> searchFiles,
        string? baselineFile,
        string outputReportDirectory,
        string targetDirectory,
        Modes mode)
    {
        var patterns = ParseBaselineFile(baselineFile);

        if (mode.HasFlag(Modes.Validate))
        {
            // If validating in any mode (Mode == Validate or Mode == All), 
            // we need to detect both unused patterns and unmatched files.
            // We simultaneously detect unused patterns and unmatched files for efficiency.

            HashSet<string> unusedPatterns = new HashSet<string>(patterns);
            HashSet<string> unmatchedFiles = new HashSet<string>(searchFiles);

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

            UpdateBaselineFile(baselineFile, outputReportDirectory, unusedPatterns);

            return unmatchedFiles;
        }
        else if (mode == Modes.Clean)
        {
            // If only cleaning and not validating (Mode == Clean),
            // we don't need to update the baseline files with unused patterns
            // so we can just detect unmatched files.

            Matcher matcher = new Matcher(StringComparison.Ordinal);
            matcher.AddInclude("**/*");
            matcher.AddExcludePatterns(patterns);

            return matcher.Match(targetDirectory, searchFiles).Files.Select(file => file.Path);
        }
        else
        {
            // Unhandled mode
            throw new ArgumentException($"Unhandled mode: {mode}");
        } 
    }

    private static IEnumerable<string> ParseBaselineFile(string? file) {
        if (!File.Exists(file))
        {
            return Enumerable.Empty<string>();
        }

        // Read the baseline file and parse the patterns, ignoring comments and empty lines
        return File.ReadLines(file)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
            .Select(line => line.Split('#')[0].Trim());
    }

    private static void UpdateBaselineFile(string? file, string outputReportDirectory, HashSet<string> unusedPatterns)
    {
        if(File.Exists(file))
        {
            var lines = File.ReadAllLines(file);
            var newLines = lines.Where(line => !unusedPatterns.Contains(line)).ToList();

            string updatedFile = Path.Combine(outputReportDirectory, "Updated" + Path.GetFileName(file));

            File.WriteAllLines(updatedFile, newLines);

            Log.LogInformation($"    Updated baseline file '{file}' written to '{updatedFile}'");
        }
    }
}