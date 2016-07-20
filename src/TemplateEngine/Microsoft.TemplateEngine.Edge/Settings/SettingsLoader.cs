using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge.Template;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public static class SettingsLoader
    {
        private static SettingsStore _userSettings;
        private static TemplateCache _userTemplateCache;
        private static IMountPointManager _mountPointManager;
        private static IComponentManager _componentManager;
        private static bool _isLoaded;
        private static Dictionary<Guid, MountPointInfo> _mountPoints;

        public static void Save(this SettingsStore store)
        {
            JObject serialized = JObject.FromObject(store);
            Paths.User.SettingsFile.WriteAllText(serialized.ToString());
        }

        private static void EnsureLoaded()
        {
            if (_isLoaded)
            {
                return;
            }

            string userSettings = Paths.User.SettingsFile.ReadAllText("{}");
            _userSettings = JObject.Parse(userSettings).ToObject<SettingsStore>();

            if (_userSettings.ProbingPaths.Count == 0)
            {
                _userSettings.ProbingPaths.Add(Paths.User.Content);
            }

            _componentManager = new ComponentManager(_userSettings);
            _mountPointManager = new MountPointManager(_componentManager);

            string userTemplateCache = Paths.User.TemplateCacheFile.ReadAllText("{}");
            _userTemplateCache = JObject.Parse(userTemplateCache).ToObject<TemplateCache>();

            _mountPoints = new Dictionary<Guid, MountPointInfo>();

            foreach (MountPointInfo info in _userSettings.MountPoints)
            {
                _mountPoints[info.MountPointId] = info;
            }

            _isLoaded = true;
        }

        public static void Reload()
        {
            _isLoaded = false;
            EnsureLoaded();
        }

        private static void LoadTemplates(TemplateCache cache, ISet<ITemplate> templates)
        {
            foreach (TemplateInfo info in cache.TemplateInfo)
            {
                IGenerator generator;
                if (!_componentManager.TryGetComponent(info.GeneratorId, out generator))
                {
                    //TODO: Log the failure to load the generator
                    continue;
                }

                IMountPoint mountPoint;
                if (_mountPointManager.TryDemandMountPoint(info.MountPointId, out mountPoint))
                {
                    IFileSystemInfo config = mountPoint.FileSystemInfo(info.Path);
                    ITemplate template;
                    if (generator.TryGetTemplateFromConfig(config, out template))
                    {
                        templates.Add(template);
                    }
                    else
                    {
                        //TODO: Log the failure to read the template info
                    }
                }
                else
                {
                    //TODO: Log the failure to mount the template config location
                }
            }
        }

        public static IComponentManager Components
        {
            get
            {
                EnsureLoaded();
                return _componentManager;
            }
        }

        public static IEnumerable<MountPointInfo> MountPoints
        {
            get
            {
                EnsureLoaded();
                return _mountPoints.Values;
            }
        }

        public static IEnumerable<ITemplate> GetTemplates()
        {
            EnsureLoaded();
            HashSet<ITemplate> templates = new HashSet<ITemplate>(TemplateEqualityComparer.Default);
            LoadTemplates(_userTemplateCache, templates);
            return templates;
        }

        public static void AddTemplate(ITemplate template)
        {
            EnsureLoaded();
            HashSet<ITemplate> templates = new HashSet<ITemplate>(TemplateEqualityComparer.Default);
            LoadTemplates(_userTemplateCache, templates);

            TemplateInfo info = new TemplateInfo
            {
                GeneratorId = template.Generator.Id,
                Path = template.Configuration.FullPath,
                MountPointId = template.Configuration.MountPoint.Info.MountPointId
            };

            _userTemplateCache.TemplateInfo.Add(info);
            JObject serialized = JObject.FromObject(_userTemplateCache);
            Paths.User.TemplateCacheFile.WriteAllText(serialized.ToString());
        }

        public static void AddProbingPath(string probeIn)
        {
            EnsureLoaded();
            if (!_userSettings.ProbingPaths.Add(probeIn))
            {
                return;
            }

            _userSettings.Save();
        }

        public static bool TryGetMountPoint(Guid mountPointId, out MountPointInfo info)
        {
            EnsureLoaded();
            return _mountPoints.TryGetValue(mountPointId, out info);
        }

        public static void AddMountPoint(IMountPoint mountPoint)
        {
            _mountPoints[mountPoint.Info.MountPointId] = mountPoint.Info;
            _userSettings.MountPoints.Add(mountPoint.Info);
            JObject serialized = JObject.FromObject(_userSettings);
            Paths.User.SettingsFile.WriteAllText(serialized.ToString());
        }
    }
}
