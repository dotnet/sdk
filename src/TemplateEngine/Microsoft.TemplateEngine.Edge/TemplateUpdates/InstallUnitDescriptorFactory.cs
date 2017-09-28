using System.Linq;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.TemplateUpdates
{
    // These are written with the assumption that a mount point can only deal with one type of template pack.
    public static class InstallUnitDescriptorFactory
    {
        // For reading an existing descriptor.
        public static bool TryParse(IEngineEnvironmentSettings environmentSettings, JObject jobject, out IInstallUnitDescriptor descriptor)
        {
            foreach (IInstallUnitDescriptorFactory factory in environmentSettings.SettingsLoader.Components.OfType<IInstallUnitDescriptorFactory>().ToList())
            {
                if (factory.TryParse(jobject, out IInstallUnitDescriptor parsedDescriptor))
                {
                    descriptor = parsedDescriptor;
                    return true;
                }
            }

            descriptor = null;
            return false;
        }

        // For creating descriptors.
        public static bool TryCreateFromMountPoint(IEngineEnvironmentSettings environmentSettings, IMountPoint mountPoint, out IReadOnlyList<IInstallUnitDescriptor> descriptorList)
        {
            foreach (IInstallUnitDescriptorFactory factory in environmentSettings.SettingsLoader.Components.OfType<IInstallUnitDescriptorFactory>().ToList())
            {
                if (factory.TryCreateFromMountPoint(mountPoint, out descriptorList))
                {
                    return true;
                }
            }

            descriptorList = null;
            return false;
        }
    }
}
