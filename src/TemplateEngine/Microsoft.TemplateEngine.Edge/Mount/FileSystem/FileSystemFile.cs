using System.IO;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Utils;

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

        public override bool Exists => _physicalPath.FileExists();

        public override Stream OpenRead()
        {
            return EngineEnvironmentSettings.Host.FileSystem.OpenRead(_physicalPath);
        }
    }
}