// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Logging;
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

        try
        {
            var manifestList = GenerateImageIndex();

            GeneratedImageIndex = manifestList.ToJson();

            await PushToRemoteRegistry(manifestList, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex);
        }

        return !Log.HasLoggedErrors;
    }

    private ManifestListV2 GenerateImageIndex()
    {
        var manifests = new PlatformSpecificManifest[ManifestsInfo.Length];

        for (int i = 0; i < ManifestsInfo.Length; i++)
        {
            var image = ManifestsInfo[i];

            var manifest = new PlatformSpecificManifest
            {
                mediaType = SchemaTypes.DockerManifestV2,
                size = long.Parse(image.GetMetadata("ManifestLength")),
                digest = image.GetMetadata("Digest"),
                platform = new PlatformInformation
                {
                    architecture = image.GetMetadata("Architecture"),
                    os = image.GetMetadata("OS")
                }
            };
            manifests[i] = manifest;
        }

        return new ManifestListV2
        {
            schemaVersion = 2,
            mediaType = SchemaTypes.DockerManifestListV2,
            manifests = manifests
        };
    }

    private async Task PushToRemoteRegistry(ManifestListV2 manifestList, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using MSBuildLoggerProvider loggerProvider = new(Log);
        ILoggerFactory msbuildLoggerFactory = new LoggerFactory(new[] { loggerProvider });
        ILogger logger = msbuildLoggerFactory.CreateLogger<CreateImageIndex>();
        var registry = new Registry(OutputRegistry, logger, RegistryMode.Push);

        foreach (var tag in ImageTags)
        {
            await registry.PushAsync(Repository, tag, manifestList, cancellationToken).ConfigureAwait(false);
        }
    }
}
