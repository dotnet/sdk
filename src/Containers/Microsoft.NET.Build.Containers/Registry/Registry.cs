// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Packaging;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;
using NuGet.RuntimeModel;
using System.Windows.Markup;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace Microsoft.NET.Build.Containers;

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
    private const int MaxDownloadRetries = 5;
    private readonly Func<TimeSpan> _retryDelayProvider;

    private readonly ILogger _logger;
    private readonly IRegistryAPI _registryAPI;
    private readonly RegistrySettings _settings;
    private readonly ContentStore _store;

    /// <summary>
    /// The name of the registry, which is the host name, optionally followed by a colon and the port number.
    /// This is used in user-facing error messages, and it should match what the user would manually enter as
    /// part of Docker commands like `docker login`.
    /// </summary>
    public string RegistryName { get; }

    internal Registry(string registryName, ILogger logger, IRegistryAPI registryAPI, RegistrySettings? settings = null, Func<TimeSpan>? retryDelayProvider = null, ContentStore? store = null) :
        this(new Uri($"https://{registryName}"), logger, registryAPI, settings, retryDelayProvider: retryDelayProvider, store: store)
    { }

    internal Registry(string registryName, ILogger logger, RegistryMode mode, RegistrySettings? settings = null, ContentStore? store = null) :
        this(new Uri($"https://{registryName}"), logger, new RegistryApiFactory(mode), settings, store: store)
    { }

    internal Registry(Uri baseUri, ILogger logger, IRegistryAPI registryAPI, RegistrySettings? settings = null, Func<TimeSpan>? retryDelayProvider = null, ContentStore? store = null) :
        this(baseUri, logger, new RegistryApiFactory(registryAPI), settings, retryDelayProvider: retryDelayProvider, store: store)
    { }

    internal Registry(Uri baseUri, ILogger logger, RegistryMode mode, RegistrySettings? settings = null, ContentStore? store = null) :
        this(baseUri, logger, new RegistryApiFactory(mode), settings, store: store)
    { }

    private Registry(Uri baseUri, ILogger logger, RegistryApiFactory factory, RegistrySettings? settings = null, Func<TimeSpan>? retryDelayProvider = null, ContentStore? store = null)
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

        _retryDelayProvider = retryDelayProvider ?? (() => TimeSpan.FromSeconds(1));
        _store = store ?? new ContentStore(new(Path.GetTempPath()));
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

    /// <summary>
    /// Fetches the data for a given manifest tag or digest from the local content store if present.
    /// If not present, fetches it from the remote and caches it in the local content store.
    /// </summary>
    /// <param name="repositoryName"></param>
    /// <param name="referenceOrDigest"></param>
    /// <param name="cTok"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task<IManifest> GetManifestCore(string repositoryName, string referenceOrDigest, CancellationToken cTok, bool skipCache = true)
    {
        cTok.ThrowIfCancellationRequested();
        // check if we have the reference in the ContentStore's reference area already.
        // if so, read it from there.
        var referencePath = _store.PathForManifestByReferenceOrDigest(RegistryName, repositoryName, referenceOrDigest);
        if (!skipCache && File.Exists(referencePath))
        {
            var lines = await File.ReadAllLinesAsync(referencePath, cTok);
            (var digest, var mediaType, var size) = (lines[0], lines[1], lines[2]);
            using var contentStream = File.OpenRead(_store.PathForDescriptor(new(mediaType, digest, long.Parse(size))));
            return await ParseManifest(mediaType, contentStream, digest);
        }
        // if not, make a remote call and add it to the ContentStore's reference area.
        else
        {
            using var response = await _registryAPI.Manifest.GetAsync(repositoryName, referenceOrDigest, cTok).ConfigureAwait(false);
            response.Headers.TryGetValues("Docker-Content-Digest", out var knownDigests);
            var digest = knownDigests?.FirstOrDefault()!;
            var mediaType = response.Content.Headers.ContentType?.MediaType!;
            long size = response.Content.Headers.ContentLength ?? 0;
            var descriptor = new Descriptor(mediaType, digest, size);
            // write the manifest contents to the durable store
            var storagePath = _store.PathForDescriptor(descriptor);
            // if the file already exists at this digest then we can skip the download
            if (File.Exists(storagePath))
            {
                using var fs = File.OpenRead(storagePath);
                return await ParseManifest(mediaType, fs, digest);
            }
            else
            {
                using var storageStream = File.OpenWrite(storagePath);
                using var responseStream = await response.Content.ReadAsStreamAsync(cTok);
                await responseStream.CopyToAsync(storageStream);
                responseStream.Position = 0;
                // write the marker file for the reference
                // IMPORTANT: must stay in sync with the lines read in the if block above
                var parentDir = Path.GetDirectoryName(referencePath)!;
                // we're creating a multi-level directory structure, so ensure the parent directories exist before writing
                Directory.CreateDirectory(parentDir);
                await File.WriteAllLinesAsync(referencePath, [
                    digest,
                mediaType,
                size.ToString()
                ], Encoding.UTF8, cTok);
                // now that the data is all set for next time, return the manifest
                return await ParseManifest(mediaType, responseStream, digest);
            }
        }

        async Task<IManifest> ParseManifest(string? mediaType, Stream content, string? digest)
        {
            IManifest? manifest = mediaType switch
            {
                SchemaTypes.DockerManifestV2 or SchemaTypes.OciManifestV1 => await JsonSerializer.DeserializeAsync<ManifestV2>(content, cancellationToken: cTok),
                SchemaTypes.DockerManifestListV2 => await JsonSerializer.DeserializeAsync<ManifestListV2>(content, cancellationToken: cTok),
                SchemaTypes.OciImageIndexV1 => await JsonSerializer.DeserializeAsync<ImageIndexV1>(content, cancellationToken: cTok),
                null => throw new ArgumentException($"No media type found for manifest {RegistryName}/{repositoryName}@{referenceOrDigest}"),
                _ => throw new ArgumentException($"Unknown manifest media type {mediaType}")
            };
            if (manifest is ManifestV2 v)
            {
                v.KnownDigest = digest;
                return v;
            }
            else
            {
                return manifest!;
            }
        }
    }

    public async Task<ImageBuilder> GetImageManifestAsync(string repositoryName, string reference, string runtimeIdentifier, IManifestPicker manifestPicker, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var manifest = await GetManifestCore(repositoryName, reference, cancellationToken);

        return manifest switch
        {
            ManifestV2 singleArchManifest => await ReadSingleImageAsync(repositoryName, singleArchManifest, cancellationToken),
            ManifestListV2 multiArchDockerManifest => await PickBestImageFromManifestListAsync(
                repositoryName,
                reference,
                multiArchDockerManifest,
                runtimeIdentifier,
                manifestPicker,
                cancellationToken).ConfigureAwait(false),
            ImageIndexV1 multiArchOciIndex =>
                await PickBestImageFromImageIndexAsync(
                repositoryName,
                reference,
                multiArchOciIndex,
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

    public async Task<JsonNode> GetJsonBlobCore(string repositoryName, string digest, string mediaType, CancellationToken cancellationToken)
    {
        // check if digest is available locally and serialize it, otherwise download from registry and store locally
        cancellationToken.ThrowIfCancellationRequested();
        var descriptor = new Descriptor(mediaType, digest, 0);
        var storagePath = _store.PathForDescriptor(descriptor);
        if (File.Exists(storagePath))
        {
            using var fs = File.OpenRead(storagePath);
            return (await JsonNode.ParseAsync(fs, cancellationToken: cancellationToken))!;
        }
        else
        {

            using var jsonStream = await _registryAPI.Blob.GetStreamAsync(repositoryName, digest, cancellationToken);
            using var fsStream = File.OpenWrite(storagePath);
            await jsonStream.CopyToAsync(fsStream, cancellationToken);
            // note: cannot just use the jsonStream here, as it may not be seekable
            fsStream.Position = 0;
            return (await JsonNode.ParseAsync(fsStream, cancellationToken: cancellationToken))!;
        }
    }

    private async Task<ImageBuilder> ReadSingleImageAsync(string repositoryName, ManifestV2 manifest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ManifestConfig config = manifest.Config;
        string configSha = config.digest;

        JsonNode configDoc = await GetJsonBlobCore(repositoryName, configSha, manifest.Config.mediaType, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        // ManifestV2.MediaType can be null, so we also provide manifest mediaType from http response
        return new ImageBuilder(manifest, manifest.MediaType ?? SchemaTypes.DockerManifestV2, new ImageConfig(configDoc), _logger);
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
        var ridManifestDict = RidMapping.GetManifestsByRid(manifestList.manifests);
        if (manifestPicker.PickBestManifestForRid(ridManifestDict, runtimeIdentifier) is PlatformSpecificManifest matchingManifest)
        {
            return await ReadSingleImageFromManifest(
                repositoryName,
                reference,
                matchingManifest.digest,
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
        var ridManifestDict = RidMapping.GetManifestsByRid(index.manifests);
        if (manifestPicker.PickBestManifestForRid(ridManifestDict, runtimeIdentifier) is PlatformSpecificOciManifest matchingManifest)
        {
            return await ReadSingleImageFromManifest(
                repositoryName,
                reference,
                matchingManifest.digest,
                runtimeIdentifier,
                ridManifestDict.Keys,
                cancellationToken);
        }
        else
        {
            throw new BaseImageNotFoundException(runtimeIdentifier, repositoryName, reference, ridManifestDict.Keys);
        }
    }

    /// <summary>
    /// Reads a manifest at the given digest, assuming it is a single-arch manifest
    /// </summary>
    /// <param name="repositoryName"></param>
    /// <param name="reference"></param>
    /// <param name="manifestDigest"></param>
    /// <param name="manifestMediaType"></param>
    /// <param name="runtimeIdentifier"></param>
    /// <param name="rids"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="BaseImageNotFoundException"></exception>
    private async Task<ImageBuilder> ReadSingleImageFromManifest(
        string repositoryName,
        string reference,
        string manifestDigest,
        string runtimeIdentifier,
        IEnumerable<string> rids,
        CancellationToken cancellationToken)
    {
        var manifest = await GetManifestCore(repositoryName, manifestDigest, cancellationToken);
        if (manifest is null) throw new BaseImageNotFoundException(runtimeIdentifier, repositoryName, reference, rids);
        if (manifest is not ManifestV2 singleArchManifest) throw new ArgumentException("Only supports single-arch manifests in this pathway");
        return await ReadSingleImageAsync(
            repositoryName,
            singleArchManifest,
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
        string localPath = _store.PathForDescriptor(descriptor);

        if (File.Exists(localPath))
        {
            // Assume file is up to date and just return it
            return localPath;
        }
    
        string tempTarballPath = _store.GetTempFile();
        
        _logger.LogInformation($"Downloading layer {descriptor.Digest} from {repository} to content store.");
        int retryCount = 0;
        while (retryCount < MaxDownloadRetries)
        {
            try
            {
                // No local copy, so download one
                using Stream responseStream = await _registryAPI.Blob.GetStreamAsync(repository, descriptor.Digest, cancellationToken).ConfigureAwait(false);
    
                using (FileStream fs = File.Create(tempTarballPath))
                {
                    await responseStream.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
                }
    
                // Break the loop if successful
                break;
            }
            catch (Exception ex)
            {
                retryCount++;
                if (retryCount >= MaxDownloadRetries)
                {
                    throw new UnableToDownloadFromRepositoryException(repository);
                }
    
                _logger.LogTrace("Download attempt {0}/{1} for repository '{2}' failed. Error: {3}", retryCount, MaxDownloadRetries, repository, ex.ToString());
    
                // Wait before retrying
                await Task.Delay(_retryDelayProvider(), cancellationToken).ConfigureAwait(false);   
            }
        }

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

    /// <summary>
    /// Uploads an opaque blob to the registry, checking for existence first.
    /// </summary>
    /// <param name="repository"></param>
    /// <param name="digest"></param>
    /// <param name="contents"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task UploadBlobAsync(string repository, string digest, Stream contents, CancellationToken cancellationToken)
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

    public async Task PushManifestListAsync(
        MultiArchImage multiArchImage,
        SourceImageReference sourceImageReference,
        DestinationImageReference destinationImageReference,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var tag in destinationImageReference.Tags)
        {
            _logger.LogInformation(Strings.Registry_TagUploadStarted, tag, RegistryName);
            await _registryAPI.Manifest.PutAsync(destinationImageReference.Repository, tag, multiArchImage.ImageIndex, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(Strings.Registry_TagUploaded, tag, RegistryName);
        }
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
                    await destinationRegistry.PushLayerAsync(Layer.FromDescriptor(descriptor, _store), destination.Repository, cancellationToken).ConfigureAwait(false);
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
        using (MemoryStream stringStream = new(Encoding.UTF8.GetBytes(builtImage.Config.ToJsonString())))
        {
            var configDigest = builtImage.Manifest.Config.digest;
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
                await _registryAPI.Manifest.PutAsync(destination.Repository, tag, builtImage.Manifest, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(Strings.Registry_TagUploaded, tag, RegistryName);
            }
        }
        else
        {
            _logger.LogInformation(Strings.Registry_ManifestUploadStarted, RegistryName, builtImage.ManifestDigest);
            await _registryAPI.Manifest.PutAsync(destination.Repository, builtImage.ManifestDigest, builtImage.Manifest, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(Strings.Registry_ManifestUploaded, RegistryName);
        }
    }

    public async Task UploadManifestAsync(string repository, string tagOrDigest, IManifest manifest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation(Strings.Registry_ManifestUploadStarted, RegistryName, tagOrDigest);
        await _registryAPI.Manifest.PutAsync(repository, tagOrDigest, manifest, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(Strings.Registry_ManifestUploaded, RegistryName);
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
