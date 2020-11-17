using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.TemplateEngine.Mocks
{
    /// <summary>
    /// Mock for IMountPointManager interface to be used for unit testing
    /// Can be set up to return failure on demanding certain mount points. The unavailable mountpoints to be defined in <see cref="UnavailableMountPoints"/>. 
    /// </summary>
    public class MockMountPointManager : IMountPointManager
    {
        public MockMountPointManager(IEngineEnvironmentSettings environmentSettings)
        {
            EnvironmentSettings = environmentSettings;
        }
        public IEngineEnvironmentSettings EnvironmentSettings
        {
            private set; get;
        }

        /// <summary>
        /// Contains the list of mount points that are considered to be unavailable when demanding
        /// </summary>
        public List<MountPointInfo> UnavailableMountPoints { get; } = new List<MountPointInfo>();

        public void ReleaseMountPoint(IMountPoint mountPoint)
        {
            // do nothing
        }

        public bool TryDemandMountPoint(MountPointInfo info, out IMountPoint mountPoint)
        {
            if (UnavailableMountPoints.Any(m => m.MountPointId == info.MountPointId))
            {
                mountPoint = null;
                return false;
            }
            mountPoint = new MockMountPoint(EnvironmentSettings);
            return true;
        }

        public bool TryDemandMountPoint(Guid mountPointId, out IMountPoint mountPoint)
        {
            if (UnavailableMountPoints.Any(m => m.MountPointId == mountPointId))
            {
                mountPoint = null;
                return false;
            }
            mountPoint = new MockMountPoint(EnvironmentSettings);
            return true;
        }
    }
}
