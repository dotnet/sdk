// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper.Commands.PrintEnvScript;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Manages shell profile file modifications to persist .NET environment configuration.
/// </summary>
public class ShellProfileManager
{
    internal const string MarkerComment = "# dotnetup";
    private const string BackupSuffix = ".dotnetup-backup";

    /// <summary>
    /// Adds profile entries to all profile files for the given shell provider.
    /// Creates backups before modifying existing files. Skips files that already contain the entry.
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
            if (AddEntryToFile(profilePath, entry))
            {
                modifiedFiles.Add(profilePath);
            }
        }

        return modifiedFiles;
    }

    /// <summary>
    /// Replaces existing dotnetup profile entries with new ones.
    /// Removes the old entries first, then adds the new entries.
    /// </summary>
    /// <returns>The list of profile file paths that were modified.</returns>
    public static IReadOnlyList<string> ReplaceProfileEntries(IEnvShellProvider provider, string dotnetupPath, bool dotnetupOnly = false)
    {
        RemoveProfileEntries(provider);
        return AddProfileEntries(provider, dotnetupPath, dotnetupOnly);
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

    private static bool AddEntryToFile(string profilePath, string entry)
    {
        if (HasProfileEntry(profilePath))
        {
            return false;
        }

        var directory = Path.GetDirectoryName(profilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Create backup of existing file
        if (File.Exists(profilePath))
        {
            File.Copy(profilePath, profilePath + BackupSuffix, overwrite: true);
        }

        // Append entry with a leading newline to separate from existing content
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
