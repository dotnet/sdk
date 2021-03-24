using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

#nullable enable

namespace Microsoft.TemplateEngine.Edge.Settings.TemplateInfoReaders
{
    public class TemplateInfoReaderVersion1_0_0_0
    {
        public static TemplateInfo FromJObject(JObject jObject)
        {
            TemplateInfoReaderVersion1_0_0_0 reader = new TemplateInfoReaderVersion1_0_0_0();
            return reader.Read(jObject);
        }

        public virtual TemplateInfo Read(JObject jObject)
        {
            TemplateInfo info = new TemplateInfo();

            ReadPrimaryInformation(jObject, info);
            info.Tags = ReadTags(jObject);
            info.CacheParameters = ReadParameters(jObject);

            return info;
        }

        protected void ReadPrimaryInformation(JObject jObject, TemplateInfo info)
        {
            info.MountPointUri = jObject.ToString(nameof(TemplateInfo.MountPointUri));
            info.Author = jObject.ToString(nameof(TemplateInfo.Author));
            JArray? classificationsArray = jObject.Get<JArray>(nameof(TemplateInfo.Classifications));

            List<string> classifications = new List<string>();
            info.Classifications = classifications;
            //using (Timing.Over("Read classifications"))
            if (classificationsArray != null)
            {
                foreach (JToken item in classificationsArray)
                {
                    classifications.Add(item.ToString());
                }
            }

            info.DefaultName = jObject.ToString(nameof(TemplateInfo.DefaultName));
            info.Description = jObject.ToString(nameof(TemplateInfo.Description));
            info.Identity = jObject.ToString(nameof(TemplateInfo.Identity));
            info.GeneratorId = Guid.Parse(jObject.ToString(nameof(TemplateInfo.GeneratorId)));
            info.GroupIdentity = jObject.ToString(nameof(TemplateInfo.GroupIdentity));
            info.Precedence = jObject.ToInt32(nameof(TemplateInfo.Precedence));
            info.Name = jObject.ToString(nameof(TemplateInfo.Name));

            ReadShortNameInfo(jObject, info);

            info.ConfigPlace = jObject.ToString(nameof(TemplateInfo.ConfigPlace));
            info.LocaleConfigPlace = jObject.ToString(nameof(TemplateInfo.LocaleConfigPlace));

            info.HostConfigPlace = jObject.ToString(nameof(TemplateInfo.HostConfigPlace));
            info.ThirdPartyNotices = jObject.ToString(nameof(TemplateInfo.ThirdPartyNotices));

            JObject? baselineJObject = jObject.Get<JObject>(nameof(ITemplateInfo.BaselineInfo));
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

        protected virtual void ReadShortNameInfo(JObject jObject, TemplateInfo info)
        {
            info.ShortName = jObject.ToString(nameof(TemplateInfo.ShortName));
        }

        protected virtual IReadOnlyDictionary<string, ICacheTag> ReadTags(JObject jObject)
        {
            Dictionary<string, ICacheTag> tags = new Dictionary<string, ICacheTag>();
            JObject? tagsObject = jObject.Get<JObject>(nameof(TemplateInfo.Tags));

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
            Dictionary<string, ParameterChoice> choicesAndDescriptions = new Dictionary<string, ParameterChoice>(StringComparer.OrdinalIgnoreCase);
            JObject? cdToken = item.Value.Get<JObject>("ChoicesAndDescriptions");
            if (cdToken != null)
            {
                foreach (JProperty cdPair in cdToken.Properties())
                {
                    choicesAndDescriptions.Add(cdPair.Name.ToString(), new ParameterChoice(null, cdPair.Value.ToString()));
                }
            }

            return new CacheTag(
                displayName: null,
                description: item.Value.ToString(nameof(ICacheTag.Description)),
                choicesAndDescriptions,
                item.Value.ToString(nameof(ICacheTag.DefaultValue)));
        }

        protected virtual IReadOnlyDictionary<string, ICacheParameter> ReadParameters(JObject jObject)
        {
            Dictionary<string, ICacheParameter> cacheParams = new Dictionary<string, ICacheParameter>();
            JObject? cacheParamsObject = jObject.Get<JObject>(nameof(TemplateInfo.CacheParameters));

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
