// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO.Compression;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Edge.Mount.Archive
{
    /// <summary>
    /// Mount point implementation for zip file.
    /// NuGet packages are zip files, so they are handled by this mount point.
    /// </summary>
    internal class ZipFileMountPoint : IMountPoint
    {
        private IReadOnlyDictionary<string, IFileSystemInfo> _universe;

        internal ZipFileMountPoint(IEngineEnvironmentSettings environmentSettings, IMountPoint parent, string mountPointUri, ZipArchive archive)
        {
            MountPointUri = mountPointUri;
            Parent = parent;
            EnvironmentSettings = environmentSettings;
            Archive = archive;
            Root = new ZipFileDirectory(this, "/", "");
        }

        public IDirectory Root { get; }

        public IMountPoint Parent { get; }

        public string MountPointUri { get; }

        public IEngineEnvironmentSettings EnvironmentSettings { get; }

        internal ZipArchive Archive { get; }

        internal IReadOnlyDictionary<string, IFileSystemInfo> Universe
        {
            get
            {
                if (_universe == null)
                {
                    Dictionary<string, IFileSystemInfo> universe = new Dictionary<string, IFileSystemInfo>
                    {
                        ["/"] = Root
                    };

                    foreach (ZipArchiveEntry entry in Archive.Entries)
                    {
                        string[] parts = entry.FullName.Split('/', '\\');
                        string path = "/";
                        IDirectory parentDir = (IDirectory)universe["/"];

                        for (int i = 0; parentDir != null && i < parts.Length - 1; ++i)
                        {
                            parts[i] = Uri.UnescapeDataString(parts[i]);
                            path += parts[i] + "/";

                            if (!universe.TryGetValue(path, out IFileSystemInfo parentDirEntry))
                            {
                                universe[path] = parentDirEntry = new ZipFileDirectory(this, path, parts[i]);
                            }

                            //If we mistakenly classified something with children as a file before, reclassify it as a directory
                            if (parentDirEntry is IFile file)
                            {
                                universe[path] = parentDirEntry = new ZipFileDirectory(this, file.FullPath, file.Name);
                            }

                            parentDir = parentDirEntry as IDirectory;
                        }

                        if (parentDir != null && !string.IsNullOrEmpty(entry.Name))
                        {
                            string unescaped = Uri.UnescapeDataString(entry.Name);
                            path += unescaped;
                            universe[path] = new ZipFileFile(this, path, unescaped, entry);
                        }
                    }

                    _universe = universe;
                }

                return _universe;
            }
        }

        internal Guid MountPointFactoryId => ZipFileMountPointFactory.FactoryId;

        public IFile FileInfo(string path)
        {
            return new ZipFileFile(this, path, path.Substring(path.LastIndexOf('/') + 1), null);
        }

        public IDirectory DirectoryInfo(string path)
        {
            if (Universe.TryGetValue(path, out IFileSystemInfo info))
            {
                return info as IDirectory;
            }
            else if (Universe.TryGetValue(path + "/", out info))
            {
                return info as IDirectory;
            }

            return new ZipFileDirectory(this, path, path.Substring(path.LastIndexOf('/') + 1));
        }

        public IFileSystemInfo FileSystemInfo(string path)
        {
            IFile file = FileInfo(path);

            if (file.Exists)
            {
                return file;
            }

            return DirectoryInfo(path);
        }

        public void Dispose()
        {
            Archive.Dispose();
        }
    }
}
