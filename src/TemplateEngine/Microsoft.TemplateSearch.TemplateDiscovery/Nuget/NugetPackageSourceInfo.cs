// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine;
using Microsoft.TemplateSearch.Common.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateSearch.TemplateDiscovery.NuGet
{
    internal class NuGetPackageSourceInfo : ITemplatePackageInfo, IEquatable<ITemplatePackageInfo>
    {
        internal NuGetPackageSourceInfo(string id, string version)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException($"'{nameof(id)}' cannot be null or whitespace.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException($"'{nameof(version)}' cannot be null or whitespace.", nameof(version));
            }

            Name = id;
            Version = version;
        }

        public string Name { get; private set; }

        public string Version { get; private set; }

        public long TotalDownloads { get; private set; }

        public IReadOnlyList<string> Owners { get; private set; } = Array.Empty<string>();

        public bool Verified { get; private set; }

        //property names are explained here: https://docs.microsoft.com/en-us/nuget/api/search-query-service-resource
        internal static NuGetPackageSourceInfo FromJObject (JObject entry)
        {
            string id = entry.ToString("id") ?? throw new ArgumentException($"{nameof(entry)} doesn't have \"id\" property.", nameof(entry));
            string version = entry.ToString("version") ?? throw new ArgumentException($"{nameof(entry)} doesn't have \"version\"  property.", nameof(entry));
            NuGetPackageSourceInfo sourceInfo = new NuGetPackageSourceInfo(id, version);
            sourceInfo.TotalDownloads = entry.ToInt32("totalDownloads");
            sourceInfo.Owners = entry.Get<JToken>("owners").JTokenStringOrArrayToCollection(Array.Empty<string>());
            sourceInfo.Verified = entry.ToBool("verified");

            return sourceInfo;
        }

#pragma warning disable SA1202 // Elements should be ordered by access
        public override bool Equals(object? obj)
#pragma warning restore SA1202 // Elements should be ordered by access
        {
            if (obj is NuGetPackageSourceInfo info)
            {
                return Name.Equals(info.Name, StringComparison.OrdinalIgnoreCase) && Version.Equals(info.Version, StringComparison.OrdinalIgnoreCase);
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return (Name, Version).GetHashCode();
        }

        public bool Equals(ITemplatePackageInfo? other)
        {
            if (other == null)
            {
                return false;
            }

            return Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase) && Version.Equals(other.Version, StringComparison.OrdinalIgnoreCase);
        }
    }
}
