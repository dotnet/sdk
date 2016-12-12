using System.IO;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Edge.Mount.FileSystem
{
    public class FileSystemMountPoint : IMountPoint
    {
        public FileSystemMountPoint(MountPointInfo info)
        {
            Info = info;
            Root = new FileSystemDirectory(this, "/", "", info.Place);
        }

        public MountPointInfo Info { get; }

        public IDirectory Root { get; }

        public IFile FileInfo(string fullPath)
        {
            string realPath = Path.Combine(Info.Place, fullPath.TrimStart('/'));
            return new FileSystemFile(this, realPath, realPath.Name(), realPath);
        }

        public IDirectory DirectoryInfo(string fullPath)
        {
            string realPath = Path.Combine(Info.Place, fullPath.TrimStart('/'));
            return new FileSystemDirectory(this, fullPath, realPath.Name(), realPath);
        }

        public IFileSystemInfo FileSystemInfo(string fullPath)
        {
            string realPath = Path.Combine(Info.Place, fullPath.TrimStart('/'));
            if (Directory.Exists(realPath))
            {
                return new FileSystemDirectory(this, fullPath, realPath.Name(), realPath);
            }

            return new FileSystemFile(this, fullPath, realPath.Name(), realPath);
        }
    }
}