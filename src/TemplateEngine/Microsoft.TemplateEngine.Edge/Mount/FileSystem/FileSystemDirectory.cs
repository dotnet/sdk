using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Edge.Mount.FileSystem
{
    internal class FileSystemDirectory : DirectoryBase
    {
        private readonly DirectoryInfo _dir;

        public FileSystemDirectory(IMountPoint mountPoint, string fullPath, string name, DirectoryInfo dir)
            : base(mountPoint, fullPath, name)
        {
            _dir = dir;
        }

        public override bool Exists => _dir.Exists;

        public override IEnumerable<IFileSystemInfo> EnumerateFileSystemInfos(string pattern, SearchOption searchOption)
        {
            return _dir.EnumerateFileSystemInfos(pattern, searchOption).Select(x =>
            {
                string baseName = x.FullName.Substring(MountPoint.Info.Place.Length).Replace(Path.DirectorySeparatorChar, '/');

                if (baseName.Length == 0)
                {
                    baseName = "/";
                }

                if (baseName[0] != '/')
                {
                    baseName = "/" + baseName;
                }

                if (x is DirectoryInfo && baseName[baseName.Length - 1] != '/')
                {
                    baseName = baseName + "/";
                }

                return MountPoint.FileSystemInfo(baseName);
            });
        }

        public override IEnumerable<IDirectory> EnumerateDirectories(string pattern, SearchOption searchOption)
        {
            return _dir.EnumerateDirectories(pattern, searchOption).Select(x =>
            {
                string baseName = x.FullName.Substring(MountPoint.Info.Place.Length).Replace(Path.DirectorySeparatorChar, '/');

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

                return new FileSystemDirectory(MountPoint, baseName, x.Name, x);
            });
        }

        public override IEnumerable<IFile> EnumerateFiles(string pattern, SearchOption searchOption)
        {
            return _dir.EnumerateFiles(pattern, searchOption).Select(x =>
            {
                string baseName = x.FullName.Substring(MountPoint.Info.Place.Length).Replace(Path.DirectorySeparatorChar, '/');

                if (baseName.Length == 0)
                {
                    baseName = "/";
                }

                if (baseName[0] != '/')
                {
                    baseName = "/" + baseName;
                }

                return new FileSystemFile(MountPoint, baseName, x.Name, x);
            });
        }
    }
}