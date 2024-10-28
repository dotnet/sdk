// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Logging;
using Microsoft.NET.Build.Containers.Resources;
using NuGet.Protocol;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Microsoft.NET.Build.Containers.Tasks;

public sealed partial class CreateImageIndex : Microsoft.Build.Utilities.Task, ICancelableTask, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public void Cancel() => _cancellationTokenSource.Cancel();

    public void Dispose()
    {
        _cancellationTokenSource.Dispose();
    }

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

    internal async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (GeneratedContainers.Length == 0)
        {
            Log.LogErrorWithCodeFromResources(nameof(Strings.GeneratedContainersEmpty));
            return !Log.HasLoggedErrors;
        }

        using MSBuildLoggerProvider loggerProvider = new(Log);
        ILoggerFactory msbuildLoggerFactory = new LoggerFactory(new[] { loggerProvider });
        ILogger logger = msbuildLoggerFactory.CreateLogger<CreateImageIndex>();

        var manifests = new PlatformSpecificManifest[GeneratedContainers.Length];
        string? manifestMediaType = null;

        for (int i = 0; i < GeneratedContainers.Length; i++)
        {
            var image = GeneratedContainers[i];

            var generatedManifestStr = image.GetMetadata("Manifest");
            var generatedManifest = generatedManifestStr.FromJson<ManifestV2>();
            var generatedConfig = new ImageConfig(image.GetMetadata("Configuration"));

            if (manifestMediaType == null)
            {
                manifestMediaType = generatedManifest.MediaType!;
            }
            else if (generatedManifest.MediaType != manifestMediaType)
            {
                Log.LogErrorWithCodeFromResources(nameof(Strings.MixedMediaTypes));
                return !Log.HasLoggedErrors;
            }

            var manifest = new PlatformSpecificManifest
            {
                mediaType = manifestMediaType,
                size = generatedManifestStr.Length,
                digest = image.GetMetadata("Digest"),
                platform = new PlatformInformation
                {
                    architecture = generatedConfig.Architecture,
                    os = generatedConfig.OS
                }
            };
            manifests[i] = manifest;
        }

        logger.LogInformation(Strings.BuildingImageIndex, GetRepositoryAndTagsString(), manifests.ToJson());

        string imageindexMediaType;
        if (manifestMediaType == SchemaTypes.DockerManifestV2)
        {
            var dockerManifestList = new ManifestListV2
            {
                schemaVersion = 2,
                mediaType = SchemaTypes.DockerManifestListV2,
                manifests = manifests
            };
            GeneratedImageIndex = JsonSerializer.SerializeToNode(dockerManifestList)?.ToJsonString() ?? "";
            imageindexMediaType = dockerManifestList.mediaType;
        }
        else if (manifestMediaType == SchemaTypes.OciManifestV1)
        {
            var ociImageIndex = new ImageIndexV1
            {
                schemaVersion = 2,
                mediaType = SchemaTypes.OciImageIndexV1,
                manifests = manifests
            };
            GeneratedImageIndex = JsonSerializer.SerializeToNode(ociImageIndex)?.ToJsonString() ?? "";
            imageindexMediaType = ociImageIndex.mediaType;
        }
        else
        {
            Log.LogErrorWithCodeFromResources(nameof(Strings.UnsupportedMediaType), manifestMediaType);
            return !Log.HasLoggedErrors;
        }

        await PushToRemoteRegistry(GeneratedImageIndex, imageindexMediaType, logger, cancellationToken);

        return !Log.HasLoggedErrors;
    }

    private async Task PushToRemoteRegistry(string manifestList, string mediaType, ILogger logger, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Debug.Assert(ImageTags.Length > 0);
        var registry = new Registry(OutputRegistry, logger, RegistryMode.Push);   
        await registry.PushManifestListAsync(Repository, ImageTags, manifestList, mediaType, cancellationToken).ConfigureAwait(false);
        logger.LogInformation(Strings.ImageIndexUploadedToRegistry, GetRepositoryAndTagsString(), OutputRegistry);
    }

    private string? _repositoryAndTagsString = null;

    private string GetRepositoryAndTagsString()
    {
        _repositoryAndTagsString ??= $"{Repository}:{string.Join(", ", ImageTags)}";
        return _repositoryAndTagsString;
    }
}
