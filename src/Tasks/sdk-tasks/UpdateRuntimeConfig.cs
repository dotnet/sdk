// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.DotNet.Build.Tasks
{
    public sealed class UpdateRuntimeConfig : Task
    {
        private static readonly JsonSerializerOptions s_writeOptions = new() { WriteIndented = true };

        [Required]
        public ITaskItem[] RuntimeConfigPaths { get; set; }

        public ITaskItem[] DestinationPaths { get; set; }

        [Required]
        public string MicrosoftNetCoreAppVersion { get; set; }

        [Required]
        public string MicrosoftAspNetCoreAppVersion { get; set; }

        public override bool Execute()
        {
            if (DestinationPaths != null && DestinationPaths.Length != RuntimeConfigPaths.Length)
            {
                Log.LogError("Runtime configuration source and destination counts must match.");
                return false;
            }

            for (int index = 0; index < RuntimeConfigPaths.Length; index++)
            {
                string source = RuntimeConfigPaths[index].ItemSpec;
                UpdateFile(source, DestinationPaths == null ? source : DestinationPaths[index].ItemSpec);
            }

            return true;
        }

        private void UpdateFile(string source, string destination)
        {
            var text = File.ReadAllText(source);
            var config = JsonNode.Parse(text)!.AsObject();
            var frameworks = config["runtimeOptions"]?["frameworks"];
            var framework = config["runtimeOptions"]?["framework"];
            if (frameworks != null)
            {
                foreach (var item in frameworks.AsArray())
                {
                    UpdateFramework(item);
                }
            }
            else if (framework != null)
            {
                UpdateFramework(framework);
            }

            PublishMutationUtilities.WriteFileIfDifferent(destination, config.ToJsonString(s_writeOptions));
        }

        private void UpdateFramework(JsonNode item)
        {
            var framework = item.AsObject();
            var name = framework["name"]!.GetValue<string>();
            if (name == "Microsoft.NETCore.App")
            {
                framework["version"] = MicrosoftNetCoreAppVersion;
            }
            else if (name == "Microsoft.AspNetCore.App")
            {
                framework["version"] = MicrosoftAspNetCoreAppVersion;
            }
        }
    }
}
