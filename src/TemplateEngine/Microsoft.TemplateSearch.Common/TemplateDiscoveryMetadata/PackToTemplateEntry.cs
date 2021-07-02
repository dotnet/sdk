// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.TemplateSearch.Common
{
    [Obsolete("The class is deprecated. Use TemplateSearchCache instead to create search cache data")]
    internal class PackToTemplateEntry
    {
        internal PackToTemplateEntry(string version, List<TemplateIdentificationEntry> templateinfo)
        {
            Version = version;
            TemplateIdentificationEntry = templateinfo;
        }

        [JsonProperty]
        internal string Version { get; }

        [JsonProperty]
        internal long TotalDownloads { get; set; }

        [JsonProperty]
        internal IReadOnlyList<TemplateIdentificationEntry> TemplateIdentificationEntry { get; }
    }
}
