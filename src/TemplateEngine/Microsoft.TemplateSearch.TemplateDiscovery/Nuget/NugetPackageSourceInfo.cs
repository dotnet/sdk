using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Nuget
{
    public class NugetPackageSourceInfo
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
        public int TotalDownloads { get; set; }

        [JsonProperty]
        public bool Verified { get; set; }

        [JsonProperty("versions")]
        public List<NugetPackageVersion> PackageVersions { get; set; }
    }
}
