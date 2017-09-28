using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Abstractions.TemplateUpdates
{
    public interface IInstallUnitDescriptorFactory : IIdentifiedComponent
    {
        // for existing descriptors saved in the metadata
        bool TryParse(JObject jobject, out IInstallUnitDescriptor descriptor);

        // for creating from a mount point
        bool TryCreateFromMountPoint(IMountPoint mountPoint, out IReadOnlyList<IInstallUnitDescriptor> descriptoListr);
    }
}
