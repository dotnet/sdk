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
        private readonly Dictionary<Guid, string> _installedItems;
        private readonly Dictionary<string, IInstallUnitDescriptor> _cache = new Dictionary<string, IInstallUnitDescriptor>();
        private readonly IEngineEnvironmentSettings _environmentSettings;

        public InstallUnitDescriptorCache(IEngineEnvironmentSettings environmentSettings)
            : this(environmentSettings, new List<IInstallUnitDescriptor>(), new Dictionary<Guid, string>())
        {
        }

        protected InstallUnitDescriptorCache(IEngineEnvironmentSettings environmentSettings, IReadOnlyList<IInstallUnitDescriptor> descriptorList, IReadOnlyDictionary<Guid, string> installedItems)
        {
            _environmentSettings = environmentSettings;

            foreach (IInstallUnitDescriptor descriptor in descriptorList)
            {
                AddOrReplaceDescriptor(descriptor);
            }

            _installedItems = installedItems.ToDictionary(x => x.Key, x => x.Value);
        }

        [JsonProperty]
        public IReadOnlyDictionary<Guid, string> InstalledItems => _installedItems;

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

                string uninstallString = mountPoint.Info.Place;

                //Adjust the uninstall string for NuGet packages if needed
                if (uninstallString.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
                {
                    bool isFile = false;
                    if (mountPoint.Info.ParentMountPointId == Guid.Empty)
                    {
                        isFile = _environmentSettings.Host.FileSystem.FileExists(mountPoint.Info.Place);
                    }
                    else if (((SettingsLoader)(_environmentSettings.SettingsLoader)).TryGetMountPointFromId(mountPoint.Info.ParentMountPointId, out IMountPoint parentMountPoint))
                    {
                        try
                        {
                            isFile = parentMountPoint.FileInfo(mountPoint.Info.Place)?.Exists ?? false;
                        }
                        finally
                        {
                            _environmentSettings.SettingsLoader.ReleaseMountPoint(parentMountPoint);
                        }
                    }

                    if (isFile)
                    {
                        if (NupkgInstallUnitDescriptorFactory.TryGetPackageInfoFromNuspec(mountPoint, out string packageName, out string _))
                        {
                            uninstallString = packageName;
                        }
                    }
                }

                _installedItems[mountPointId] = uninstallString;

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
            _installedItems.Remove(mountPointId);
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

            Dictionary<Guid, string> installedItems = new Dictionary<Guid, string>();

            foreach (KeyValuePair<string, string> item in cacheObj.ToStringDictionary(propertyName: nameof(InstalledItems)))
            {
                installedItems[Guid.Parse(item.Key)] = item.Value;
            }

            return new InstallUnitDescriptorCache(environmentSettings, allDescriptors, installedItems);
        }
    }
}
