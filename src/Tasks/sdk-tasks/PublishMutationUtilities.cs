// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.DotNet.Build.Tasks
{
    public class PublishMutationUtilities
    {
        private static readonly JsonSerializerOptions s_writeOptions = new() { WriteIndented = true };

        public static void ChangeEntryPointLibraryName(string depsFile, string newName)
        {
            var deps = JsonNode.Parse(File.ReadAllText(depsFile));

            string version = null;
            foreach (var target in deps["targets"]!.AsObject())
            {
                var targetObj = target.Value!.AsObject();
                var targetLibrary = targetObj.FirstOrDefault();
                if (targetLibrary.Key == null)
                {
                    continue;
                }
                version = targetLibrary.Key.Substring(targetLibrary.Key.IndexOf('/') + 1);
                var targetLibraryValue = targetLibrary.Value;
                targetObj.Remove(targetLibrary.Key);
                if (newName != null)
                {
                    targetObj.Add(newName + '/' + version, targetLibraryValue);
                }
            }
            if (version != null)
            {
                var librariesObj = deps["libraries"]!.AsObject();
                var library = librariesObj.First();
                var libraryValue = library.Value;
                librariesObj.Remove(library.Key);
                if (newName != null)
                {
                    librariesObj.Add(newName + '/' + version, libraryValue);
                }
                File.WriteAllText(depsFile, deps.ToJsonString(s_writeOptions));
            }
        }
    }
}
