// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.LocalDaemons;
using Microsoft.NET.Build.Containers.Logging;
using Microsoft.NET.Build.Containers.Resources;
using ILogger = Microsoft.Extensions.Logging.ILogger;

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

    private bool IsLocalPull => string.IsNullOrEmpty(BaseRegistry);

    public void Cancel() => _cancellationTokenSource.Cancel();

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

        using MSBuildLoggerProvider loggerProvider = new(Log);
        ILoggerFactory msbuildLoggerFactory = new LoggerFactory(new[] { loggerProvider });
        ILogger logger = msbuildLoggerFactory.CreateLogger<CreateNewImage>();

        if (!Directory.Exists(PublishDirectory))
        {
            Log.LogErrorWithCodeFromResources(nameof(Strings.PublishDirectoryDoesntExist), nameof(PublishDirectory), PublishDirectory);
            return !Log.HasLoggedErrors;
        }

        RegistryMode sourceRegistryMode = BaseRegistry.Equals(OutputRegistry, StringComparison.InvariantCultureIgnoreCase) ? RegistryMode.PullFromOutput : RegistryMode.Pull;
        Registry? sourceRegistry = IsLocalPull ? null : new Registry(BaseRegistry, logger, sourceRegistryMode);
        SourceImageReference sourceImageReference = new(sourceRegistry, BaseImageName, BaseImageTag, BaseImageDigest);

        DestinationImageReference destinationImageReference = DestinationImageReference.CreateFromSettings(
            Repository,
            ImageTags,
            msbuildLoggerFactory,
            ArchiveOutputPath,
            OutputRegistry,
            LocalRegistry);

        var telemetry = CreateTelemetryContext(sourceImageReference, destinationImageReference);

        ImageBuilder? imageBuilder;
        if (sourceRegistry is { } registry)
        {
            try
            {
                var picker = new RidGraphManifestPicker(RuntimeIdentifierGraphPath);
                imageBuilder = await registry.GetImageManifestAsync(
                    BaseImageName,
                    sourceImageReference.Reference,
                    ContainerRuntimeIdentifier,
                    picker,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (RepositoryNotFoundException)
            {
                telemetry.LogUnknownRepository();
                Log.LogErrorWithCodeFromResources(nameof(Strings.RepositoryNotFound), BaseImageName, BaseImageTag, BaseImageDigest, registry.RegistryName);
                return !Log.HasLoggedErrors;
            }
            catch (UnableToAccessRepositoryException)
            {
                telemetry.LogCredentialFailure(sourceImageReference);
                Log.LogErrorWithCodeFromResources(nameof(Strings.UnableToAccessRepository), BaseImageName, registry.RegistryName);
                return !Log.HasLoggedErrors;
            }
            catch (ContainerHttpException e)
            {
                Log.LogErrorFromException(e, showStackTrace: false, showDetail: true, file: null);
                return !Log.HasLoggedErrors;
            }
            catch (BaseImageNotFoundException e)
            {
                telemetry.LogRidMismatch(e.RequestedRuntimeIdentifier, e.AvailableRuntimeIdentifiers.ToArray());
                Log.LogErrorFromException(e, showStackTrace: false, showDetail: true, file: null);
                return !Log.HasLoggedErrors;
            }
        }
        else
        {
            throw new NotSupportedException(Resource.GetString(nameof(Strings.ImagePullNotSupported)));
        }

        if (imageBuilder is null)
        {
            Log.LogErrorWithCodeFromResources(nameof(Strings.BaseImageNotFound), sourceImageReference, ContainerRuntimeIdentifier);
            return !Log.HasLoggedErrors;
        }

        SafeLog(Strings.ContainerBuilder_StartBuildingImage, Repository, string.Join(",", ImageTags), sourceImageReference);

        // forcibly change the media type if required
        if (ImageFormat is not null)
        {
            if (Enum.TryParse<KnownImageFormats>(ImageFormat, out var imageFormat))
            {
                imageBuilder.ManifestMediaType = imageFormat switch
                {
                    KnownImageFormats.Docker => SchemaTypes.DockerManifestV2,
                    KnownImageFormats.OCI => SchemaTypes.OciManifestV1,
                    _ => imageBuilder.ManifestMediaType // should be impossible unless we add to the enum
                };
            }
            else
            {
                Log.LogErrorWithCodeFromResources(nameof(Strings.InvalidContainerImageFormat), ImageFormat, string.Join(",", Enum.GetValues<KnownImageFormats>()));
            }
        }

        Layer newLayer = Layer.FromDirectory(PublishDirectory, WorkingDirectory, imageBuilder.IsWindows, imageBuilder.ManifestMediaType);
        imageBuilder.AddLayer(newLayer);
        imageBuilder.SetWorkingDirectory(WorkingDirectory);

        (string[] entrypoint, string[] cmd) = DetermineEntrypointAndCmd(baseImageEntrypoint: imageBuilder.BaseImageConfig.GetEntrypoint());
        imageBuilder.SetEntrypointAndCmd(entrypoint, cmd);

        if (GenerateLabels)
        {
            foreach (ITaskItem label in Labels)
            {
                imageBuilder.AddLabel(label.ItemSpec, label.GetMetadata("Value"));
            }

            if (GenerateDigestLabel)
            {
                imageBuilder.AddBaseImageDigestLabel();
            }
        }
        else
        {
            if (GenerateDigestLabel)
            {
                Log.LogMessageFromResources(nameof(Strings.GenerateDigestLabelWithoutGenerateLabels));
            }
        }

        SetEnvironmentVariables(imageBuilder, ContainerEnvironmentVariables);

        SetPorts(imageBuilder, ExposedPorts);

        if (ContainerUser is { Length: > 0 } user)
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
        GeneratedContainerDigest = builtImage.Manifest.GetDigest();
        GeneratedArchiveOutputPath = ArchiveOutputPath;
        GeneratedContainerMediaType = builtImage.ManifestMediaType;
        GeneratedContainerNames = destinationImageReference.FullyQualifiedImageNames().Select(name => new Microsoft.Build.Utilities.TaskItem(name)).ToArray();

        switch (destinationImageReference.Kind)
        {
            case DestinationImageReferenceKind.LocalRegistry:
                await PushToLocalRegistryAsync(builtImage,
                    sourceImageReference,
                    destinationImageReference,
                    telemetry,
                    cancellationToken).ConfigureAwait(false);
                break;
            case DestinationImageReferenceKind.RemoteRegistry:
                await PushToRemoteRegistryAsync(builtImage,
                    sourceImageReference,
                    destinationImageReference,
                    telemetry,
                    cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        telemetry.LogPublishSuccess();

        return !Log.HasLoggedErrors;
    }

    private async Task PushToLocalRegistryAsync(BuiltImage builtImage, SourceImageReference sourceImageReference,
        DestinationImageReference destinationImageReference,
        Telemetry telemetry,
        CancellationToken cancellationToken)
    {
        ILocalRegistry localRegistry = destinationImageReference.LocalRegistry!;
        if (!(await localRegistry.IsAvailableAsync(cancellationToken).ConfigureAwait(false)))
        {
            telemetry.LogMissingLocalBinary();
            Log.LogErrorWithCodeFromResources(nameof(Strings.LocalRegistryNotAvailable));
            return;
        }
        try
        {
            await localRegistry.LoadAsync(builtImage, sourceImageReference, destinationImageReference, cancellationToken).ConfigureAwait(false);
            SafeLog(Strings.ContainerBuilder_ImageUploadedToLocalDaemon, destinationImageReference, localRegistry);

            if (localRegistry is ArchiveFileRegistry archive)
            {
                GeneratedArchiveOutputPath = archive.ArchiveOutputPath;
            }
        }
        catch (ContainerHttpException e)
        {
            if (BuildEngine != null)
            {
                Log.LogErrorFromException(e, true);
            }
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
    }

    private async Task PushToRemoteRegistryAsync(BuiltImage builtImage, SourceImageReference sourceImageReference,
        DestinationImageReference destinationImageReference,
        Telemetry telemetry,
        CancellationToken cancellationToken)
    {
        try
        {
            await destinationImageReference.RemoteRegistry!.PushAsync(
                builtImage,
                sourceImageReference,
                destinationImageReference,
                cancellationToken).ConfigureAwait(false);
            SafeLog(Strings.ContainerBuilder_ImageUploadedToRegistry, destinationImageReference, OutputRegistry);
        }
        catch (UnableToAccessRepositoryException)
        {
            if (BuildEngine != null)
            {
                Log.LogErrorWithCodeFromResources(nameof(Strings.UnableToAccessRepository), destinationImageReference.Repository, destinationImageReference.RemoteRegistry!.RegistryName);
            }
        }
        catch (ContainerHttpException e)
        {
            if (BuildEngine != null)
            {
                Log.LogErrorFromException(e, true);
            }
        }
        catch (Exception e)
        {
            if (BuildEngine != null)
            {
                Log.LogErrorWithCodeFromResources(nameof(Strings.RegistryOutputPushFailed), e.Message);
                Log.LogMessage(MessageImportance.Low, "Details: {0}", e);
            }
        }
    }

    private void SetPorts(ImageBuilder image, ITaskItem[] exposedPorts)
    {
        foreach (var port in exposedPorts)
        {
            var portNo = port.ItemSpec;
            var portType = port.GetMetadata("Type");
            if (ContainerHelpers.TryParsePort(portNo, portType, out Port? parsedPort, out ContainerHelpers.ParsePortError? errors))
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
                    if (parsedErrors.HasFlag(ContainerHelpers.ParsePortError.InvalidPortNumber) && parsedErrors.HasFlag(ContainerHelpers.ParsePortError.InvalidPortType))
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

    private void SetEnvironmentVariables(ImageBuilder img, ITaskItem[] envVars)
    {
        foreach (ITaskItem envVar in envVars)
        {
            var value = envVar.GetMetadata("Value");
            img.AddEnvironmentVariable(envVar.ItemSpec, value);
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

    internal (string[] entrypoint, string[] cmd) DetermineEntrypointAndCmd(string[]? baseImageEntrypoint)
    {
        string[] entrypoint = Entrypoint.Select(i => i.ItemSpec).ToArray();
        string[] entrypointArgs = EntrypointArgs.Select(i => i.ItemSpec).ToArray();
        string[] cmd = DefaultArgs.Select(i => i.ItemSpec).ToArray();
        string[] appCommand = AppCommand.Select(i => i.ItemSpec).ToArray();
        string[] appCommandArgs = AppCommandArgs.Select(i => i.ItemSpec).ToArray();
        string appCommandInstruction = AppCommandInstruction;

        return ImageBuilder.DetermineEntrypointAndCmd(entrypoint, entrypointArgs, cmd, appCommand, appCommandArgs, appCommandInstruction, baseImageEntrypoint,
            logWarning: s => Log.LogWarningWithCodeFromResources(s),
            logError: (s, a) => { if (a is null) Log.LogErrorWithCodeFromResources(s); else Log.LogErrorWithCodeFromResources(s, a); });
    }

    private Telemetry CreateTelemetryContext(SourceImageReference source, DestinationImageReference destination)
    {
        var context = new PublishTelemetryContext(
            source.Registry is not null ? GetRegistryType(source.Registry) : null,
            null, // we don't support local pull yet, but we may in the future
            destination.RemoteRegistry is not null ? GetRegistryType(destination.RemoteRegistry) : null,
            destination.LocalRegistry is not null ? GetLocalStorageType(destination.LocalRegistry) : null);
        return new Telemetry(Log, context);
    }

    private RegistryType GetRegistryType(Registry r)
    {
        if (r.IsMcr) return RegistryType.MCR;
        if (r.IsGithubPackageRegistry) return RegistryType.GitHub;
        if (r.IsAmazonECRRegistry) return RegistryType.AWS;
        if (r.IsAzureContainerRegistry) return RegistryType.Azure;
        if (r.IsGoogleArtifactRegistry) return RegistryType.Google;
        if (r.IsDockerHub) return RegistryType.DockerHub;
        return RegistryType.Other;
    }

    private LocalStorageType GetLocalStorageType(ILocalRegistry r)
    {
        if (r is ArchiveFileRegistry) return LocalStorageType.Tarball;
        var d = r as DockerCli;
        System.Diagnostics.Debug.Assert(d != null, "Unknown local registry type");
        if (d.GetCommand() == DockerCli.DockerCommand) return LocalStorageType.Docker;
        else return LocalStorageType.Podman;
    }

    /// <summary>
    /// Interesting data about the container publish - used to track the usage rates of various sources/targets of the process
    /// and to help diagnose issues with the container publish overall.
    /// </summary>
    /// <param name="RemotePullType">If the base image came from a remote registry, what kind of registry was it?</param>
    /// <param name="LocalPullType">If the base image came from a local store of some kind, what kind of store was it?</param>
    /// <param name="RemotePushType">If the new image is being pushed to a remote registry, what kind of registry is it?</param>
    /// <param name="LocalPushType">If the new image is being stored in a local store of some kind, what kind of store is it?</param>
    private record class PublishTelemetryContext(RegistryType? RemotePullType, LocalStorageType? LocalPullType, RegistryType? RemotePushType, LocalStorageType? LocalPushType);
    private enum RegistryType { Azure, AWS, Google, GitHub, DockerHub, MCR, Other }
    private enum LocalStorageType { Docker, Podman, Tarball }

    private class Telemetry(Microsoft.Build.Utilities.TaskLoggingHelper Log, PublishTelemetryContext context)
    {
        private IDictionary<string, string?> ContextProperties() => new Dictionary<string, string?>
            {
                { nameof(context.RemotePullType), context.RemotePullType?.ToString() },
                { nameof(context.LocalPullType), context.LocalPullType?.ToString() },
                { nameof(context.RemotePushType), context.RemotePushType?.ToString() },
                { nameof(context.LocalPushType), context.LocalPushType?.ToString() }
            };

        public void LogPublishSuccess()
        {
            Log.LogTelemetry("sdk/container/publish/success", ContextProperties());
        }

        public void LogUnknownRepository()
        {
            var props = ContextProperties();
            props.Add("error", "unknown_repository");
            Log.LogTelemetry("sdk/container/publish/error", props);
        }

        public void LogCredentialFailure(SourceImageReference _)
        {
            var props = ContextProperties();
            props.Add("error", "credential_failure");
            props.Add("direction", "pull");
            Log.LogTelemetry("sdk/container/publish/error", props);
        }

        public void LogCredentialFailure(DestinationImageReference d)
        {
            var props = ContextProperties();
            props.Add("error", "credential_failure");
            props.Add("direction", "push");
            Log.LogTelemetry("sdk/container/publish/error", props);
        }

        public void LogRidMismatch(string desiredRid, string[] availableRids)
        {
            var props = ContextProperties();
            props.Add("error", "rid_mismatch");
            props.Add("target_rid", desiredRid);
            props.Add("available_rids", string.Join(",", availableRids));
            Log.LogTelemetry("sdk/container/publish/error", props);
        }

        public void LogMissingLocalBinary()
        {
            var props = ContextProperties();
            props.Add("error", "missing_binary");
            Log.LogTelemetry("sdk/container/publish/error", props);
        }

        public void LogLocalLoadError()
        {
            var props = ContextProperties();
            props.Add("error", "local_load");
            Log.LogTelemetry("sdk/container/publish/error", props);
        }

    }
}
