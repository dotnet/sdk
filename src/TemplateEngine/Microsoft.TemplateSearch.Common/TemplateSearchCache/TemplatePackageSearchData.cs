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
    public class TemplatePackageSearchData
    {
        public TemplatePackageSearchData(IPackageInfo packInfo, IEnumerable<TemplateSearchData> templates, IDictionary<string, object>? data = null)
        {
            PackageInfo = packInfo;
            Templates = templates.ToList();
            AdditionalData = data ?? new Dictionary<string, object>();
        }

        internal TemplatePackageSearchData(JObject jObject, ILogger logger, IReadOnlyDictionary<string, Func<object, object>>? additionalDataReaders = null)
        {
            JObject? packageInfoObject = jObject.Get<JObject>(nameof(PackageInfo));
            if (packageInfoObject == null)
            {
                throw new ArgumentException($"{nameof(jObject)} doesn't have {nameof(PackageInfo)} property or it is empty.", nameof(jObject));
            }
            PackageInfo = new PackInfo(packageInfoObject);
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
        public IPackageInfo PackageInfo { get; }

        [JsonProperty]
        public IReadOnlyList<TemplateSearchData> Templates { get; }

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalData { get; } = new Dictionary<string, object>();
    }
}
