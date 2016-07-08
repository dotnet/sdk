using System;

namespace Microsoft.TemplateEngine.Abstractions.Mount
{
    public interface IMountPointManager
    {
        bool TryDemandMountPoint(MountPointInfo info, out IMountPoint mountPoint);

        bool TryDemandMountPointById(Guid mountPointId, out IMountPoint parent);

        void ReleaseMountPoint(IMountPoint mountPoint);
    }
}