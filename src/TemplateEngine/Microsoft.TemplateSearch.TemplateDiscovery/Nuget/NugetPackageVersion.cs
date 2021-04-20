// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Newtonsoft.Json;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Nuget
{
    public class NugetPackageVersion
    {
        [JsonProperty]
        public string Version { get; set; }

        [JsonProperty]
        public int Downloads { get; set; }

        [JsonProperty("@id")]
        public string IdUrl { get; set; }
    }
}
