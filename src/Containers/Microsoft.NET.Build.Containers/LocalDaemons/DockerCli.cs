// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
#if NET
using System.Formats.Tar;
#endif
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

// Wraps the 'docker'/'podman' cli.
internal sealed class DockerCli
#if NET
: ILocalRegistry
#endif
{
    public const string DockerCommand = "docker";
    public const string PodmanCommand = "podman";

    private const string Commands = $"{DockerCommand}/{PodmanCommand}";

    private readonly ILogger _logger;
    private string? _command;

#if NET
    private string? _fullCommandPath;
#endif

    private const string _blobsPath = "blobs/sha256";

    public DockerCli(string? command, ILoggerFactory loggerFactory)
    {
        if (!(command == null ||
              command == PodmanCommand ||
              command == DockerCommand))
        {
            throw new ArgumentException($"{command} is an unknown command.");
        }

        this._command = command;
        this._logger = loggerFactory.CreateLogger<DockerCli>();
    }

    public DockerCli(ILoggerFactory loggerFactory) : this(null, loggerFactory)
    { }

    private static string FindFullPathFromPath(string command)
    {
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            string fullPath = Path.Combine(directory, command + FileNameSuffixes.CurrentPlatform.Exe);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return command;
    }

#if NET
    private async ValueTask<string> FindFullCommandPath(CancellationToken cancellationToken)
    {
        if (_fullCommandPath != null)
        {
            return _fullCommandPath;
        }

        string? command = await GetCommandAsync(cancellationToken);
        if (command is null)
        {
            throw new NotImplementedException(Resource.FormatString(Strings.ContainerRuntimeProcessCreationFailed, Commands));
        }

        _fullCommandPath = FindFullPathFromPath(command);

        return _fullCommandPath;
    }

    public async Task LoadAsync(BuiltImage image, SourceImageReference sourceReference, DestinationImageReference destinationReference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string commandPath = await FindFullCommandPath(cancellationToken);

        // call `docker load` and get it ready to receive input
        ProcessStartInfo loadInfo = new(commandPath, $"load");
        loadInfo.RedirectStandardInput = true;
        loadInfo.RedirectStandardOutput = true;
        loadInfo.RedirectStandardError = true;

        using Process? loadProcess = Process.Start(loadInfo);

        if (loadProcess is null)
        {
            throw new NotImplementedException(Resource.FormatString(Strings.ContainerRuntimeProcessCreationFailed, commandPath));
        }

        // Create new stream tarball
        // We want to be able to export to docker, even oci images.
        await WriteDockerImageToStreamAsync(image, sourceReference, destinationReference, loadProcess.StandardInput.BaseStream, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        loadProcess.StandardInput.Close();

        await loadProcess.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (loadProcess.ExitCode != 0)
        {
            throw new DockerLoadException(Resource.FormatString(nameof(Strings.ImageLoadFailed), await loadProcess.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false)));
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        bool commandPathWasUnknown = this._command is null; // avoid running the version command twice.
        string? command = await GetCommandAsync(cancellationToken);
        if (command is null)
        {
            _logger.LogError($"Cannot find {Commands} executable.");
            return false;
        }

        try
        {
            switch (command)
            {
                case DockerCommand:
                    {
                        JsonDocument config = GetDockerConfig();

                        if (!config.RootElement.TryGetProperty("ServerErrors", out JsonElement errorProperty))
                        {
                            return true;
                        }
                        else if (errorProperty.ValueKind == JsonValueKind.Array && errorProperty.GetArrayLength() == 0)
                        {
                            return true;
                        }
                        else
                        {
                            // we have errors, turn them into a string and log them
                            string messages = string.Join(Environment.NewLine, errorProperty.EnumerateArray());
                            _logger.LogError($"The daemon server reported errors: {messages}");
                            return false;
                        }
                    }
                case PodmanCommand:
                    return commandPathWasUnknown || await TryRunVersionCommandAsync(PodmanCommand, cancellationToken);
                default:
                    throw new NotImplementedException($"{command} is an unknown command.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(Strings.LocalDocker_FailedToGetConfig, ex.Message);
            _logger.LogTrace("Full information: {0}", ex);
            return false;
        }
    }

    ///<inheritdoc/>
    public bool IsAvailable()
        => IsAvailableAsync(default).GetAwaiter().GetResult();

    public string? GetCommand()
        => GetCommandAsync(default).GetAwaiter().GetResult();
#endif

    /// <summary>
    /// Gets docker configuration.
    /// </summary>
    /// <param name="sync">when <see langword="true"/>, the method is executed synchronously.</param>
    /// <exception cref="DockerLoadException">when failed to retrieve docker configuration.</exception>
    internal static JsonDocument GetDockerConfig()
    {
        string dockerPath = FindFullPathFromPath("docker");
        Process proc = new()
        {
            StartInfo = new ProcessStartInfo(dockerPath, "info --format=\"{{json .}}\"")
        };

        try
        {
            Command dockerCommand = new(proc);
            dockerCommand.CaptureStdOut();
            dockerCommand.CaptureStdErr();
            CommandResult dockerCommandResult = dockerCommand.Execute();

            if (dockerCommandResult.ExitCode != 0)
            {
                throw new DockerLoadException(Resource.FormatString(
                    nameof(Strings.DockerInfoFailed),
                    dockerCommandResult.ExitCode,
                    dockerCommandResult.StdOut,
                    dockerCommandResult.StdErr));
            }

            return JsonDocument.Parse(dockerCommandResult.StdOut);
        }
        catch (Exception e) when (e is not DockerLoadException)
        {
            throw new DockerLoadException(Resource.FormatString(nameof(Strings.DockerInfoFailed_Ex), e.Message));
        }
    }
    /// <summary>
    /// Checks if the registry is marked as insecure in the docker/podman config.
    /// </summary>
    /// <param name="registryDomain"></param>
    /// <returns></returns>
    public static bool IsInsecureRegistry(string registryDomain)
    {
        try
        {
            //check the docker config to see if the registry is marked as insecure
            var rootElement = GetDockerConfig().RootElement;

            //for docker
            if (rootElement.TryGetProperty("RegistryConfig", out var registryConfig) && registryConfig.ValueKind == JsonValueKind.Object)
            {
                if (registryConfig.TryGetProperty("IndexConfigs", out var indexConfigs) && indexConfigs.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in indexConfigs.EnumerateObject())
                    {
                        if (property.Value.ValueKind == JsonValueKind.Object && property.Value.TryGetProperty("Secure", out var secure) && !secure.GetBoolean())
                        {
                            if (property.Name.Equals(registryDomain, StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            //for podman
            if (rootElement.TryGetProperty("registries", out var registries) && registries.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in registries.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Object && property.Value.TryGetProperty("Insecure", out var insecure) && insecure.GetBoolean())
                    {
                        if (property.Name.Equals(registryDomain, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        catch (DockerLoadException)
        {
            //if docker load fails, we can't check the config so we assume the registry is secure
            return false;
        }
    }

    private static void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e) => throw new NotImplementedException();

#if NET
    public static async Task WriteImageToStreamAsync(BuiltImage image, SourceImageReference sourceReference, DestinationImageReference destinationReference, Stream imageStream, CancellationToken cancellationToken)
    {
        if (image.ManifestMediaType == SchemaTypes.DockerManifestV2)
        {
            await WriteDockerImageToStreamAsync(image, sourceReference, destinationReference, imageStream, cancellationToken);
        }
        else if (image.ManifestMediaType == SchemaTypes.OciManifestV1)
        {
            await WriteOciImageToStreamAsync(image, sourceReference, destinationReference, imageStream, cancellationToken);
        }
        else
        {
            throw new ArgumentException(Resource.FormatString(nameof(Strings.UnsupportedMediaTypeForTarball), image.Manifest.MediaType));
        }
    }

    private static async Task WriteDockerImageToStreamAsync(
        BuiltImage image,
        SourceImageReference sourceReference,
        DestinationImageReference destinationReference,
        Stream imageStream,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using TarWriter writer = new(imageStream, TarEntryFormat.Pax, leaveOpen: true);

        JsonArray layerTarballPaths = new JsonArray();
        await WriteImageLayers(writer, image, sourceReference, d => $"{d.Substring("sha256:".Length)}/layer.tar", cancellationToken, layerTarballPaths)
            .ConfigureAwait(false);

        string configTarballPath = $"{image.ImageSha}.json";
        await WriteImageConfig(writer, image, configTarballPath, cancellationToken)
            .ConfigureAwait(false);

        // Add manifest
        await WriteManifestForDockerImage(writer, destinationReference, configTarballPath, layerTarballPaths, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task WriteImageLayers(
        TarWriter writer,
        BuiltImage image,
        SourceImageReference sourceReference,
        Func<string, string> layerPathFunc,
        CancellationToken cancellationToken,
        JsonArray? layerTarballPaths = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var d in image.LayerDescriptors)
        {
            if (sourceReference.Registry is { } registry)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string localPath = await registry.DownloadBlobAsync(sourceReference.Repository, d, cancellationToken).ConfigureAwait(false); ;

                // Stuff that (uncompressed) tarball into the image tar stream
                // TODO uncompress!!
                string layerTarballPath = layerPathFunc(d.Digest);
                await writer.WriteEntryAsync(localPath, layerTarballPath, cancellationToken).ConfigureAwait(false);
                layerTarballPaths?.Add(layerTarballPath);
            }
            else
            {
                throw new NotImplementedException(Resource.FormatString(
                    nameof(Strings.MissingLinkToRegistry),
                    d.Digest,
                    sourceReference.Registry?.ToString() ?? "<null>"));
            }
        }
    }

    private static async Task WriteImageConfig(
        TarWriter writer,
        BuiltImage image,
        string configPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using (MemoryStream configStream = new MemoryStream(Encoding.UTF8.GetBytes(image.Config)))
        {
            PaxTarEntry configEntry = new(TarEntryType.RegularFile, configPath)
            {
                DataStream = configStream
            };
            await writer.WriteEntryAsync(configEntry, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WriteManifestForDockerImage(
        TarWriter writer,
        DestinationImageReference destinationReference,
        string configTarballPath,
        JsonArray layerTarballPaths,
        CancellationToken cancellationToken)
    {
        JsonArray tagsNode = new();
        foreach (string tag in destinationReference.Tags)
        {
            tagsNode.Add($"{destinationReference.Repository}:{tag}");
        }

        JsonNode manifestNode = new JsonArray(new JsonObject
        {
            { "Config", configTarballPath },
            { "RepoTags", tagsNode },
            { "Layers", layerTarballPaths }
        });

        cancellationToken.ThrowIfCancellationRequested();
        using (MemoryStream manifestStream = new MemoryStream(Encoding.UTF8.GetBytes(manifestNode.ToJsonString())))
        {
            PaxTarEntry manifestEntry = new(TarEntryType.RegularFile, "manifest.json")
            {
                DataStream = manifestStream
            };

            await writer.WriteEntryAsync(manifestEntry, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WriteOciImageToStreamAsync(
        BuiltImage image,
        SourceImageReference sourceReference,
        DestinationImageReference destinationReference,
        Stream imageStream,
        CancellationToken cancellationToken)
    {
        if (destinationReference.Tags.Length > 1)
        {
            throw new ArgumentException(Resource.FormatString(nameof(Strings.OciImageMultipleTagsNotSupported)));
        }

        cancellationToken.ThrowIfCancellationRequested();
        using TarWriter writer = new(imageStream, TarEntryFormat.Pax, leaveOpen: true);

        await WriteOciLayout(writer, cancellationToken)
            .ConfigureAwait(false);

        await WriteImageLayers(writer, image, sourceReference, d => $"{_blobsPath}/{d.Substring("sha256:".Length)}", cancellationToken)
            .ConfigureAwait(false);

        await WriteImageConfig(writer, image, $"{_blobsPath}/{image.ImageSha}", cancellationToken)
            .ConfigureAwait(false);

        await WriteManifestForOciImage(writer, image, destinationReference, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task WriteOciLayout(TarWriter writer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string ociLayoutPath = "oci-layout";
        var ociLayoutContent = "{\"imageLayoutVersion\": \"1.0.0\"}";
        using (MemoryStream ociLayoutStream = new MemoryStream(Encoding.UTF8.GetBytes(ociLayoutContent)))
        {
            PaxTarEntry layoutEntry = new(TarEntryType.RegularFile, ociLayoutPath)
            {
                DataStream = ociLayoutStream
            };
            await writer.WriteEntryAsync(layoutEntry, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WriteManifestForOciImage(
        TarWriter writer,
        BuiltImage image,
        DestinationImageReference destinationReference,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string manifestContent = JsonSerializer.SerializeToNode(image.Manifest)!.ToJsonString();
        string manifestDigest = image.Manifest.GetDigest();

        // 1. add manifest to blobs
        string manifestPath = $"{_blobsPath}/{manifestDigest.Substring("sha256:".Length)}";
        using (MemoryStream manifestStream = new MemoryStream(Encoding.UTF8.GetBytes(manifestContent)))
        {
            PaxTarEntry manifestEntry = new(TarEntryType.RegularFile, manifestPath)
            {
                DataStream = manifestStream
            };
            await writer.WriteEntryAsync(manifestEntry, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        // 2. add index.json
        var index = new ImageIndexV1
        {
            schemaVersion = 2,
            mediaType = SchemaTypes.OciImageIndexV1,
            manifests =
            [
                new PlatformSpecificOciManifest
                {
                    mediaType = SchemaTypes.OciManifestV1,
                    size = manifestContent.Length,
                    digest = manifestDigest,
                    annotations = new Dictionary<string, string> { { "org.opencontainers.image.ref.name", $"{destinationReference.Repository}:{destinationReference.Tags[0]}" } }
                }
            ]
        };
        using (MemoryStream indexStream = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.SerializeToNode(index)!.ToJsonString())))
        {
            PaxTarEntry indexEntry = new(TarEntryType.RegularFile, "index.json")
            {
                DataStream = indexStream
            };
            await writer.WriteEntryAsync(indexEntry, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<string?> GetCommandAsync(CancellationToken cancellationToken)
    {
        if (_command != null)
        {
            return _command;
        }

        // Try to find the docker or podman cli.
        // On systems with podman it's not uncommon for docker to be an alias to podman.
        // We have to attempt to locate both binaries and inspect the output of the 'docker' binary if present to determine
        // if it is actually podman.
        var podmanCommand = TryRunVersionCommandAsync(PodmanCommand, cancellationToken);
        var dockerCommand = TryRunVersionCommandAsync(DockerCommand, cancellationToken);

        await Task.WhenAll(
            podmanCommand,
            dockerCommand
        ).ConfigureAwait(false);

        // be explicit with this check so that we don't do the link target check unless it might actually be a solution.
        if (dockerCommand.Result && podmanCommand.Result && IsPodmanAlias())
        {
            _command = PodmanCommand;
        }
        else if (dockerCommand.Result)
        {
            _command = DockerCommand;
        }
        else if (podmanCommand.Result)
        {
            _command = PodmanCommand;
        }

        return _command;
    }
#endif

    private static bool IsPodmanAlias()
    {
        // If both exist we need to check and see if the docker command is actually docker,
        // or if it is a podman script in a trenchcoat.
        try
        {
            var dockerinfo = GetDockerConfig().RootElement;
            // Docker's info output has a 'DockerRootDir' top-level property string that is a good marker,
            // while Podman has a 'host' top-level property object with a 'buildahVersion' subproperty
            var hasdockerProperty =
                dockerinfo.TryGetProperty("DockerRootDir", out var dockerRootDir) && dockerRootDir.GetString() is not null;
            var hasPodmanProperty = dockerinfo.TryGetProperty("host", out var host) && host.TryGetProperty("buildahVersion", out var buildahVersion) && buildahVersion.GetString() is not null;
            return !hasdockerProperty && hasPodmanProperty;
        }
        catch
        {
            return false;
        }
    }

#if NET
    private async Task<bool> TryRunVersionCommandAsync(string command, CancellationToken cancellationToken)
    {
        try
        {
            ProcessStartInfo psi = new(command, "version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(psi)!;
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }
#endif

    public override string ToString()
    {
        return string.Format(Strings.DockerCli_PushInfo, _command);
    }
}
