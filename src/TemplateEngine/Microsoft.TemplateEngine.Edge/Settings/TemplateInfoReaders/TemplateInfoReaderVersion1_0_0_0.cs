using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings.TemplateInfoReaders
{
    public class TemplateInfoReaderVersion1_0_0_0
    {
        public static TemplateInfo FromJObject(JObject jObject)
        {
            TemplateInfoReaderVersion1_0_0_0 reader = new TemplateInfoReaderVersion1_0_0_0(jObject);
            return reader.FromJObject();
        }

        public TemplateInfoReaderVersion1_0_0_0(JObject jObject)
        {
            _jObject = jObject;
        }

        protected readonly JObject _jObject;

        public virtual TemplateInfo FromJObject()
        {
            TemplateInfo info = new TemplateInfo();

            info.ConfigMountPointId = Guid.Parse(_jObject.ToString(nameof(TemplateInfo.ConfigMountPointId)));
            info.Author = _jObject.ToString(nameof(TemplateInfo.Author));
            JArray classificationsArray = _jObject.Get<JArray>(nameof(TemplateInfo.Classifications));

            List<string> classifications = new List<string>();
            info.Classifications = classifications;
            //using (Timing.Over("Read classifications"))
            foreach (JToken item in classificationsArray)
            {
                classifications.Add(item.ToString());
            }

            info.DefaultName = _jObject.ToString(nameof(TemplateInfo.DefaultName));
            info.Description = _jObject.ToString(nameof(TemplateInfo.Description));
            info.Identity = _jObject.ToString(nameof(TemplateInfo.Identity));
            info.GeneratorId = Guid.Parse(_jObject.ToString(nameof(TemplateInfo.GeneratorId)));
            info.GroupIdentity = _jObject.ToString(nameof(TemplateInfo.GroupIdentity));
            info.Precedence = _jObject.ToInt32(nameof(TemplateInfo.Precedence));
            info.Name = _jObject.ToString(nameof(TemplateInfo.Name));
            info.ShortName = _jObject.ToString(nameof(TemplateInfo.ShortName));

            // parse the cached tags
            Dictionary<string, ICacheTag> tags = new Dictionary<string, ICacheTag>();
            JObject tagsObject = _jObject.Get<JObject>(nameof(TemplateInfo.Tags));
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
            JObject cacheParamsObject = _jObject.Get<JObject>(nameof(TemplateInfo.CacheParameters));
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

            info.ConfigPlace = _jObject.ToString(nameof(TemplateInfo.ConfigPlace));
            info.LocaleConfigMountPointId = Guid.Parse(_jObject.ToString(nameof(TemplateInfo.LocaleConfigMountPointId)));
            info.LocaleConfigPlace = _jObject.ToString(nameof(TemplateInfo.LocaleConfigPlace));

            info.HostConfigMountPointId = Guid.Parse(_jObject.ToString(nameof(TemplateInfo.HostConfigMountPointId)));
            info.HostConfigPlace = _jObject.ToString(nameof(TemplateInfo.HostConfigPlace));
            info.ThirdPartyNotices = _jObject.ToString(nameof(TemplateInfo.ThirdPartyNotices));

            JObject baselineJObject = _jObject.Get<JObject>(nameof(ITemplateInfo.BaselineInfo));
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
