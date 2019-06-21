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
