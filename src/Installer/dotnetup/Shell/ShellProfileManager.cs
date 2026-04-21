// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Shell;

/// <summary>
/// Manages shell profile file modifications to persist .NET environment configuration.
/// </summary>
public class ShellProfileManager
{
    internal const string BeginMarkerComment = "# dotnetup: begin";
    internal const string EndMarkerComment = "# dotnetup: end";
    // Used only during file replacement and deleted after a successful update.
    private const string BackupSuffix = ".dotnetup-backup";

    private sealed record ProfileFileState(
        List<string> Lines,
        Encoding Encoding,
        string NewLine,
        bool EndsWithTrailingNewLine);

    /// <summary>
    /// Ensures the correct dotnetup profile entry is present in all profile files for the given shell provider.
    /// If an entry already exists, it is replaced in-place. If no entry exists, one is appended.
    /// Existing files are updated via a write-and-rename flow to avoid partially written profiles.
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
            // New file — just write the managed block.
            File.WriteAllText(profilePath, WrapEntryWithMarkers(entry) + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return true;
        }

        var fileState = ReadProfileFile(profilePath);
        var entryLines = WrapEntryWithMarkers(entry).Split('\n', StringSplitOptions.None)
            .Select(l => l.TrimEnd('\r'))
            .ToArray();
        var existingBlocks = FindManagedBlocks(fileState.Lines, profilePath);

        if (existingBlocks.Count > 0)
        {
            var firstBlock = existingBlocks[0];
            var oldEntry = fileState.Lines.GetRange(firstBlock.Start, firstBlock.EndExclusive - firstBlock.Start);
            if (existingBlocks.Count == 1 &&
                oldEntry.Count == entryLines.Length &&
                oldEntry.Zip(entryLines).All(pair => pair.First.TrimEnd() == pair.Second.TrimEnd()))
            {
                return false; // Already correct
            }

            for (int i = existingBlocks.Count - 1; i >= 0; i--)
            {
                var block = existingBlocks[i];
                fileState.Lines.RemoveRange(block.Start, block.EndExclusive - block.Start);
            }

            fileState.Lines.InsertRange(firstBlock.Start, entryLines);
            WriteProfileFile(profilePath, fileState);
            return true;
        }

        // No existing entry — append
        if (fileState.Lines.Count > 0 && !string.IsNullOrWhiteSpace(fileState.Lines[^1]))
        {
            fileState.Lines.Add(string.Empty);
        }

        fileState.Lines.AddRange(entryLines);
        fileState = fileState with { EndsWithTrailingNewLine = true };
        WriteProfileFile(profilePath, fileState);
        return true;
    }

    private static bool RemoveEntryFromFile(string profilePath)
    {
        if (!File.Exists(profilePath))
        {
            return false;
        }

        var fileState = ReadProfileFile(profilePath);
        var existingBlocks = FindManagedBlocks(fileState.Lines, profilePath);

        if (existingBlocks.Count == 0)
        {
            return false;
        }

        for (int i = existingBlocks.Count - 1; i >= 0; i--)
        {
            var block = existingBlocks[i];
            fileState.Lines.RemoveRange(block.Start, block.EndExclusive - block.Start);
        }

        fileState = fileState with
        {
            EndsWithTrailingNewLine = fileState.Lines.Count > 0 && fileState.EndsWithTrailingNewLine
        };

        WriteProfileFile(profilePath, fileState);
        return true;
    }

    private static ProfileFileState ReadProfileFile(string profilePath)
    {
        byte[] bytes = File.ReadAllBytes(profilePath);
        var utf8FallbackEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        using var stream = new MemoryStream(bytes);
        using var reader = new StreamReader(
            stream,
            // Use UTF-8 as the fallback, but allow an existing BOM to override it.
            encoding: utf8FallbackEncoding,
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

    private static void WriteProfileFile(string profilePath, ProfileFileState fileState)
    {
        string content = string.Join(fileState.NewLine, fileState.Lines);

        if (fileState.Lines.Count > 0 && fileState.EndsWithTrailingNewLine)
        {
            content += fileState.NewLine;
        }

        if (!File.Exists(profilePath))
        {
            File.WriteAllText(profilePath, content, fileState.Encoding);
            return;
        }

        var directory = Path.GetDirectoryName(profilePath)
            ?? throw new InvalidOperationException($"Unable to determine the directory for '{profilePath}'.");
        var tempPath = Path.Combine(directory, $"{Path.GetFileName(profilePath)}.{Path.GetRandomFileName()}.tmp");
        var backupPath = profilePath + BackupSuffix;

        File.WriteAllText(tempPath, content, fileState.Encoding);

        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }

        try
        {
            File.Move(profilePath, backupPath);

            try
            {
                File.Move(tempPath, profilePath);
            }
            catch (IOException)
            {
                RestoreOriginalFile(profilePath, backupPath, tempPath);
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                RestoreOriginalFile(profilePath, backupPath, tempPath);
                throw;
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        File.Delete(backupPath);
    }

    private static void RestoreOriginalFile(string profilePath, string backupPath, string tempPath)
    {
        if (File.Exists(profilePath))
        {
            File.Delete(profilePath);
        }

        if (File.Exists(backupPath))
        {
            File.Move(backupPath, profilePath);
        }

        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }
    }

    private static string WrapEntryWithMarkers(string entry) =>
        $"{BeginMarkerComment}\n{entry}\n{EndMarkerComment}";

    private static List<(int Start, int EndExclusive)> FindManagedBlocks(List<string> lines, string profilePath)
    {
        var blocks = new List<(int Start, int EndExclusive)>();

        for (int i = 0; i < lines.Count; i++)
        {
            var trimmedLine = lines[i].TrimEnd();
            if (trimmedLine == BeginMarkerComment)
            {
                int endIndex = i + 1;
                while (endIndex < lines.Count && lines[endIndex].TrimEnd() != EndMarkerComment)
                {
                    endIndex++;
                }

                if (endIndex >= lines.Count)
                {
                    throw new DotnetInstallException(
                        DotnetInstallErrorCode.UserConfigurationCorrupted,
                        $"The shell profile '{profilePath}' contains a malformed dotnetup block: '{BeginMarkerComment}' does not have a matching '{EndMarkerComment}'. Remove or repair the block manually and try again.");
                }

                blocks.Add((i, endIndex + 1));
                i = endIndex;
            }
        }

        return blocks;
    }

    private static string DetectLineEnding(string content)
    {
        if (content.Contains("\r\n", StringComparison.Ordinal))
        {
            return "\r\n";
        }

        if (content.Contains('\n', StringComparison.Ordinal))
        {
            return "\n";
        }

        if (content.Contains('\r', StringComparison.Ordinal))
        {
            return "\r";
        }

        return Environment.NewLine;
    }

    private static bool EndsWithLineEnding(string content) =>
        content.EndsWith("\r\n", StringComparison.Ordinal) ||
        content.EndsWith('\n', StringComparison.Ordinal) ||
        content.EndsWith('\r', StringComparison.Ordinal);

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
        if (encoding.CodePage == Encoding.UTF8.CodePage)
        {
            return bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
        }

        byte[] preamble = encoding.GetPreamble();
        return preamble.Length > 0 && bytes.AsSpan().StartsWith(preamble);
    }
}
