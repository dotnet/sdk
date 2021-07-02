// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine;
using Microsoft.TemplateSearch.Common.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateSearch.Common
{
    internal class PackInfo : IPackageInfo
    {
        internal PackInfo(string name, string version)
        {
            Name = name;
            Version = version;
        }

        internal PackInfo(string name, string version, long totalDownloads)
        {
            Name = name;
            Version = version;
            TotalDownloads = totalDownloads;
        }

        internal PackInfo(JObject jObject)
        {
            string? name = jObject.ToString(nameof(Name));
            string? version = jObject.ToString(nameof(Version));
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"{nameof(jObject)} doesn't have {nameof(Name)} property or it is empty.", nameof(jObject));
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException($"'{nameof(jObject)} doesn't have {nameof(Version)} property or it is empty.", nameof(jObject));
            }

            Name = name!;
            Version = version!;
            TotalDownloads = jObject.ToInt32(nameof(TotalDownloads));
        }

        [JsonProperty]
        public string Name { get; }

        [JsonProperty]
        public string Version { get; }

        [JsonProperty]
        public long TotalDownloads { get; }
    }
}
