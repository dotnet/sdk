// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.Build.Tasks;

/// <summary>
/// Extracts version numbers and saves them into a metadata file.
/// </summary>
public sealed class ProcessRuntimeAnalyzerVersions : Task
{
    [Required]
    public ITaskItem[]? Inputs { get; set; }

    [Required]
    public string? MetadataFilePath { get; set; }

    [Output]
    public ITaskItem[]? Outputs { get; set; }

    public override bool Execute()
    {
        var metadata = new Dictionary<string, string>();

        // Extract version from a path like:
        //   ...\packs\Microsoft.NetCore.App.Ref\<version>\analyzers\**\*.*
        // The version segment is always the first segment in %(RecursiveDir).
        foreach (var input in Inputs ?? [])
        {
            var deploymentSubpath = input.GetMetadata("DeploymentSubpath");
            var recursiveDir = input.GetMetadata("CustomRecursiveDir");

            var slashIndex = recursiveDir.IndexOfAny('/', '\\');
            var version = recursiveDir.Substring(0, slashIndex);
            var rest = recursiveDir.Substring(slashIndex + 1);

            input.SetMetadata("CustomRecursiveDir", rest);

            if (!metadata.TryGetValue(deploymentSubpath, out var existingVersion))
            {
                metadata.Add(deploymentSubpath, version);
            }
            else if (existingVersion != version)
            {
                Log.LogError($"Version mismatch for '{deploymentSubpath}': '{existingVersion}' != '{version}'. " +
                    $"Expected only one version of '{input.GetMetadata("Identity")}'.");
                return false;
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(MetadataFilePath)!);
        File.WriteAllText(path: MetadataFilePath!, JsonSerializer.Serialize(metadata));

        Outputs = Inputs;
        return true;
    }
}
