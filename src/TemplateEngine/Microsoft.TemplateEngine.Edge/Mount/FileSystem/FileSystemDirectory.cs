using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Edge.Mount.FileSystem
{
    internal class FileSystemDirectory : DirectoryBase
    {
        private readonly string _physicalPath;
        private readonly Paths _paths;

        public FileSystemDirectory(IMountPoint mountPoint, string fullPath, string name, string physicalPath)
            : base(mountPoint, EnsureTrailingSlash(fullPath), name)
        {
            _physicalPath = physicalPath;
            _paths = new Paths(mountPoint.EnvironmentSettings);
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

        public override bool Exists => _paths.DirectoryExists(_physicalPath);

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

                if (_paths.DirectoryExists(x) && baseName[baseName.Length - 1] != '/')
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
    }
}
