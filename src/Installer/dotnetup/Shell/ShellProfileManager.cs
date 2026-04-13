// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Shell;

/// <summary>
/// Manages shell profile file modifications to persist .NET environment configuration.
/// </summary>
public class ShellProfileManager
{
    internal const string MarkerComment = "# dotnetup";
    private const string BackupSuffix = ".dotnetup-backup";

    /// <summary>
    /// Ensures the correct dotnetup profile entry is present in all profile files for the given shell provider.
    /// If an entry already exists, it is replaced in-place. If no entry exists, one is appended.
    /// Creates backups before modifying existing files.
    /// </summary>
    /// <param name="provider">The shell provider to use.</param>
    /// <param name="dotnetupPath">The full path to the dotnetup binary.</param>
    /// <param name="dotnetupOnly">When true, the profile entry only adds dotnetup to PATH (no DOTNET_ROOT or dotnet PATH).</param>
    /// <returns>The list of profile file paths that were modified.</returns>
    public static IReadOnlyList<string> AddProfileEntries(IEnvShellProvider provider, string dotnetupPath, bool dotnetupOnly = false)
    {
        var profilePaths = provider.GetProfilePaths();
        var entry = provider.GenerateProfileEntry(dotnetupPath, dotnetupOnly);
        var modifiedFiles = new List<string>();

        foreach (var profilePath in profilePaths)
        {
            if (EnsureEntryInFile(profilePath, entry))
            {
                modifiedFiles.Add(profilePath);
            }
        }

        return modifiedFiles;
    }

    /// <summary>
    /// Removes dotnetup profile entries from all profile files for the given shell provider.
    /// </summary>
    /// <returns>The list of profile file paths that were modified.</returns>
    public static IReadOnlyList<string> RemoveProfileEntries(IEnvShellProvider provider)
    {
        var profilePaths = provider.GetProfilePaths();
        var modifiedFiles = new List<string>();

        foreach (var profilePath in profilePaths)
        {
            if (RemoveEntryFromFile(profilePath))
            {
                modifiedFiles.Add(profilePath);
            }
        }

        return modifiedFiles;
    }

    /// <summary>
    /// Checks whether a profile file already contains a dotnetup entry.
    /// </summary>
    public static bool HasProfileEntry(string profilePath)
    {
        if (!File.Exists(profilePath))
        {
            return false;
        }

        var content = File.ReadAllText(profilePath);
        return content.Contains(MarkerComment, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the given entry is present in the file. If an existing dotnetup entry is found,
    /// it is replaced in-place to preserve the user's ordering. Otherwise the entry is appended.
    /// Returns true if the file was modified, false if the entry was already correct.
    /// </summary>
    private static bool EnsureEntryInFile(string profilePath, string entry)
    {
        var directory = Path.GetDirectoryName(profilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(profilePath))
        {
            // New file — just write the entry
            File.WriteAllText(profilePath, entry + Environment.NewLine);
            return true;
        }

        var lines = File.ReadAllLines(profilePath).ToList();
        var entryLines = entry.Split('\n', StringSplitOptions.None)
            .Select(l => l.TrimEnd('\r'))
            .ToArray();

        // Look for an existing marker
        int markerIndex = lines.FindIndex(l => l.TrimEnd() == MarkerComment);

        if (markerIndex >= 0)
        {
            // Determine how many lines the old entry spans (marker + command lines)
            int oldEntryEnd = markerIndex + 1;
            // The old entry is the marker line plus the next line (the eval/invoke line)
            if (oldEntryEnd < lines.Count)
            {
                oldEntryEnd++;
            }

            // Check if the existing entry already matches
            var oldEntry = lines.GetRange(markerIndex, oldEntryEnd - markerIndex);
            if (oldEntry.Count == entryLines.Length &&
                oldEntry.Zip(entryLines).All(pair => pair.First.TrimEnd() == pair.Second.TrimEnd()))
            {
                return false; // Already correct
            }

            // Replace in-place
            File.Copy(profilePath, profilePath + BackupSuffix, overwrite: true);
            lines.RemoveRange(markerIndex, oldEntryEnd - markerIndex);
            lines.InsertRange(markerIndex, entryLines);
            File.WriteAllLines(profilePath, lines);
            return true;
        }

        // No existing entry — append
        File.Copy(profilePath, profilePath + BackupSuffix, overwrite: true);
        using var writer = File.AppendText(profilePath);
        writer.WriteLine();
        writer.WriteLine(entry);
        return true;
    }

    private static bool RemoveEntryFromFile(string profilePath)
    {
        if (!File.Exists(profilePath))
        {
            return false;
        }

        var lines = File.ReadAllLines(profilePath).ToList();
        bool modified = false;

        for (int i = lines.Count - 1; i >= 0; i--)
        {
            if (lines[i].TrimEnd() == MarkerComment)
            {
                // Remove the marker line and the line after it (the eval/invoke line)
                lines.RemoveAt(i);
                if (i < lines.Count)
                {
                    lines.RemoveAt(i);
                }
                modified = true;
            }
        }

        if (modified)
        {
            File.WriteAllLines(profilePath, lines);
        }

        return modified;
    }
}
