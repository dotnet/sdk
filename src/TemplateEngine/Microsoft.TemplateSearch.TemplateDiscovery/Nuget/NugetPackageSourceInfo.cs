// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateSearch.TemplateDiscovery.PackProviders;
using Newtonsoft.Json;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Nuget
{
    public class NugetPackageSourceInfo : IPackInfo, IEquatable<IPackInfo>
    {
        [JsonIgnore]
        public string VersionedPackageIdentity
        {
            get
            {
                return $"{Id}::{Version}";
            }
        }

        [JsonProperty("@id")]
        public string IdUrl { get; set; }

        [JsonProperty("@type")]
        public string Type { get; set; }

        [JsonProperty]
        public string Registration { get; set; }

        [JsonProperty]
        public string Id { get; set; }

        [JsonProperty]
        public string Version { get; set; }

        [JsonProperty]
        public string Description { get; set; }

        [JsonProperty]
        public string Summary { get; set; }

        [JsonProperty]
        public string Title { get; set; }

        [JsonProperty]
        public string LicenseUrl { get; set; }

        [JsonProperty]
        public string ProjectUrl { get; set; }

        [JsonProperty]
        public List<string> Tags { get; set; }

        [JsonProperty]
        public List<string> Authors { get; set; }

        [JsonProperty]
        public long TotalDownloads { get; set; }

        [JsonProperty]
        public bool Verified { get; set; }

        [JsonProperty("versions")]
        public List<NugetPackageVersion> PackageVersions { get; set; }

        public override bool Equals(object obj)
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

        public bool Equals(IPackInfo other)
        {
            if (other == null) return false;
            return Id.Equals(other.Id, StringComparison.OrdinalIgnoreCase) && Version.Equals(other.Version, StringComparison.OrdinalIgnoreCase);
        }
    }
}
