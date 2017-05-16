using System.Collections.Generic;
using System.IO.Compression;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Edge.Mount.Archive
{
    internal class ZipFileMountPoint : IMountPoint
    {
        private IReadOnlyDictionary<string, IFileSystemInfo> _universe;

        public ZipFileMountPoint(IEngineEnvironmentSettings environmentSettings, IMountPoint parent, MountPointInfo info, ZipArchive archive)
        {
            Parent = parent;
            EnvironmentSettings = environmentSettings;
            Archive = archive;
            Info = info;
            Root = new ZipFileDirectory(this, "/", "");
        }

        public ZipArchive Archive { get; }

        public MountPointInfo Info { get; }

        public IDirectory Root { get; }

        public IFile FileInfo(string fullPath)
        {
            return new ZipFileFile(this, fullPath, fullPath.Substring(fullPath.LastIndexOf('/') + 1), null);
        }

        public IDirectory DirectoryInfo(string fullPath)
        {
            if(Universe.TryGetValue(fullPath, out IFileSystemInfo info))
            {
                return info as IDirectory;
            }
            else if (Universe.TryGetValue(fullPath + "/", out info))
            {
                return info as IDirectory;
            }

            return new ZipFileDirectory(this, fullPath, fullPath.Substring(fullPath.LastIndexOf('/') + 1));
        }

        public IFileSystemInfo FileSystemInfo(string fullPath)
        {
            IFile file = FileInfo(fullPath);

            if (file.Exists)
            {
                return file;
            }

            return DirectoryInfo(fullPath);
        }

        public IReadOnlyDictionary<string, IFileSystemInfo> Universe
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

                        if (parentDir != null)
                        {
                            path += entry.Name;
                            universe[path] = new ZipFileFile(this, path, entry.Name, entry);
                        }
                    }

                    _universe = universe;
                }

                return _universe;
            }
        }

        public IEngineEnvironmentSettings EnvironmentSettings { get; }

        public IMountPoint Parent { get; }
    }
}
