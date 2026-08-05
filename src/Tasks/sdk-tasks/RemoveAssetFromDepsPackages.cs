// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Build.Tasks
{
    public class RemoveAssetFromDepsPackages : Task
    {
        private static readonly JsonSerializerOptions s_writeOptions = new() { WriteIndented = true };

        [Required]
        public string DepsFile { get; set; }

        [Required]
        public string SectionName { get; set; }

        [Required]
        public string AssetPath { get; set; }

        public override bool Execute()
        {
            DoRemoveAssetFromDepsPackages(DepsFile, SectionName, AssetPath);

            return true;
        }

        public static void DoRemoveAssetFromDepsPackages(string depsFile, string sectionName, string assetPath)
        {
            var deps = JsonNode.Parse(File.ReadAllText(depsFile));

            bool found = false;
            foreach (var target in deps["targets"]!.AsObject())
            {
                foreach (var pv in target.Value!.AsObject())
                {
                    var section = pv.Value![sectionName];
                    if (section != null)
                    {
                        var sectionObj = section.AsObject();
                        if (assetPath.Equals("*"))
                        {
                            pv.Value.AsObject().Remove(sectionName);
                            found = true;
                        }
                        else if (sectionObj.ContainsKey(assetPath))
                        {
                            sectionObj.Remove(assetPath);
                            found = true;
                        }
                    }
                }
            }

            if (found)
            {
                File.WriteAllText(depsFile, deps.ToJsonString(s_writeOptions));
            }
        }
    }
}
