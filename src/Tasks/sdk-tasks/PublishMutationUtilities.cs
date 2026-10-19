// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.DotNet.Build.Tasks
{
    public class PublishMutationUtilities
    {
        private static readonly JsonSerializerOptions s_writeOptions = new() { WriteIndented = true };

        public static void ChangeEntryPointLibraryName(string depsFile, string newName, string destinationFile = null, bool removeResources = false)
        {
            string text = File.ReadAllText(depsFile);
            var deps = JsonNode.Parse(text);
            var targets = deps["targets"]!.AsObject();
            var targetLibraries = targets.SelectMany(target => target.Value!.AsObject()).ToArray();

            // Renaming moves the entry to the end of each object. On a repeated invocation,
            // find that entry by its new name rather than renaming the next dependency.
            var entryPoint = newName == null
                ? targetLibraries.FirstOrDefault()
                : targetLibraries.FirstOrDefault(library => library.Key.StartsWith(newName + '/', StringComparison.Ordinal));
            if (entryPoint.Key == null)
            {
                entryPoint = targetLibraries.FirstOrDefault();
            }

            bool changed = false;
            if (entryPoint.Key != null)
            {
                string version = entryPoint.Key.Substring(entryPoint.Key.IndexOf('/') + 1);
                string renamedKey = newName == null ? null : newName + '/' + version;
                if (entryPoint.Key != renamedKey)
                {
                    foreach (var target in targets)
                    {
                        var targetObj = target.Value!.AsObject();
                        if (targetObj.TryGetPropertyValue(entryPoint.Key, out JsonNode value))
                        {
                            targetObj.Remove(entryPoint.Key);
                            if (renamedKey != null)
                            {
                                targetObj.Add(renamedKey, value);
                            }
                        }
                    }

                    var libraries = deps["libraries"]!.AsObject();
                    JsonNode library = libraries[entryPoint.Key];
                    libraries.Remove(entryPoint.Key);
                    if (renamedKey != null)
                    {
                        libraries.Add(renamedKey, library);
                    }
                    changed = true;
                }
            }

            if (removeResources)
            {
                changed |= RemoveAssetFromDepsPackages.RemoveAssets(deps, "resources", "*");
            }

            if (changed || destinationFile != null)
            {
                WriteFileIfDifferent(destinationFile ?? depsFile, changed ? deps.ToJsonString(s_writeOptions) : text);
            }
        }

        internal static void WriteFileIfDifferent(string path, string contents)
        {
            if (!File.Exists(path) ||
                !File.ReadAllBytes(path).AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(contents)))
            {
                IncrementalLayoutState.WriteFile(path, contents);
            }
        }
    }
}
