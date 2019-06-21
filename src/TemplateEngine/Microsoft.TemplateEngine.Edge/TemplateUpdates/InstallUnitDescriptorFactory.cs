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
        public static bool TryParse(IEngineEnvironmentSettings environmentSettings, JProperty descriptorProperty, out IInstallUnitDescriptor parsedDescriptor)
        {
            if (descriptorProperty == null)
            {
                parsedDescriptor = null;
                return false;
            }

            if (!Guid.TryParse(descriptorProperty.Name, out Guid descriptorId)
                || descriptorId == Guid.Empty)
            {
                parsedDescriptor = null;
                return false;
            }

            if (!(descriptorProperty.Value is JObject descriptorObj))
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

            if (!descriptorObj.TryGetValue(nameof(IInstallUnitDescriptor.MountPointId), StringComparison.OrdinalIgnoreCase, out JToken mountPointIdToken)
                || (mountPointIdToken == null)
                || (mountPointIdToken.Type != JTokenType.String)
                || !Guid.TryParse(mountPointIdToken.ToString(), out Guid mountPointId))
            {
                parsedDescriptor = null;
                return false;
            }

            if (!descriptorObj.TryGetValue(nameof(IInstallUnitDescriptor.Identifier), StringComparison.OrdinalIgnoreCase, out JToken identifierToken)
                || (identifierToken == null)
                || (identifierToken.Type != JTokenType.String))
            {
                parsedDescriptor = null;
                return false;
            }

            Dictionary<string, string> details = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (JProperty property in descriptorObj.PropertiesOf(nameof(IInstallUnitDescriptor.Details)))
            {
                if (property.Value.Type != JTokenType.String)
                {
                    parsedDescriptor = null;
                    return false;
                }

                details[property.Name] = property.Value.ToString();
            }

            if (factory.TryCreateFromDetails(descriptorId, identifierToken.ToString(), mountPointId, details, out IInstallUnitDescriptor descriptor))
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
            IInstallUnitDescriptorFactory defaultFactory = null;

            foreach (IInstallUnitDescriptorFactory factory in environmentSettings.SettingsLoader.Components.OfType<IInstallUnitDescriptorFactory>().ToList())
            {
                if (factory is DefaultInstallUnitDescriptorFactory)
                {
                    defaultFactory = factory;
                    continue;
                }

                if (factory.TryCreateFromMountPoint(mountPoint, out descriptorList))
                {
                    return true;
                }
            }

            return defaultFactory.TryCreateFromMountPoint(mountPoint, out descriptorList);
        }
    }
}
