using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Abstractions.TemplateUpdates
{
    public interface IInstallUnitDescriptorFactory : IIdentifiedComponent
    {
        // for existing descriptors saved in the metadata
        bool TryCreateFromDetails(Guid descriptorId, string identifier, Guid mountPointId, bool isPartOfAnOptionalWorkload,
            IReadOnlyDictionary<string, string> details, out IInstallUnitDescriptor descriptor);

        // for creating from a mount point
        bool TryCreateFromMountPoint(IMountPoint mountPoint, bool isPartOfAnOptionalWorkload, out IReadOnlyList<IInstallUnitDescriptor> descriptorList);
    }
}
