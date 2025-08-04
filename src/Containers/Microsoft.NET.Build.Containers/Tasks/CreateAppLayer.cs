// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Mime;
using Microsoft.Build.Framework;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers.Tasks;

public sealed partial class CreateAppLayer : Microsoft.Build.Utilities.Task, ICancelableTask, IDisposable
{
    /// <summary>
    /// Unused. For interface parity with the ToolTask implementation of the task.
    /// </summary>
    public string ToolExe { get; set; } = null!;

    /// <summary>
    /// Unused. For interface parity with the ToolTask implementation of the task.
    /// </summary>
    public string ToolPath { get; set; } = null!;

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
        var isWindowsLayer = TargetRuntimeIdentifier.StartsWith("win", StringComparison.OrdinalIgnoreCase);
        var userId = isWindowsLayer ? null : ContainerBuilder.TryParseUserId(ContainerUser);
        if (Enum.TryParse(ParentImageFormat, out KnownImageFormats format))
        {
        }
        else
        {
            format = ParentImageFormat switch
            {
                SchemaTypes.DockerManifestV2 => KnownImageFormats.Docker,
                SchemaTypes.OciManifestV1 => KnownImageFormats.OCI,
                _ => throw new ArgumentException(string.Format(Strings.UnrecognizedMediaType, ParentImageFormat)),
            };
        }
        string manifestMediaType = format switch
        {
            KnownImageFormats.Docker => SchemaTypes.DockerManifestV2,
            KnownImageFormats.OCI => SchemaTypes.OciManifestV1,
            _ => throw new ArgumentException(string.Format(Strings.UnrecognizedMediaType, ParentImageFormat)),
        };
        Layer newLayer = await Layer.FromFiles(filesWithRelativePaths, ContainerRootDirectory, isWindowsLayer, manifestMediaType, new(new(ContentStoreRoot)), new(GeneratedLayerPath), cancellationToken, userId: userId);
        GeneratedAppContainerLayer = new Microsoft.Build.Utilities.TaskItem(GeneratedLayerPath, new Dictionary<string, string>(3)
        {
            ["Size"] = newLayer.Descriptor.Size.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["MediaType"] = newLayer.Descriptor.MediaType,
            ["Digest"] = newLayer.Descriptor.Digest.ToString(),
        });
        return true;
    }

}
