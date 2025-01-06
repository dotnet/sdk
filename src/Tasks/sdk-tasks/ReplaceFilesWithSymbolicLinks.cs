// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if !NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Enumeration;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
#endif
namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Replaces files that have the same content with hard links.
    /// </summary>
    public sealed class ReplaceFilesWithSymbolicLinks  : Task
    {
        /// <summary>
        /// The path to the directory to recursively search for files to replace with symbolic links.
        /// </summary>
        [Required]
        public string Directory { get; set; } = "";

        /// <summary>
        /// The path to the directory with files to link to.
        /// </summary>
        [Required]
        public string LinkToFilesFrom { get; set; } = "";

#if NETFRAMEWORK
        public override bool Execute()
        {
            Log.LogError($"{nameof(ReplaceFilesWithSymbolicLinks)} is not supported on .NET Framework.");
            return false;
        }
#else
        public override bool Execute()
        {
            if (OperatingSystem.IsWindows())
            {
                Log.LogError($"{nameof(ReplaceFilesWithSymbolicLinks)} is not supported on Windows.");
                return false;
            }

            if (!System.IO.Directory.Exists(Directory))
            {
                Log.LogError($"'{Directory}' does not exist.");
                return false;
            }

            if (!System.IO.Directory.Exists(LinkToFilesFrom))
            {
                Log.LogError($"'{LinkToFilesFrom}' does not exist.");
                return false;
            }

            // Find all non-empty, non-symbolic link files.
            string[] files = new FileSystemEnumerable<string>(
                                        Directory,
                                        (ref FileSystemEntry entry) => entry.ToFullPath(),
                                        new EnumerationOptions()
                                        {
                                            AttributesToSkip = FileAttributes.ReparsePoint,
                                            RecurseSubdirectories = true
                                        })
            {
                ShouldIncludePredicate = (ref FileSystemEntry entry) => !entry.IsDirectory
                                                                        && entry.Length > 0
            }.ToArray();

            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);

                // Look for a file with the same name in LinkToFilesFrom
                // and replace it with a symbolic link if it has the same content.
                string targetFile = Path.Combine(LinkToFilesFrom, fileName);
                if (File.Exists(targetFile) && FilesHaveSameContent(file, targetFile))
                {
                    ReplaceByLinkTo(file, targetFile);
                }
            }

            return true;
        }

        private unsafe bool FilesHaveSameContent(string path1, string path2)
        {
            using var mappedFile1 = MemoryMappedFile.CreateFromFile(path1, FileMode.Open);
            using var accessor1 = mappedFile1.CreateViewAccessor();
            byte* ptr1 = null;

            using var mappedFile2 = MemoryMappedFile.CreateFromFile(path2, FileMode.Open);
            using var accessor2 = mappedFile2.CreateViewAccessor();
            byte* ptr2 = null;

            try
            {
                accessor1.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr1);
                Span<byte> span1 = new Span<byte>(ptr1, checked((int)accessor1.SafeMemoryMappedViewHandle.ByteLength));

                accessor2.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr2);
                Span<byte> span2 = new Span<byte>(ptr2, checked((int)accessor2.SafeMemoryMappedViewHandle.ByteLength));

                return span1.SequenceEqual(span2);
            }
            finally
            {
                if (ptr1 != null)
                {
                    accessor1.SafeMemoryMappedViewHandle.ReleasePointer();
                    ptr1 = null;
                }
                if (ptr2 != null)
                {
                    accessor2.SafeMemoryMappedViewHandle.ReleasePointer();
                    ptr2 = null;
                }
            }
        }

        void ReplaceByLinkTo(string path, string pathToTarget)
        {
            // To link, the target mustn't exist. Make a backup, so we can restore it when linking fails.
            string backupFile = $"{path}.pre_link_backup";
            File.Move(path, backupFile);

            try
            {
                string relativePath = Path.GetRelativePath(Path.GetDirectoryName(path)!, pathToTarget);
                File.CreateSymbolicLink(path, relativePath);

                File.Delete(backupFile);

                Log.LogMessage(MessageImportance.Normal, $"Linked '{path}' to '{relativePath}'.");
            }
            catch (Exception ex)
            {
                Log.LogError($"Unable to link '{path}' to '{pathToTarget}.': {ex}");

                File.Move(backupFile, path);

                throw;
            }
        }
#endif
    }
}
