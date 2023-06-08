// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Enumeration;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Replaces files that have the same content with hard links.
    /// </summary>
    public sealed class RemoveDuplicateFiles  : Task
    {
        /// <summary>
        /// The path to the directory.
        /// </summary>
        [Required]
        public string Directory { get; set; } = "";

        public override bool Execute()
        {
            if (OperatingSystem.IsWindows())
            {
                Log.LogError($"{nameof(RemoveDuplicateFiles)} is not supported on Windows.");
                return false;
            }

            if (!System.IO.Directory.Exists(Directory))
            {
                Log.LogError($"'{Directory}' does not exist.");
                return false;
            }

            // Find all non-empty, non-symbolic link files.
            IEnumerable<FileInfo> fse = new FileSystemEnumerable<FileInfo>(
                                            Directory,
                                            (ref FileSystemEntry entry) => (FileInfo)entry.ToFileSystemInfo(),
                                            new EnumerationOptions()
                                            {
                                                AttributesToSkip = FileAttributes.ReparsePoint,
                                                RecurseSubdirectories = true
                                            })
            {
                ShouldIncludePredicate = (ref FileSystemEntry entry) => !entry.IsDirectory
                                                                        && entry.Length > 0
            };

            // Group them by file size.
            IEnumerable<string?[]> filesGroupedBySize = fse.GroupBy(file => file.Length,
                                                                    file => file.FullName,
                                                                    (size, files) => files.ToArray());

            // Replace files with same content with hard link.
            foreach (var files in filesGroupedBySize)
            {
                for (int i = 0; i < files.Length; i++)
                {
                    string? path1 = files[i];
                    if (path1 is null)
                    {
                        continue; // already linked.
                    }
                    for (int j = i + 1; j < files.Length; j++)
                    {
                        string? path2 = files[j];
                        if (path2 is null)
                        {
                            continue; // already linked.
                        }

                        // note: There's no public API we can use to see if paths are already linked.
                        //       We treat those paths as unlinked files, and link them again.
                        if (FilesHaveSameContent(path1, path2))
                        {
                            ReplaceByLink(path1, path2);

                            files[j] = null;
                        }
                    }
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

        void ReplaceByLink(string path1, string path2)
        {
            // To link, the target mustn't exist. Make a backup, so we can restore it when linking fails.
            string path2Backup = $"{path2}.pre_link_backup";
            File.Move(path2, path2Backup);

            int rv = SystemNative_Link(path1, path2);
            if (rv != 0)
            {
                var ex = new Win32Exception(); // Captures the LastError.

                Log.LogError($"Unable to link '{path2}' to '{path1}.': {ex}");

                File.Move(path2Backup, path2);

                throw ex;
            }
            else
            {
                File.Delete(path2Backup);

                Log.LogMessage(MessageImportance.Normal, $"Linked '{path1}' and '{path2}'.");
            }
        }

        // This native method is used by the runtime to create hard links. It is not exposed through a public .NET API.
        [DllImport("libSystem.Native", SetLastError = true)]
        static extern int SystemNative_Link(string source, string link);
    }
}
