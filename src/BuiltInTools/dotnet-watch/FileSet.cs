// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.DotNet.Watcher
{
    internal sealed class FileSet : IEnumerable<FileItem>
    {
        private readonly Dictionary<string, FileItem> _files;

        public FileSet(ProjectInfo projectInfo, IEnumerable<FileItem> files)
        {
            Project = projectInfo;
            _files = new Dictionary<string, FileItem>(StringComparer.Ordinal);
            foreach (var item in files)
            {
                _files[item.FilePath] = item;
            }
        }

        public bool TryGetValue(string filePath, out FileItem fileItem) => _files.TryGetValue(filePath, out fileItem);

        public int Count => _files.Count;

        public ProjectInfo Project { get; }

        public static readonly FileSet Empty = new FileSet(null, Array.Empty<FileItem>());

        public IEnumerator<FileItem> GetEnumerator() => _files.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
