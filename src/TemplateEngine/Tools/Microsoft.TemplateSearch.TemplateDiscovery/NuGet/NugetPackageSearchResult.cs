// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;
using Microsoft.TemplateEngine;

namespace Microsoft.TemplateSearch.TemplateDiscovery.NuGet
{
    internal class NuGetPackageSearchResult
    {
        internal int TotalHits { get; private set; }

        internal List<NuGetPackageSourceInfo> Data { get; } = new List<NuGetPackageSourceInfo>();

        //property names are explained here: https://docs.microsoft.com/en-us/nuget/api/search-query-service-resource
        internal static NuGetPackageSearchResult FromJObject(JsonObject entry)
        {
            NuGetPackageSearchResult searchResult = new NuGetPackageSearchResult
            {
                TotalHits = entry.ToInt32("totalHits")
            };
            var dataArray = entry.Get<JsonArray>("data");
            if (dataArray != null)
            {
                foreach (JsonNode? data in dataArray)
                {
                    if (data is JsonObject dataObj)
                    {
                        searchResult.Data.Add(NuGetPackageSourceInfo.FromJObject(dataObj));
                    }
                }

            }
            return searchResult;
        }
    }
}
