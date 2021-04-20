// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Edge.Mount.Archive
{
    internal class ZipFileDirectory : DirectoryBase
    {
        private readonly ZipFileMountPoint _mountPoint;

        public ZipFileDirectory(IMountPoint mountPoint, string fullPath, string name)
            : base(mountPoint, fullPath, name)
        {
            _mountPoint = (ZipFileMountPoint)mountPoint;
        }

        public override bool Exists => _mountPoint.Universe.ContainsKey(FullPath);

        public override IEnumerable<IFileSystemInfo> EnumerateFileSystemInfos(string pattern, SearchOption searchOption)
        {
            string rx = Regex.Escape(pattern);
            rx = rx.Replace("\\*", ".*").Replace("\\?", ".?");
            Regex r = new Regex($"^{rx}$");
            return _mountPoint.Universe.Values.Where(x => x.FullPath.StartsWith(FullPath, StringComparison.Ordinal) && x.FullPath.Length != FullPath.Length && r.IsMatch(x.Name)).Where(x => searchOption == SearchOption.AllDirectories || x.FullPath.TrimEnd('/').Count(y => y == '/') == FullPath.Count(y => y == '/'));
        }
    }
}
