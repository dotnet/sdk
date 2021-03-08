// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.DotNet.Watcher
{
    public class FileSet : IEnumerable<FileItem>
    {
        private static readonly Version Version3_1 = new Version(3, 1);
        private static readonly Version Version6_0 = new Version(6, 0);

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

        public record ProjectInfo
        (
             string ProjectPath,
             bool IsNetCoreApp,
             Version TargetFrameworkVersion,
             string RunCommand,
             string RunArguments,
             string RunWorkingDirectory
        )
        {
            public bool IsNetCoreApp31OrNewer()
            {
                return IsNetCoreApp && TargetFrameworkVersion >= Version3_1;
            }

            public bool IsNetCoreApp60OrNewer()
            {
                return IsNetCoreApp && TargetFrameworkVersion >= Version6_0;
            }
        }
    }
}
