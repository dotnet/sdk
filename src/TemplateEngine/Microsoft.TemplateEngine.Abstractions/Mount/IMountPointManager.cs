using System;

namespace Microsoft.TemplateEngine.Abstractions.Mount
{
    public interface IMountPointManager
    {
        IEngineEnvironmentSettings EnvironmentSettings { get; }

        bool TryDemandMountPoint(MountPointInfo info, out IMountPoint mountPoint);

        bool TryDemandMountPoint(Guid mountPointId, out IMountPoint mountPoint);

        void ReleaseMountPoint(IMountPoint mountPoint);
    }
}
