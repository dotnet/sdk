// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers.Tasks;

public sealed partial class CreateAppLayerTask : Microsoft.Build.Utilities.ToolTask, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    // Unused, ToolExe is set via targets and overrides this.
    protected override string ToolName => "dotnet";
    public void Dispose() => _cancellationTokenSource.Dispose();

    public override bool Execute()
    {
        try
        {
            Task.Run(() => ExecuteAsync(_cancellationTokenSource.Token)).GetAwaiter().GetResult();
        }
        catch (TaskCanceledException ex)
        {
            Log.LogWarningFromException(ex);
        }
        catch (OperationCanceledException ex)
        {
            Log.LogWarningFromException(ex);
        }
        return !Log.HasLoggedErrors;
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

    override protected string GenerateCommandLineCommands() => GenerateCommandLineCommandsInternal();
    internal string GenerateCommandLineCommandsInternal()
    {
        Microsoft.Build.Utilities.CommandLineBuilder builder = new();
        return builder.ToString();
    }

    private async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        (string absolutefilePath, string relativeContainerPath)[] filesWithRelativePaths =
            PublishFiles
            .Select(f => (f.ItemSpec, f.GetMetadata("RelativePath")))
            .Where(x => !string.IsNullOrWhiteSpace(x.ItemSpec) && !string.IsNullOrWhiteSpace(x.Item2))
            .ToArray();
        Layer newLayer = await Layer.FromFiles(filesWithRelativePaths, ContainerRootDirectory, TargetRuntimeIdentifier.StartsWith("win"), LayerMediaType, new(new(ContentStoreRoot)), new(GeneratedLayerPath), cancellationToken);
        GeneratedAppContainerLayer = new Microsoft.Build.Utilities.TaskItem(GeneratedLayerPath, new Dictionary<string, string>(3)
        {
            ["Size"] = newLayer.Descriptor.Size.ToString(),
            ["MediaType"] = newLayer.Descriptor.MediaType,
            ["Digest"] = newLayer.Descriptor.Digest,
        });
        return true;
    }
        
}