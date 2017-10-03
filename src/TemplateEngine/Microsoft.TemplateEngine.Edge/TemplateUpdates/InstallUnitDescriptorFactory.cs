using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.TemplateUpdates
{
    public static class InstallUnitDescriptorFactory
    {
        public static bool TryParse(IEngineEnvironmentSettings environmentSettings, JObject descriptorObj, out IInstallUnitDescriptor parsedDescriptor)
        {
            if (descriptorObj == null)
            {
                parsedDescriptor = null;
                return false;
            }

            if (!descriptorObj.TryGetValue(nameof(IInstallUnitDescriptor.FactoryId), StringComparison.OrdinalIgnoreCase, out JToken factoryIdToken)
                || (factoryIdToken == null)
                || (factoryIdToken.Type != JTokenType.String)
                || !Guid.TryParse(factoryIdToken.ToString(), out Guid factoryId)
                || !environmentSettings.SettingsLoader.Components.TryGetComponent(factoryId, out IInstallUnitDescriptorFactory factory))
            {
                parsedDescriptor = null;
                return false;
            }

            if (!descriptorObj.TryGetValue(nameof(IInstallUnitDescriptor.Details), StringComparison.OrdinalIgnoreCase, out JToken descriptorToken))
            {
                parsedDescriptor = null;
                return false;
            }

            if (factory.TryParse(descriptorToken.ToString(), out parsedDescriptor))
            {
                return true;
            }

            parsedDescriptor = null;
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
