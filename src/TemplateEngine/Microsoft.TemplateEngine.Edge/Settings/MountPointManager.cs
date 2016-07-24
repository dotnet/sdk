using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    internal class MountPointManager : IMountPointManager
    {
        private readonly IComponentManager _componentManager;

        public MountPointManager(IComponentManager componentManager)
        {
            _componentManager = componentManager;
        }

        public bool TryDemandMountPoint(MountPointInfo info, out IMountPoint mountPoint)
        {
            //using (Timing.Over("Get mount point - inner"))
            {
                IMountPointFactory factory;
                if (_componentManager.TryGetComponent(info.MountPointFactoryId, out factory))
                {
                    return factory.TryMount(this, info, out mountPoint);
                }

                mountPoint = null;
                return false;
            }
        }

        public bool TryDemandMountPoint(Guid mountPointId, out IMountPoint mountPoint)
        {
            //using (Timing.Over("Get mount point"))
            {
                MountPointInfo info;
                if (SettingsLoader.TryGetMountPoint(mountPointId, out info))
                {
                    return TryDemandMountPoint(info, out mountPoint);
                }

                mountPoint = null;
                return false;
            }
        }

        public void ReleaseMountPoint(IMountPoint mountPoint)
        {
            Guid? factoryId = mountPoint?.Info.MountPointFactoryId;

            if (!factoryId.HasValue)
            {
                return;
            }

            IMountPointFactory factory;
            if (_componentManager.TryGetComponent(factoryId.Value, out factory))
            {
                factory.DisposeMountPoint(mountPoint);
            }
        }
    }
}
