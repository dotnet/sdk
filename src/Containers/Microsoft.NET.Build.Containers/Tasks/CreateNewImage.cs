// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
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

    private bool IsLocalPull => string.IsNullOrWhiteSpace(BaseRegistry);

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

        var telemetry = new Telemetry(sourceImageReference, destinationImageReference, Log);

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

        (string message, object[] parameters) = SkipPublishing ?
            (Strings.ContainerBuilder_StartBuildingImageForRid, new object[] { Repository, ContainerRuntimeIdentifier, sourceImageReference }) :
            (Strings.ContainerBuilder_StartBuildingImage, new object[] { Repository, String.Join(",", ImageTags), sourceImageReference });
        Log.LogMessage(MessageImportance.High, message, parameters);

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

        string? baseImageLabel = null;
        string? baseImageDigest = null;
        if (GenerateLabels)
        {
            foreach (ITaskItem label in Labels)
            {
                imageBuilder.AddLabel(label.ItemSpec, label.GetMetadata("Value"));
            }

            if (GenerateDigestLabel)
            {
                (baseImageLabel, baseImageDigest) = imageBuilder.AddBaseImageDigestLabel();
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
        GeneratedContainerManifest = builtImage.Manifest;
        GeneratedContainerConfiguration = builtImage.Config;
        GeneratedContainerDigest = builtImage.ManifestDigest;
        GeneratedArchiveOutputPath = ArchiveOutputPath;
        GeneratedContainerMediaType = builtImage.ManifestMediaType;
        GeneratedContainerNames = destinationImageReference.FullyQualifiedImageNames().Select(name => new Microsoft.Build.Utilities.TaskItem(name)).ToArray();
        if (baseImageLabel is not null && baseImageDigest is not null)
        {
            var labelItem = new Microsoft.Build.Utilities.TaskItem(baseImageLabel);
            labelItem.SetMetadata("Value", baseImageDigest);
            GeneratedDigestLabel = labelItem;
        }

        if (!SkipPublishing)
        {
            await ImagePublisher.PublishImageAsync(builtImage, sourceImageReference, destinationImageReference, Log, telemetry, cancellationToken)
                .ConfigureAwait(false);
        }

        return !Log.HasLoggedErrors;
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
}
