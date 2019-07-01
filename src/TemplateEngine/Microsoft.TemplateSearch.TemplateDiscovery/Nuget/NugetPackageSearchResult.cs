using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Nuget
{
    public class NugetPackageSearchResult
    {
        [JsonProperty("@context")]
        public Dictionary<string, string> Context { get; set; }

        [JsonProperty]
        public int TotalHits { get; set; }

        [JsonProperty]
        public string LastReopen { get; set; }

        [JsonProperty]
        public string Index { get; set; }

        [JsonProperty]
        public List<NugetPackageSourceInfo> Data { get; set; }
    }
}
