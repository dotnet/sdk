// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json.Nodes;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GetRuntimePackRids : Task
    {
        [Required]
        public string MetapackagePath { get; set; }

        [Output]
        public ITaskItem[] AvailableRuntimePackRuntimeIdentifiers { get; set; }

        public override bool Execute()
        {
            string runtimeJsonPath = Path.Combine(MetapackagePath, "runtime.json");
            string runtimeJsonContents = File.ReadAllText(runtimeJsonPath);
            var runtimeJsonRoot = JsonNode.Parse(runtimeJsonContents)!.AsObject();
            string [] runtimeIdentifiers = runtimeJsonRoot["runtimes"]!.AsObject().Select(p => p.Key).ToArray();
            AvailableRuntimePackRuntimeIdentifiers = runtimeIdentifiers.Select(rid => new TaskItem(rid)).ToArray();

            return true;
        }
    }
}
