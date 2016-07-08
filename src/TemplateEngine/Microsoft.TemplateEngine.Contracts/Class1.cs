using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Contracts
{
    public enum FileSystemInfoKind
    {
        File,
        Directory
    }

    public enum TemplateParameterType
    {
        FreeText,
        Choice,
        Boolean,
        Integer,
        FloatingPoint
    }

    public interface IComponent
    {
        Guid Id { get; }
    }

    public interface IDirectory
    {
        IEnumerable<IDirectory> EnumerateDirectories(string pattern, SearchOption searchOption);

        IEnumerable<IFile> EnumerateFiles(string pattern, SearchOption searchOption);

        IEnumerable<IFileSystemInfo> EnumerateFileSystemInfos(string pattern, SearchOption searchOption);
    }

    public interface IFile
    {
        IDirectory Parent { get; }
    }

    public interface IFileSystemInfo
    {
        bool Exists { get; }

        FileSystemInfoKind Kind { get; }

        string Name { get; }

        IDirectory Parent { get; }
    }

    public interface IGenerator : IComponent
    {
        Task<ITemplate> DiscoverTemplatesAsync(IDirectory directory);
    }

    public interface IMountPoint : IDirectory
    {
        MountPointInfo Info { get; }
    }

    public interface IMountPointFactory : IComponent
    {
        bool TryMount(MountPointInfo info, out IMountPoint mountPoint);
    }

    public interface ITemplate
    {
        IFile ConfigurationFile { get; }

        string Description { get; }

        string Name { get; }

        IReadOnlyList<ITemplateParameter> Parameters { get; }

        IReadOnlyDictionary<string, IReadOnlyList<string>> Pivots { get; }

        IReadOnlyDictionary<string, object> Properties { get; }

        Task CreateAsync(IReadOnlyDictionary<string, string> pivots, IReadOnlyDictionary<ITemplateParameter, string> parameters);
    }

    public interface ITemplateParameter
    {
        IReadOnlyList<ITemplateParameterValue> AllowedValues { get; }

        string Description { get; }

        string Name { get; }

        TemplateParameterType Type { get; }
    }

    public interface ITemplateParameterValue
    {
        string Description { get; }

        string Value { get; }
    }

    public class MountPointInfo
    {
        public MountPointInfo(Guid parentMountPointId, Guid mountPointId, Guid mountPointFactoryId, string pathInParent)
        {
            ParentMountPointIdId = parentMountPointId;
            MountPointId = mountPointId;
            MountPointFactoryId = mountPointFactoryId;
            PathInParent = pathInParent;
        }

        public Guid MountPointFactoryId { get; }

        public Guid MountPointId { get; }

        public Guid ParentMountPointIdId { get; }

        public string PathInParent { get; }
    }
}
