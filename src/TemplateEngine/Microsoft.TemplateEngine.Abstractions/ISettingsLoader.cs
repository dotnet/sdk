using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface ISettingsLoader
    {
        IComponentManager Components { get; }
   
        IEngineEnvironmentSettings EnvironmentSettings { get; }

        IEnumerable<MountPointInfo> MountPoints { get; }

        void AddMountPoint(IMountPoint mountPoint);

        void AddProbingPath(string probeIn);

        void GetTemplates(HashSet<ITemplateInfo> templates);

        ITemplate LoadTemplate(ITemplateInfo info, string baselineName);

        void Save();

        bool TryGetFileFromIdAndPath(Guid mountPointId, string place, out IFile file, out IMountPoint mountPoint);

        bool TryGetMountPointFromPlace(string mountPointPlace, out IMountPoint mountPoint);

        bool TryGetMountPointInfo(Guid mountPointId, out MountPointInfo info);

        void WriteTemplateCache(IList<ITemplateInfo> templates, string locale);

        IFile FindBestHostTemplateConfigFile(IFileSystemInfo config);

        void ReleaseMountPoint(IMountPoint mountPoint);

        void RemoveMountPoints(IEnumerable<Guid> mountPoints);

        void RemoveMountPoint(IMountPoint mountPoint);
    }
}
