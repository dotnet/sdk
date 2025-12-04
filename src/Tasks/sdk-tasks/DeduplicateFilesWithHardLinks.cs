// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Deduplicates files in a directory by replacing duplicates with hardlinks.
    /// Files are grouped by content hash, and a deterministic "master" file is selected
    /// (closest to root, alphabetically first). All other duplicates are replaced with hardlinks.
    /// </summary>
    public sealed class DeduplicateFilesWithHardLinks : Task
    {
        /// <summary>
        /// The root directory to scan for duplicate files.
        /// </summary>
        [Required]
        public string LayoutDirectory { get; set; } = null!;

        /// <summary>
        /// Minimum file size in bytes to consider for deduplication (default: 1024).
        /// Small files have minimal impact on archive size.
        /// </summary>
        public int MinimumFileSize { get; set; } = 1024;

        public override bool Execute()
        {
            if (!Directory.Exists(LayoutDirectory))
            {
                Log.LogError($"LayoutDirectory '{LayoutDirectory}' does not exist.");
                return false;
            }

            Log.LogMessage(MessageImportance.High, $"Scanning for duplicate files in '{LayoutDirectory}'...");

            // Find all eligible files
            var files = Directory.GetFiles(LayoutDirectory, "*", SearchOption.AllDirectories)
                .Where(f => new FileInfo(f).Length >= MinimumFileSize)
                .ToList();

            Log.LogMessage(MessageImportance.Normal, $"Found {files.Count} files eligible for deduplication (>= {MinimumFileSize} bytes).");

            if (files.Count == 0)
            {
                return true;
            }

            var filesByHash = HashAndGroupFiles(files);
            var duplicateGroups = filesByHash.Values.Where(g => g.Count > 1).ToList();

            Log.LogMessage(MessageImportance.Normal, $"Found {duplicateGroups.Count} groups of duplicate files.");

            DeduplicateFileGroups(duplicateGroups);

            return true;
        }

        private Dictionary<string, List<FileEntry>> HashAndGroupFiles(List<string> files)
        {
            var filesByHash = new Dictionary<string, List<FileEntry>>();

            foreach (var filePath in files)
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    var hash = ComputeFileHash(filePath);
                    var entry = new FileEntry
                    {
                        Path = filePath,
                        Hash = hash,
                        Size = fileInfo.Length,
                        Depth = GetPathDepth(filePath, LayoutDirectory)
                    };

                    if (!filesByHash.ContainsKey(hash))
                    {
                        filesByHash[hash] = new List<FileEntry>();
                    }

                    filesByHash[hash].Add(entry);
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"Failed to hash file '{filePath}': {ex.Message}");
                }
            }

            return filesByHash;
        }

        private void DeduplicateFileGroups(List<List<FileEntry>> duplicateGroups)
        {
            int totalFilesDeduped = 0;
            long totalBytesSaved = 0;

            foreach (var group in duplicateGroups)
            {
                // Sort deterministically: by depth (ascending), then alphabetically
                var sorted = group.OrderBy(f => f.Depth).ThenBy(f => f.Path).ToList();

                // First file is the "master"
                var master = sorted[0];
                var duplicates = sorted.Skip(1).ToList();

                Log.LogMessage(MessageImportance.Low, $"Master file: {master.Path}");

                foreach (var duplicate in duplicates)
                {
                    try
                    {
                        if (CreateHardLink(duplicate.Path, master.Path))
                        {
                            totalFilesDeduped++;
                            totalBytesSaved += duplicate.Size;
                            Log.LogMessage(MessageImportance.Low, $"  Linked: {duplicate.Path}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.LogWarning($"Failed to create hardlink from '{duplicate.Path}' to '{master.Path}': {ex.Message}");
                    }
                }
            }

            Log.LogMessage(MessageImportance.High,
                $"Deduplication complete: {totalFilesDeduped} files replaced with hardlinks, saving {totalBytesSaved / (1024.0 * 1024.0):F2} MB.");
        }

        private string ComputeFileHash(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = sha256.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        private int GetPathDepth(string filePath, string rootDirectory)
        {
            var relativePath = Path.GetRelativePath(rootDirectory, filePath);
            return relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length - 1;
        }

        private bool CreateHardLink(string duplicateFilePath, string masterFilePath)
        {
            // TODO: Replace P/Invoke with File.CreateHardLink() when SDK targets .NET 11+
            // See: https://github.com/dotnet/runtime/issues/69030

            // Delete the duplicate file first
            File.Delete(duplicateFilePath);

            // Create hardlink
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return CreateHardLinkWindows(duplicateFilePath, masterFilePath);
            }
            else
            {
                return CreateHardLinkUnix(duplicateFilePath, masterFilePath);
            }
        }

        private bool CreateHardLinkWindows(string linkPath, string targetPath)
        {
            bool result = CreateHardLinkWin32(linkPath, targetPath, IntPtr.Zero);
            if (!result)
            {
                int errorCode = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"CreateHardLink failed with error code {errorCode}");
            }
            return result;
        }

        private bool CreateHardLinkUnix(string linkPath, string targetPath)
        {
            int result = link(targetPath, linkPath);
            if (result != 0)
            {
                int errorCode = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"link() failed with error code {errorCode}");
            }
            return true;
        }

        // P/Invoke declarations
        [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateHardLinkWin32(
            string lpFileName,
            string lpExistingFileName,
            IntPtr lpSecurityAttributes);

        [DllImport("libc", SetLastError = true)]
        private static extern int link(string oldpath, string newpath);

        private class FileEntry
        {
            public required string Path { get; set; }
            public required string Hash { get; set; }
            public long Size { get; set; }
            public int Depth { get; set; }
        }
    }
}
#endif
