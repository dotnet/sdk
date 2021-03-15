using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings.TemplateInfoReaders
{
    public static class TemplateInfoReaderInitialVersion
    {
        public static TemplateInfo FromJObject(JObject entry)
        {
            TemplateInfo info = new TemplateInfo();

            info.ConfigMountPointId = Guid.Parse(entry.ToString(nameof(TemplateInfo.ConfigMountPointId)));
            info.Author = entry.ToString(nameof(TemplateInfo.Author));
            JArray classificationsArray = entry.Get<JArray>(nameof(TemplateInfo.Classifications));

            List<string> classifications = new List<string>();
            info.Classifications = classifications;
            //using (Timing.Over("Read classifications"))
            foreach (JToken item in classificationsArray)
            {
                classifications.Add(item.ToString());
            }

            info.DefaultName = entry.ToString(nameof(TemplateInfo.DefaultName));
            info.Description = entry.ToString(nameof(TemplateInfo.Description));
            info.Identity = entry.ToString(nameof(TemplateInfo.Identity));
            info.GeneratorId = Guid.Parse(entry.ToString(nameof(TemplateInfo.GeneratorId)));
            info.GroupIdentity = entry.ToString(nameof(TemplateInfo.GroupIdentity));
            info.Name = entry.ToString(nameof(TemplateInfo.Name));
            info.ShortName = entry.ToString(nameof(TemplateInfo.ShortName));

            // tags are just "name": "description"
            // e.g.: "language": "C#"
            JObject tagsObject = entry.Get<JObject>(nameof(TemplateInfo.Tags));
            Dictionary<string, ICacheTag> tags = new Dictionary<string, ICacheTag>();
            info.Tags = tags;
            foreach (JProperty item in tagsObject.Properties())
            {
                Dictionary<string, ParameterChoice> choicesAndDescriptions = new Dictionary<string, ParameterChoice>(StringComparer.OrdinalIgnoreCase);
                choicesAndDescriptions.Add(item.Value.ToString(), new ParameterChoice(string.Empty, string.Empty));
                ICacheTag cacheTag = new CacheTag(
                    displayName: string.Empty,
                    description: string.Empty,
                    choicesAndDescriptions,
                    item.Value.ToString());

                tags.Add(item.Name.ToString(), cacheTag);
            }

            info.ConfigPlace = entry.ToString(nameof(TemplateInfo.ConfigPlace));
            info.LocaleConfigMountPointId = Guid.Parse(entry.ToString(nameof(TemplateInfo.LocaleConfigMountPointId)));
            info.LocaleConfigPlace = entry.ToString(nameof(TemplateInfo.LocaleConfigPlace));

            return info;
        }
    }
}
