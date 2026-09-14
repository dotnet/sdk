// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET7_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateSearch.Common
{
    [System.Text.Json.Serialization.JsonConverter(typeof(TemplateSearchData.TemplateSearchDataJsonConverter))]
    public partial class TemplateSearchData : ITemplateInfo
    {
        internal TemplateSearchData(JsonObject jObject, ILogger logger, IReadOnlyDictionary<string, Func<object, object>>? additionalDataReaders = null)
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
            AdditionalData = additionalDataReaders != null
                ? TemplateSearchCache.ReadAdditionalData(jObject, additionalDataReaders, logger)
                : new Dictionary<string, object>();
        }

        #region JsonConverter
        private class TemplateSearchDataJsonConverter : System.Text.Json.Serialization.JsonConverter<TemplateSearchData>
        {
            //falls back to default de-serializer if not implemented
            public override TemplateSearchData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                => throw new NotImplementedException();

#if NET7_0_OR_GREATER
            [UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode", Justification = "BaselineInfo and AdditionalData are serialized with known types.")]
            [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "BaselineInfo and AdditionalData are serialized with known types.")]
#endif
            public override void Write(Utf8JsonWriter writer, TemplateSearchData value, JsonSerializerOptions options)
            {
                if (value == null)
                {
                    return;
                }
                writer.WriteStartObject();
                writer.WritePropertyName(nameof(ITemplateInfo.Identity));
                writer.WriteStringValue(value.TemplateInfo.Identity);
                if (!string.IsNullOrWhiteSpace(value.TemplateInfo.GroupIdentity))
                {
                    writer.WritePropertyName(nameof(ITemplateInfo.GroupIdentity));
                    writer.WriteStringValue(value.TemplateInfo.GroupIdentity);
                }
                if (value.TemplateInfo.Precedence != 0)
                {
                    writer.WritePropertyName(nameof(ITemplateInfo.Precedence));
                    writer.WriteNumberValue(value.TemplateInfo.Precedence);
                }
                writer.WritePropertyName(nameof(ITemplateInfo.Name));
                writer.WriteStringValue(value.TemplateInfo.Name);
                writer.WritePropertyName(nameof(ITemplateInfo.ShortNameList));
                writer.WriteStartArray();
                foreach (string shortName in value.TemplateInfo.ShortNameList)
                {
                    if (!string.IsNullOrWhiteSpace(shortName))
                    {
                        writer.WriteStringValue(shortName);
                    }
                }
                writer.WriteEndArray();
                if (!string.IsNullOrWhiteSpace(value.TemplateInfo.Author))
                {
                    writer.WritePropertyName(nameof(ITemplateInfo.Author));
                    writer.WriteStringValue(value.TemplateInfo.Author);
                }
                if (!string.IsNullOrWhiteSpace(value.TemplateInfo.Description))
                {
                    writer.WritePropertyName(nameof(ITemplateInfo.Description));
                    writer.WriteStringValue(value.TemplateInfo.Description);
                }
                if (!string.IsNullOrWhiteSpace(value.TemplateInfo.ThirdPartyNotices))
                {
                    writer.WritePropertyName(nameof(ITemplateInfo.ThirdPartyNotices));
                    writer.WriteStringValue(value.TemplateInfo.ThirdPartyNotices);
                }

                if (value.TemplateInfo.Classifications.Any())
                {
                    writer.WritePropertyName(nameof(ITemplateInfo.Classifications));
                    writer.WriteStartArray();
                    foreach (string classification in value.TemplateInfo.Classifications)
                    {
                        if (!string.IsNullOrWhiteSpace(classification))
                        {
                            writer.WriteStringValue(classification);
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
                        writer.WriteStringValue(tag.Value);
                    }
                    writer.WriteEndObject();
                }

                if (value.TemplateInfo.ParameterDefinitions.Any())
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    writer.WritePropertyName(nameof(ITemplateInfo.Parameters));
#pragma warning restore CS0618 // Type or member is obsolete
                    writer.WriteStartArray();
                    foreach (ITemplateParameter param in value.TemplateInfo.ParameterDefinitions)
                    {
                        writer.WriteStartObject();
                        writer.WritePropertyName(nameof(ITemplateParameter.Name));
                        writer.WriteStringValue(param.Name);
                        if (!string.IsNullOrWhiteSpace(param.DataType))
                        {
                            writer.WritePropertyName(nameof(ITemplateParameter.DataType));
                            writer.WriteStringValue(param.DataType);
                        }
                        if (!string.IsNullOrWhiteSpace(param.Description))
                        {
                            writer.WritePropertyName(nameof(ITemplateParameter.Description));
                            writer.WriteStringValue(param.Description);
                        }
                        if (!string.IsNullOrWhiteSpace(param.DefaultIfOptionWithoutValue))
                        {
                            writer.WritePropertyName(nameof(ITemplateParameter.DefaultIfOptionWithoutValue));
                            writer.WriteStringValue(param.DefaultIfOptionWithoutValue);
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
                                    writer.WriteStringValue(choice.Value.Description);
                                }
                                if (!string.IsNullOrWhiteSpace(choice.Value.DisplayName))
                                {
                                    writer.WritePropertyName(nameof(ParameterChoice.DisplayName));
                                    writer.WriteStringValue(choice.Value.DisplayName);
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
                    JsonSerializer.Serialize(writer, value.TemplateInfo.BaselineInfo, options);
                }

                if (value.TemplateInfo.PostActions.Any())
                {
                    writer.WritePropertyName(nameof(ITemplateInfo.PostActions));
                    writer.WriteStartArray();
                    foreach (Guid guid in value.TemplateInfo.PostActions)
                    {
                        writer.WriteStringValue(guid.ToString());
                    }
                    writer.WriteEndArray();
                }

                if (value.AdditionalData.Any())
                {
                    foreach (var item in value.AdditionalData)
                    {
                        writer.WritePropertyName(item.Key);
                        JsonSerializer.Serialize(writer, item.Value, options);
                    }
                }

                writer.WriteEndObject();
            }
        }
        #endregion
    }
}
