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
    public class TemplateSearchData : ITemplateInfo
    {
        public TemplateSearchData(ITemplateInfo templateInfo, IDictionary<string, object>? data = null)
        {
            TemplateInfo = new BlobStorageTemplateInfo(templateInfo);
            AdditionalData = data ?? new Dictionary<string, object>();
        }

        internal TemplateSearchData(JObject jObject, ILogger logger, IReadOnlyDictionary<string, Func<object, object>>? additionalDataReaders = null)
        {
            TemplateInfo = BlobStorageTemplateInfo.FromJObject(jObject);
            //read additional data
            if (additionalDataReaders != null)
            {
                AdditionalData = TemplateSearchCache.ReadAdditionalData(jObject, additionalDataReaders, logger);
            }
        }

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalData { get; } = new Dictionary<string, object>();

        #region ITemplateInfo implementation
        [JsonProperty]
        string? ITemplateInfo.Author => TemplateInfo.Author;

        [JsonProperty]
        string? ITemplateInfo.Description => TemplateInfo.Description;

        [JsonProperty]
        IReadOnlyList<string> ITemplateInfo.Classifications => TemplateInfo.Classifications;

        [JsonIgnore]
        string? ITemplateInfo.DefaultName => TemplateInfo.DefaultName;

        [JsonProperty]
        string ITemplateInfo.Identity => TemplateInfo.Identity;

        [JsonIgnore]
        Guid ITemplateInfo.GeneratorId => TemplateInfo.GeneratorId;

        [JsonProperty]
        string? ITemplateInfo.GroupIdentity => TemplateInfo.GroupIdentity;

        [JsonProperty]
        int ITemplateInfo.Precedence => TemplateInfo.Precedence;

        [JsonProperty]
        string ITemplateInfo.Name => TemplateInfo.Name;

        [JsonIgnore]
        [Obsolete]
        string ITemplateInfo.ShortName => TemplateInfo.ShortName;

        [JsonIgnore]
        [Obsolete]
        IReadOnlyDictionary<string, ICacheTag> ITemplateInfo.Tags => TemplateInfo.Tags;

        [JsonProperty]
        IReadOnlyDictionary<string, string> ITemplateInfo.TagsCollection => TemplateInfo.TagsCollection;

        [JsonIgnore]
        [Obsolete]
        IReadOnlyDictionary<string, ICacheParameter> ITemplateInfo.CacheParameters => TemplateInfo.CacheParameters;

        [JsonProperty]
        IReadOnlyList<ITemplateParameter> ITemplateInfo.Parameters => TemplateInfo.Parameters;

        [JsonIgnore]
        string ITemplateInfo.MountPointUri => TemplateInfo.MountPointUri;

        [JsonIgnore]
        string ITemplateInfo.ConfigPlace => TemplateInfo.ConfigPlace;

        [JsonIgnore]
        string? ITemplateInfo.LocaleConfigPlace => TemplateInfo.LocaleConfigPlace;

        [JsonIgnore]
        string? ITemplateInfo.HostConfigPlace => TemplateInfo.HostConfigPlace;

        [JsonProperty]
        string? ITemplateInfo.ThirdPartyNotices => TemplateInfo.ThirdPartyNotices;

        [JsonProperty]
        IReadOnlyDictionary<string, IBaselineInfo> ITemplateInfo.BaselineInfo => TemplateInfo.BaselineInfo;

        [JsonIgnore]
        [Obsolete]
        bool ITemplateInfo.HasScriptRunningPostActions { get => TemplateInfo.HasScriptRunningPostActions; set => throw new NotImplementedException(); }

        [JsonProperty]
        IReadOnlyList<string> ITemplateInfo.ShortNameList => TemplateInfo.ShortNameList;
        #endregion

        [JsonIgnore]
        private ITemplateInfo TemplateInfo { get; }

        #region JsonConverter
        private class TemplateSearchDataJsonConverter : JsonConverter<TemplateSearchData>
        {
            public override TemplateSearchData ReadJson(JsonReader reader, Type objectType, TemplateSearchData existingValue, bool hasExistingValue, JsonSerializer serializer)
                => throw new NotImplementedException();

            public override void WriteJson(JsonWriter writer, TemplateSearchData value, JsonSerializer serializer)
            {
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
                        }
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                }

                if (value.TemplateInfo.BaselineInfo.Any())
                {
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
