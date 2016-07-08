namespace Microsoft.TemplateEngine.Abstractions.Mount
{
    public interface IFileSystemInfo
    {
        bool Exists { get; }

        string FullPath { get; }

        FileSystemInfoKind Kind { get; }

        IDirectory Parent { get; }

        string Name { get; }

        IMountPoint MountPoint { get; }
    }
}