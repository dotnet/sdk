using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings.TemplateInfoReaders
{
    public static class TemplateInfoReaderVersion1_0_0_2
    {
        public static TemplateInfo FromJObject(JObject entry)
        {
            TemplateInfo info = new TemplateInfo();

            TemplateInfoReaderVersion1_0_0_0.ReadPrimaryInformation(entry, info);
            info.Tags = ReadTags(entry);
            info.CacheParameters = ReadParameters(entry);

            return info;
        }

        internal static IReadOnlyDictionary<string, ICacheTag> ReadTags(JObject entry)
        {
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

                    if (cacheTag is IAllowDefaultIfOptionWithoutValue tagWithNoValueDefault)
                    {
                        tagWithNoValueDefault.DefaultIfOptionWithoutValue = item.Value.ToString(nameof(IAllowDefaultIfOptionWithoutValue.DefaultIfOptionWithoutValue));
                        tags[item.Name.ToString()] = tagWithNoValueDefault as CacheTag;
                    }
                    else
                    {
                        tags.Add(item.Name.ToString(), cacheTag);
                    }
                }
            }

            return tags;
        }

        internal static IReadOnlyDictionary<string, ICacheParameter> ReadParameters(JObject entry)
        {
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

                    if (param is IAllowDefaultIfOptionWithoutValue paramWithNoValueDefault)
                    {
                        paramWithNoValueDefault.DefaultIfOptionWithoutValue = item.Value.ToString(nameof(IAllowDefaultIfOptionWithoutValue.DefaultIfOptionWithoutValue));
                        cacheParams[item.Name.ToString()] = paramWithNoValueDefault as CacheParameter;
                    }
                    else
                    {
                        cacheParams[item.Name.ToString()] = param;
                    }
                }
            }

            return cacheParams;
        }
    }
}
