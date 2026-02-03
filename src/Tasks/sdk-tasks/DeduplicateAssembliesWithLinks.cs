// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks;

/// <summary>
/// Deduplicates assemblies (.dll and .exe files) in a directory by replacing duplicates with links (hard or symbolic).
/// Assemblies are grouped by content hash, and a deterministic "primary" file is selected (closest to root, alphabetically
/// first) which duplicates are linked to. Text-based files (config, json, xml, etc.) are not deduplicated.
/// </summary>
public sealed class DeduplicateAssembliesWithLinks : Task
{
    /// <summary>
    /// The root directory to scan for duplicate assemblies.
    /// </summary>
    [Required]
    public string LayoutDirectory { get; set; } = null!;

    /// <summary>
    /// If true, creates hard links. If false, creates symbolic links.
    /// </summary>
    public bool UseHardLinks { get; set; } = false;

    private string LinkType => UseHardLinks ? "hard link" : "symbolic link";

    public override bool Execute()
    {
        if (!Directory.Exists(LayoutDirectory))
        {
            Log.LogError($"LayoutDirectory '{LayoutDirectory}' does not exist.");
            return false;
        }

        Log.LogMessage(MessageImportance.High, $"Scanning for duplicate assemblies in '{LayoutDirectory}' (using {LinkType}s)...");

        // Only deduplicate assemblies - non-assembly files are small and offer minimal ROI.
        // Some non-assembly files such as config files shouldn't be linked (may be edited).
        var files = Directory.GetFiles(LayoutDirectory, "*", SearchOption.AllDirectories)
            .Where(f => IsAssembly(f))
            .ToList();

        Log.LogMessage(MessageImportance.Normal, $"Found {files.Count} assemblies eligible for deduplication.");

        var (filesByHash, hashingSuccess) = HashAndGroupFiles(files);
        if (!hashingSuccess)
        {
            return false;
        }

        var duplicateGroups = filesByHash.Values.Where(g => g.Count > 1).ToList();
        Log.LogMessage(MessageImportance.Normal, $"Found {duplicateGroups.Count} groups of duplicate assemblies.");
        return DeduplicateFileGroups(duplicateGroups);
    }

    private (Dictionary<string, List<FileEntry>> filesByHash, bool success) HashAndGroupFiles(List<string> files)
    {
        var filesByHash = new Dictionary<string, List<FileEntry>>();
        bool hasErrors = false;

        foreach (var filePath in files)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var hash = ComputeFileHash(filePath);
                var entry = new FileEntry(
                    filePath,
                    hash,
                    fileInfo.Length,
                    GetPathDepth(filePath, LayoutDirectory));

                if (!filesByHash.ContainsKey(hash))
                {
                    filesByHash[hash] = new List<FileEntry>();
                }

                filesByHash[hash].Add(entry);
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to hash file '{filePath}': {ex.Message}");
                hasErrors = true;
            }
        }

        return (filesByHash, !hasErrors);
    }

    private bool DeduplicateFileGroups(List<List<FileEntry>> duplicateGroups)
    {
        int totalFilesDeduped = 0;
        long totalBytesSaved = 0;
        bool hasErrors = false;

        foreach (var group in duplicateGroups)
        {
            // Sort deterministically: by depth (ascending), then alphabetically (ordinal for reproducibility)
            var sorted = group.OrderBy(f => f.Depth).ThenBy(f => f.Path, StringComparer.Ordinal).ToList();

            // First file is the "primary" - all duplicates will link to it
            var primary = sorted[0];
            var duplicates = sorted.Skip(1).ToList();

            foreach (var duplicate in duplicates)
            {
                try
                {
                    CreateLink(duplicate.Path, primary.Path);
                    totalFilesDeduped++;
                    totalBytesSaved += duplicate.Size;
                    Log.LogMessage(MessageImportance.Low, $"  Linked: {duplicate.Path} -> {primary.Path}");
                }
                catch (Exception ex)
                {
                    Log.LogError($"Failed to create {LinkType} from '{duplicate.Path}' to '{primary.Path}': {ex.Message}");
                    hasErrors = true;
                }
            }
        }

        Log.LogMessage(MessageImportance.High,
            $"Deduplication complete: {totalFilesDeduped} files replaced with {LinkType}s, saving {totalBytesSaved / (1024.0 * 1024.0):F2} MB.");

        return !hasErrors;
    }

    private void CreateLink(string duplicateFilePath, string primaryFilePath)
    {
        // Delete the duplicate file before creating the link
        File.Delete(duplicateFilePath);

        if (UseHardLinks)
        {
            File.CreateHardLink(duplicateFilePath, primaryFilePath);
        }
        else
        {
            // Create relative symlink so it works when directory is moved/archived
            var duplicateDirectory = Path.GetDirectoryName(duplicateFilePath)!;
            var relativePath = Path.GetRelativePath(duplicateDirectory, primaryFilePath);
            File.CreateSymbolicLink(duplicateFilePath, relativePath);
        }
    }

    private static string ComputeFileHash(string filePath)
    {
        var xxHash = new XxHash64();
        using var stream = File.OpenRead(filePath);

        byte[] buffer = new byte[65536]; // 64KB buffer
        int bytesRead;
        while ((bytesRead = stream.Read(buffer)) > 0)
        {
            xxHash.Append(buffer[..bytesRead]);
        }

        return Convert.ToHexString(xxHash.GetCurrentHash());
    }

    private static int GetPathDepth(string filePath, string rootDirectory)
    {
        var relativePath = Path.GetRelativePath(rootDirectory, filePath);
        return relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length - 1;
    }

    private static bool IsAssembly(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".exe", StringComparison.OrdinalIgnoreCase);
    }

    private record FileEntry(string Path, string Hash, long Size, int Depth);
}
#endif
