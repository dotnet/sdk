using System.IO;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Edge.Mount.FileSystem
{
    internal class FileSystemFile : FileBase
    {
        private readonly string _physicalPath;

        public FileSystemFile(IMountPoint mountPoint, string fullPath, string name, string physicalPath)
            : base(mountPoint, fullPath, name)
        {
            _physicalPath = physicalPath;
        }

        public override bool Exists => MountPoint.EnvironmentSettings.Host.FileSystem.FileExists(_physicalPath);

        public override Stream OpenRead()
        {
            return MountPoint.EnvironmentSettings.Host.FileSystem.OpenRead(_physicalPath);
        }
    }
}
