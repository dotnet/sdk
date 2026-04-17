// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace Microsoft.DotNet.Tools.Bootstrapper.Shell;

/// <summary>
/// Manages shell profile file modifications to persist .NET environment configuration.
/// </summary>
public class ShellProfileManager
{
    internal const string MarkerComment = "# dotnetup";
    private const string BackupSuffix = ".dotnetup-backup";

    private sealed record ProfileFileState(
        List<string> Lines,
        Encoding Encoding,
        string NewLine,
        bool EndsWithTrailingNewLine);

    /// <summary>
    /// Ensures the correct dotnetup profile entry is present in all profile files for the given shell provider.
    /// If an entry already exists, it is replaced in-place. If no entry exists, one is appended.
    /// Creates backups before modifying existing files.
    /// </summary>
    /// <param name="provider">The shell provider to use.</param>
    /// <param name="dotnetupPath">The full path to the dotnetup binary.</param>
    /// <param name="dotnetupOnly">When true, the profile entry only adds dotnetup to PATH (no DOTNET_ROOT or dotnet PATH).</param>
    /// <param name="dotnetInstallPath">An optional .NET install path to pass through to <c>print-env-script</c>.</param>
    /// <returns>The list of profile file paths that were modified.</returns>
    public static IReadOnlyList<string> AddProfileEntries(
        IEnvShellProvider provider,
        string dotnetupPath,
        bool dotnetupOnly = false,
        string? dotnetInstallPath = null)
    {
        var profilePaths = provider.GetProfilePaths();
        var entry = provider.GenerateProfileEntry(dotnetupPath, dotnetupOnly, dotnetInstallPath);
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
            File.WriteAllText(profilePath, entry + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return true;
        }

        var fileState = ReadProfileFile(profilePath);
        var lines = fileState.Lines;
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
            WriteProfileFile(profilePath, lines, fileState, ensureTrailingNewLine: fileState.EndsWithTrailingNewLine);
            return true;
        }

        // No existing entry — append
        File.Copy(profilePath, profilePath + BackupSuffix, overwrite: true);
        if (lines.Count > 0)
        {
            lines.Add(string.Empty);
        }

        lines.AddRange(entryLines);
        WriteProfileFile(profilePath, lines, fileState, ensureTrailingNewLine: true);
        return true;
    }

    private static bool RemoveEntryFromFile(string profilePath)
    {
        if (!File.Exists(profilePath))
        {
            return false;
        }

        var fileState = ReadProfileFile(profilePath);
        var lines = fileState.Lines;
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
            WriteProfileFile(
                profilePath,
                lines,
                fileState,
                ensureTrailingNewLine: lines.Count > 0 && fileState.EndsWithTrailingNewLine);
        }

        return modified;
    }

    private static ProfileFileState ReadProfileFile(string profilePath)
    {
        byte[] bytes = File.ReadAllBytes(profilePath);

        using var stream = new MemoryStream(bytes);
        using var reader = new StreamReader(
            stream,
            encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            detectEncodingFromByteOrderMarks: true);

        string content = reader.ReadToEnd();
        var encoding = GetWritableEncoding(reader.CurrentEncoding, HasPreamble(bytes, reader.CurrentEncoding));
        var lines = new List<string>();

        using var stringReader = new StringReader(content);
        string? line;
        while ((line = stringReader.ReadLine()) is not null)
        {
            lines.Add(line);
        }

        return new ProfileFileState(
            lines,
            encoding,
            DetectLineEnding(content),
            EndsWithLineEnding(content));
    }

    private static void WriteProfileFile(
        string profilePath,
        IReadOnlyList<string> lines,
        ProfileFileState fileState,
        bool ensureTrailingNewLine)
    {
        string content = string.Join(fileState.NewLine, lines);

        if (lines.Count > 0 && ensureTrailingNewLine)
        {
            content += fileState.NewLine;
        }

        File.WriteAllText(profilePath, content, fileState.Encoding);
    }

    private static string DetectLineEnding(string content)
    {
        if (content.Contains("\r\n", StringComparison.Ordinal))
        {
            return "\r\n";
        }

        if (content.Contains('\n'))
        {
            return "\n";
        }

        if (content.Contains('\r'))
        {
            return "\r";
        }

        return Environment.NewLine;
    }

    private static bool EndsWithLineEnding(string content) =>
        content.EndsWith("\r\n", StringComparison.Ordinal) ||
        content.EndsWith('\n') ||
        content.EndsWith('\r');

    private static Encoding GetWritableEncoding(Encoding detectedEncoding, bool hadBom)
    {
        if (detectedEncoding.CodePage == Encoding.UTF8.CodePage)
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: hadBom);
        }

        return detectedEncoding;
    }

    private static bool HasPreamble(byte[] bytes, Encoding encoding)
    {
        byte[] preamble = encoding.GetPreamble();
        return preamble.Length > 0 && bytes.AsSpan().StartsWith(preamble);
    }
}
