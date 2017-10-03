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

            Dictionary<string, string> details = new Dictionary<string, string>();
            foreach (JProperty property in descriptorObj.PropertiesOf(nameof(IInstallUnitDescriptor.Details)))
            {
                if (property.Value.Type != JTokenType.String)
                {
                    parsedDescriptor = null;
                    return false;
                }

                details[property.Name] = property.Value.ToString();
            }

            if (factory.TryCreateFromDetails(details, out IInstallUnitDescriptor descriptor))
            {
                parsedDescriptor = descriptor;
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
