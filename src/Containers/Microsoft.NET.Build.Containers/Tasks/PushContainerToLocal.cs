// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.MSBuild;
using Microsoft.NET.Build.Containers.Resources;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Microsoft.NET.Build.Containers.Tasks;

public class PushContainerToLocal : Microsoft.Build.Utilities.Task, ICancelableTask
{
    private CancellationTokenSource _cts = new();

    public string? LocalRegistry { get; set; }

    [Required]
    public string Repository { get; set; } = string.Empty;

    [Required]
    public string[] Tags { get; set; } = [];

    [Required]
    public ITaskItem Manifest { get; set; } = null!;

    [Required]
    public ITaskItem Configuration { get; set; } = null!;

    [Required]
    public ITaskItem[] Layers { get; set; } = [];


    public void Cancel() => _cts.Cancel();

    public override bool Execute() => ExecuteAsync().GetAwaiter().GetResult();

    public async Task<bool> ExecuteAsync()
    {
        using MSBuildLoggerProvider loggerProvider = new(Log);
        ILoggerFactory msbuildLoggerFactory = new LoggerFactory([loggerProvider]);
        ILogger logger = msbuildLoggerFactory.CreateLogger<CreateImageIndex>();
        (long manifestSize, string manifestDigest, ManifestV2 manifestStructure) = await ReadManifest();
        var configDigest = manifestStructure.Config.Digest;
        var config = await Json.DeserializeAsync<JsonObject>(File.OpenRead(Configuration.ItemSpec), cancellationToken: _cts.Token);
        var containerCli = new DockerCli(LocalRegistry, msbuildLoggerFactory);

        var telemetry = new Telemetry(new(null, null, null, containerCli.IsDocker ? Telemetry.LocalStorageType.Docker : Telemetry.LocalStorageType.Podman), Log);
        if (!await containerCli.IsAvailableAsync(_cts.Token).ConfigureAwait(false))
        {
            telemetry.LogMissingLocalBinary();
            Log.LogErrorWithCodeFromResources(nameof(Strings.LocalRegistryNotAvailable));
            return false;
        }

        var layers = Layers.Select(l => Layer.FromBackingFile(new(l.ItemSpec), GetDescriptor(l))).ToArray();
        try
        {
            await containerCli.LoadAsync((Repository, Tags, configDigest, config!, layers), DockerCli.WriteDockerImageToStreamAsync, _cts.Token);
        }
        catch (AggregateException ex) when (ex.InnerException is DockerLoadException dle)
        {
            telemetry.LogLocalLoadError();
            Log.LogErrorFromException(dle, showStackTrace: false);
        }
        catch (ArgumentException argEx)
        {
            Log.LogErrorFromException(argEx, showStackTrace: false);
        }
        catch (DockerLoadException dle)
        {
            telemetry.LogLocalLoadError();
            Log.LogErrorFromException(dle, showStackTrace: false);
        }
        return true;
    }

    private Descriptor GetDescriptor(ITaskItem item)
    {
        var mediaType = item.GetMetadata("MediaType");
        var digest = Digest.Parse(item.GetMetadata("Digest"));
        var size = long.Parse(item.GetMetadata("Size")!);
        return new Descriptor
        {
            MediaType = mediaType,
            Digest = digest,
            Size = size
        };
    }

    private async Task<(long size, string digest, ManifestV2 manifest)> ReadManifest()
    {
        var size = long.Parse(Manifest.GetMetadata("Size")!);
        var digest = Manifest.GetMetadata("Digest")!;
        var manifestStructure = await Json.DeserializeAsync<ManifestV2>(File.OpenRead(Manifest.ItemSpec), cancellationToken: _cts.Token);
        return (size, digest, manifestStructure!);
    }
}
