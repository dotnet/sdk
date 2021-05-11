// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Edge.Mount.FileSystem
{
    internal class FileSystemDirectory : DirectoryBase
    {
        private readonly string _physicalPath;
        private readonly SettingsFilePaths _paths;
        private readonly IPhysicalFileSystem _fileSystem;

        internal FileSystemDirectory(IMountPoint mountPoint, string fullPath, string name, string physicalPath)
            : base(mountPoint, EnsureTrailingSlash(fullPath), name)
        {
            _physicalPath = physicalPath;
            _paths = new SettingsFilePaths(mountPoint.EnvironmentSettings);
            _fileSystem = mountPoint.EnvironmentSettings.Host.FileSystem;
        }

        public override bool Exists => _fileSystem.DirectoryExists(_physicalPath);

        public override IEnumerable<IFileSystemInfo> EnumerateFileSystemInfos(string pattern, SearchOption searchOption)
        {
            return _paths.EnumerateFileSystemEntries(_physicalPath, pattern, searchOption).Select(x =>
            {
                string baseName = x.Substring(((FileSystemMountPoint)MountPoint).MountPointRootPath.Length).Replace(Path.DirectorySeparatorChar, '/');

                if (baseName.Length == 0)
                {
                    baseName = "/";
                }

                if (baseName[0] != '/')
                {
                    baseName = "/" + baseName;
                }

                if (_fileSystem.DirectoryExists(x) && baseName[baseName.Length - 1] != '/')
                {
                    baseName = baseName + "/";
                }

                return MountPoint.FileSystemInfo(baseName);
            });
        }

        public override IEnumerable<IDirectory> EnumerateDirectories(string pattern, SearchOption searchOption)
        {
            return _paths.EnumerateDirectories(_physicalPath, pattern, searchOption).Select(x =>
            {
                string baseName = x.Substring(((FileSystemMountPoint)MountPoint).MountPointRootPath.Length).Replace(Path.DirectorySeparatorChar, '/');

                if (baseName.Length == 0)
                {
                    baseName = "/";
                }

                if (baseName[0] != '/')
                {
                    baseName = "/" + baseName;
                }

                if (baseName[baseName.Length - 1] != '/')
                {
                    baseName = baseName + "/";
                }

                return new FileSystemDirectory(MountPoint, baseName, _paths.Name(x), x);
            });
        }

        public override IEnumerable<IFile> EnumerateFiles(string pattern, SearchOption searchOption)
        {
            return _paths.EnumerateFiles(_physicalPath, pattern, searchOption).Select(x =>
            {
                string baseName = x.Substring(((FileSystemMountPoint)MountPoint).MountPointRootPath.Length).Replace(Path.DirectorySeparatorChar, '/');

                if (baseName.Length == 0)
                {
                    baseName = "/";
                }

                if (baseName[0] != '/')
                {
                    baseName = "/" + baseName;
                }

                return new FileSystemFile(MountPoint, baseName, _paths.Name(x), x);
            });
        }

        private static string EnsureTrailingSlash(string path)
        {
            if (path.Last() == '/')
            {
                return path;
            }
            else
            {
                return path + "/";
            }
        }
    }
}
