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
            return reader.Read();
        }

        public TemplateInfoReaderVersion1_0_0_0(JObject jObject)
        {
            _jObject = jObject;
        }

        protected readonly JObject _jObject;

        public virtual TemplateInfo Read()
        {
            TemplateInfo info = new TemplateInfo();

            ReadPrimaryInformation(info);
            info.Tags = ReadTags();
            info.CacheParameters = ReadParameters();

            return info;
        }

        protected void ReadPrimaryInformation(TemplateInfo info)
        {
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
        }

        protected virtual IReadOnlyDictionary<string, ICacheTag> ReadTags()
        {
            Dictionary<string, ICacheTag> tags = new Dictionary<string, ICacheTag>();
            JObject tagsObject = _jObject.Get<JObject>(nameof(TemplateInfo.Tags));

            if (tagsObject != null)
            {
                foreach (JProperty item in tagsObject.Properties())
                {
                    tags[item.Name.ToString()] = ReadOneTag(item);
                }
            }

            return tags;
        }

        protected virtual ICacheTag ReadOneTag(JProperty item)
        {
            Dictionary<string, string> choicesAndDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            JObject cdToken = item.Value.Get<JObject>(nameof(ICacheTag.ChoicesAndDescriptions));
            foreach (JProperty cdPair in cdToken.Properties())
            {
                choicesAndDescriptions.Add(cdPair.Name.ToString(), cdPair.Value.ToString());
            }

            return new CacheTag(
                item.Value.ToString(nameof(ICacheTag.Description)),
                choicesAndDescriptions,
                item.Value.ToString(nameof(ICacheTag.DefaultValue)));
        }

        protected virtual IReadOnlyDictionary<string, ICacheParameter> ReadParameters()
        {
            Dictionary<string, ICacheParameter> cacheParams = new Dictionary<string, ICacheParameter>();
            JObject cacheParamsObject = _jObject.Get<JObject>(nameof(TemplateInfo.CacheParameters));

            if (cacheParamsObject != null)
            {
                foreach (JProperty item in cacheParamsObject.Properties())
                {
                    cacheParams[item.Name.ToString()] = ReadOneParameter(item);
                }
            }

            return cacheParams;
        }

        protected virtual ICacheParameter ReadOneParameter(JProperty item)
        {
            return new CacheParameter
            {
                DataType = item.Value.ToString(nameof(ICacheParameter.DataType)),
                DefaultValue = item.Value.ToString(nameof(ICacheParameter.DefaultValue)),
                Description = item.Value.ToString(nameof(ICacheParameter.Description))
            };
        }
    }
}
