// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine;
using Microsoft.TemplateSearch.Common.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateSearch.Common
{
    [JsonConverter(typeof(TemplatePackageSearchData.TemplatePackageSearchDataJsonConverter))]
    public class TemplatePackageSearchData : IPackageInfo
    {
        public TemplatePackageSearchData(IPackageInfo packInfo, IEnumerable<TemplateSearchData> templates, IDictionary<string, object>? data = null)
        {
            Name = packInfo.Name;
            Version = packInfo.Version;
            TotalDownloads = packInfo.TotalDownloads;
            Templates = templates.ToList();
            AdditionalData = data ?? new Dictionary<string, object>();
        }

        internal TemplatePackageSearchData(JObject jObject, ILogger logger, IReadOnlyDictionary<string, Func<object, object>>? additionalDataReaders = null)
        {
            string? name = jObject.ToString(nameof(Name));

            Name = name!;
            Version = jObject.ToString(nameof(Version));
            TotalDownloads = jObject.ToInt32(nameof(TotalDownloads));

            JArray? templatesData = jObject.Get<JArray>(nameof(Templates));
            if (templatesData == null)
            {
                throw new ArgumentException($"{nameof(jObject)} doesn't have {nameof(Templates)} property or it is not an array.", nameof(jObject));
            }
            List<TemplateSearchData> templates = new List<TemplateSearchData>();
            foreach (JToken template in templatesData)
            {
                JObject? templateObj = template as JObject;
                try
                {
                    if (templateObj == null)
                    {
                        throw new Exception($"Unexpected data in template search cache data, property: {nameof(Templates)}, value: {template}");
                    }
                    templates.Add(new TemplateSearchData(templateObj, logger, additionalDataReaders));
                }
                catch (Exception ex)
                {
                    logger.LogDebug($"Failed to read template package data {templateObj}, details: {ex}");
                }
            }
            Templates = templates;
            //read additional data
            if (additionalDataReaders != null)
            {
                AdditionalData = TemplateSearchCache.ReadAdditionalData(jObject, additionalDataReaders, logger);
            }
        }

        [JsonProperty]
        public string Name { get; }

        [JsonProperty]
        public string? Version { get; }

        [JsonProperty]
        public long TotalDownloads { get; }

        [JsonProperty]
        public IReadOnlyList<TemplateSearchData> Templates { get; }

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalData { get; } = new Dictionary<string, object>();

        #region JsonConverter
        private class TemplatePackageSearchDataJsonConverter : JsonConverter<TemplatePackageSearchData>
        {
            public override TemplatePackageSearchData ReadJson(JsonReader reader, Type objectType, TemplatePackageSearchData existingValue, bool hasExistingValue, JsonSerializer serializer)
                => throw new NotImplementedException();

            public override void WriteJson(JsonWriter writer, TemplatePackageSearchData value, JsonSerializer serializer)
            {
                writer.WriteStartObject();
                writer.WritePropertyName(nameof(Name));
                writer.WriteValue(value.Name);
                if (!string.IsNullOrWhiteSpace(value.Version))
                {
                    writer.WritePropertyName(nameof(Version));
                    writer.WriteValue(value.Version);
                }
                if (value.TotalDownloads != 0)
                {
                    writer.WritePropertyName(nameof(TotalDownloads));
                    writer.WriteValue(value.TotalDownloads);
                }

                writer.WritePropertyName(nameof(Templates));
                serializer.Serialize(writer, value.Templates);

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
