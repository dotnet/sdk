using System.IO;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Edge.Mount.FileSystem
{
    public class FileSystemMountPoint : IMountPoint
    {
        private Paths _paths;

        public FileSystemMountPoint(IEngineEnvironmentSettings environmentSettings, IMountPoint parent, MountPointInfo info)
        {
            EnvironmentSettings = environmentSettings;
            _paths = new Paths(environmentSettings);
            Info = info;
            Root = new FileSystemDirectory(this, "/", "", info.Place);
        }

        public MountPointInfo Info { get; }

        public IDirectory Root { get; }

        public IEngineEnvironmentSettings EnvironmentSettings { get; }

        public IFile FileInfo(string fullPath)
        {
            string realPath = Path.Combine(Info.Place, fullPath.TrimStart('/'));

            if (!fullPath.StartsWith("/"))
            {
                fullPath = "/" + fullPath;
            }

            return new FileSystemFile(this, fullPath, _paths.Name(realPath), realPath);
        }

        public IDirectory DirectoryInfo(string fullPath)
        {
            string realPath = Path.Combine(Info.Place, fullPath.TrimStart('/'));
            return new FileSystemDirectory(this, fullPath, _paths.Name(realPath), realPath);
        }

        public IFileSystemInfo FileSystemInfo(string fullPath)
        {
            string realPath = Path.Combine(Info.Place, fullPath.TrimStart('/'));

            if (EnvironmentSettings.Host.FileSystem.DirectoryExists(realPath))
            {
                return new FileSystemDirectory(this, fullPath, _paths.Name(realPath), realPath);
            }

            return new FileSystemFile(this, fullPath, _paths.Name(realPath), realPath);
        }

        public IMountPoint Parent { get; }
    }
}
