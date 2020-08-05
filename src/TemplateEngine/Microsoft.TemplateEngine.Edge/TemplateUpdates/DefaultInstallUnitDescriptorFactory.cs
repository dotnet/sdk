using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;

namespace Microsoft.TemplateEngine.Edge.TemplateUpdates
{
    public class DefaultInstallUnitDescriptorFactory : IInstallUnitDescriptorFactory
    {
        public static readonly Guid FactoryId = new Guid("F85702B8-D199-42B1-BB9B-7F1380AF57F8");

        public Guid Id => FactoryId;

        public bool TryCreateFromDetails(Guid descriptorId, string identifier, Guid mountPointId, bool isPartOfAnOptionalWorkload,
            IReadOnlyDictionary<string, string> details, out IInstallUnitDescriptor descriptor)
        {
            descriptor = new DefaultInstallUnitDescriptor(descriptorId, mountPointId, identifier, isPartOfAnOptionalWorkload);
            return true;
        }

        public bool TryCreateFromMountPoint(IMountPoint mountPoint, bool isPartOfAnOptionalWorkload, out IReadOnlyList<IInstallUnitDescriptor> descriptorList)
        {
            descriptorList = new List<IInstallUnitDescriptor>()
            {
                new DefaultInstallUnitDescriptor(Guid.NewGuid(), mountPoint.Info.MountPointId, mountPoint.Info.Place, isPartOfAnOptionalWorkload),
            };

            return true;
        }
    }
}
