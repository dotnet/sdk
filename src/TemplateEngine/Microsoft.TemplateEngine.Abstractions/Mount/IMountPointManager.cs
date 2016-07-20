using System;

namespace Microsoft.TemplateEngine.Abstractions.Mount
{
    public interface IMountPointManager
    {
        bool TryDemandMountPoint(MountPointInfo info, out IMountPoint mountPoint);

        bool TryDemandMountPoint(Guid mountPointId, out IMountPoint mountPoint);

        void ReleaseMountPoint(IMountPoint mountPoint);
    }
}