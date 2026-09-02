// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Build.Tasks;

/// <summary>
/// Copies files while preserving relative symbolic links. Unlike the standard MSBuild Copy task,
/// this task recreates symbolic links at the destination with their original relative targets
/// instead of resolving and copying the target file contents.
/// </summary>
public sealed class CopyPreservingRelativeSymlinks : Task
{
    /// <summary>
    /// The source files to copy.
    /// </summary>
    [Required]
    public ITaskItem[] SourceFiles { get; set; } = [];

    /// <summary>
    /// The destination files (must match SourceFiles count).
    /// </summary>
    [Required]
    public ITaskItem[] DestinationFiles { get; set; } = [];

    /// <summary>
    /// The files that were successfully copied.
    /// </summary>
    [Output]
    public ITaskItem[] CopiedFiles { get; private set; } = [];

    public override bool Execute()
    {
        if (SourceFiles.Length == 0)
        {
            return true;
        }

        if (SourceFiles.Length != DestinationFiles.Length)
        {
            Log.LogError($"SourceFiles count ({SourceFiles.Length}) must match DestinationFiles count ({DestinationFiles.Length}).");
            return false;
        }

        // Build a set of normalized source paths for symlink target validation
        var sourcePathSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in SourceFiles)
        {
            sourcePathSet.Add(Path.GetFullPath(item.ItemSpec));
        }

        var copiedFiles = new List<ITaskItem>();
        bool hasErrors = false;

        for (int i = 0; i < SourceFiles.Length; i++)
        {
            var sourcePath = SourceFiles[i].ItemSpec;
            var destPath = DestinationFiles[i].ItemSpec;

            try
            {
                CopyFile(sourcePath, destPath, sourcePathSet);
                copiedFiles.Add(new TaskItem(destPath));
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to copy '{sourcePath}' to '{destPath}': {ex.Message}");
                hasErrors = true;
            }
        }

        CopiedFiles = copiedFiles.ToArray();
        Log.LogMessage(MessageImportance.Normal, $"Copied {copiedFiles.Count} files.");

        return !hasErrors;
    }

    private void CopyFile(string sourcePath, string destPath, HashSet<string> sourcePathSet)
    {
        var sourceInfo = new FileInfo(sourcePath);

        if (!sourceInfo.Exists)
        {
            throw new FileNotFoundException($"Source file does not exist: '{sourcePath}'");
        }

        // Create destination directory if needed
        var destDir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        // Remove existing file/link at destination
        if (File.Exists(destPath))
        {
            File.Delete(destPath);
        }

        // Check if source is a symbolic link
        if (sourceInfo.LinkTarget != null)
        {
            // Validate that the symlink target resolves to a file within the copy scope
            var sourceDir = Path.GetDirectoryName(Path.GetFullPath(sourcePath))!;
            var resolvedTarget = Path.GetFullPath(Path.Combine(sourceDir, sourceInfo.LinkTarget));

            if (!sourcePathSet.Contains(resolvedTarget))
            {
                throw new InvalidOperationException(
                    $"Symbolic link target '{sourceInfo.LinkTarget}' resolves to '{resolvedTarget}' which is outside the copy scope.");
            }

            // Recreate the symbolic link with the same relative target
            File.CreateSymbolicLink(destPath, sourceInfo.LinkTarget);
            Log.LogMessage(MessageImportance.Low, $"Created symlink: '{destPath}' -> '{sourceInfo.LinkTarget}'");
        }
        else
        {
            File.Copy(sourcePath, destPath);
            Log.LogMessage(MessageImportance.Low, $"Copied: '{sourcePath}' -> '{destPath}'");
        }
    }
}
#endif
