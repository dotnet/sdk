// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.MSBuild;
using Microsoft.NET.Build.Containers.Resources;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Microsoft.NET.Build.Containers.Tasks;

public sealed partial class CreateNewImage : Microsoft.Build.Utilities.Task, ICancelableTask, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

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

        KnownImageFormats? format = null;
        if (ImageFormat is not null)
        {
            if (Enum.TryParse(ImageFormat, out KnownImageFormats knownFormat))
            {
                format = knownFormat;
            }
            else
            {
                Log.LogErrorWithCodeFromResources(nameof(Strings.InvalidContainerImageFormat), ImageFormat, string.Join(",", Enum.GetNames(typeof(KnownImageFormats))));
                return false;
            }
        }

        ImageBuilder? imageBuilder;
        if (sourceRegistry is { } registry)
        {
            imageBuilder = await ContainerBuilder.LoadFromManifestAndConfig(BaseImageManifestPath.ItemSpec, format, BaseImageConfigurationPath.ItemSpec, logger);
        }
        else
        {
            throw new NotSupportedException(Resource.GetString(nameof(Strings.ImagePullNotSupported)));
        }

        (string message, object[] parameters) =
            (Strings.ContainerBuilder_StartBuildingImage, new object[] { Repository, String.Join(",", ImageTags), sourceImageReference });
        Log.LogMessage(MessageImportance.High, message, parameters);

        var storePath = new DirectoryInfo(ContentStoreRoot);
        if (!storePath.Exists)
        {
            throw new ArgumentException($"The content store path '{ContentStoreRoot}' does not exist.");
        }
        var store = new ContentStore(storePath);

        var descriptor = GetDescriptor(GeneratedApplicationLayer);
        var appLayer = Layer.FromBackingFile(new(GeneratedApplicationLayer.ItemSpec), descriptor);
        imageBuilder.AddLayer(appLayer);
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

        var serializedManifest = JsonSerializer.Serialize(builtImage.Manifest);
        var manifestWriteTask = File.WriteAllTextAsync(GeneratedManifestPath, serializedManifest, DigestUtils.UTF8);

        var serializedConfig = JsonSerializer.Serialize(builtImage.Config);
        var configWriteTask = File.WriteAllTextAsync(GeneratedConfigurationPath, serializedConfig, DigestUtils.UTF8);

        await Task.WhenAll(manifestWriteTask, configWriteTask).ConfigureAwait(false);

        GeneratedContainerManifest = serializedManifest;
        GeneratedContainerConfiguration = serializedConfig;
        GeneratedContainerDigest = builtImage.ManifestDigest;
        GeneratedArchiveOutputPath = ArchiveOutputPath;
        GeneratedContainerMediaType = builtImage.ManifestMediaType;
        GeneratedContainerNames = destinationImageReference.FullyQualifiedImageNames().Select(name => new Microsoft.Build.Utilities.TaskItem(name)).ToArray();

        GeneratedAppContainerConfig = new Microsoft.Build.Utilities.TaskItem(GeneratedConfigurationPath, new Dictionary<string, string>(2)
        {
            ["Size"] = builtImage.Manifest.Config.size.ToString(),
            ["MediaType"] = builtImage.Manifest.Config.mediaType,
            ["Digest"] = builtImage.Manifest.Config.digest,
        });

        GeneratedAppContainerManifest = new Microsoft.Build.Utilities.TaskItem(GeneratedManifestPath, new Dictionary<string, string>(2)
        {
            ["Size"] = new FileInfo(GeneratedManifestPath).Length.ToString(),
            ["MediaType"] = builtImage.Manifest.MediaType!,
            ["Digest"] = builtImage.Manifest.GetDigest(),
        });

        if (baseImageLabel is not null && baseImageDigest is not null)
        {
            var labelItem = new Microsoft.Build.Utilities.TaskItem(baseImageLabel);
            labelItem.SetMetadata("Value", baseImageDigest);
            GeneratedDigestLabel = labelItem;
        }

        return !Log.HasLoggedErrors;
    }

    private static Descriptor GetDescriptor(ITaskItem generatedApplicationLayer) => new Descriptor
    {
        Size = generatedApplicationLayer.GetMetadata("Size") is string sizeStr && long.TryParse(sizeStr, out long size) ? size : throw new ArgumentException($"Invalid size for layer '{generatedApplicationLayer.ItemSpec}'."),
        MediaType = generatedApplicationLayer.GetMetadata("MediaType"),
        Digest = generatedApplicationLayer.GetMetadata("Digest")
    };

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
