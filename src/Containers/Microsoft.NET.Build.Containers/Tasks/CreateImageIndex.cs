﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Logging;
using Microsoft.NET.Build.Containers.Resources;
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

        var images = ParseImages();
        if (Log.HasLoggedErrors)
        {
            return !Log.HasLoggedErrors;
        }

        using MSBuildLoggerProvider loggerProvider = new(Log);
        ILoggerFactory msbuildLoggerFactory = new LoggerFactory(new[] { loggerProvider });
        ILogger logger = msbuildLoggerFactory.CreateLogger<CreateImageIndex>();

        logger.LogInformation(Strings.BuildingImageIndex, GetRepositoryAndTagsString(), string.Join(", ", images.Select(i => i.ManifestDigest)));

        try
        {
            (string imageIndex, string mediaType) = ImageIndexGenerator.GenerateImageIndex(images);

            GeneratedImageIndex = imageIndex;

            await PushToRemoteRegistry(GeneratedImageIndex, mediaType, logger, cancellationToken);
        }
        catch (ContainerHttpException e)
        {
            if (BuildEngine != null)
            {
                Log.LogErrorFromException(e, true);
            }
        }
        catch (ArgumentException ex)
        {
            Log.LogErrorFromException(ex);
        }

        return !Log.HasLoggedErrors;
    }

    private ImageInfo[] ParseImages()
    {
        var images = new ImageInfo[GeneratedContainers.Length];

        for (int i = 0; i < GeneratedContainers.Length; i++)
        {
            var unparsedImage = GeneratedContainers[i];

            string config = unparsedImage.GetMetadata("Configuration");
            string manifestDigest = unparsedImage.GetMetadata("ManifestDigest");
            string manifest = unparsedImage.GetMetadata("Manifest");
            string manifestMediaType = unparsedImage.GetMetadata("ManifestMediaType");

            if (string.IsNullOrEmpty(config) || string.IsNullOrEmpty(manifestDigest) || string.IsNullOrEmpty(manifest))
            {
                Log.LogError(Strings.InvalidImageMetadata, unparsedImage.ItemSpec);
                break;
            }

            images[i] = new ImageInfo
            {
                Config = config,
                ManifestDigest = manifestDigest,
                Manifest = manifest,
                ManifestMediaType = manifestMediaType
            };
        }

        return images;
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