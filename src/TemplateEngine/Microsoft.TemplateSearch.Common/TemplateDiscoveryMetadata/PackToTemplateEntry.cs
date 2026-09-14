// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.TemplateSearch.Common
{
    [Obsolete("The class is deprecated. Use TemplateSearchCache instead to create search cache data.")]
    internal class PackToTemplateEntry
    {
        internal PackToTemplateEntry(string version, List<TemplateIdentificationEntry> templateIdentificationEntry)
        {
            Version = version;
            TemplateIdentificationEntry = templateIdentificationEntry;
        }

        [JsonInclude]
        internal string Version { get; }

        [JsonInclude]
        internal long TotalDownloads { get; set; }

        [JsonInclude]
        internal IReadOnlyList<string> Owners { get; set; } = [];

        [JsonInclude]
        internal bool Reserved { get; set; }

        [JsonInclude]
        internal IReadOnlyList<TemplateIdentificationEntry> TemplateIdentificationEntry { get; }
    }
}
