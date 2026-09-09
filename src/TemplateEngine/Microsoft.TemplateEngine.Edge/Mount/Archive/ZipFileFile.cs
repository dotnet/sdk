// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Edge.Mount.Archive
{
    internal class ZipFileFile : FileBase, IKnownLengthFile
    {
        private readonly ZipFileMountPoint _mountPoint;
        private ZipArchiveEntry? _entry;

        internal ZipFileFile(IMountPoint mountPoint, string fullPath, string name, ZipArchiveEntry? entry)
            : base(mountPoint, fullPath, name)
        {
            _entry = entry;
            _mountPoint = (ZipFileMountPoint)mountPoint;
        }

        public override bool Exists => _entry != null || (_mountPoint.Universe.TryGetValue(FullPath, out var info) && info.Kind == FileSystemInfoKind.File);

        long IKnownLengthFile.Length => GetEntry().Length;

        public override Stream OpenRead()
        {
            return GetEntry().Open();
        }

        private ZipArchiveEntry GetEntry()
        {
            if (_entry == null)
            {
                if (!_mountPoint.Universe.TryGetValue(FullPath, out var info) || info.Kind != FileSystemInfoKind.File || !info.Exists)
                {
                    throw new FileNotFoundException("File not found", FullPath);
                }
                if (info is not ZipFileFile self)
                {
                    throw new FileNotFoundException("File not found", FullPath);
                }

                if (self._entry == null)
                {
                    throw new FileNotFoundException("File not found", FullPath);
                }

                _entry = self._entry;
            }

            return _entry;
        }
    }
}
