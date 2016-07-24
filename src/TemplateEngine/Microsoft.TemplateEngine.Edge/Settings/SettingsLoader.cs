using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Edge.Template;
using Newtonsoft.Json;
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
        private static bool _templatesLoaded;

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

            string userSettings;
            using (Timing.Over("Read settings"))
                userSettings = Paths.User.SettingsFile.ReadAllText("{}");
            JObject parsed;
            using (Timing.Over("Parse settings"))
                parsed = JObject.Parse(userSettings);
            using (Timing.Over("Deserialize user settings"))
                _userSettings = new SettingsStore(parsed);

            using (Timing.Over("Init probing paths"))
                if (_userSettings.ProbingPaths.Count == 0)
            {
                _userSettings.ProbingPaths.Add(Paths.User.Content);
            }

            using (Timing.Over("Init Component manager"))
                _componentManager = new ComponentManager(_userSettings);
            using (Timing.Over("Init Mount Point manager"))
            _mountPointManager = new MountPointManager(_componentManager);

            using (Timing.Over("Demand template load"))
                EnsureTemplatesLoaded();

            _mountPoints = new Dictionary<Guid, MountPointInfo>();

            using (Timing.Over("Load mount points"))
                foreach (MountPointInfo info in _userSettings.MountPoints)
            {
                _mountPoints[info.MountPointId] = info;
            }

            _isLoaded = true;
        }

        private static void EnsureTemplatesLoaded()
        {
            if (_templatesLoaded)
            {
                return;
            }

            string userTemplateCache;
            using (Timing.Over("Read template cache"))
                userTemplateCache = Paths.User.TemplateCacheFile.ReadAllText("{}");
            JObject parsed;
            using (Timing.Over("Parse template cache"))
                parsed = JObject.Parse(userTemplateCache);
            using (Timing.Over("Init template cache"))
                _userTemplateCache = new TemplateCache(parsed);

            _templatesLoaded = true;
        }

        public static void Reload()
        {
            _isLoaded = false;
            EnsureLoaded();
        }

        private static void LoadTemplates(TemplateCache cache, ISet<ITemplateInfo> templates)
        {
            using (Timing.Over("Enumerate infos"))
                templates.UnionWith(cache.TemplateInfo);
        }

        public static ITemplate LoadTemplate(ITemplateInfo info)
        {

            IGenerator generator;
            if (!Components.TryGetComponent(info.GeneratorId, out generator))
            {
                return null;
            }

            IMountPoint mountPoint;
            if (!_mountPointManager.TryDemandMountPoint(info.ConfigMountPointId, out mountPoint))
            {
                return null;
            }

            IFileSystemInfo config = mountPoint.FileSystemInfo(info.ConfigPlace);

            ITemplate template;
            using (Timing.Over("Template from config"))
                if (generator.TryGetTemplateFromConfig(config, out template))
                {
                    return template;
                }
                else
                {
                    //TODO: Log the failure to read the template info
                }

            return null;
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

        public static void GetTemplates(HashSet<ITemplateInfo> templates)
        {
            using (Timing.Over("Settings init"))
                EnsureLoaded();
            using (Timing.Over("Template load"))
                LoadTemplates(_userTemplateCache, templates);
        }

        public static void AddTemplate(ITemplate template)
        {
            EnsureLoaded();
            HashSet<ITemplateInfo> templates = new HashSet<ITemplateInfo>(TemplateEqualityComparer.Default);
            LoadTemplates(_userTemplateCache, templates);

            TemplateInfo info = new TemplateInfo
            {
                GeneratorId = template.Generator.Id,
                ConfigPlace = template.Configuration.FullPath,
                ConfigMountPointId = template.Configuration.MountPoint.Info.MountPointId,
                Name = template.Name,
                Tags = template.Tags,
                ShortName = template.ShortName,
                Classifications = template.Classifications,
                Author = template.Author,
                GroupIdentity = template.GroupIdentity,
                Identity = template.Identity,
                DefaultName = template.DefaultName
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
            using(Timing.Over("Mount point lookup"))
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
