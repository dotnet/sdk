// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers.Tasks;

public sealed partial class CreateAppLayerTask : Microsoft.Build.Utilities.Task, ICancelableTask, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public void Cancel() => _cancellationTokenSource.Cancel();

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