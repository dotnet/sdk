using System;

namespace Microsoft.TemplateEngine.Abstractions.Mount
{
    public interface IMountPointFactory : IIdentifiedComponent
    {
        bool TryMount(IMountPoint parent, string place, out IMountPoint mountPoint);

        bool TryMount(IMountPointManager manager, MountPointInfo info, out IMountPoint mountPoint);

        void DisposeMountPoint(IMountPoint mountPoint);
    }
}