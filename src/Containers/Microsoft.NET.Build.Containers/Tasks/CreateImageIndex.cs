// Licensed to the .NET Foundation under one or more agreements.
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

        using MSBuildLoggerProvider loggerProvider = new(Log);
        ILoggerFactory msbuildLoggerFactory = new LoggerFactory(new[] { loggerProvider });
        ILogger logger = msbuildLoggerFactory.CreateLogger<CreateImageIndex>();

        logger.LogInformation(Strings.BuildingImageIndex, GetRepositoryAndTagsString(), string.Join(", ", images.Select(i => i.Digest)));

        try
        {
            (string imageIndex, string mediaType) = ImageIndexGenerator.GenerateImageIndex(images);

            GeneratedImageIndex = imageIndex;

            await PushToRemoteRegistry(GeneratedImageIndex, mediaType, logger, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex);
            return !Log.HasLoggedErrors;
        }

        return !Log.HasLoggedErrors;
    }

    private ImageInfo[] ParseImages()
    {
        var images = new ImageInfo[GeneratedContainers.Length];

        for (int i = 0; i < GeneratedContainers.Length; i++)
        {
            var unparsedImage = GeneratedContainers[i];

            images[i] = new ImageInfo
            {
                Config = unparsedImage.GetMetadata("Configuration"),
                Digest = unparsedImage.GetMetadata("Digest"),
                Manifest = unparsedImage.GetMetadata("Manifest"),
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
