// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateSearch.Common.Abstractions;
using Newtonsoft.Json;

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackChecking
{
    internal class FilteredPackageInfo : ITemplatePackageInfo
    {
        internal FilteredPackageInfo(ITemplatePackageInfo info, string reason)
        {
            Reason = reason;
            Name = info.Name;
            Version = info.Version;
            Owners = info.Owners;
            TotalDownloads = info.TotalDownloads;
            Verified = info.Verified;
        }

        [JsonConstructor]
        private FilteredPackageInfo(string name, string reason)
        {
            Name = name;
            Reason = reason;
        }

        [JsonProperty]
        public string Name { get; private set; }

        [JsonProperty]
        public string? Version { get; private set; }

        [JsonProperty]
        public string Reason { get; private set; }

        [JsonProperty]
        public long TotalDownloads { get; private set; }

        [JsonProperty]
        public IReadOnlyList<string> Owners { get; private set; } = Array.Empty<string>();

        [JsonProperty]
        public bool Verified { get; private set; }
    }
}

