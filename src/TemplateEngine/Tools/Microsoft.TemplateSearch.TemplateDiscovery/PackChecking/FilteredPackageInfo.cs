// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;
using Microsoft.TemplateSearch.Common.Abstractions;

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackChecking
{
    [System.Diagnostics.DebuggerDisplay("{Name}@{Version} - {Reason}")]
    internal class FilteredPackageInfo : ITemplatePackageInfo
    {
        internal FilteredPackageInfo(ITemplatePackageInfo info, string reason)
        {
            Reason = reason;
            Name = info.Name;
            Version = info.Version;
            Owners = info.Owners;
            TotalDownloads = info.TotalDownloads;
            Reserved = info.Reserved;
            Description = info.Description;
            IconUrl = info.IconUrl;
        }

        [System.Text.Json.Serialization.JsonConstructor]
        private FilteredPackageInfo(string name, string reason)
        {
            Name = name;
            Reason = reason;
        }

        [JsonInclude]
        public string Name { get; private set; }

        [JsonInclude]
        public string? Version { get; private set; }

        [JsonInclude]
        public string Reason { get; private set; }

        [JsonInclude]
        public long TotalDownloads { get; private set; }

        [JsonInclude]
        public IReadOnlyList<string> Owners { get; private set; } = [];

        [JsonInclude]
        public bool Reserved { get; private set; }

        [JsonIgnore]
        public string? Description { get; }

        [JsonIgnore]
        public string? IconUrl { get; }
    }
}

