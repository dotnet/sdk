// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine;
using Microsoft.TemplateSearch.TemplateDiscovery.PackProviders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Nuget
{
    internal class NugetPackageSourceInfo : IPackInfo, IEquatable<IPackInfo>
    {
        internal NugetPackageSourceInfo(string id, string version)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException($"'{nameof(id)}' cannot be null or whitespace.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException($"'{nameof(version)}' cannot be null or whitespace.", nameof(version));
            }

            Id = id;
            Version = version;
        }

        public string VersionedPackageIdentity
        {
            get
            {
                return $"{Id}::{Version}";
            }
        }

        [JsonProperty]
        public string Id { get; private set; }

        [JsonProperty]
        public string Version { get; private set; }

        [JsonProperty]
        public long TotalDownloads { get; set; }

        internal static NugetPackageSourceInfo FromJObject (JObject entry)
        {
            string id = entry.ToString(nameof(Id)) ?? throw new ArgumentException($"{nameof(entry)} doesn't have {nameof(Id)} property.", nameof(entry));
            string version = entry.ToString(nameof(Version)) ?? throw new ArgumentException($"{nameof(entry)} doesn't have {nameof(Version)} property.", nameof(entry));
            NugetPackageSourceInfo sourceInfo = new NugetPackageSourceInfo(id, version);
            sourceInfo.TotalDownloads = entry.ToInt32(nameof(TotalDownloads));
            return sourceInfo;
        }

#pragma warning disable SA1202 // Elements should be ordered by access
        public override bool Equals(object? obj)
#pragma warning restore SA1202 // Elements should be ordered by access
        {
            if (obj is NugetPackageSourceInfo info)
            {
                return Id.Equals(info.Id, StringComparison.OrdinalIgnoreCase) && Version.Equals(info.Version, StringComparison.OrdinalIgnoreCase);
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return (Id, Version).GetHashCode();
        }

        public bool Equals(IPackInfo? other)
        {
            if (other == null)
            {
                return false;
            }

            return Id.Equals(other.Id, StringComparison.OrdinalIgnoreCase) && Version.Equals(other.Version, StringComparison.OrdinalIgnoreCase);
        }
    }
}
