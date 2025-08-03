// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

internal enum KnownImageFormats
{
    OCI,
    Docker
}

internal static class ContainerBuilder
{
    internal static async Task<int> ContainerizeAsync(
        (string absolutePath, string relativePath)[] inputFiles,
        string workingDir,
        string baseRegistry,
        string baseImageName,
        string baseImageTag,
        string? baseImageDigest,
        string[] entrypoint,
        string[] entrypointArgs,
        string[] defaultArgs,
        string[] appCommand,
        string[] appCommandArgs,
        string appCommandInstruction,
        string imageName,
        string[] imageTags,
        string? outputRegistry,
        Dictionary<string, string> labels,
        Port[]? exposedPorts,
        Dictionary<string, string> envVars,
        string localRegistry,
        string? containerUser,
        string? archiveOutputPath,
        bool generateLabels,
        bool generateDigestLabel,
        KnownImageFormats? imageFormat,
        string contentStoreRoot,
        FileInfo baseImageManifestPath,
        FileInfo baseImageConfigPath,
        FileInfo generatedConfigPath,
        FileInfo generatedManifestPath,
        FileInfo generatedLayerPath,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ILogger logger = loggerFactory.CreateLogger("Containerize");
        logger.LogTrace("Trace logging: enabled.");

        bool isLocalPull = string.IsNullOrEmpty(baseRegistry);
        RegistryMode sourceRegistryMode = baseRegistry.Equals(outputRegistry, StringComparison.InvariantCultureIgnoreCase) ? RegistryMode.PullFromOutput : RegistryMode.Pull;
        Registry? sourceRegistry = isLocalPull ? null : new Registry(baseRegistry, logger, sourceRegistryMode);
        SourceImageReference sourceImageReference = new(sourceRegistry, baseImageName, baseImageTag, baseImageDigest);

        DestinationImageReference destinationImageReference = DestinationImageReference.CreateFromSettings(
            imageName,
            imageTags,
            loggerFactory,
            archiveOutputPath,
            outputRegistry,
            localRegistry);

        ImageBuilder? imageBuilder;
        if (sourceRegistry is { } registry)
        {
            try
            {
                imageBuilder = await LoadFromManifestAndConfig(baseImageManifestPath.FullName, imageFormat, baseImageConfigPath.FullName, logger, cancellationToken);
            }
            catch (RepositoryNotFoundException)
            {
                logger.LogError(Resource.FormatString(nameof(Strings.RepositoryNotFound), baseImageName, baseImageTag, baseImageDigest, registry.RegistryName));
                return 1;
            }
            catch (UnableToAccessRepositoryException)
            {
                logger.LogError(Resource.FormatString(nameof(Strings.UnableToAccessRepository), baseImageName, registry.RegistryName));
                return 1;
            }
            catch (ContainerHttpException e)
            {
                logger.LogError(e.Message);
                return 1;
            }
        }
        else
        {
            throw new NotSupportedException(Resource.GetString(nameof(Strings.ImagePullNotSupported)));
        }
        logger.LogInformation(Strings.ContainerBuilder_StartBuildingImage, imageName, string.Join(",", imageName), sourceImageReference);
        cancellationToken.ThrowIfCancellationRequested();

        // forcibly change the media type if required
        imageBuilder.ManifestMediaType = imageFormat switch
        {
            null => imageBuilder.ManifestMediaType,
            KnownImageFormats.Docker => SchemaTypes.DockerManifestV2,
            KnownImageFormats.OCI => SchemaTypes.OciManifestV1,
            _ => imageBuilder.ManifestMediaType // should be impossible unless we add to the enum
        };

        var storePath = new DirectoryInfo(contentStoreRoot);
        if (!storePath.Exists)
        {
            throw new ArgumentException($"The content store path '{contentStoreRoot}' does not exist.");
        }
        var store = new ContentStore(storePath);

        var userId = imageBuilder.IsWindows ? null : TryParseUserId(containerUser);
        Layer newLayer = await Layer.FromFiles(inputFiles, workingDir, imageBuilder.IsWindows, imageBuilder.ManifestMediaType, store, generatedLayerPath, cancellationToken, userId: userId);
        imageBuilder.AddLayer(newLayer);
        imageBuilder.SetWorkingDirectory(workingDir);

        bool hasErrors = false;
        (string[] imageEntrypoint, string[] imageCmd) = ImageBuilder.DetermineEntrypointAndCmd(entrypoint, entrypointArgs, defaultArgs, appCommand, appCommandArgs, appCommandInstruction,
            baseImageEntrypoint: imageBuilder.BaseImageConfig.Entrypoint,
            logWarning: s =>
            {
                logger.LogWarning(Resource.GetString(nameof(s)));
            },
            logError: (s, a) =>
            {
                hasErrors = true;
                if (a is null)
                {
                    logger.LogError(Resource.GetString(nameof(s)));
                }
                else
                {
                    logger.LogError(Resource.FormatString(nameof(s), a));
                }
            });
        if (hasErrors)
        {
            return 1;
        }
        imageBuilder.SetEntrypointAndCmd(imageEntrypoint, imageCmd);

        if (generateLabels)
        {
            foreach (KeyValuePair<string, string> label in labels)
            {
                // labels are validated by System.CommandLine API
                imageBuilder.AddLabel(label.Key, label.Value);
            }

            if (generateDigestLabel)
            {
                imageBuilder.AddBaseImageDigestLabel();
            }
        }

        foreach (KeyValuePair<string, string> envVar in envVars)
        {
            imageBuilder.AddEnvironmentVariable(envVar.Key, envVar.Value);
        }
        foreach ((int number, PortType type) in exposedPorts ?? Array.Empty<Port>())
        {
            // ports are validated by System.CommandLine API
            imageBuilder.ExposePort(number, type);
        }
        if (containerUser is { Length: > 0 } user)
        {
            imageBuilder.SetUser(user);
        }
        BuiltImage builtImage = imageBuilder.Build();
        cancellationToken.ThrowIfCancellationRequested();

        // at this point we're done with modifications and are just pushing the data other places
        var serializedManifest = Json.Serialize(builtImage.Manifest);
        var manifestWriteTask = File.WriteAllTextAsync(generatedManifestPath.FullName, serializedManifest, DigestAlgorithmExtensions.UTF8NoBom);

        var serializedConfig = Json.Serialize(builtImage.Image);
        var configWriteTask = File.WriteAllTextAsync(generatedConfigPath.FullName, serializedConfig, DigestAlgorithmExtensions.UTF8NoBom);

        await Task.WhenAll(manifestWriteTask, configWriteTask).ConfigureAwait(false);

        int exitCode;
        switch (destinationImageReference.Kind)
        {
            case DestinationImageReferenceKind.LocalRegistry:
                exitCode = await PushToLocalRegistryAsync(
                    logger,
                    builtImage,
                    sourceImageReference,
                    destinationImageReference,
                    cancellationToken).ConfigureAwait(false);
                break;
            case DestinationImageReferenceKind.RemoteRegistry:
                exitCode = await PushToRemoteRegistryAsync(
                    logger,
                    builtImage,
                    sourceImageReference,
                    destinationImageReference,
                    cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return exitCode;
    }

    public static int? TryParseUserId(string? containerUser)
    {
        if (containerUser is null)
        {
            return null;
        }
        if (int.TryParse(containerUser, out int userId))
        {
            return userId;
        }
        if (containerUser.Equals("root", StringComparison.OrdinalIgnoreCase))
        {
            return 0; // root user
        }
        // TODO: on Linux we could _potentially_ try to map the user name to a UID
        return null;
    }

    private static async Task<int> PushToLocalRegistryAsync(ILogger logger, BuiltImage builtImage, SourceImageReference sourceImageReference,
        DestinationImageReference destinationImageReference,
        CancellationToken cancellationToken)
    {
        ILocalRegistry containerRegistry = destinationImageReference.LocalRegistry!;
        if (!(await containerRegistry.IsAvailableAsync(cancellationToken).ConfigureAwait(false)))
        {
            logger.LogError(Resource.FormatString(nameof(Strings.LocalRegistryNotAvailable)));
            return 7;
        }

        try
        {
            await containerRegistry.LoadAsync(builtImage, sourceImageReference, destinationImageReference, cancellationToken).ConfigureAwait(false);
            logger.LogInformation(Strings.ContainerBuilder_ImageUploadedToLocalDaemon, destinationImageReference, containerRegistry);
        }
        catch (UnableToDownloadFromRepositoryException)
        {
            logger.LogError(Resource.FormatString(nameof(Strings.UnableToDownloadFromRepository)), sourceImageReference);
            return 1;
        }
        catch (Exception ex)
        {
            logger.LogError(Resource.FormatString(nameof(Strings.RegistryOutputPushFailed), ex.Message));
            return 1;
        }

        return 0;
    }

    private static async Task<int> PushToRemoteRegistryAsync(ILogger logger, BuiltImage builtImage, SourceImageReference sourceImageReference,
        DestinationImageReference destinationImageReference,
        CancellationToken cancellationToken)
    {
        try
        {
            await (destinationImageReference.RemoteRegistry!.PushAsync(
                builtImage,
                sourceImageReference,
                destinationImageReference,
                cancellationToken)).ConfigureAwait(false);
            logger.LogInformation(Strings.ContainerBuilder_ImageUploadedToRegistry, destinationImageReference, destinationImageReference.RemoteRegistry.RegistryName);
        }
        catch (UnableToDownloadFromRepositoryException)
        {
            logger.LogError(Resource.FormatString(nameof(Strings.UnableToDownloadFromRepository)), sourceImageReference);
            return 1;
        }
        catch (Exception e)
        {
            logger.LogError(Resource.FormatString(nameof(Strings.RegistryOutputPushFailed), e.Message));
            return 1;
        }

        return 0;
    }

    public static async Task<ImageBuilder> LoadFromManifestAndConfig(string manifestPath, KnownImageFormats? desiredImageFormat, string configPath, ILogger logger, CancellationToken cancellationToken)
    {
        var baseImageManifest = await Json.DeserializeAsync<ManifestV2>(File.OpenRead(manifestPath), cancellationToken: cancellationToken);
        var baseImageConfig = await Json.DeserializeAsync<Image>(File.OpenRead(configPath), cancellationToken: cancellationToken);
        if (baseImageConfig is null || baseImageManifest is null) throw new ArgumentException($"Expected to load manifest from {manifestPath} and config from {configPath}");
        // forcibly change the media type if required from that of the base image
        var mediaType = baseImageManifest.MediaType;
        if (desiredImageFormat is not null)
        {
            mediaType = desiredImageFormat switch
            {
                KnownImageFormats.Docker => SchemaTypes.DockerManifestV2,
                KnownImageFormats.OCI => SchemaTypes.OciManifestV1,
                _ => mediaType // should be impossible unless we add to the enum
            };
        }
        return new ImageBuilder(baseImageManifest, mediaType!, baseImageConfig, logger);
    }
}
