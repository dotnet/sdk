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
            FileInfo info = new FileInfo(fullPath);
            return new FileSystemFile(this, fullPath, info.Name, info);
        }

        public IDirectory DirectoryInfo(string fullPath)
        {
            DirectoryInfo info = new DirectoryInfo(fullPath);
            return new FileSystemDirectory(this, fullPath, info.Name, info);
        }

        public IFileSystemInfo FileSystemInfo(string fullPath)
        {
            if (Directory.Exists(fullPath))
            {
                DirectoryInfo info = new DirectoryInfo(fullPath);
                return new FileSystemDirectory(this, fullPath, info.Name, info);
            }
            else
            {
                FileInfo info = new FileInfo(fullPath);
                return new FileSystemFile(this, fullPath, info.Name, info);
            }
        }
    }
}