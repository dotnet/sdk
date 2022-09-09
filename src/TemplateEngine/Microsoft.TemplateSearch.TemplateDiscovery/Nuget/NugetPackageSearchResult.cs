// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateSearch.TemplateDiscovery.NuGet
{
    internal class NuGetPackageSearchResult
    {
        internal int TotalHits { get; private set; }

        internal List<NuGetPackageSourceInfo> Data { get; private set; } = new List<NuGetPackageSourceInfo>();

        //property names are explained here: https://docs.microsoft.com/en-us/nuget/api/search-query-service-resource
        internal static NuGetPackageSearchResult FromJObject(JObject entry)
        {
            NuGetPackageSearchResult searchResult = new NuGetPackageSearchResult
            {
                TotalHits = entry.ToInt32("totalHits")
            };
            var dataArray = entry.Get<JArray>("data");
            if (dataArray != null)
            {
                foreach (JToken data in dataArray)
                {
                    if (data is JObject dataObj)
                    {
                        searchResult.Data.Add(NuGetPackageSourceInfo.FromJObject(dataObj));
                    }
                }

            }
            return searchResult;
        }
    }
}
