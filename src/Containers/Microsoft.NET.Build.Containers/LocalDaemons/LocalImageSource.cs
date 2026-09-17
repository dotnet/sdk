// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Docker.DotNet;
using Microsoft.Extensions.Logging;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Reads image manifests and blobs exported from a local container runtime.
/// </summary>
internal sealed class LocalImageSource : IImageSource, IAsyncDisposable
{
    private readonly string _imageReference;
    private readonly ILogger _logger;
    private readonly DirectoryInfo _temporaryDirectory;
    private readonly string _extractionPath;
    private ManifestV2? _legacyManifest;
    private string? _legacyConfig;
    private readonly Dictionary<string, string> _legacyBlobPaths = new(StringComparer.Ordinal);

    private LocalImageSource(
        string imageReference,
        ILogger logger,
        DirectoryInfo temporaryDirectory,
        string extractionPath)
    {
        _imageReference = imageReference;
        _logger = logger;
        _temporaryDirectory = temporaryDirectory;
        _extractionPath = extractionPath;
    }

    internal static async Task<LocalImageSource> CreateAsync(
        string imageReference,
        string? localRegistry,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        using IDockerClient client = LocalDaemonClient.Create(localRegistry, loggerFactory, out ContainerRuntimeKind runtimeKind);
        Stream archive;
        try
        {
            archive = await client.Images.SaveImageAsync(imageReference, cancellationToken).ConfigureAwait(false);
        }
        catch (DockerApiException) when (
            runtimeKind == ContainerRuntimeKind.Podman &&
            GetPodmanLocalReference(imageReference) is { } podmanLocalReference)
        {
            archive = await client.Images.SaveImageAsync(podmanLocalReference, cancellationToken).ConfigureAwait(false);
        }

        await using (archive)
        {
            await using BufferedImageArchive bufferedArchive = await BufferedImageArchive.CreateAsync(archive, cancellationToken).ConfigureAwait(false);
            return await CreateFromArchiveAsync(imageReference, bufferedArchive.Content, loggerFactory, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static string? GetPodmanLocalReference(string imageReference)
    {
        const string dockerHubPrefix = "docker.io/";
        if (!imageReference.StartsWith(dockerHubPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        ReadOnlySpan<char> repositoryAndReference = imageReference.AsSpan(dockerHubPrefix.Length);
        const string officialImagesPrefix = "library/";
        if (repositoryAndReference.StartsWith(officialImagesPrefix, StringComparison.OrdinalIgnoreCase))
        {
            repositoryAndReference = repositoryAndReference[officialImagesPrefix.Length..];
        }

        return $"localhost/{repositoryAndReference}";
    }

    internal static async Task<LocalImageSource> CreateFromArchiveAsync(
        string imageReference,
        Stream archive,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        DirectoryInfo temporaryDirectory = Directory.CreateTempSubdirectory("dotnet-local-base-");
        string extractionPath = Path.Combine(temporaryDirectory.FullName, "image");
        ILogger logger = loggerFactory.CreateLogger<LocalImageSource>();
        LocalImageSource source = new(imageReference, logger, temporaryDirectory, extractionPath);

        try
        {
            Directory.CreateDirectory(extractionPath);
            await TarFile.ExtractToDirectoryAsync(archive, extractionPath, overwriteFiles: false, cancellationToken).ConfigureAwait(false);

            // Docker's containerd image store emits a hybrid archive. Its OCI index can retain
            // descriptors for platforms that are not present in the archive, while manifest.json
            // describes the exported local platform and its available layer paths.
            if (File.Exists(Path.Combine(extractionPath, "manifest.json")))
            {
                await source.ConvertDockerArchiveAsync(cancellationToken).ConfigureAwait(false);
            }
            else if (!File.Exists(Path.Combine(extractionPath, "oci-layout")))
            {
                throw new InvalidDataException("The exported image is neither a Docker archive nor an OCI image layout.");
            }

            return source;
        }
        catch
        {
            await source.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<ImageBuilder> GetImageManifestAsync(
        string repositoryName,
        string reference,
        string runtimeIdentifier,
        IManifestPicker manifestPicker,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_legacyManifest is not null && _legacyConfig is not null)
        {
            ImageConfig imageConfig = new(_legacyConfig);
            EnsurePlatformMatches(
                imageConfig,
                repositoryName,
                reference,
                runtimeIdentifier,
                manifestPicker);
            return new ImageBuilder(_legacyManifest, SchemaTypes.DockerManifestV2, imageConfig, _logger);
        }

        string indexJson = await File.ReadAllTextAsync(
            Path.Combine(_extractionPath, "index.json"),
            cancellationToken).ConfigureAwait(false);
        ArchiveIndex index = JsonSerializer.Deserialize<ArchiveIndex>(indexJson)
            ?? throw new InvalidDataException("The exported image index is invalid.");
        ArchiveDescriptor root = SelectRootDescriptor(index.Manifests);
        ArchiveDescriptor manifestDescriptor = await ResolveManifestDescriptorAsync(
            root,
            repositoryName,
            reference,
            runtimeIdentifier,
            manifestPicker,
            cancellationToken).ConfigureAwait(false);

        string manifestJson = await File.ReadAllTextAsync(
            GetOciBlobPath(manifestDescriptor.Digest),
            cancellationToken).ConfigureAwait(false);
        ManifestV2 manifest = JsonSerializer.Deserialize<ManifestV2>(manifestJson)
            ?? throw new InvalidDataException("The exported image manifest is invalid.");
        manifest.KnownDigest = manifestDescriptor.Digest;

        string configJson = await File.ReadAllTextAsync(
            GetOciBlobPath(manifest.Config.digest),
            cancellationToken).ConfigureAwait(false);
        return new ImageBuilder(
            manifest,
            manifest.MediaType ?? manifestDescriptor.MediaType,
            new ImageConfig(configJson),
            _logger);
    }

    public Task<string> GetBlobPathAsync(
        string repository,
        Descriptor descriptor,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string cachedPath = ContentStore.PathForDescriptor(descriptor);
        if (File.Exists(cachedPath))
        {
            using FileStream cachedBlob = File.OpenRead(cachedPath);
            string actualDigest = DigestUtils.FormatSha256Digest(Convert.ToHexStringLower(SHA256.HashData(cachedBlob)));
            if (actualDigest == descriptor.Digest)
            {
                return Task.FromResult(cachedPath);
            }
        }

        string path = _legacyManifest is null
            ? GetOciBlobPath(descriptor.Digest)
            : _legacyBlobPaths.TryGetValue(descriptor.Digest, out string? legacyPath)
                ? legacyPath
                : throw new FileNotFoundException($"Blob '{descriptor.Digest}' was not found in local image '{_imageReference}'.");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Blob '{descriptor.Digest}' was not found in local image '{_imageReference}'.", path);
        }

        return Task.FromResult(path);
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            _temporaryDirectory.Delete(recursive: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Failed to delete temporary local image directory '{TemporaryDirectory}': {Message}",
                _temporaryDirectory.FullName,
                ex.Message);
        }

        return ValueTask.CompletedTask;
    }

    private ArchiveDescriptor SelectRootDescriptor(ArchiveDescriptor[] descriptors)
    {
        if (descriptors.Length == 1)
        {
            return descriptors[0];
        }

        ArchiveDescriptor? matchingDescriptor = descriptors.FirstOrDefault(
            descriptor => descriptor.Annotations?.Values.Any(value =>
                value.Equals(_imageReference, StringComparison.OrdinalIgnoreCase)) == true);
        return matchingDescriptor
            ?? throw new InvalidDataException($"The exported archive does not contain image '{_imageReference}'.");
    }

    private async Task<ArchiveDescriptor> ResolveManifestDescriptorAsync(
        ArchiveDescriptor descriptor,
        string repositoryName,
        string reference,
        string runtimeIdentifier,
        IManifestPicker manifestPicker,
        CancellationToken cancellationToken)
    {
        if (descriptor.MediaType is SchemaTypes.DockerManifestV2 or SchemaTypes.OciManifestV1)
        {
            return descriptor;
        }

        if (descriptor.MediaType is not (SchemaTypes.DockerManifestListV2 or SchemaTypes.OciImageIndexV1))
        {
            throw new NotSupportedException($"The local image uses unsupported media type '{descriptor.MediaType}'.");
        }

        string indexJson = await File.ReadAllTextAsync(
            GetOciBlobPath(descriptor.Digest),
            cancellationToken).ConfigureAwait(false);
        ArchiveIndex index = JsonSerializer.Deserialize<ArchiveIndex>(indexJson)
            ?? throw new InvalidDataException("The exported image index is invalid.");

        ArchiveDescriptor selected;
        if (index.Manifests.Length == 1 && index.Manifests[0].Platform is null)
        {
            selected = index.Manifests[0];
        }
        else if (descriptor.MediaType == SchemaTypes.DockerManifestListV2)
        {
            PlatformSpecificManifest[] manifests = index.Manifests
                .Where(candidate => candidate.Platform is not null)
                .Select(candidate => new PlatformSpecificManifest(
                    candidate.MediaType,
                    candidate.Size,
                    candidate.Digest,
                    candidate.Platform!.Value))
                .ToArray();
            IReadOnlyDictionary<string, PlatformSpecificManifest> byRid = Registry.GetManifestsByRid(manifests);
            PlatformSpecificManifest match = manifestPicker.PickBestManifestForRid(byRid, runtimeIdentifier)
                ?? throw new BaseImageNotFoundException(runtimeIdentifier, repositoryName, reference, byRid.Keys);
            selected = index.Manifests.First(candidate => candidate.Digest == match.digest);
        }
        else
        {
            PlatformSpecificOciManifest[] manifests = index.Manifests
                .Where(candidate => candidate.Platform is not null)
                .Select(candidate => new PlatformSpecificOciManifest(
                    candidate.MediaType,
                    candidate.Size,
                    candidate.Digest,
                    candidate.Platform!.Value,
                    candidate.Annotations ?? new Dictionary<string, string>()))
                .ToArray();
            IReadOnlyDictionary<string, PlatformSpecificOciManifest> byRid = Registry.GetManifestsByRid(manifests);
            PlatformSpecificOciManifest match = manifestPicker.PickBestManifestForRid(byRid, runtimeIdentifier)
                ?? throw new BaseImageNotFoundException(runtimeIdentifier, repositoryName, reference, byRid.Keys);
            selected = index.Manifests.First(candidate => candidate.Digest == match.digest);
        }

        return await ResolveManifestDescriptorAsync(
            selected,
            repositoryName,
            reference,
            runtimeIdentifier,
            manifestPicker,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task ConvertDockerArchiveAsync(CancellationToken cancellationToken)
    {
        string manifestPath = Path.Combine(_extractionPath, "manifest.json");
        string manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        DockerArchiveManifest[] manifests = JsonSerializer.Deserialize<DockerArchiveManifest[]>(manifestJson)
            ?? throw new InvalidDataException("The exported Docker image manifest is invalid.");
        DockerArchiveManifest selected = manifests.Length == 1
            ? manifests[0]
            : manifests.FirstOrDefault(manifest => manifest.RepoTags.Contains(_imageReference, StringComparer.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"The exported archive does not contain image '{_imageReference}'.");

        string configPath = GetArchiveEntryPath(selected.Config);
        _legacyConfig = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
        string configDigest = ComputeFileDigest(configPath);
        List<ManifestLayer> layers = new(selected.Layers.Length);
        string convertedPath = Path.Combine(_extractionPath, "converted");
        Directory.CreateDirectory(convertedPath);

        foreach (string layerEntry in selected.Layers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string inputPath = GetArchiveEntryPath(layerEntry);
            string pendingPath = Path.Combine(convertedPath, $"{Guid.NewGuid():N}.tmp");

            await using (FileStream input = File.OpenRead(inputPath))
            await using (FileStream output = File.Create(pendingPath))
            {
                if (IsGzip(input))
                {
                    await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await using GZipStream gzip = new(output, CompressionLevel.SmallestSize, leaveOpen: true);
                    await input.CopyToAsync(gzip, cancellationToken).ConfigureAwait(false);
                }
            }

            string digest = ComputeFileDigest(pendingPath);
            string blobPath = Path.Combine(convertedPath, DigestUtils.GetEncoded(digest));
            File.Move(pendingPath, blobPath);
            long size = new FileInfo(blobPath).Length;
            _legacyBlobPaths.Add(digest, blobPath);
            layers.Add(new ManifestLayer(SchemaTypes.DockerLayerGzip, size, digest, urls: null));
        }

        _legacyManifest = new ManifestV2
        {
            SchemaVersion = 2,
            MediaType = SchemaTypes.DockerManifestV2,
            Config = new ManifestConfig(SchemaTypes.DockerContainerV1, new FileInfo(configPath).Length, configDigest),
            Layers = layers
        };
    }

    private void EnsurePlatformMatches(
        ImageConfig config,
        string repositoryName,
        string reference,
        string runtimeIdentifier,
        IManifestPicker manifestPicker)
    {
        PlatformInformation platform = new(config.Architecture, config.OS, variant: null, features: [], version: null);
        string? rid = Registry.CreateRidForPlatform(platform);
        if (rid is null)
        {
            throw new BaseImageNotFoundException(runtimeIdentifier, repositoryName, reference, []);
        }

        PlatformSpecificManifest manifest = new(
            SchemaTypes.DockerManifestV2,
            0,
            _legacyManifest!.GetDigest(),
            platform);
        Dictionary<string, PlatformSpecificManifest> manifests = new() { [rid] = manifest };
        if (manifestPicker.PickBestManifestForRid(manifests, runtimeIdentifier) is null)
        {
            throw new BaseImageNotFoundException(runtimeIdentifier, repositoryName, reference, manifests.Keys);
        }
    }

    private string GetOciBlobPath(string digest)
    {
        DigestUtils.ValidateDigest(digest);
        int separator = digest.IndexOf(':');
        return Path.Combine(_extractionPath, "blobs", digest[..separator], digest[(separator + 1)..]);
    }

    private string GetArchiveEntryPath(string entryPath)
    {
        string path = Path.GetFullPath(Path.Combine(_extractionPath, entryPath.Replace('/', Path.DirectorySeparatorChar)));
        string root = Path.EndsInDirectorySeparator(_extractionPath)
            ? _extractionPath
            : _extractionPath + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Archive entry '{entryPath}' is outside the image archive.");
        }
        return path;
    }

    private static bool IsGzip(Stream stream)
    {
        int first = stream.ReadByte();
        int second = stream.ReadByte();
        stream.Position = 0;
        return first == 0x1f && second == 0x8b;
    }

    private static string ComputeFileDigest(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return DigestUtils.FormatSha256Digest(Convert.ToHexStringLower(SHA256.HashData(stream)));
    }

    private sealed class ArchiveIndex
    {
        [JsonPropertyName("manifests")]
        public ArchiveDescriptor[] Manifests { get; init; } = [];
    }

    private sealed class ArchiveDescriptor
    {
        [JsonPropertyName("mediaType")]
        public string MediaType { get; init; } = string.Empty;

        [JsonPropertyName("digest")]
        public string Digest { get; init; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; init; }

        [JsonPropertyName("platform")]
        public PlatformInformation? Platform { get; init; }

        [JsonPropertyName("annotations")]
        public Dictionary<string, string>? Annotations { get; init; }
    }

    private sealed class DockerArchiveManifest
    {
        [JsonPropertyName("Config")]
        public string Config { get; init; } = string.Empty;

        [JsonPropertyName("RepoTags")]
        public string[] RepoTags { get; init; } = [];

        [JsonPropertyName("Layers")]
        public string[] Layers { get; init; } = [];
    }
}
