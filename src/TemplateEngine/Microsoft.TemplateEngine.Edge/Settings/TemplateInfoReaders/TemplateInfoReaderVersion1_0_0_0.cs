using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings.TemplateInfoReaders
{
    public static class TemplateInfoReaderVersion1_0_0_0
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
            info.Precedence = entry.ToInt32(nameof(TemplateInfo.Precedence));
            info.Name = entry.ToString(nameof(TemplateInfo.Name));
            info.ShortName = entry.ToString(nameof(TemplateInfo.ShortName));

            // parse the cached tags
            Dictionary<string, ICacheTag> tags = new Dictionary<string, ICacheTag>();
            JObject tagsObject = entry.Get<JObject>(nameof(TemplateInfo.Tags));
            if (tagsObject != null)
            {
                foreach (JProperty item in tagsObject.Properties())
                {
                    Dictionary<string, string> choicesAndDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    JObject cdToken = item.Value.Get<JObject>(nameof(ICacheTag.ChoicesAndDescriptions));
                    foreach (JProperty cdPair in cdToken.Properties())
                    {
                        choicesAndDescriptions.Add(cdPair.Name.ToString(), cdPair.Value.ToString());
                    }

                    ICacheTag cacheTag = new CacheTag(
                        item.Value.ToString(nameof(ICacheTag.Description)),
                        choicesAndDescriptions,
                        item.Value.ToString(nameof(ICacheTag.DefaultValue)));
                    tags.Add(item.Name.ToString(), cacheTag);
                }
            }
            info.Tags = tags;

            // parse the cached params
            JObject cacheParamsObject = entry.Get<JObject>(nameof(TemplateInfo.CacheParameters));
            Dictionary<string, ICacheParameter> cacheParams = new Dictionary<string, ICacheParameter>();
            if (cacheParamsObject != null)
            {
                foreach (JProperty item in cacheParamsObject.Properties())
                {
                    ICacheParameter param = new CacheParameter
                    {
                        DataType = item.Value.ToString(nameof(ICacheParameter.DataType)),
                        DefaultValue = item.Value.ToString(nameof(ICacheParameter.DefaultValue)),
                        Description = item.Value.ToString(nameof(ICacheParameter.Description))
                    };

                    cacheParams[item.Name.ToString()] = param;
                }
            }
            info.CacheParameters = cacheParams;

            info.ConfigPlace = entry.ToString(nameof(TemplateInfo.ConfigPlace));
            info.LocaleConfigMountPointId = Guid.Parse(entry.ToString(nameof(TemplateInfo.LocaleConfigMountPointId)));
            info.LocaleConfigPlace = entry.ToString(nameof(TemplateInfo.LocaleConfigPlace));

            info.HostConfigMountPointId = Guid.Parse(entry.ToString(nameof(TemplateInfo.HostConfigMountPointId)));
            info.HostConfigPlace = entry.ToString(nameof(TemplateInfo.HostConfigPlace));
            info.ThirdPartyNotices = entry.ToString(nameof(TemplateInfo.ThirdPartyNotices));

            JObject baselineJObject = entry.Get<JObject>(nameof(ITemplateInfo.BaselineInfo));
            Dictionary<string, IBaselineInfo> baselineInfo = new Dictionary<string, IBaselineInfo>();
            if (baselineJObject != null)
            {
                foreach (JProperty item in baselineJObject.Properties())
                {
                    IBaselineInfo baseline = new BaselineCacheInfo()
                    {
                        Description = item.Value.ToString(nameof(IBaselineInfo.Description)),
                        DefaultOverrides = item.Value.ToStringDictionary(propertyName: nameof(IBaselineInfo.DefaultOverrides))
                    };
                    baselineInfo.Add(item.Name, baseline);
                }
            }
            info.BaselineInfo = baselineInfo;

            return info;
        }

    }
}
