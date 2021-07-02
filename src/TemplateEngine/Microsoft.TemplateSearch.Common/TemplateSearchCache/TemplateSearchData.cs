// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateSearch.Common
{
    public class TemplateSearchData
    {
        public TemplateSearchData(ITemplateInfo templateInfo, IDictionary<string, object>? data = null)
        {
            TemplateInfo = new BlobStorageTemplateInfo(templateInfo);
            AdditionalData = data ?? new Dictionary<string, object>();
        }

        internal TemplateSearchData(JObject jObject, ILogger logger, IReadOnlyDictionary<string, Func<object, object>>? additionalDataReaders = null)
        {
            JObject? templateInfoObject = jObject.Get<JObject>(nameof(TemplateInfo));
            if (templateInfoObject == null)
            {
                throw new ArgumentException($"{nameof(jObject)} doesn't have {nameof(TemplateInfo)} property or it is empty.", nameof(jObject));
            }
            TemplateInfo = BlobStorageTemplateInfo.FromJObject(templateInfoObject);
            //read additional data
            if (additionalDataReaders != null)
            {
                AdditionalData = TemplateSearchCache.ReadAdditionalData(jObject, additionalDataReaders, logger);
            }
        }

        [JsonProperty]
        public ITemplateInfo TemplateInfo { get; }

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalData { get; } = new Dictionary<string, object>();
    }
}
