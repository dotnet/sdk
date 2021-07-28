// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.TemplateSearch.Common
{
    internal partial class TemplateSearchCache
    {
        private const string CurrentVersion = "2.0";

        internal TemplateSearchCache(IReadOnlyList<TemplatePackageSearchData> data)
        {
            TemplatePackages = data;
            Version = CurrentVersion;
        }

        private TemplateSearchCache(IReadOnlyList<TemplatePackageSearchData> data, string version)
        {
            TemplatePackages = data;
            Version = version;
        }

        [JsonProperty]
        internal IReadOnlyList<TemplatePackageSearchData> TemplatePackages { get; }

        [JsonProperty]
        internal string Version { get; private set; }
    }
}
