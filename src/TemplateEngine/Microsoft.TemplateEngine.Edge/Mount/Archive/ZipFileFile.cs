// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.IO.Compression;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Edge.Mount.Archive
{
    internal class ZipFileFile : FileBase
    {
        private ZipArchiveEntry _entry;
        private readonly ZipFileMountPoint _mountPoint;

        public ZipFileFile(IMountPoint mountPoint, string fullPath, string name, ZipArchiveEntry entry)
            : base(mountPoint, fullPath, name)
        {
            _entry = entry;
            _mountPoint = (ZipFileMountPoint) mountPoint;
        }

        public override bool Exists
        {
            get
            {
                return _entry != null || (_mountPoint.Universe.TryGetValue(FullPath, out var info) && info.Kind == FileSystemInfoKind.File);
            }
        }

        public override Stream OpenRead()
        {
            if (_entry == null)
            {
                if (!_mountPoint.Universe.TryGetValue(FullPath, out var info) || info.Kind != FileSystemInfoKind.File || !info.Exists)
                {
                    throw new FileNotFoundException("File not found", FullPath);
                }

                ZipFileFile self = info as ZipFileFile;

                if (self == null)
                {
                    throw new FileNotFoundException("File not found", FullPath);
                }

                _entry = self._entry;
            }

            return _entry.Open();
        }
    }
}
