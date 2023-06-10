// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Containers.Tasks;

public sealed class MapRidToDockerPlatform : Microsoft.Build.Utilities.Task
{
    [Required]
    public ITaskItem[] RuntimeIdentifiers { get; set; }

    [Output]
    public ITaskItem[] ModifiedRuntimeIdentifiers { get; set; }

    public MapRidToDockerPlatform()
    {
        RuntimeIdentifiers = Array.Empty<ITaskItem>();
        ModifiedRuntimeIdentifiers = Array.Empty<ITaskItem>();
    }
    public override bool Execute()
    {
        bool result = true;
        var modifiedRuntimeIdentifiers = new List<ITaskItem>(RuntimeIdentifiers.Length);
        foreach (ITaskItem rid in RuntimeIdentifiers) {
            if (PlatformMapping.TryGetDockerImageTagForRid(rid.ItemSpec, out string? dockerPlatform))
            {
                rid.SetMetadata("DockerPlatformTag", dockerPlatform);
                modifiedRuntimeIdentifiers.Add(rid);
            }
            else {
                Log.LogError($"No Docker platform mapping found for RuntimeIdentifier '{rid.ItemSpec}'");
                result = false;
            }
        }
        ModifiedRuntimeIdentifiers = modifiedRuntimeIdentifiers.ToArray();
        return result;
    }
}
