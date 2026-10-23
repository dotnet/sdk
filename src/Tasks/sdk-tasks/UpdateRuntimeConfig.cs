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

        [Required]
        public string MicrosoftNetCoreAppVersion { get; set; }

        [Required]
        public string MicrosoftAspNetCoreAppVersion { get; set; }

        /// <summary>
        /// Optional runtime host configuration options to write into every runtimeconfig.
        /// Used for the OpenHarmony sandbox defaults on the SDK's own tools, so they are not
        /// inherited by user applications.
        /// </summary>
        public ITaskItem[] RuntimeHostConfigurationOptions { get; set; }

        public override bool Execute()
        {
            foreach (var file in RuntimeConfigPaths)
            {
                UpdateFile(file.ItemSpec);
            }

            return true;
        }

        private void UpdateFile(string file)
        {
            var text = File.ReadAllText(file);
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

            if (RuntimeHostConfigurationOptions is { Length: > 0 })
            {
                var runtimeOptions = config["runtimeOptions"] as JsonObject;
                if (runtimeOptions == null)
                {
                    runtimeOptions = new JsonObject();
                    config["runtimeOptions"] = runtimeOptions;
                }

                var configProperties = runtimeOptions["configProperties"] as JsonObject;
                if (configProperties == null)
                {
                    configProperties = new JsonObject();
                    runtimeOptions["configProperties"] = configProperties;
                }

                foreach (var option in RuntimeHostConfigurationOptions)
                {
                    var value = option.GetMetadata("Value");
                    configProperties[option.ItemSpec] = bool.TryParse(value, out bool boolValue)
                        ? JsonValue.Create(boolValue)
                        : JsonValue.Create(value);
                }
            }

            File.WriteAllText(file, config.ToJsonString(s_writeOptions));
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
