using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Abstractions.TemplateUpdates
{
    public interface IInstallUnitDescriptorFactory : IIdentifiedComponent
    {
        // for existing descriptors saved in the metadata
        bool TryCreateFromDetails(IReadOnlyDictionary<string, string> details, out IInstallUnitDescriptor descriptor);

        // for creating from a mount point
        bool TryCreateFromMountPoint(IMountPoint mountPoint, out IReadOnlyList<IInstallUnitDescriptor> descriptorList);
    }
}
