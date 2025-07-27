// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers.Tasks;

public sealed partial class CreateAppLayerTask : Microsoft.Build.Utilities.ToolTask, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    // Unused, ToolExe is set via targets and overrides this.
    protected override string ToolName => "dotnet";
    public void Dispose() => _cancellationTokenSource.Dispose();

    private StringBuilder stdout = new();

    public override bool Execute()
    {

        base.Execute();
        if (Log.HasLoggedErrors)
        {
            return false;
        }
        // if that succeeds, we should have a new layer created at the GeneratedLayerPath
        // stdout should contain a JSON serialization of a Descriptor (but we don't have that data structure on the net472 build)
        var jsonObject = JsonSerializer.Deserialize<Dictionary<string, string>>(stdout.ToString());
        GeneratedAppContainerLayer = new Microsoft.Build.Utilities.TaskItem(GeneratedLayerPath, new Dictionary<string, string>(3)
        {
            ["Size"] = jsonObject["size"],
            ["MediaType"] = jsonObject["mediaType"],
            ["Digest"] = jsonObject["digest"],
        });
        return true;
    }

    protected override Process StartToolProcess(Process proc)
    {
        // need to get stdout so we can interpret the response, which should be a json serialization of a Descriptor
        proc.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                stdout.AppendLine(e.Data);
            }
        };
        return base.StartToolProcess(proc);
    }

    private string DotNetPath
    {
        get
        {
            // DOTNET_HOST_PATH, if set, is the full path to the dotnet host executable.
            // However, not all environments/scenarios set it correctly - some set it to just the directory.

            // this is the expected correct case - DOTNET_HOST_PATH is set to the full path of the dotnet host executable
            string path = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "";
            if (Path.IsPathRooted(path) && File.Exists(path))
            {
                return path;
            }
            // some environments set it to just the directory, so we need to check that too
            if (Path.IsPathRooted(path) && Directory.Exists(path))
            {
                path = Path.Combine(path, ToolExe);
                if (File.Exists(path))
                {
                    return path;
                }
            }
            // last-chance fallback - use the ToolPath and ToolExe properties to try to synthesize the path
            if (string.IsNullOrEmpty(path))
            {
                // no
                path = string.IsNullOrEmpty(ToolPath) ? "" : ToolPath;
                path = Path.Combine(path, ToolExe);
            }

            return path;
        }
    }

    protected override string GenerateFullPathToTool() => DotNetPath;

    override protected string GenerateResponseFileCommands() => GenerateCommandLineCommandsInternal();

    internal string GenerateCommandLineCommandsInternal()
    {
        Microsoft.Build.Utilities.CommandLineBuilder builder = new();
        // we want to build a a command line for a command that takes
        // * --container-root-dir <dir>
        // * --target-runtime-identifier <rid>
        // * --layer-media-type <mediaType>
        // * --content-store-root <dir>
        // * --generated-layer-path <path>
        // * --publish-files <file1> <file2> ...
        // * --relative-paths <relativePath1> <relativePath2> ...
        // and for each of publish files and relative paths, the matching indexes will be paired together.
        builder.AppendSwitchIfNotNull("--container-root-dir", ContainerRootDirectory);
        builder.AppendSwitchIfNotNull("--target-runtime-identifier", TargetRuntimeIdentifier);
        builder.AppendSwitchIfNotNull("--layer-media-type", LayerMediaType);
        builder.AppendSwitchIfNotNull("--content-store-root", ContentStoreRoot);
        builder.AppendSwitchIfNotNull("--generated-layer-path", GeneratedLayerPath);
        builder.AppendSwitchIfNotNull("--container-user", ContainerUser);
        if (PublishFiles.Length > 0)
        {
            var publishFilePaths = new List<(string, string)>(PublishFiles.Length);
            foreach (ITaskItem file in PublishFiles)
            {
                if (file.GetMetadata("RelativePath") is string relativePath && !string.IsNullOrWhiteSpace(relativePath) && !string.IsNullOrWhiteSpace(file.ItemSpec))
                {
                    publishFilePaths.Add((file.ItemSpec, relativePath));
                }
            }
            if (publishFilePaths.Count > 0)
            {
                foreach (var (filePath, relativePath) in publishFilePaths)
                {
                    builder.AppendSwitch("--input-file");
                    builder.AppendFileNamesIfNotNull([filePath, relativePath], "=");
                }
            }
        }
        return builder.ToString();
    }
}
