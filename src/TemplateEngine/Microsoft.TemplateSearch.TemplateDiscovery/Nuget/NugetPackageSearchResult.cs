// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Nuget
{
    internal class NugetPackageSearchResult
    {
        internal int TotalHits { get; private set; }

        internal List<NugetPackageSourceInfo> Data { get; private set; } = new List<NugetPackageSourceInfo>();

        internal static NugetPackageSearchResult FromJObject(JObject entry)
        {
            NugetPackageSearchResult searchResult = new NugetPackageSearchResult();
            searchResult.TotalHits = entry.ToInt32(nameof(TotalHits));
            var dataArray = entry.Get<JArray>(nameof(Data));
            if (dataArray != null)
            {
                foreach (JToken data in dataArray)
                {
                    JObject? dataObj = data as JObject;
                    if (dataObj != null)
                    {
                        searchResult.Data.Add(NugetPackageSourceInfo.FromJObject(dataObj));
                    }
                }

            }
            return searchResult;
        }
    }
}
