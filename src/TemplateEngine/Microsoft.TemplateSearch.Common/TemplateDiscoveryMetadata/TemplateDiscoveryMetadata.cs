// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateSearch.Common
{
    [Obsolete("The class is deprecated. Use TemplateSearchCache instead to create search cache data")]
    internal class TemplateDiscoveryMetadata
    {
        internal TemplateDiscoveryMetadata(string version, IReadOnlyList<ITemplateInfo> templateCache, IReadOnlyDictionary<string, PackToTemplateEntry> packToTemplateMap, IReadOnlyDictionary<string, object> additionalData)
        {
            Version = version;
            TemplateCache = templateCache;
            PackToTemplateMap = packToTemplateMap;
            AdditionalData = additionalData;
        }

        [JsonProperty]
        internal string Version { get; }

        [JsonProperty]
        internal IReadOnlyList<ITemplateInfo> TemplateCache { get; }

        [JsonProperty]
        internal IReadOnlyDictionary<string, PackToTemplateEntry> PackToTemplateMap { get; }

        [JsonProperty]
        internal IReadOnlyDictionary<string, object> AdditionalData { get; }

        internal JObject ToJObject()
        {
            return JObject.FromObject(this);
        }
    }
}
