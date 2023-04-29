// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.Resources;
using Microsoft.NET.Build.Containers.Tasks;

namespace Microsoft.NET.Build.Containers;

public static class ContainerBuilder
{
    public static async Task<int> ContainerizeAsync(
        DirectoryInfo publishDirectory,
        string workingDir,
        string baseRegistry,
        string baseImageName,
        string baseImageTag,
        string[] entrypoint,
        string[]? entrypointArgs,
        string imageName,
        string[] imageTags,
        string?[] outputRegistries,
        Dictionary<string, string> labels,
        Port[]? exposedPorts,
        Dictionary<string, string> envVars,
        string containerRuntimeIdentifier,
        string ridGraphPath,
        string localContainerDaemon,
        string? containerUser,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!publishDirectory.Exists)
        {
            throw new ArgumentException(string.Format(Resource.GetString(nameof(Strings.PublishDirectoryDoesntExist)), nameof(publishDirectory), publishDirectory.FullName));
        }
        bool isDaemonPull = string.IsNullOrEmpty(baseRegistry);
        Registry? sourceRegistry = isDaemonPull ? null : new Registry(baseRegistry);
        ImageReference sourceImageReference = new(sourceRegistry, baseImageName, baseImageTag);

        IEnumerable<Registry?> destinationRegistries = Registry.BuildDestinationRegistries(outputRegistries);
        DestinationImageReference[] destinationImageReferences = destinationRegistries
            .Select(r => new DestinationImageReference(r, imageName, imageTags))
            .ToArray();

        ImageBuilder? imageBuilder;
        if (sourceRegistry is { } registry)
        {
            imageBuilder = await registry.GetImageManifestAsync(
                baseImageName,
                baseImageTag,
                containerRuntimeIdentifier,
                ridGraphPath,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            throw new NotSupportedException(Resource.GetString(nameof(Strings.DontKnowHowToPullImages)));
        }
        if (imageBuilder is null)
        {
            Console.WriteLine(Resource.GetString(nameof(Strings.BaseImageNotFound)), sourceImageReference.RepositoryAndTag, containerRuntimeIdentifier);
            return 1;
        }
        Console.WriteLine("Containerize: building image '{0}' with tags {1} on top of base image {2}", imageName, string.Join(",", imageName), sourceImageReference);
        cancellationToken.ThrowIfCancellationRequested();

        Layer newLayer = Layer.FromDirectory(publishDirectory.FullName, workingDir, imageBuilder.IsWindows);
        imageBuilder.AddLayer(newLayer);
        imageBuilder.SetWorkingDirectory(workingDir);
        imageBuilder.SetEntryPoint(entrypoint, entrypointArgs);
        foreach (KeyValuePair<string, string> label in labels)
        {
            // labels are validated by System.CommandLine API
            imageBuilder.AddLabel(label.Key, label.Value);
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
        if (containerUser is { } user)
        {
            imageBuilder.SetUser(user);
        }
        BuiltImage builtImage = imageBuilder.Build();
        cancellationToken.ThrowIfCancellationRequested();

        foreach (DestinationImageReference destinationImageReference in destinationImageReferences)
        {
            int result = 0;
            switch (destinationImageReference.Registry)
            {
                case { IsTarGzFile: true }:
                    result = await WriteTarGzAync(imageName,
                        builtImage, sourceImageReference, destinationImageReference,
                        cancellationToken).ConfigureAwait(false);
                    break;
                case not null:
                    result = await PushImagesToRemoteRegistryAsync(builtImage, sourceImageReference,
                        destinationImageReference,
                        cancellationToken).ConfigureAwait(false);
                    break;
                case null:
                    result = await PushImagesToLocalDaemonAsync(localContainerDaemon,
                        builtImage, sourceImageReference,
                        destinationImageReference,
                        cancellationToken).ConfigureAwait(false);
                    break;
            }

            if (result != 0)
            {
                return result;
            }
        }

        return 0;
    }

    private static async Task<int> PushImagesToLocalDaemonAsync(string localContainerDaemon, BuiltImage builtImage,
        ImageReference sourceImageReference, DestinationImageReference destinationImageReference,
        CancellationToken cancellationToken)
    {
        LocalDocker localDaemon = GetLocalDaemon(localContainerDaemon,Console.WriteLine);
        if (!(await localDaemon.IsAvailableAsync(cancellationToken).ConfigureAwait(false)))
        {
            Console.WriteLine(DiagnosticMessage.ErrorFromResourceWithCode(nameof(Strings.LocalDaemondNotAvailable)));
            return 7;
        }

        try
        {
            await localDaemon.LoadAsync(builtImage, sourceImageReference, destinationImageReference, cancellationToken).ConfigureAwait(false);
            Console.WriteLine("Containerize: Pushed container '{0}' to Docker daemon", destinationImageReference);
        }
        catch (Exception ex)
        {
            Console.WriteLine(DiagnosticMessage.ErrorFromResourceWithCode(nameof(Strings.RegistryOutputPushFailed), ex.Message));
            return 1;
        }

        return 0;
    }

    private static async Task<int> PushImagesToRemoteRegistryAsync(BuiltImage builtImage,
        ImageReference sourceImageReference, DestinationImageReference destinationImageReference,
        CancellationToken cancellationToken)
    {
        try
        {
            if (destinationImageReference.Registry is {} registry)
            {
                await (destinationImageReference.Registry.PushAsync(
                    builtImage,
                    sourceImageReference,
                    destinationImageReference,
                    message => Console.WriteLine($"Containerize: {message}"),
                    cancellationToken)).ConfigureAwait(false);
                Console.WriteLine($"Containerize: Pushed container '{destinationImageReference}' to registry '{registry.RegistryName}'");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(DiagnosticMessage.ErrorFromResourceWithCode(nameof(Strings.RegistryOutputPushFailed), e.Message));
            return 1;
        }

        return 0;
    }

    private static async Task<int> WriteTarGzAync(
        string imageName,
        BuiltImage builtImage, ImageReference sourceImageReference,
        DestinationImageReference destinationImageReference, CancellationToken cancellationToken)
    {
        string outputFile = destinationImageReference.Registry!.BaseUri.LocalPath;
        try
        {
            string? parentDirectory = Path.GetDirectoryName(Path.GetFullPath(outputFile));
            if (string.IsNullOrEmpty(parentDirectory) || !Directory.Exists(parentDirectory))
            {
                Console.WriteLine(DiagnosticMessage.ErrorFromResourceWithCode(nameof(Strings.OutputFileDirectoryDoesntExist), outputFile));
                return 7;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Containerize: {e.Message}");
            return 1;
        }

        try
        {
            await using FileStream fileStream = File.Create(outputFile);
            await LocalDocker.WriteImageToStreamAsync(builtImage, sourceImageReference, destinationImageReference,
                fileStream, cancellationToken).ConfigureAwait(false);
            Console.Write($"Containerize:Written image '{imageName}' to path '{outputFile}'");
        }
        catch (Exception e)
        {
            Console.WriteLine(DiagnosticMessage.ErrorFromResourceWithCode(nameof(Strings.RegistryOutputPushFailed), e.Message));
            return 1;
        }

        return 0;
    }

    private static LocalDocker GetLocalDaemon(string localDaemonType, Action<string> logger)
    {
        LocalDocker daemon = localDaemonType switch
        {
            KnownDaemonTypes.Docker => new LocalDocker(logger),
            _ => throw new ArgumentException(Resource.FormatString(nameof(Strings.UnknownDaemonType), localDaemonType, String.Join(",", KnownDaemonTypes.SupportedLocalDaemonTypes)), nameof(localDaemonType))
        };
        return daemon;
    }
}
