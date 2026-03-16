// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine;

namespace Microsoft.TemplateSearch.Common
{
    [System.Text.Json.Serialization.JsonConverter(typeof(TemplatePackageSearchDataJsonConverter))]
    public partial class TemplatePackageSearchData
    {
        internal TemplatePackageSearchData(JsonObject jObject, ILogger logger, IReadOnlyDictionary<string, Func<object, object>>? additionalDataReaders = null)
        {
            if (jObject is null)
            {
                throw new ArgumentNullException(nameof(jObject));
            }

            if (logger is null)
            {
                throw new ArgumentNullException(nameof(logger));
            }
            string? name = jObject.ToString(nameof(Name));
            Name = !string.IsNullOrWhiteSpace(name) ? name!
                : throw new ArgumentException($"{nameof(jObject)} doesn't have {nameof(Name)} property or it is not a string.", nameof(jObject));
            Version = jObject.ToString(nameof(Version));
            TotalDownloads = jObject.ToInt32(nameof(TotalDownloads));
            Owners = jObject.Get<JsonNode>(nameof(Owners)).JTokenStringOrArrayToCollection([]);
            Reserved = jObject.ToBool(nameof(Reserved));

            Description = jObject.ToString(nameof(Description));
            IconUrl = jObject.ToString(nameof(IconUrl));

            JsonArray? templatesData = jObject.Get<JsonArray>(nameof(Templates))
                ?? throw new ArgumentException($"{nameof(jObject)} doesn't have {nameof(Templates)} property or it is not an array.", nameof(jObject));
            List<TemplateSearchData> templates = new List<TemplateSearchData>();
            foreach (JsonNode? template in templatesData)
            {
                try
                {
                    if (template is JsonObject templateObj)
                    {
                        templates.Add(new TemplateSearchData(templateObj, logger, additionalDataReaders));
                    }
                    else
                    {
                        throw new Exception($"Unexpected data in template package cache data, property: {nameof(Templates)}.");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug($"Template package {Name}: Failed to read template data {template}, details: {ex}.");
                }
            }
            Templates = templates;
            //read additional data
            AdditionalData = additionalDataReaders != null
                ? TemplateSearchCache.ReadAdditionalData(jObject, additionalDataReaders, logger)
                : new Dictionary<string, object>();
        }

        #region JsonConverter
        private class TemplatePackageSearchDataJsonConverter : System.Text.Json.Serialization.JsonConverter<TemplatePackageSearchData>
        {
            public override TemplatePackageSearchData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                => throw new NotImplementedException();

            public override void Write(Utf8JsonWriter writer, TemplatePackageSearchData value, JsonSerializerOptions options)
            {
                if (value == null)
                {
                    return;
                }
                writer.WriteStartObject();
                writer.WritePropertyName(nameof(Name));
                writer.WriteStringValue(value.Name);
                if (!string.IsNullOrWhiteSpace(value.Version))
                {
                    writer.WritePropertyName(nameof(Version));
                    writer.WriteStringValue(value.Version);
                }
                if (value.TotalDownloads != 0)
                {
                    writer.WritePropertyName(nameof(TotalDownloads));
                    writer.WriteNumberValue(value.TotalDownloads);
                }
                if (value.Owners.Any())
                {
                    writer.WritePropertyName(nameof(Owners));
                    if (value.Owners.Count == 1)
                    {
                        writer.WriteStringValue(value.Owners[0]);
                    }
                    else
                    {
                        writer.WriteStartArray();
                        foreach (string owner in value.Owners)
                        {
                            writer.WriteStringValue(owner);
                        }
                        writer.WriteEndArray();
                    }
                }

                if (value.Reserved)
                {
                    writer.WritePropertyName(nameof(Reserved));
                    writer.WriteBooleanValue(value.Reserved);
                }
                if (!string.IsNullOrWhiteSpace(value.Description))
                {
                    writer.WritePropertyName(nameof(Description));
                    writer.WriteStringValue(value.Description);
                }
                if (!string.IsNullOrWhiteSpace(value.IconUrl))
                {
                    writer.WritePropertyName(nameof(IconUrl));
                    writer.WriteStringValue(value.IconUrl);
                }

                writer.WritePropertyName(nameof(Templates));
                JsonSerializer.Serialize(writer, value.Templates, options);

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
