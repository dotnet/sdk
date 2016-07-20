using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Edge.Mount
{
    public abstract class FileSystemInfoBase : IFileSystemInfo
    {
        private IDirectory _parent;

        protected FileSystemInfoBase(IMountPoint mountPoint, string fullPath, string name, FileSystemInfoKind kind)
        {
            FullPath = fullPath;
            Name = name;
            Kind = kind;
            MountPoint = mountPoint;
        }

        public abstract bool Exists { get; }

        public string FullPath { get; }

        public FileSystemInfoKind Kind { get; }

        public virtual IDirectory Parent
        {
            get
            {
                if (_parent == null)
                {
                    if (FullPath == "/" || FullPath.Length < 2)
                    {
                        return null;
                    }

                    int lastSlash = FullPath.LastIndexOf('/', FullPath.Length - 2);

                    if (lastSlash < 0)
                    {
                        return null;
                    }

                    string parentPath = FullPath.Substring(0, lastSlash);
                    _parent = MountPoint.DirectoryInfo(parentPath);
                }

                return (_parent?.Exists ?? false) ? _parent : null;
            }
        }

        public string Name { get; }

        public IMountPoint MountPoint { get; }
    }
}