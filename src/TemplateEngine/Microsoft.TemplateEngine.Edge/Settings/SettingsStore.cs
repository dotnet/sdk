using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    public class SettingsStore
    {
        public SettingsStore()
        {
            MountPoints = new List<MountPointInfo>();
            ComponentGuidToAssemblyQualifiedName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ComponentTypeToGuidList = new Dictionary<string, List<Guid>>();
            ProbingPaths = new HashSet<string>();
        }

        [JsonProperty]
        public List<MountPointInfo> MountPoints { get; }

        [JsonProperty]
        public Dictionary<string, string> ComponentGuidToAssemblyQualifiedName { get; }

        [JsonProperty]
        public HashSet<string> ProbingPaths { get; }

        [JsonProperty]
        public Dictionary<string, List<Guid>> ComponentTypeToGuidList { get; }
    }

    public class ComponentInfo
    {
        [JsonProperty]
        public string AssemblyQualifiedTypeName { get; set; }

        [JsonProperty]
        public string Package { get; set; }
    }
}
