using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class TemplateCache
    {
        public TemplateCache()
        {
            TemplateInfo = new List<TemplateInfo>();
        }

        public TemplateCache(JObject parsed)
            : this()
        {
            JToken templateInfoToken;
            if (parsed.TryGetValue("TemplateInfo", StringComparison.OrdinalIgnoreCase, out templateInfoToken))
            {
                JArray arr = templateInfoToken as JArray;
                if (arr != null)
                {
                    foreach (JToken entry in arr)
                    {
                        if (entry != null && entry.Type == JTokenType.Object)
                        {
                            TemplateInfo.Add(new TemplateInfo((JObject) entry));
                        }
                    }
                }
            }
        }

        [JsonProperty]
        public List<TemplateInfo> TemplateInfo { get; set; }

        public static void Scan(string templateDir)
        {
            foreach (IMountPointFactory factory in SettingsLoader.Components.OfType<IMountPointFactory>())
            {
                IMountPoint mountPoint;
                if (factory.TryMount(null, templateDir, out mountPoint))
                {
                    foreach (IGenerator generator in SettingsLoader.Components.OfType<IGenerator>())
                    {
                        IComponentManager componentManager = SettingsLoader.Components;
                        foreach (ITemplate template in generator.GetTemplatesFromSource(mountPoint, componentManager))
                        {
                            SettingsLoader.AddTemplate(template);
                            SettingsLoader.AddMountPoint(mountPoint);
                        }
                    }
                }
            }
        }

        public static void Scan(IReadOnlyList<string> templateRoots)
        {
            foreach (string templateDir in templateRoots)
            {
                Scan(templateDir);
            }
        }
    }
}
