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

        public static InstallStateContents FromString(string contents)
        {
            return JsonSerializer.Deserialize<InstallStateContents>(contents);
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize<InstallStateContents>(this, new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            });
        }
    }
}