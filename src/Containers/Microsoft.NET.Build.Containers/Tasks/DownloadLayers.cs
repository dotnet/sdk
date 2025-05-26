// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.MSBuild;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Microsoft.NET.Build.Containers.Tasks;

public class DownloadLayers : Microsoft.Build.Utilities.Task, ICancelableTask
{
    private CancellationTokenSource _cts = new();

    [Required]
    public string Registry { get; set; } = string.Empty;

    [Required]
    public string Repository { get; set; } = string.Empty;

    [Required]
    public string ContentStore { get; set; } = string.Empty;

    /// <summary>
    /// should have the same data model as the output layers from <see cref="DownloadContainerManifest.Layers"/>
    /// </summary>
    [Required]
    public ITaskItem[] Layers { get; set; } = Array.Empty<ITaskItem>();

    public void Cancel() => _cts.Cancel();

    public override bool Execute() => ExecuteAsync().GetAwaiter().GetResult();

    public async Task<bool> ExecuteAsync()
    {
        using MSBuildLoggerProvider loggerProvider = new(Log);
        ILoggerFactory msbuildLoggerFactory = new LoggerFactory(new[] { loggerProvider });
        ILogger logger = msbuildLoggerFactory.CreateLogger<CreateImageIndex>();
        var store = new ContentStore(new(ContentStore));
        var registry = new Registry(Registry, logger, RegistryMode.Pull, store: store);

        var layerDownloadTasks = new List<Task>(Layers.Length);
        foreach (var layer in Layers)
        {
            var storagePath = layer.ItemSpec;
            var digest = layer.GetMetadata("Digest");
            var size = layer.GetMetadata("Size");
            var mediaType = layer.GetMetadata("MediaType");

            if (string.IsNullOrEmpty(digest) || string.IsNullOrEmpty(size) || string.IsNullOrEmpty(mediaType))
            {
                throw new ArgumentException($"Layer {layer.ItemSpec} must have Digest, Size, and MediaType metadata");
            }

            var descriptor = new Descriptor(mediaType, digest, long.Parse(size));
            layerDownloadTasks.Add(
                registry.DownloadBlobAsync(Repository, descriptor, _cts.Token).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        this.Log.LogError($"Failed to download layer {digest} from {Registry}/{Repository}: {t.Exception?.Flatten().Message}");
                    }
                    else if (t.IsCanceled)
                    {
                        this.Log.LogError($"Failed to download layer {digest} from {Registry}/{Repository}: Task was cancelled");
                    }
                })
            );
        }
        await Task.WhenAll(layerDownloadTasks);
        return true;
    }

    
}
