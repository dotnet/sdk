// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers.Tasks;

public sealed partial class CreateNewImage : Microsoft.Build.Utilities.Task, ICancelableTask, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    /// <summary>
    /// Unused. For interface parity with the ToolTask implementation of the task.
    /// </summary>
    public string ToolExe { get; set; }

    /// <summary>
    /// Unused. For interface parity with the ToolTask implementation of the task.
    /// </summary>
    public string ToolPath { get; set; }

    private bool IsDaemonPull => string.IsNullOrEmpty(BaseRegistry);

    public void Cancel() => _cancellationTokenSource.Cancel();

    public override bool Execute()
    {
        return Task.Run(() => ExecuteAsync(_cancellationTokenSource.Token)).GetAwaiter().GetResult();
    }

    internal async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(PublishDirectory))
        {
            Log.LogErrorWithCodeFromResources(nameof(Strings.PublishDirectoryDoesntExist), nameof(PublishDirectory),
                PublishDirectory);
            return !Log.HasLoggedErrors;
        }

        ImageReference sourceImageReference = new(SourceRegistry.Value, BaseImageName, BaseImageTag);

        IEnumerable<Registry?> destinationRegistries = Registry.BuildDestinationRegistries(OutputRegistries);
        DestinationImageReference[] destinationImageReferences = destinationRegistries
                .Select(r =>new DestinationImageReference(r, ImageName, ImageTags))
                .ToArray();

        ImageBuilder? imageBuilder;
        if (SourceRegistry.Value is { } registry)
        {
            imageBuilder = await registry.GetImageManifestAsync(
                BaseImageName,
                BaseImageTag,
                ContainerRuntimeIdentifier,
                RuntimeIdentifierGraphPath,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            throw new NotSupportedException(Resource.GetString(nameof(Strings.DontKnowHowToPullImages)));
        }

        if (imageBuilder is null)
        {
            Log.LogErrorWithCodeFromResources(nameof(Strings.BaseImageNotFound), sourceImageReference.RepositoryAndTag,
                ContainerRuntimeIdentifier);
            return !Log.HasLoggedErrors;
        }

        SafeLog("Building image '{0}' with tags {1} on top of base image {2}", ImageName, String.Join(",", ImageTags),
            sourceImageReference);

        Layer newLayer = Layer.FromDirectory(PublishDirectory, WorkingDirectory, imageBuilder.IsWindows);
        imageBuilder.AddLayer(newLayer);
        imageBuilder.SetWorkingDirectory(WorkingDirectory);
        imageBuilder.SetEntryPoint(Entrypoint.Select(i => i.ItemSpec).ToArray(),
            EntrypointArgs.Select(i => i.ItemSpec).ToArray());

        foreach (ITaskItem label in Labels)
        {
            imageBuilder.AddLabel(label.ItemSpec, label.GetMetadata("Value"));
        }

        SetEnvironmentVariables(imageBuilder, ContainerEnvironmentVariables);

        SetPorts(imageBuilder, ExposedPorts);

        if (ContainerUser is { } user)
        {
            imageBuilder.SetUser(user);
        }

        // at the end of this step, if any failed then bail out.
        if (Log.HasLoggedErrors)
        {
            return false;
        }

        BuiltImage builtImage = imageBuilder.Build();
        cancellationToken.ThrowIfCancellationRequested();

        // at this point we're done with modifications and are just pushing the data other places
        GeneratedContainerManifest = JsonSerializer.Serialize(builtImage.Manifest);
        GeneratedContainerConfiguration = builtImage.Config;

        foreach (DestinationImageReference destinationImageReference in destinationImageReferences)
        {
            switch (destinationImageReference.Registry)
            {
                case { IsTarGzFile: true }:
                    if (!await WriteTarGzAync(builtImage, sourceImageReference, destinationImageReference,
                            cancellationToken).ConfigureAwait(false))
                    {
                        return false;
                    }

                    break;

                case not null:
                    if (!await PushImagesToRemoteRegistryAsync(builtImage, sourceImageReference, destinationImageReference,
                            cancellationToken).ConfigureAwait(false))
                    {
                        return false;
                    }

                    break;
                case null:
                    if (!await PushImagesToLocalDaemonAsync(builtImage, sourceImageReference, destinationImageReference,
                            cancellationToken).ConfigureAwait(false))
                    {
                        return false;
                    }

                    break;

            }
        }

        return !Log.HasLoggedErrors;
    }

    private async Task<bool> WriteTarGzAync(BuiltImage builtImage, ImageReference sourceImageReference,
        DestinationImageReference destinationImageReference, CancellationToken cancellationToken)
    {
        string outputFile = destinationImageReference.Registry!.BaseUri.LocalPath;
        try
        {
            string? parentDirectory = Path.GetDirectoryName(Path.GetFullPath(outputFile));
            if (string.IsNullOrEmpty(parentDirectory) || !Directory.Exists(parentDirectory))
            {
                Log.LogErrorWithCodeFromResources(nameof(Strings.OutputFileDirectoryDoesntExist), outputFile);
                return false;
            }
        }
        catch (Exception e)
        {
            if (BuildEngine != null)
            {
                Log.LogErrorFromException(e, true);
            }

            return false;
        }

        try
        {
            await using FileStream fileStream = File.Create(outputFile);
            await LocalDocker.WriteImageToStreamAsync(builtImage, sourceImageReference, destinationImageReference,
                fileStream, cancellationToken).ConfigureAwait(false);
            SafeLog("Written image '{0}' to path '{1}'", ImageName, outputFile);
        }
        catch (Exception e)
        {
            if (BuildEngine != null)
            {
                Log.LogErrorFromException(e, true);
            }

            return false;
        }

        return true;
    }

    private async Task<bool> PushImagesToRemoteRegistryAsync(BuiltImage builtImage, ImageReference sourceImageReference,
        DestinationImageReference destinationImageReference, CancellationToken cancellationToken)
    {
        try
        {
            if (destinationImageReference.Registry is {} registry)
            {
                await (registry.PushAsync(
                    builtImage,
                    sourceImageReference,
                    destinationImageReference,
                    message => SafeLog(message),
                    cancellationToken)).ConfigureAwait(false);
                SafeLog("Pushed container images '{0}' to registry '{2}'",
                    destinationImageReference,
                    registry.RegistryName);
            }
        }
        catch (ContainerHttpException e)
        {
            if (BuildEngine != null)
            {
                Log.LogErrorFromException(e, true);
            }

            return false;
        }
        catch (Exception e)
        {
            if (BuildEngine != null)
            {
                Log.LogErrorWithCodeFromResources(nameof(Strings.RegistryOutputPushFailed), e.Message);
                Log.LogMessage(MessageImportance.Low, "Details: {0}", e);
            }

            return false;
        }

        return true;
    }

    private async Task<bool> PushImagesToLocalDaemonAsync(BuiltImage builtImage, ImageReference sourceImageReference,
        DestinationImageReference destinationImageReference, CancellationToken cancellationToken)
    {
        LocalDocker localDaemon = GetLocalDaemon(msg => Log.LogMessage(msg));
        if (!await localDaemon.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            Log.LogErrorWithCodeFromResources(nameof(Strings.LocalDaemondNotAvailable));
            return false;
        }

        try
        {
            await localDaemon
                .LoadAsync(builtImage, sourceImageReference, destinationImageReference, cancellationToken)
                .ConfigureAwait(false);
            SafeLog("Pushed container '{0}' to local daemon", destinationImageReference);
        }
        catch (AggregateException ex) when (ex.InnerException is DockerLoadException dle)
        {
            Log.LogErrorFromException(dle, showStackTrace: false);
            return false;
        }

        return true;
    }

    private void SetPorts(ImageBuilder image, ITaskItem[] exposedPorts)
    {
        foreach (var port in exposedPorts)
        {
            var portNo = port.ItemSpec;
            var portType = port.GetMetadata("Type");
            if (ContainerHelpers.TryParsePort(portNo, portType, out Port? parsedPort,
                    out ContainerHelpers.ParsePortError? errors))
            {
                image.ExposePort(parsedPort.Value.Number, parsedPort.Value.Type);
            }
            else
            {
                ContainerHelpers.ParsePortError parsedErrors = (ContainerHelpers.ParsePortError)errors!;

                if (parsedErrors.HasFlag(ContainerHelpers.ParsePortError.MissingPortNumber))
                {
                    Log.LogErrorWithCodeFromResources(nameof(Strings.MissingPortNumber), port.ItemSpec);
                }
                else
                {
                    if (parsedErrors.HasFlag(ContainerHelpers.ParsePortError.InvalidPortNumber) &&
                        parsedErrors.HasFlag(ContainerHelpers.ParsePortError.InvalidPortType))
                    {
                        Log.LogErrorWithCodeFromResources(nameof(Strings.InvalidPort_NumberAndType), portNo, portType);
                    }
                    else if (parsedErrors.HasFlag(ContainerHelpers.ParsePortError.InvalidPortNumber))
                    {
                        Log.LogErrorWithCodeFromResources(nameof(Strings.InvalidPort_Number), portNo);
                    }
                    else if (parsedErrors.HasFlag(ContainerHelpers.ParsePortError.InvalidPortType))
                    {
                        Log.LogErrorWithCodeFromResources(nameof(Strings.InvalidPort_Type), portType);
                    }
                }
            }
        }
    }

    private LocalDocker GetLocalDaemon(Action<string> logger)
    {
        return LocalContainerDaemon switch
        {
            KnownDaemonTypes.Docker => new LocalDocker(logger),
            _ => throw new NotSupportedException(
                Resource.FormatString(
                    nameof(Strings.UnknownDaemonType),
                    LocalContainerDaemon,
                    string.Join(",", KnownDaemonTypes.SupportedLocalDaemonTypes)))
        };
    }

    private Lazy<Registry?> SourceRegistry
    {
        get
        {
            if (IsDaemonPull)
            {
                return new Lazy<Registry?>(() => null);
            }
            else
            {
                return new Lazy<Registry?>(() => new Registry(BaseRegistry));
            }
        }
    }

    private static void SetEnvironmentVariables(ImageBuilder img, ITaskItem[] envVars)
    {
        foreach (ITaskItem envVar in envVars)
        {
            img.AddEnvironmentVariable(envVar.ItemSpec, envVar.GetMetadata("Value"));
        }
    }

    private void SafeLog(string message, params object[] formatParams)
    {
        if (BuildEngine != null) Log.LogMessage(MessageImportance.High, message, formatParams);
    }

    public void Dispose()
    {
        _cancellationTokenSource.Dispose();
    }
}
