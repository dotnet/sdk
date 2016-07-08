using System.IO;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Edge.Mount.FileSystem
{
    internal class FileSystemFile : FileBase
    {
        private readonly FileInfo _file;

        public FileSystemFile(IMountPoint mountPoint, string fullPath, string name, FileInfo fileInfo)
            : base(mountPoint, fullPath, name)
        {
            _file = fileInfo;
        }

        public override bool Exists => _file.Exists;

        public override Stream OpenRead()
        {
            return _file.OpenRead();
        }
    }
}