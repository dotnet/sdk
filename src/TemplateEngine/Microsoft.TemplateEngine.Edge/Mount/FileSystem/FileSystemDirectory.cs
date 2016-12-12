using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Edge.Mount.FileSystem
{
    internal class FileSystemDirectory : DirectoryBase
    {
        private readonly string _physicalPath;

        public FileSystemDirectory(IMountPoint mountPoint, string fullPath, string name, string physicalPath)
            : base(mountPoint, EnsureTrailingSlash(fullPath), name)
        {
            _physicalPath = physicalPath;
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

        public override bool Exists => _physicalPath.DirectoryExists();

        public override IEnumerable<IFileSystemInfo> EnumerateFileSystemInfos(string pattern, SearchOption searchOption)
        {
            return _physicalPath.EnumerateFileSystemEntries(pattern, searchOption).Select(x =>
            {
                string baseName = x.Substring(MountPoint.Info.Place.Length).Replace(Path.DirectorySeparatorChar, '/');

                if (baseName.Length == 0)
                {
                    baseName = "/";
                }

                if (baseName[0] != '/')
                {
                    baseName = "/" + baseName;
                }

                if (x.DirectoryExists() && baseName[baseName.Length - 1] != '/')
                {
                    baseName = baseName + "/";
                }

                return MountPoint.FileSystemInfo(baseName);
            });
        }

        public override IEnumerable<IDirectory> EnumerateDirectories(string pattern, SearchOption searchOption)
        {
            return _physicalPath.EnumerateDirectories(pattern, searchOption).Select(x =>
            {
                string baseName = x.Substring(MountPoint.Info.Place.Length).Replace(Path.DirectorySeparatorChar, '/');

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

                return new FileSystemDirectory(MountPoint, baseName, x.Name(), x);
            });
        }

        public override IEnumerable<IFile> EnumerateFiles(string pattern, SearchOption searchOption)
        {
            return _physicalPath.EnumerateFiles(pattern, searchOption).Select(x =>
            {
                string baseName = x.Substring(new DirectoryInfo(MountPoint.Info.Place).FullName.Length).Replace(Path.DirectorySeparatorChar, '/');

                if (baseName.Length == 0)
                {
                    baseName = "/";
                }

                if (baseName[0] != '/')
                {
                    baseName = "/" + baseName;
                }

                return new FileSystemFile(MountPoint, baseName, x.Name(), x);
            });
        }
    }
}