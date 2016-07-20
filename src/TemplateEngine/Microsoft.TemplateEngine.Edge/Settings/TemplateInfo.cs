using System;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class TemplateInfo
    {
        [JsonProperty]
        public Guid MountPointId { get; set; }

        [JsonProperty]
        public Guid GeneratorId { get; set; }

        [JsonProperty]
        public string Path { get; set; }
    }
}