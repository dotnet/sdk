// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    [MSBuildMultiThreadableTask]
    public class GetRuntimePackRids : Task, IMultiThreadableTask
    {
        /// <summary>
        /// Gets or sets the task environment for thread-safe operations.
        /// </summary>
        public TaskEnvironment? TaskEnvironment { get; set; }

        [Required]
        public string MetapackagePath { get; set; }

        [Output]
        public ITaskItem[] AvailableRuntimePackRuntimeIdentifiers { get; set; }

        public override bool Execute()
        {
            string absoluteMetapackagePath = TaskEnvironment?.GetAbsolutePath(MetapackagePath) ?? MetapackagePath;
            string runtimeJsonPath = Path.Combine(absoluteMetapackagePath, "runtime.json");
            string runtimeJsonContents;
            using (var stream = new FileStream(runtimeJsonPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(stream))
            {
                runtimeJsonContents = reader.ReadToEnd();
            }
            var runtimeJsonRoot = JObject.Parse(runtimeJsonContents);
            string [] runtimeIdentifiers = ((JObject)runtimeJsonRoot["runtimes"]).Properties().Select(p => p.Name).ToArray();
            AvailableRuntimePackRuntimeIdentifiers = runtimeIdentifiers.Select(rid => new TaskItem(rid)).ToArray();

            return true;
        }
    }
}
