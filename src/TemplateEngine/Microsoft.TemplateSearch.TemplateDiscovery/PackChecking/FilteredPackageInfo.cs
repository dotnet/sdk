// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateSearch.Common.Abstractions;
using Newtonsoft.Json;

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackChecking
{
    internal class FilteredPackageInfo : ITemplatePackageInfo
    {
        private readonly ITemplatePackageInfo _baseInfo;

        internal FilteredPackageInfo(ITemplatePackageInfo info, string reason)
        {
            Reason = reason;
            _baseInfo = info;
        }

        [JsonConstructor]
        private FilteredPackageInfo(string name, string reason, string? version, long totalDownloads, IEnumerable<string> owners, bool verified)
        {
            Reason = reason;
            _baseInfo = new InnerPackInfo(name, version, totalDownloads, owners, verified);
        }

        [JsonProperty]
        public string Name => _baseInfo.Name;

        [JsonProperty]
        public string? Version => _baseInfo.Version;

        [JsonProperty]
        public string Reason { get; private set; }

        [JsonProperty]
        public long TotalDownloads => _baseInfo.TotalDownloads;

        [JsonProperty]
        public IReadOnlyList<string> Owners => _baseInfo.Owners;

        [JsonProperty]
        public bool Verified => _baseInfo.Verified;

        private class InnerPackInfo : ITemplatePackageInfo
        {
            internal InnerPackInfo(string name, string? version, long totalDownloads, IEnumerable<string> owners, bool verified)
            {
                Name = name;
                Version = version;
                TotalDownloads = totalDownloads;
                Owners = owners?.ToArray() ?? Array.Empty<string>();
                Verified = verified;
            }

            public string Name { get; }

            public string? Version { get; }

            public long TotalDownloads { get; }

            public IReadOnlyList<string> Owners { get; }

            public bool Verified { get; }
        }
    }
}

