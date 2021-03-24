using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    internal class MountPointManager : IMountPointManager
    {
        private readonly IComponentManager _componentManager;
        private readonly IReadOnlyList<IMountPointFactory> mountPointFactories;

        public MountPointManager(IEngineEnvironmentSettings environmentSettings, IComponentManager componentManager)
        {
            _componentManager = componentManager;
            EnvironmentSettings = environmentSettings;
            mountPointFactories = componentManager.OfType<IMountPointFactory>().ToList();
        }

        public IEngineEnvironmentSettings EnvironmentSettings { get; }

        public bool TryDemandMountPoint(string mountPointUri, out IMountPoint mountPoint)
        {
            //TODO: Split mountPointUri with ;(semicolon) and loop parents instead of always `null`
            using (Timing.Over(EnvironmentSettings.Host, "Get mount point"))
            {
                foreach (var factory in mountPointFactories)
                {
                    if (factory.TryMount(EnvironmentSettings, null, mountPointUri, out var myMountPoint))
                    {
                        mountPoint = myMountPoint;
                        return true;
                    }
                }

                mountPoint = null;
                return false;
            }
        }
    }
}
