// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

internal abstract class ContainerRuntimeBase(ContainerRuntimeOperations operations, ILogger logger) : IContainerRuntime
{
    private bool _wasProbed;

    protected ContainerRuntimeOperations Operations { get; } = operations;

    protected ILogger Logger { get; } = logger;

    protected abstract string Command { get; }

    protected virtual string ProbeArguments => "version";

    /// <inheritdoc />
    public async Task<bool> ProbeAsync(CancellationToken cancellationToken)
    {
        _wasProbed = await Operations.ProbeCommandAsync(Command, ProbeArguments, cancellationToken).ConfigureAwait(false);
        return _wasProbed;
    }

    /// <inheritdoc />
    public virtual async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
        => _wasProbed || await ProbeAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public virtual string GetManifestMediaType(string defaultManifestMediaType, KnownImageFormats? imageFormat)
        => GetDefaultManifestMediaType(defaultManifestMediaType, imageFormat);

    /// <inheritdoc />
    public abstract ContainerRuntimeKind GetTelemetryValue();

    /// <inheritdoc />
    public virtual Task LoadAsync(
        BuiltImage image,
        SourceImageReference sourceReference,
        DestinationImageReference destinationReference,
        CancellationToken cancellationToken)
        => Operations.LoadFromStandardInputAsync(
            Command,
            ["load"],
            image,
            sourceReference,
            destinationReference,
            ContainerArchive.WriteDockerImageToStreamAsync,
            cancellationToken);

    /// <inheritdoc />
    public abstract Task LoadAsync(
        MultiArchImage image,
        SourceImageReference sourceReference,
        DestinationImageReference destinationReference,
        CancellationToken cancellationToken);

    internal static string GetDefaultManifestMediaType(string defaultManifestMediaType, KnownImageFormats? imageFormat)
        => imageFormat switch
        {
            null => defaultManifestMediaType,
            KnownImageFormats.Docker => SchemaTypes.DockerManifestV2,
            KnownImageFormats.OCI => SchemaTypes.OciManifestV1,
            _ => defaultManifestMediaType
        };
}

internal sealed class DockerContainerRuntime(ContainerRuntimeOperations operations, ILogger logger) : ContainerRuntimeBase(operations, logger)
{
    protected override string Command => ContainerRuntime.DockerCommand;

    /// <inheritdoc />
    public override Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        JsonDocument config = GetDockerConfig();
        if (!config.RootElement.TryGetProperty("ServerErrors", out JsonElement errorProperty) ||
            errorProperty.ValueKind == JsonValueKind.Array && errorProperty.GetArrayLength() == 0)
        {
            return Task.FromResult(true);
        }

        string messages = string.Join(Environment.NewLine, errorProperty.EnumerateArray());
        Logger.LogError($"The daemon server reported errors: {messages}");
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public override ContainerRuntimeKind GetTelemetryValue() => ContainerRuntimeKind.Docker;

    /// <inheritdoc />
    public override Task LoadAsync(
        MultiArchImage image,
        SourceImageReference sourceReference,
        DestinationImageReference destinationReference,
        CancellationToken cancellationToken)
    {
        if (!IsContainerdStoreEnabled())
        {
            throw new DockerLoadException(Strings.ImageLoadFailed_ContainerdStoreDisabled);
        }

        return Operations.LoadFromStandardInputAsync(
            Command,
            ["load"],
            image,
            sourceReference,
            destinationReference,
            ContainerArchive.WriteMultiArchOciImageToStreamAsync,
            cancellationToken);
    }

    internal static bool IsPodmanAlias()
    {
        try
        {
            JsonElement dockerInfo = GetDockerConfig().RootElement;
            bool hasDockerProperty =
                dockerInfo.TryGetProperty("DockerRootDir", out JsonElement dockerRootDir) &&
                dockerRootDir.GetString() is not null;
            bool hasPodmanProperty =
                dockerInfo.TryGetProperty("host", out JsonElement host) &&
                host.TryGetProperty("buildahVersion", out JsonElement buildahVersion) &&
                buildahVersion.GetString() is not null;
            return !hasDockerProperty && hasPodmanProperty;
        }
        catch
        {
            return false;
        }
    }

    internal static JsonDocument GetDockerConfig()
    {
        string dockerPath = ContainerRuntimeOperations.FindFullPathFromPath(ContainerRuntime.DockerCommand);
        Process process = new()
        {
            StartInfo = new ProcessStartInfo(dockerPath, "info --format=\"{{json .}}\"")
        };

        try
        {
            Command dockerCommand = new(process);
            dockerCommand.CaptureStdOut();
            dockerCommand.CaptureStdErr();
            CommandResult result = dockerCommand.Execute();
            if (result.ExitCode != 0)
            {
                throw new DockerLoadException(Resource.FormatString(
                    nameof(Strings.DockerInfoFailed),
                    result.ExitCode,
                    result.StdOut,
                    result.StdErr));
            }

            return JsonDocument.Parse(result.StdOut ?? string.Empty);
        }
        catch (Exception e) when (e is not DockerLoadException)
        {
            throw new DockerLoadException(Resource.FormatString(nameof(Strings.DockerInfoFailed_Ex), e.Message));
        }
    }

    internal static bool IsInsecureRegistry(string registryDomain)
    {
        try
        {
            JsonElement rootElement = GetDockerConfig().RootElement;
            if (rootElement.TryGetProperty("RegistryConfig", out JsonElement registryConfig) &&
                registryConfig.ValueKind == JsonValueKind.Object &&
                registryConfig.TryGetProperty("IndexConfigs", out JsonElement indexConfigs) &&
                indexConfigs.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in indexConfigs.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Object &&
                        property.Value.TryGetProperty("Secure", out JsonElement secure) &&
                        !secure.GetBoolean() &&
                        property.Name.Equals(registryDomain, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            if (rootElement.TryGetProperty("registries", out JsonElement registries) &&
                registries.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in registries.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Object &&
                        property.Value.TryGetProperty("Insecure", out JsonElement insecure) &&
                        insecure.GetBoolean() &&
                        property.Name.Equals(registryDomain, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        catch (DockerLoadException)
        {
            return false;
        }
    }

    internal static bool IsContainerdStoreEnabled()
    {
        try
        {
            if (!GetDockerConfig().RootElement.TryGetProperty("DriverStatus", out JsonElement driverStatus) ||
                driverStatus.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (JsonElement item in driverStatus.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Array || item.GetArrayLength() != 2)
                {
                    continue;
                }

                JsonElement[] values = item.EnumerateArray().ToArray();
                if (values[0].GetString() == "driver-type" &&
                    values[1].GetString()!.StartsWith("io.containerd.snapshotter"))
                {
                    return true;
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}

internal sealed class PodmanContainerRuntime(ContainerRuntimeOperations operations, ILogger logger) : ContainerRuntimeBase(operations, logger)
{
    protected override string Command => ContainerRuntime.PodmanCommand;

    /// <inheritdoc />
    public override ContainerRuntimeKind GetTelemetryValue() => ContainerRuntimeKind.Podman;

    /// <inheritdoc />
    public override Task LoadAsync(
        MultiArchImage image,
        SourceImageReference sourceReference,
        DestinationImageReference destinationReference,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string commandPath = Operations.FindFullCommandPath(Command);
        var createdImages = new List<string>();
        return LoadAsyncCore();

        async Task LoadAsyncCore()
        {
            try
            {
                string firstTag = destinationReference.Tags.First();
                string manifestName = $"{destinationReference.Repository}:{firstTag}";
                await RunAndIgnoreAsync($"rmi {manifestName}");
                await RunAsync($"manifest create {manifestName}");
                createdImages.Add(manifestName);

                Debug.Assert(image.Images is not null);
                foreach (BuiltImage architectureImage in image.Images)
                {
                    string repo = $"{destinationReference.Repository}-{architectureImage.Architecture}";
                    string tag = $"{repo}:{firstTag}";
                    var architectureDestination = new DestinationImageReference(destinationReference.LocalRegistry!, repo, [firstTag]);

                    await Operations.LoadFromStandardInputAsync(
                        Command,
                        ["load"],
                        architectureImage,
                        sourceReference,
                        architectureDestination,
                        ContainerArchive.WriteDockerImageToStreamAsync,
                        cancellationToken).ConfigureAwait(false);
                    createdImages.Add(tag);
                    await RunAsync($"manifest add {manifestName} {tag}");
                }

                foreach (string tag in destinationReference.Tags.Skip(1))
                {
                    string additionalTag = $"{destinationReference.Repository}:{tag}";
                    await RunAsync($"tag {manifestName} {additionalTag}");
                    createdImages.Add(additionalTag);
                }
            }
            catch
            {
                foreach (string createdImage in createdImages)
                {
                    await RunAndIgnoreAsync($"rmi {createdImage}");
                }
                throw;
            }
        }

        async Task RunAsync(string arguments)
        {
            (int exitCode, string stderr) = await ContainerRuntimeOperations.RunProcessAsync(commandPath, arguments, cancellationToken);
            if (exitCode != 0)
            {
                throw new DockerLoadException(Resource.FormatString(nameof(Strings.ImageLoadFailed), stderr));
            }
        }

        async Task RunAndIgnoreAsync(string arguments)
        {
            try
            {
                _ = await ContainerRuntimeOperations.RunProcessAsync(commandPath, arguments, CancellationToken.None);
            }
            catch
            { }
        }
    }
}
