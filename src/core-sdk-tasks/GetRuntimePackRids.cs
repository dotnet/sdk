// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Cli.Build
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
            var runtimeJsonRoot = JObject.Parse(runtimeJsonContents);
            string [] runtimeIdentifiers = ((JObject)runtimeJsonRoot["runtimes"]).Properties().Select(p => p.Name).ToArray();
            AvailableRuntimePackRuntimeIdentifiers = runtimeIdentifiers.Select(rid => new TaskItem(rid)).ToArray();
            return true;
        }
    }
}
