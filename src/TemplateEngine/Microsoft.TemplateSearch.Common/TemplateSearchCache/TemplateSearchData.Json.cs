// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateSearch.Common
{
    [JsonConverter(typeof(TemplateSearchData.TemplateSearchDataJsonConverter))]
    public partial class TemplateSearchData : ITemplateInfo
    {
        internal TemplateSearchData(JObject jObject, ILogger logger, IReadOnlyDictionary<string, Func<object, object>>? additionalDataReaders = null)
        {
            if (jObject is null)
            {
                throw new ArgumentNullException(nameof(jObject));
            }

            if (logger is null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

#pragma warning disable CS0618 // Type or member is obsolete. The code will be moved to TemplateSearchData.Json when BlobStorageTemplateInfo is ready to be removed.
            TemplateInfo = BlobStorageTemplateInfo.FromJObject(jObject);
#pragma warning restore CS0618 // Type or member is obsolete

            //read additional data
            if (additionalDataReaders != null)
            {
                AdditionalData = TemplateSearchCache.ReadAdditionalData(jObject, additionalDataReaders, logger);
            }
            else
            {
                AdditionalData = new Dictionary<string, object>();
            }
        }

        #region JsonConverter
        private class TemplateSearchDataJsonConverter : JsonConverter<TemplateSearchData>
        {
            //falls back to default de-serializer if not implemented
            public override TemplateSearchData ReadJson(JsonReader reader, Type objectType, TemplateSearchData? existingValue, bool hasExistingValue, JsonSerializer serializer)
                => throw new NotImplementedException();

            public override void WriteJson(JsonWriter writer, TemplateSearchData? value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    return;
                }
                writer.WriteStartObject();
                writer.WritePropertyName(nameof(ITemplateInfo.Identity));
                writer.WriteValue(value.TemplateInfo.Identity);
                if (!string.IsNullOrWhiteSpace(value.TemplateInfo.GroupIdentity))
                {
                    writer.WritePropertyName(nameof(ITemplateInfo.GroupIdentity));
                    writer.WriteValue(value.TemplateInfo.GroupIdentity);
                }
                if (value.TemplateInfo.Precedence != 0)
                {
                    writer.WritePropertyName(nameof(ITemplateInfo.Precedence));
                    writer.WriteValue(value.TemplateInfo.Precedence);
                }
                writer.WritePropertyName(nameof(ITemplateInfo.Name));
                writer.WriteValue(value.TemplateInfo.Name);
                writer.WritePropertyName(nameof(ITemplateInfo.ShortNameList));
                writer.WriteStartArray();
                foreach (string shortName in value.TemplateInfo.ShortNameList)
                {
                    if (!string.IsNullOrWhiteSpace(shortName))
                    {
                        writer.WriteValue(shortName);
                    }
                }
                writer.WriteEndArray();
                if (!string.IsNullOrWhiteSpace(value.TemplateInfo.Author))
                {
                    writer.WritePropertyName(nameof(ITemplateInfo.Author));
                    writer.WriteValue(value.TemplateInfo.Author);
                }
                if (!string.IsNullOrWhiteSpace(value.TemplateInfo.Description))
                {
                    writer.WritePropertyName(nameof(ITemplateInfo.Description));
                    writer.WriteValue(value.TemplateInfo.Description);
                }
                if (!string.IsNullOrWhiteSpace(value.TemplateInfo.ThirdPartyNotices))
                {
                    writer.WritePropertyName(nameof(ITemplateInfo.ThirdPartyNotices));
                    writer.WriteValue(value.TemplateInfo.ThirdPartyNotices);
                }

                if (value.TemplateInfo.Classifications.Any())
                {
                    writer.WritePropertyName(nameof(ITemplateInfo.Classifications));
                    writer.WriteStartArray();
                    foreach (string classification in value.TemplateInfo.Classifications)
                    {
                        if (!string.IsNullOrWhiteSpace(classification))
                        {
                            writer.WriteValue(classification);
                        }
                    }
                    writer.WriteEndArray();
                }

                if (value.TemplateInfo.TagsCollection.Any())
                {
                    writer.WritePropertyName(nameof(ITemplateInfo.TagsCollection));
                    writer.WriteStartObject();
                    foreach (var tag in value.TemplateInfo.TagsCollection)
                    {
                        writer.WritePropertyName(tag.Key);
                        writer.WriteValue(tag.Value);
                    }
                    writer.WriteEndObject();
                }

                if (value.TemplateInfo.Parameters.Any())
                {
                    writer.WritePropertyName(nameof(ITemplateInfo.Parameters));
                    writer.WriteStartArray();
                    foreach (ITemplateParameter param in value.TemplateInfo.Parameters)
                    {
                        writer.WriteStartObject();
                        writer.WritePropertyName(nameof(ITemplateParameter.Name));
                        writer.WriteValue(param.Name);
                        if (!string.IsNullOrWhiteSpace(param.DataType))
                        {
                            writer.WritePropertyName(nameof(ITemplateParameter.DataType));
                            writer.WriteValue(param.DataType);
                        }
                        if (!string.IsNullOrWhiteSpace(param.Description))
                        {
                            writer.WritePropertyName(nameof(ITemplateParameter.Description));
                            writer.WriteValue(param.Description);
                        }
                        if (!string.IsNullOrWhiteSpace(param.DefaultIfOptionWithoutValue))
                        {
                            writer.WritePropertyName(nameof(ITemplateParameter.DefaultIfOptionWithoutValue));
                            writer.WriteValue(param.DefaultIfOptionWithoutValue);
                        }
                        if (param.Priority != default)
                        {
                            writer.WritePropertyName(nameof(ITemplateParameter.Priority));
                            writer.WriteValue(param.Priority);
                        }
                        if (param.Choices != null && param.Choices.Any())
                        {
                            writer.WritePropertyName(nameof(ITemplateParameter.Choices));
                            writer.WriteStartObject();
                            foreach (var choice in param.Choices)
                            {
                                writer.WritePropertyName(choice.Key);
                                writer.WriteStartObject();
                                if (!string.IsNullOrWhiteSpace(choice.Value.Description))
                                {
                                    writer.WritePropertyName(nameof(ParameterChoice.Description));
                                    writer.WriteValue(choice.Value.Description);
                                }
                                if (!string.IsNullOrWhiteSpace(choice.Value.DisplayName))
                                {
                                    writer.WritePropertyName(nameof(ParameterChoice.DisplayName));
                                    writer.WriteValue(choice.Value.DisplayName);
                                }
                                writer.WriteEndObject();
                            }
                            writer.WriteEndObject();
                        }
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                }

                if (value.TemplateInfo.BaselineInfo.Any())
                {
                    writer.WritePropertyName(nameof(ITemplateInfo.BaselineInfo));
                    serializer.Serialize(writer, value.TemplateInfo.BaselineInfo);
                }

                if (value.AdditionalData.Any())
                {
                    foreach (var item in value.AdditionalData)
                    {
                        writer.WritePropertyName(item.Key);
                        serializer.Serialize(writer, item.Value);
                    }
                }

                writer.WriteEndObject();
            }
        }

        #endregion
    }
}
