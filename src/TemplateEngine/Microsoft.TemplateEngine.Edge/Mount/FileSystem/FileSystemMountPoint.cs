using System.IO;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Edge.Mount.FileSystem
{
    public class FileSystemMountPoint : IMountPoint
    {
        public FileSystemMountPoint(MountPointInfo info)
        {
            Info = info;
            Root = new FileSystemDirectory(this, "/", "", new DirectoryInfo(info.Place));
        }

        public MountPointInfo Info { get; }

        public IDirectory Root { get; }

        public IFile FileInfo(string fullPath)
        {
            string realPath = Path.Combine(Info.Place, fullPath.TrimStart('/'));
            FileInfo info = new FileInfo(realPath);
            return new FileSystemFile(this, realPath, info.Name, info);
        }

        public IDirectory DirectoryInfo(string fullPath)
        {
            string realPath = Path.Combine(Info.Place, fullPath.TrimStart('/'));
            DirectoryInfo info = new DirectoryInfo(realPath);
            return new FileSystemDirectory(this, fullPath, info.Name, info);
        }

        public IFileSystemInfo FileSystemInfo(string fullPath)
        {
            string realPath = Path.Combine(Info.Place, fullPath.TrimStart('/'));
            if (Directory.Exists(realPath))
            {
                DirectoryInfo info = new DirectoryInfo(realPath);
                return new FileSystemDirectory(this, fullPath, info.Name, info);
            }
            else
            {
                FileInfo info = new FileInfo(realPath);
                return new FileSystemFile(this, fullPath, info.Name, info);
            }
        }
    }
}