using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;
using Microsoft.TemplateEngine.Edge.TemplateUpdates;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class InstallUnitDescriptorCache
    {
        private Dictionary<string, IInstallUnitDescriptor> _cache = new Dictionary<string, IInstallUnitDescriptor>();
        private IEngineEnvironmentSettings _environmentSettings;

        public InstallUnitDescriptorCache(IEngineEnvironmentSettings environmentSettings)
            : this(environmentSettings, new List<IInstallUnitDescriptor>())
        {
        }

        protected InstallUnitDescriptorCache(IEngineEnvironmentSettings environmentSettings, IReadOnlyList<IInstallUnitDescriptor> descriptorList)
        {
            _environmentSettings = environmentSettings;

            foreach (IInstallUnitDescriptor descriptor in descriptorList)
            {
                AddOrReplaceDescriptor(descriptor);
            }
        }

        [JsonProperty]
        public IReadOnlyDictionary<string, IInstallUnitDescriptor> Descriptors => _cache;

        public bool TryAddDescriptorForLocation(Guid mountPointId)
        {
            IMountPoint mountPoint = null;

            try
            {
                if (!((SettingsLoader)(_environmentSettings.SettingsLoader)).TryGetMountPointFromId(mountPointId, out mountPoint))
                {
                    return false;
                }

                if (!InstallUnitDescriptorFactory.TryCreateFromMountPoint(_environmentSettings, mountPoint, out IReadOnlyList<IInstallUnitDescriptor> descriptorList))
                {
                    return false;
                }

                foreach (IInstallUnitDescriptor descriptor in descriptorList)
                {
                    AddOrReplaceDescriptor(descriptor);
                }
            }
            finally
            {
                if (mountPoint != null)
                {
                    _environmentSettings.SettingsLoader.ReleaseMountPoint(mountPoint);
                }
            }

            return true;
        }

        public void AddOrReplaceDescriptor(IInstallUnitDescriptor descriptor)
        {
            _cache[descriptor.Identifier] = descriptor;
        }

        public void RemoveDescriptorsForLocationList(IEnumerable<Guid> mountPointIdList)
        {
            foreach (Guid mountPointId in mountPointIdList)
            {
                RemoveDescriptorsForLocation(mountPointId);
            }
        }

        public void RemoveDescriptorsForLocation(Guid mountPointId)
        {
            IList<IInstallUnitDescriptor> descriptorsToRemove = new List<IInstallUnitDescriptor>();

            foreach (IInstallUnitDescriptor descriptor in _cache.Where(x => x.Value.MountPointId == mountPointId).Select(x => x.Value))
            {
                descriptorsToRemove.Add(descriptor);
            }

            foreach (IInstallUnitDescriptor descriptor in descriptorsToRemove)
            {
                RemoveDescriptor(descriptor);
            }
        }

        public void RemoveDescriptor(IInstallUnitDescriptor descriptor)
        {
            _cache.Remove(descriptor.Identifier);
        }

        public static InstallUnitDescriptorCache FromJObject(IEngineEnvironmentSettings environmentSettings, JObject cacheObj)
        {
            List<IInstallUnitDescriptor> allDescriptors = new List<IInstallUnitDescriptor>();

            foreach (JProperty prop in cacheObj.PropertiesOf(nameof(Descriptors)))
            {
                JObject descriptorObj = prop.Value as JObject;

                if (InstallUnitDescriptorFactory.TryParse(environmentSettings, descriptorObj, out IInstallUnitDescriptor parsedDescriptor))
                {
                    allDescriptors.Add(parsedDescriptor);
                }
            }

            return new InstallUnitDescriptorCache(environmentSettings, allDescriptors);
        }
    }
}
