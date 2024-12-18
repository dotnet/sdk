// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;
using NuGet.RuntimeModel;

namespace Microsoft.NET.Build.Containers;

internal interface IManifestPicker
{
    public PlatformSpecificManifest? PickBestManifestForRid(IReadOnlyDictionary<string, PlatformSpecificManifest> manifestList, string runtimeIdentifier);
    public PlatformSpecificOciManifest? PickBestManifestForRid(IReadOnlyDictionary<string, PlatformSpecificOciManifest> manifestList, string runtimeIdentifier);
}

internal sealed class RidGraphManifestPicker : IManifestPicker
{
    private readonly RuntimeGraph _runtimeGraph;

    public RidGraphManifestPicker(string runtimeIdentifierGraphPath)
    {
        _runtimeGraph = GetRuntimeGraphForDotNet(runtimeIdentifierGraphPath);
    }
    public PlatformSpecificManifest? PickBestManifestForRid(IReadOnlyDictionary<string, PlatformSpecificManifest> ridManifestDict, string runtimeIdentifier)
    {
        var bestManifestRid = GetBestMatchingRid(_runtimeGraph, runtimeIdentifier, ridManifestDict.Keys);
        if (bestManifestRid is null)
        {
            return null;
        }
        return ridManifestDict[bestManifestRid];
    }

    public PlatformSpecificOciManifest? PickBestManifestForRid(IReadOnlyDictionary<string, PlatformSpecificOciManifest> ridManifestDict, string runtimeIdentifier)
    {
        var bestManifestRid = GetBestMatchingRid(_runtimeGraph, runtimeIdentifier, ridManifestDict.Keys);
        if (bestManifestRid is null)
        {
            return null;
        }
        return ridManifestDict[bestManifestRid];
    }

    private static string? GetBestMatchingRid(RuntimeGraph runtimeGraph, string runtimeIdentifier, IEnumerable<string> availableRuntimeIdentifiers)
    {
        HashSet<string> availableRids = new HashSet<string>(availableRuntimeIdentifiers, StringComparer.Ordinal);
        foreach (var candidateRuntimeIdentifier in runtimeGraph.ExpandRuntime(runtimeIdentifier))
        {
            if (availableRids.Contains(candidateRuntimeIdentifier))
            {
                return candidateRuntimeIdentifier;
            }
        }

        return null;
    }

    private static RuntimeGraph GetRuntimeGraphForDotNet(string ridGraphPath) => JsonRuntimeFormat.ReadRuntimeGraph(ridGraphPath);

}

internal enum RegistryMode
{
    Push,
    Pull,
    PullFromOutput
}

internal sealed class Registry
{
    private const string DockerHubRegistry1 = "registry-1.docker.io";
    private const string DockerHubRegistry2 = "registry.hub.docker.com";
    private static readonly int s_defaultChunkSizeBytes = 1024 * 64;

    private readonly ILogger _logger;
    private readonly IRegistryAPI _registryAPI;
    private readonly RegistrySettings _settings;

    /// <summary>
    /// The name of the registry, which is the host name, optionally followed by a colon and the port number.
    /// This is used in user-facing error messages, and it should match what the user would manually enter as
    /// part of Docker commands like `docker login`.
    /// </summary>
    public string RegistryName { get; }

    internal Registry(string registryName, ILogger logger, IRegistryAPI registryAPI, RegistrySettings? settings = null) :
        this(new Uri($"https://{registryName}"), logger, registryAPI, settings)
    { }

    internal Registry(string registryName, ILogger logger, RegistryMode mode, RegistrySettings? settings = null) :
        this(new Uri($"https://{registryName}"), logger, new RegistryApiFactory(mode), settings)
    { }


    internal Registry(Uri baseUri, ILogger logger, IRegistryAPI registryAPI, RegistrySettings? settings = null) :
        this(baseUri, logger, new RegistryApiFactory(registryAPI), settings)
    { }

    internal Registry(Uri baseUri, ILogger logger, RegistryMode mode, RegistrySettings? settings = null) :
        this(baseUri, logger, new RegistryApiFactory(mode), settings)
    { }

    private Registry(Uri baseUri, ILogger logger, RegistryApiFactory factory, RegistrySettings? settings = null)
    {
        RegistryName = DeriveRegistryName(baseUri);

        // "docker.io" is not a real registry. Replace the uri to refer to an actual registry.
        if (baseUri.Host == ContainerHelpers.DockerRegistryAlias)
        {
            baseUri = new UriBuilder(baseUri.ToString()) { Host = DockerHubRegistry1 }.Uri;
        }
        BaseUri = baseUri;

        _logger = logger;
        _settings = settings ?? new RegistrySettings(RegistryName);
        _registryAPI = factory.Create(RegistryName, BaseUri, logger, _settings.IsInsecure);
    }

    private static string DeriveRegistryName(Uri baseUri)
    {
        var port = baseUri.Port == -1 ? string.Empty : $":{baseUri.Port}";
        if (baseUri.OriginalString.EndsWith(port, ignoreCase: true, culture: null))
        {
            // the port was part of the original assignment, so it's ok to consider it part of the 'name'
            return baseUri.GetComponents(UriComponents.HostAndPort, UriFormat.Unescaped);
        }
        else
        {
            // the port was not part of the original assignment, so it's not part of the 'name'
            return baseUri.GetComponents(UriComponents.Host, UriFormat.Unescaped);
        }
    }

    public Uri BaseUri { get; }

    /// <summary>
    /// The max chunk size for patch blob uploads.
    /// </summary>
    /// <remarks>
    /// This varies by registry target, for example Amazon Elastic Container Registry requires 5MB chunks for all but the last chunk.
    /// </remarks>
    public int MaxChunkSizeBytes => _settings.ChunkedUploadSizeBytes.HasValue ? _settings.ChunkedUploadSizeBytes.Value : (IsAmazonECRRegistry ? 5248080 : s_defaultChunkSizeBytes);

    public bool IsAmazonECRRegistry => BaseUri.IsAmazonECRRegistry();

    /// <summary>
    /// Check to see if the registry is GitHub Packages, which always uses ghcr.io.
    /// </summary>
    public bool IsGithubPackageRegistry => RegistryName.StartsWith(RegistryConstants.GitHubPackageRegistryDomain, StringComparison.Ordinal);

    /// <summary>
    /// Is this registry the public Microsoft Container Registry.
    /// </summary>
    public bool IsMcr => RegistryName.Equals(RegistryConstants.MicrosoftContainerRegistryDomain, StringComparison.Ordinal);

    /// <summary>
    /// Check to see if the registry is Docker Hub, which uses two well-known domains.
    /// </summary>
    public bool IsDockerHub => RegistryName.Equals(ContainerHelpers.DockerRegistryAlias, StringComparison.Ordinal)
                            || RegistryName.Equals(DockerHubRegistry1, StringComparison.Ordinal)
                            || RegistryName.Equals(DockerHubRegistry2, StringComparison.Ordinal);

    /// <summary>
    /// Check to see if the registry is for Google Artifact Registry.
    /// </summary>
    /// <remarks>
    /// Google Artifact Registry locations (one for each availability zone) are of the form "ZONE-docker.pkg.dev".
    /// </remarks>
    public bool IsGoogleArtifactRegistry
    {
        get => RegistryName.EndsWith("-docker.pkg.dev", StringComparison.Ordinal);
    }

    public bool IsAzureContainerRegistry => RegistryName.EndsWith(".azurecr.io", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Pushing to ECR uses a much larger chunk size. To avoid getting too many socket disconnects trying to do too many
    /// parallel uploads be more conservative and upload one layer at a time.
    /// </summary>
    private bool SupportsParallelUploads => !IsAmazonECRRegistry && _settings.ParallelUploadEnabled;

    public async Task<ImageBuilder> GetImageManifestAsync(string repositoryName, string reference, string runtimeIdentifier, IManifestPicker manifestPicker, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using HttpResponseMessage initialManifestResponse = await _registryAPI.Manifest.GetAsync(repositoryName, reference, cancellationToken).ConfigureAwait(false);

        return initialManifestResponse.Content.Headers.ContentType?.MediaType switch
        {
            SchemaTypes.DockerManifestV2 or SchemaTypes.OciManifestV1 => await ReadSingleImageAsync(
                repositoryName,
                await ReadManifest().ConfigureAwait(false),
                initialManifestResponse.Content.Headers.ContentType.MediaType,
                cancellationToken).ConfigureAwait(false),
            SchemaTypes.DockerManifestListV2 => await PickBestImageFromManifestListAsync(
                repositoryName,
                reference,
                await initialManifestResponse.Content.ReadFromJsonAsync<ManifestListV2>(cancellationToken: cancellationToken).ConfigureAwait(false),
                runtimeIdentifier,
                manifestPicker,
                cancellationToken).ConfigureAwait(false),
            SchemaTypes.OciImageIndexV1 =>
                await PickBestImageFromImageIndexAsync(
                repositoryName,
                reference,
                await initialManifestResponse.Content.ReadFromJsonAsync<ImageIndexV1>(cancellationToken: cancellationToken).ConfigureAwait(false),
                runtimeIdentifier,
                manifestPicker,
                cancellationToken).ConfigureAwait(false),
            var unknownMediaType => throw new NotImplementedException(Resource.FormatString(
                nameof(Strings.UnknownMediaType),
                repositoryName,
                reference,
                BaseUri,
                unknownMediaType))
        };

        async Task<ManifestV2> ReadManifest()
        {
            initialManifestResponse.Headers.TryGetValues("Docker-Content-Digest", out var knownDigest);
            var manifest = (await initialManifestResponse.Content.ReadFromJsonAsync<ManifestV2>(cancellationToken: cancellationToken).ConfigureAwait(false))!;
            if (knownDigest?.FirstOrDefault() is string knownDigestValue)
            {
                manifest.KnownDigest = knownDigestValue;
            }
            return manifest;
        }
    }

    internal async Task<ManifestListV2?> GetManifestListAsync(string repositoryName, string reference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using HttpResponseMessage initialManifestResponse = await _registryAPI.Manifest.GetAsync(repositoryName, reference, cancellationToken).ConfigureAwait(false);

        return initialManifestResponse.Content.Headers.ContentType?.MediaType switch
        {
            SchemaTypes.DockerManifestListV2 => await initialManifestResponse.Content.ReadFromJsonAsync<ManifestListV2>(cancellationToken: cancellationToken).ConfigureAwait(false),
            _ => null
        };
    }

    private async Task<ImageBuilder> ReadSingleImageAsync(string repositoryName, ManifestV2 manifest, string manifestMediaType, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ManifestConfig config = manifest.Config;
        string configSha = config.digest;

        JsonNode configDoc = await _registryAPI.Blob.GetJsonAsync(repositoryName, configSha, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        // ManifestV2.MediaType can be null, so we also provide manifest mediaType from http response
        return new ImageBuilder(manifest, manifest.MediaType ?? manifestMediaType, new ImageConfig(configDoc), _logger);
    }


    private static IReadOnlyDictionary<string, PlatformSpecificManifest> GetManifestsByRid(PlatformSpecificManifest[] manifestList)
    {
        var ridDict = new Dictionary<string, PlatformSpecificManifest>();
        foreach (var manifest in manifestList)
        {
            if (CreateRidForPlatform(manifest.platform) is { } rid)
            {
                ridDict.TryAdd(rid, manifest);
            }
        }

        return ridDict;
    }

    private static IReadOnlyDictionary<string, PlatformSpecificOciManifest> GetManifestsByRid(PlatformSpecificOciManifest[] manifestList)
    {
        var ridDict = new Dictionary<string, PlatformSpecificOciManifest>();
        foreach (var manifest in manifestList)
        {
            if (CreateRidForPlatform(manifest.platform) is { } rid)
            {
                ridDict.TryAdd(rid, manifest);
            }
        }

        return ridDict;
    }

    private static string? CreateRidForPlatform(PlatformInformation platform)
    {
        // we only support linux and windows containers explicitly, so anything else we should skip past.
        var osPart = platform.os switch
        {
            "linux" => "linux",
            "windows" => "win",
            _ => null
        };
        // TODO: this part needs a lot of work, the RID graph isn't super precise here and version numbers (especially on windows) are _whack_
        // TODO: we _may_ need OS-specific version parsing. Need to do more research on what the field looks like across more manifest lists.
        var versionPart = platform.version?.Split('.') switch
        {
        [var major, ..] => major,
            _ => null
        };
        var platformPart = platform.architecture switch
        {
            "amd64" => "x64",
            "x386" => "x86",
            "arm" => $"arm{(platform.variant != "v7" ? platform.variant : "")}",
            "arm64" => "arm64",
            "ppc64le" => "ppc64le",
            "s390x" => "s390x",
            "riscv64" => "riscv64",
            "loongarch64" => "loongarch64",
            _ => null
        };

        if (osPart is null || platformPart is null) return null;
        return $"{osPart}{versionPart ?? ""}-{platformPart}";
    }


    private async Task<ImageBuilder> PickBestImageFromManifestListAsync(
        string repositoryName,
        string reference,
        ManifestListV2 manifestList,
        string runtimeIdentifier,
        IManifestPicker manifestPicker,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ridManifestDict = GetManifestsByRid(manifestList.manifests);
        if (manifestPicker.PickBestManifestForRid(ridManifestDict, runtimeIdentifier) is PlatformSpecificManifest matchingManifest)
        {
            return await ReadImageFromManifest(
                repositoryName,
                reference,
                matchingManifest.digest,
                matchingManifest.mediaType,
                runtimeIdentifier,
                ridManifestDict.Keys,
                cancellationToken);
        }
        else
        {
            throw new BaseImageNotFoundException(runtimeIdentifier, repositoryName, reference, ridManifestDict.Keys);
        }
    }

    private async Task<ImageBuilder> PickBestImageFromImageIndexAsync(
        string repositoryName,
        string reference,
        ImageIndexV1 index,
        string runtimeIdentifier,
        IManifestPicker manifestPicker,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ridManifestDict = GetManifestsByRid(index.manifests);
        if (manifestPicker.PickBestManifestForRid(ridManifestDict, runtimeIdentifier) is PlatformSpecificOciManifest matchingManifest)
        {
            return await ReadImageFromManifest(
                repositoryName,
                reference,
                matchingManifest.digest,
                matchingManifest.mediaType,
                runtimeIdentifier,
                ridManifestDict.Keys,
                cancellationToken);
        }
        else
        {
            throw new BaseImageNotFoundException(runtimeIdentifier, repositoryName, reference, ridManifestDict.Keys);
        }
    }

    private async Task<ImageBuilder> ReadImageFromManifest(
        string repositoryName,
        string reference,
        string manifestDigest,
        string mediaType,
        string runtimeIdentifier,
        IEnumerable<string> rids,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage manifestResponse = await _registryAPI.Manifest.GetAsync(repositoryName, manifestDigest, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        var manifest = await manifestResponse.Content.ReadFromJsonAsync<ManifestV2>(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (manifest is null) throw new BaseImageNotFoundException(runtimeIdentifier, repositoryName, reference, rids);
        manifest.KnownDigest = manifestDigest;
        return await ReadSingleImageAsync(
            repositoryName,
            manifest,
            mediaType,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Ensure a blob associated with <paramref name="repository"/> from the registry is available locally.
    /// </summary>
    /// <param name="repository">Name of the associated image repository.</param>
    /// <param name="descriptor"><see cref="Descriptor"/> that describes the blob.</param>
    /// <returns>Local path to the (decompressed) blob content.</returns>
    public async Task<string> DownloadBlobAsync(string repository, Descriptor descriptor, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string localPath = ContentStore.PathForDescriptor(descriptor);

        if (File.Exists(localPath))
        {
            // Assume file is up to date and just return it
            return localPath;
        }

        // No local copy, so download one
        using Stream responseStream = await _registryAPI.Blob.GetStreamAsync(repository, descriptor.Digest, cancellationToken).ConfigureAwait(false);

        string tempTarballPath = ContentStore.GetTempFile();
        using (FileStream fs = File.Create(tempTarballPath))
        {
            await responseStream.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        File.Move(tempTarballPath, localPath, overwrite: true);

        return localPath;
    }

    internal async Task PushLayerAsync(Layer layer, string repository, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string digest = layer.Descriptor.Digest;

        using (Stream contents = layer.OpenBackingFile())
        {
            await UploadBlobAsync(repository, digest, contents, cancellationToken).ConfigureAwait(false);
        }
    }

    internal async Task<FinalizeUploadInformation> UploadBlobChunkedAsync(Stream contents, StartUploadInformation startUploadInformation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Uri patchUri = startUploadInformation.UploadUri;

        // TODO: this chunking is super tiny and probably not necessary; what does the docker client do
        //       and can we be smarter?

        byte[] chunkBackingStore = new byte[MaxChunkSizeBytes];

        int chunkCount = 0;
        int chunkStart = 0;

        _logger.LogTrace("Uploading {0} bytes of content in chunks of {1} bytes.", contents.Length, chunkBackingStore.Length);

        while (contents.Position < contents.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogTrace("Processing next chunk because current position {0} < content size {1}, chunk size: {2}.", contents.Position, contents.Length, chunkBackingStore.Length);

            int bytesRead = await contents.ReadAsync(chunkBackingStore, cancellationToken).ConfigureAwait(false);

            ByteArrayContent content = new(chunkBackingStore, offset: 0, count: bytesRead);
            content.Headers.ContentLength = bytesRead;

            // manual because ACR throws an error with the .NET type {"Range":"bytes 0-84521/*","Reason":"the Content-Range header format is invalid"}
            //    content.Headers.Add("Content-Range", $"0-{contents.Length - 1}");
            Debug.Assert(content.Headers.TryAddWithoutValidation("Content-Range", $"{chunkStart}-{chunkStart + bytesRead - 1}"));

            NextChunkUploadInformation nextChunk = await _registryAPI.Blob.Upload.UploadChunkAsync(patchUri, content, cancellationToken).ConfigureAwait(false);
            patchUri = nextChunk.UploadUri;

            chunkCount += 1;
            chunkStart += bytesRead;
        }
        return new(patchUri);
    }

    private Task<FinalizeUploadInformation> UploadBlobContentsAsync(Stream contents, StartUploadInformation startUploadInformation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_settings.ForceChunkedUpload)
        {
            //the chunked upload was forced in configuration
            _logger.LogTrace("Chunked upload is forced in configuration, attempting to upload blob in chunks. Content length: {0}.", contents.Length);
            return UploadBlobChunkedAsync(contents, startUploadInformation, cancellationToken);
        }

        try
        {
            _logger.LogTrace("Attempting to upload whole blob, content length: {0}.", contents.Length);
            return _registryAPI.Blob.Upload.UploadAtomicallyAsync(startUploadInformation.UploadUri, contents, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogTrace("Errored while uploading whole blob: {0}.\nRetrying with chunked upload. Content length: {1}.", ex, contents.Length);
            contents.Seek(0, SeekOrigin.Begin);
            return UploadBlobChunkedAsync(contents, startUploadInformation, cancellationToken);
        }
    }

    private async Task UploadBlobAsync(string repository, string digest, Stream contents, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (await _registryAPI.Blob.ExistsAsync(repository, digest, cancellationToken).ConfigureAwait(false))
        {
            // Already there!
            _logger.LogInformation(Strings.Registry_LayerExists, digest);
            return;
        }

        // Three steps to this process:
        // * start an upload session
        StartUploadInformation uploadUri = await _registryAPI.Blob.Upload.StartAsync(repository, cancellationToken).ConfigureAwait(false);
        _logger.LogTrace("Started upload session for {0}", digest);

        // * upload the blob
        cancellationToken.ThrowIfCancellationRequested();
        FinalizeUploadInformation finalChunkUri = await UploadBlobContentsAsync(contents, uploadUri, cancellationToken).ConfigureAwait(false);
        _logger.LogTrace("Uploaded content for {0}", digest);
        // * finish the upload session
        cancellationToken.ThrowIfCancellationRequested();
        await _registryAPI.Blob.Upload.CompleteAsync(finalChunkUri.UploadUri, digest, cancellationToken).ConfigureAwait(false);
        _logger.LogTrace("Finalized upload session for {0}", digest);

    }

    public Task PushAsync(BuiltImage builtImage, SourceImageReference source, DestinationImageReference destination, CancellationToken cancellationToken)
        => PushAsync(builtImage, source, destination, pushTags: true, cancellationToken);

    private async Task PushAsync(BuiltImage builtImage, SourceImageReference source, DestinationImageReference destination, bool pushTags, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Registry destinationRegistry = destination.RemoteRegistry!;

        Func<Descriptor, Task> uploadLayerFunc = async (descriptor) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            string digest = descriptor.Digest;

            _logger.LogInformation(Strings.Registry_LayerUploadStarted, digest, destinationRegistry.RegistryName);
            if (await _registryAPI.Blob.ExistsAsync(destination.Repository, digest, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogInformation(Strings.Registry_LayerExists, digest);
                return;
            }

            // Blob wasn't there; can we tell the server to get it from the base image?
            if (!await _registryAPI.Blob.Upload.TryMountAsync(destination.Repository, source.Repository, digest, cancellationToken).ConfigureAwait(false))
            {
                // The blob wasn't already available in another namespace, so fall back to explicitly uploading it

                if (source.Registry is { } sourceRegistry)
                {
                    // Ensure the blob is available locally
                    await sourceRegistry.DownloadBlobAsync(source.Repository, descriptor, cancellationToken).ConfigureAwait(false);
                    // Then push it to the destination registry
                    await destinationRegistry.PushLayerAsync(Layer.FromDescriptor(descriptor), destination.Repository, cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation(Strings.Registry_LayerUploaded, digest, destinationRegistry.RegistryName);
                }
                else
                {
                    throw new NotImplementedException(Resource.GetString(nameof(Strings.MissingLinkToRegistry)));
                }
            }
        };

        if (SupportsParallelUploads)
        {
            await Task.WhenAll(builtImage.LayerDescriptors.Select(descriptor => uploadLayerFunc(descriptor))).ConfigureAwait(false);
        }
        else
        {
            foreach (var descriptor in builtImage.LayerDescriptors)
            {
                await uploadLayerFunc(descriptor).ConfigureAwait(false);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        using (MemoryStream stringStream = new(Encoding.UTF8.GetBytes(builtImage.Config)))
        {
            var configDigest = builtImage.ImageDigest;
            _logger.LogInformation(Strings.Registry_ConfigUploadStarted, configDigest);
            await UploadBlobAsync(destination.Repository, configDigest, stringStream, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(Strings.Registry_ConfigUploaded);
        }

        // Tags can refer to an image manifest or an image manifest list.
        // In the first case, we push tags to the registry.
        // In the second case, we push the manifest digest so the manifest list can refer to it.
        if (pushTags)
        {
            Debug.Assert(destination.Tags.Length > 0);
            foreach (string tag in destination.Tags)
            {
                _logger.LogInformation(Strings.Registry_TagUploadStarted, tag, RegistryName);
                await _registryAPI.Manifest.PutAsync(destination.Repository, tag, builtImage.Manifest, builtImage.ManifestMediaType, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(Strings.Registry_TagUploaded, tag, RegistryName);
            }
        }
        else
        {
            string manifestDigest = builtImage.Manifest.GetDigest();
            _logger.LogInformation(Strings.Registry_ManifestUploadStarted, RegistryName, manifestDigest);
            await _registryAPI.Manifest.PutAsync(destination.Repository, manifestDigest, builtImage.Manifest, builtImage.ManifestMediaType, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(Strings.Registry_ManifestUploaded, RegistryName);
        }
    }

    private readonly ref struct RegistryApiFactory
    {
        private readonly IRegistryAPI? _registryApi;
        private readonly RegistryMode? _mode;
        public RegistryApiFactory(IRegistryAPI registryApi)
        {
            _registryApi = registryApi;
        }

        public RegistryApiFactory(RegistryMode mode)
        {
            _mode = mode;
        }

        public IRegistryAPI Create(string registryName, Uri baseUri, ILogger logger, bool isInsecureRegistry)
        {
            return _registryApi ?? new DefaultRegistryAPI(registryName, baseUri, isInsecureRegistry, logger, _mode!.Value);
        }
    }
}
