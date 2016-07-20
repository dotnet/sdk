using System;
using System.IO;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Edge.Mount.FileSystem
{
    public class FileSystemMountPointFactory : IMountPointFactory
    {
        internal static readonly Guid FactoryId = new Guid("8C19221B-DEA3-4250-86FE-2D4E189A11D2");

        public Guid Id => FactoryId;

        public bool TryMount(IMountPoint parent, string place, out IMountPoint mountPoint)
        {
            if (parent != null || !Directory.Exists(place))
            {
                mountPoint = null;
                return false;
            }

            Guid mountPointId = Guid.NewGuid();
            MountPointInfo info = new MountPointInfo(Guid.Empty, Id, mountPointId, place);
            mountPoint = new FileSystemMountPoint(info);
            return true;
        }

        public bool TryMount(IMountPointManager manager, MountPointInfo info, out IMountPoint mountPoint)
        {
            if (info.ParentMountPointId != Guid.Empty || !Directory.Exists(info.Place))
            {
                mountPoint = null;
                return false;
            }

            mountPoint = new FileSystemMountPoint(info);
            return true;
        }

        public void DisposeMountPoint(IMountPoint mountPoint)
        {
        }
    }
}
