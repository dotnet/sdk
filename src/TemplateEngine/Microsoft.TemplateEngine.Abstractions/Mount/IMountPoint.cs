using System;

namespace Microsoft.TemplateEngine.Abstractions.Mount
{
    public interface IMountPoint
    {
        MountPointInfo Info { get; }

        IDirectory Root { get; }

        IFile FileInfo(string fullPath);

        IDirectory DirectoryInfo(string fullPath);

        IFileSystemInfo FileSystemInfo(string fullPath);
    }
}