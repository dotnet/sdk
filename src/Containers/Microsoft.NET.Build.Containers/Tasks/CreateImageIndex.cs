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
using Task = System.Threading.Tasks.Task;

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

        var firstManifestMediaType = GetFirstManifestMediaType();
        if (firstManifestMediaType == null)
        {
            Log.LogErrorWithCodeFromResources(nameof(Strings.UnsupportedMediaType), "");
            return !Log.HasLoggedErrors;
        }

        cancellationToken.ThrowIfCancellationRequested();

        using MSBuildLoggerProvider loggerProvider = new(Log);
        ILoggerFactory msbuildLoggerFactory = new LoggerFactory(new[] { loggerProvider });
        ILogger logger = msbuildLoggerFactory.CreateLogger<CreateImageIndex>();

        string imageindexMediaType;      
        if (firstManifestMediaType ==  SchemaTypes.DockerManifestV2)
        {
            GenerateDockerManifestList(firstManifestMediaType, logger);
            if (Log.HasLoggedErrors)
            {
                return !Log.HasLoggedErrors;
            }
            imageindexMediaType = SchemaTypes.DockerManifestListV2;
        }
        else if (firstManifestMediaType == SchemaTypes.OciManifestV1)
        {
            GenerateOciImageIndex(firstManifestMediaType, logger);
            if (Log.HasLoggedErrors)
            {
                return !Log.HasLoggedErrors;
            }
            imageindexMediaType = SchemaTypes.OciImageIndexV1;
        }
        else
        {
            Log.LogErrorWithCodeFromResources(nameof(Strings.UnsupportedMediaType), firstManifestMediaType);
            return !Log.HasLoggedErrors;
        }

        await PushToRemoteRegistry(GeneratedImageIndex, imageindexMediaType, logger, cancellationToken);

        return !Log.HasLoggedErrors;

    }

    private string? GetFirstManifestMediaType()
    {
        var generatedManifestStr = GeneratedContainers[0].GetMetadata("Manifest");
        var generatedManifest = generatedManifestStr.FromJson<ManifestV2>();
        return generatedManifest.MediaType;
    }

    private void GenerateDockerManifestList(string firstManifestMediaType, ILogger logger)
    {
        var manifests = new PlatformSpecificManifest[GeneratedContainers.Length];
        for (int i = 0; i < GeneratedContainers.Length; i++)
        {
            var image = GeneratedContainers[i];

            var generatedManifestStr = image.GetMetadata("Manifest");
            var generatedConfig = new ImageConfig(image.GetMetadata("Configuration"));

            if (i > 0 && generatedManifestStr.FromJson<ManifestV2>().MediaType != firstManifestMediaType)
            {
                Log.LogErrorWithCodeFromResources(nameof(Strings.MixedMediaTypes));
                return;
            }

            var manifest = new PlatformSpecificManifest
            {
                mediaType = firstManifestMediaType,
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

        var dockerManifestList = new ManifestListV2
        {
            schemaVersion = 2,
            mediaType = SchemaTypes.DockerManifestListV2,
            manifests = manifests
        };
        GeneratedImageIndex = JsonSerializer.SerializeToNode(dockerManifestList)?.ToJsonString() ?? "";
    }

    private void GenerateOciImageIndex(string firstManifestMediaType, ILogger logger)
    {
        var manifests = new PlatformSpecificOciManifest[GeneratedContainers.Length];
        for (int i = 0; i < GeneratedContainers.Length; i++)
        {
            var image = GeneratedContainers[i];

            var generatedManifestStr = image.GetMetadata("Manifest");
            var generatedManifest = generatedManifestStr.FromJson<ManifestV2>();
            var generatedConfig = new ImageConfig(image.GetMetadata("Configuration"));

            if (i > 0 && generatedManifestStr.FromJson<ManifestV2>().MediaType != firstManifestMediaType)
            {
                Log.LogErrorWithCodeFromResources(nameof(Strings.MixedMediaTypes));
                return;
            }

            var manifest = new PlatformSpecificOciManifest
            {
                mediaType = firstManifestMediaType,
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

        var ociImageIndex = new ImageIndexV1
        {
            schemaVersion = 2,
            mediaType = SchemaTypes.OciImageIndexV1,
            manifests = manifests
        };
        GeneratedImageIndex = JsonSerializer.SerializeToNode(ociImageIndex)?.ToJsonString() ?? "";
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
