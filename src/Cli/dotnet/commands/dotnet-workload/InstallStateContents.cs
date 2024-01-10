using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.DotNet.Workloads.Workload
{
    internal class InstallStateContents
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? UseWorkloadSets { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, string> Manifests { get; set; }

        private static readonly JsonSerializerOptions s_options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        public static InstallStateContents FromString(string contents)
        {
            return JsonSerializer.Deserialize<InstallStateContents>(contents, s_options);
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize<InstallStateContents>(this, s_options);
        }
    }
}