using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    internal class MountPointManager : IMountPointManager
    {
        private readonly IComponentManager _componentManager;

        public MountPointManager(IEngineEnvironmentSettings environmentSettings, IComponentManager componentManager)
        {
            _componentManager = componentManager;
            EnvironmentSettings = environmentSettings;
        }

        public IEngineEnvironmentSettings EnvironmentSettings { get; }

        public bool TryDemandMountPoint(MountPointInfo info, out IMountPoint mountPoint)
        {
            using (Timing.Over(EnvironmentSettings.Host, "Get mount point - inner"))
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
            using (Timing.Over(EnvironmentSettings.Host, "Get mount point"))
            {
                MountPointInfo info;
                if (EnvironmentSettings.SettingsLoader.TryGetMountPointInfo(mountPointId, out info))
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
