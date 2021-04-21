// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateSearch.Common
{
    public class NuGetSearchCacheConfig : ISearchCacheConfig
    {
        public NuGetSearchCacheConfig(string templateDiscoveryFileName)
        {
            TemplateDiscoveryFileName = templateDiscoveryFileName;

            AdditionalDataReaders = new Dictionary<string, Func<JObject, object>>(StringComparer.OrdinalIgnoreCase);
        }

        public string TemplateDiscoveryFileName { get; }

        IReadOnlyDictionary<string, Func<JObject, object>> ISearchCacheConfig.AdditionalDataReaders => AdditionalDataReaders;

        protected Dictionary<string, Func<JObject, object>> AdditionalDataReaders { get; }
    }
}
