// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    public sealed class UpdateRuntimeConfig : Task
    {
        [Required]
        public ITaskItem[] RuntimeConfigPaths { get; set; }

        [Required]
        public string MicrosoftNetCoreAppVersion { get; set; }

        [Required]
        public string MicrosoftAspNetCoreAppVersion { get; set; }

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
            JObject config = JObject.Parse(text);
            var frameworks = config["runtimeOptions"]?["frameworks"];
            var framework = config["runtimeOptions"]?["framework"];
            if (frameworks != null)
            {
                foreach (var item in frameworks)
                {
                    UpdateFramework(item);
                }
            }
            else if (framework != null)
            {
                UpdateFramework(framework);
            }

            File.WriteAllText(file, config.ToString());
        }

        private void UpdateFramework(JToken item)
        {
            var framework = (JObject)item;
            var name = framework["name"].Value<string>();
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