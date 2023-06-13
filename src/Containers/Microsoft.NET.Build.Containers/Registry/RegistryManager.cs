// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.NET.Build.Containers.Resources;
using NuGet.RuntimeModel;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.NET.Build.Containers.Registry;

internal sealed class RegistryManager
{
    /// <summary>
    /// When chunking is enabled, allows explicit control over the size of the chunks uploaded
    /// </summary>
    /// <remarks>
    /// Our default of 64KB is very conservative, so raising this to 1MB or more can speed up layer uploads reasonably well.
    /// </remarks>
    private static readonly int? s_chunkedUploadSizeBytes = Env.GetEnvironmentVariableAsNullableInt(ContainerHelpers.ChunkedUploadSizeBytes);

    private static readonly int s_defaultChunkSizeBytes = 1024 * 64;

    private static readonly int s_fiveMegs = 5_242_880;

    /// <summary>
    /// Whether we should upload blobs in parallel (enabled by default, but disabled for certain registries in conjunction with the explicit support check below).
    /// </summary>
    /// <remarks>
    /// Enabling this can swamp some registries, so this is an escape hatch.
    /// </remarks>
    private static readonly bool s_parallelUploadEnabled = Env.GetEnvironmentVariableAsBool(ContainerHelpers.ParallelUploadEnabled, defaultValue: true);
    private readonly ILogger _logger;

    public RegistryManager(Uri baseUri, ILogger? logger = null)
    {
        BaseUri = baseUri;
        _logger = logger ?? (ILogger)NullLogger.Instance;
        RegistryName = DeriveRegistryName(baseUri);
        API = new DefaultRegistryAPI(BaseUri, _logger);
    }

    /// <summary>
    /// Creates a new registry instance with the specified base URI and API for testing purposes
    /// </summary>
    internal RegistryManager(Uri baseUri, IRegistryAPI api, ILogger? logger = null)
    {
        BaseUri = baseUri;
        _logger = logger ?? (ILogger)NullLogger.Instance;
        RegistryName = DeriveRegistryName(baseUri);
        API = api;
    }

    public Uri BaseUri { get; }

    /// <summary>
    /// Check to see if the registry is for Amazon Elastic Container Registry (ECR).
    /// </summary>
    public bool IsAmazonECRRegistry
    {
        get => BaseUri.IsAmazonECRRegistry();
    }

    /// <summary>
    /// Check to see if the registry is Docker Hub, which uses two well-known domains.
    /// </summary>
    public bool IsDockerHub => RegistryName.Equals("registry-1.docker.io", StringComparison.Ordinal) || RegistryName.Equals("registry.hub.docker.com", StringComparison.Ordinal);

    /// <summary>
    /// Check to see if the registry is GitHub Packages, which always uses ghcr.io.
    /// </summary>
    public bool IsGithubPackageRegistry => RegistryName.StartsWith("ghcr.io", StringComparison.Ordinal);

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

    /// <summary>
    /// The name of the registry, which is the host name, optionally followed by a colon and the port number.
    /// This is used in user-facing error messages, and it should match what the user would manually enter as
    /// part of Docker commands like `docker login`.
    /// </summary>
    public string RegistryName { get; init; }

    private IRegistryAPI API { get; }

    /// <summary>
    /// Pushing to ECR uses a much larger chunk size. To avoid getting too many socket disconnects trying to do too many
    /// parallel uploads be more conservative and upload one layer at a time.
    /// </summary>
    private bool SupportsParallelUploads => !IsAmazonECRRegistry && s_parallelUploadEnabled;

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
        var response = await API.Blob.GetAsync(repository, descriptor.Digest, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        response.EnsureSuccessStatusCode();

        string tempTarballPath = ContentStore.GetTempFile();
        using (FileStream fs = File.Create(tempTarballPath))
        {
            using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            await responseStream.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        File.Move(tempTarballPath, localPath, overwrite: true);

        return localPath;
    }

    public async Task<ImageBuilder> GetImageManifestAsync(string repositoryName, string reference, string runtimeIdentifier, string runtimeIdentifierGraphPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var initialManifestResponse = await API.Manifest.GetAsync(repositoryName, reference, cancellationToken).ConfigureAwait(false);

        return initialManifestResponse.Content.Headers.ContentType?.MediaType switch
        {
            SchemaTypes.DockerManifestV2 => await ReadSingleImageAsync(
                repositoryName,
                await initialManifestResponse.Content.ReadFromJsonAsync<ManifestV2>(cancellationToken: cancellationToken).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false),
            SchemaTypes.DockerManifestListV2 => await PickBestImageFromManifestListAsync(
                repositoryName,
                reference,
                await initialManifestResponse.Content.ReadFromJsonAsync<ManifestListV2>(cancellationToken: cancellationToken).ConfigureAwait(false),
                runtimeIdentifier,
                runtimeIdentifierGraphPath,
                cancellationToken).ConfigureAwait(false),
            var unknownMediaType => throw new NotImplementedException(Resource.FormatString(
                nameof(Strings.UnknownMediaType),
                repositoryName,
                reference,
                BaseUri,
                unknownMediaType))
        };
    }

    public async Task PushAsync(Layer layer, string repository, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string digest = layer.Descriptor.Digest;

        using (Stream contents = layer.OpenBackingFile())
        {
            await UploadBlobAsync(repository, digest, contents, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Pushes a built image to a destination, attempting to mount the layer from the source if possible.
    /// Otherwise we push the image layers, manifest, and config locally using atomic or chunked uploads.
    /// </summary>
    public async Task PushAsync(BuiltImage builtImage, ImageReference source, ImageReference destination, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RegistryManager destinationRegistry = destination.Registry!;

        await PushLayers().ConfigureAwait(false);
        await PushConfig().ConfigureAwait(false);
        await PushManifest().ConfigureAwait(false);


        async Task PushLayers()
        {
            Func<Descriptor, Task> uploadLayerFunc = async (descriptor) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            string digest = descriptor.Digest;

            _logger.LogInformation("Uploading layer {0} to {1}", digest, destinationRegistry.RegistryName);
            if (await API.Blob.ExistsAsync(destination.Repository, digest, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogInformation("Layer {0} already existed", digest);
                return;
            }

            if (!(await API.Blob.Upload.TryMountAsync(destination.Repository, source.Repository, digest, cancellationToken)))
            {
                // The blob wasn't already available in another namespace, so fall back to explicitly uploading it
                if (source.Registry is { } sourceRegistry)
                {
                    // Ensure the blob is available locally
                    await sourceRegistry.DownloadBlobAsync(source.Repository, descriptor, cancellationToken).ConfigureAwait(false);
                    // Then push it to the destination registry
                    await destinationRegistry.PushAsync(Layer.FromDescriptor(descriptor), destination.Repository, cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("Finished uploading layer {0} to {1}", digest, destinationRegistry.RegistryName);
                }
                else
                {
                    throw new NotImplementedException(Resource.GetString(nameof(Strings.MissingLinkToRegistry)));
                }
            }
        };

            if (SupportsParallelUploads)
            {
                await Task.WhenAll(builtImage.LayerDescriptors.Select(uploadLayerFunc)).ConfigureAwait(false);
            }
            else
            {
                foreach (var descriptor in builtImage.LayerDescriptors)
                {
                    await uploadLayerFunc(descriptor).ConfigureAwait(false);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        async Task PushConfig()
        {
            using (MemoryStream stringStream = new MemoryStream(Encoding.UTF8.GetBytes(builtImage.Config)))
            {
                var configDigest = builtImage.ImageDigest;
                _logger.LogInformation("Uploading config to registry at blob {0}", configDigest);
                await UploadBlobAsync(destination.Repository, configDigest, stringStream, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Uploaded config to registry");
            }
            cancellationToken.ThrowIfCancellationRequested();
        }

        async Task PushManifest()
        {
            var manifestDigest = builtImage.Manifest.GetDigest();
            _logger.LogInformation("Uploading manifest to registry {0} as blob {1}", RegistryName, manifestDigest);
            string manifestJson = JsonSerializer.SerializeToNode(builtImage.Manifest)?.ToJsonString() ?? "";
            StringContent manifestContent = new(manifestJson);
            manifestContent.Headers.ContentType = new MediaTypeHeaderValue(SchemaTypes.DockerManifestV2);

            var putResponse = await API.Manifest.PutAsync(destination.Repository, manifestDigest, manifestContent, cancellationToken).ConfigureAwait(false);

            if (!putResponse.IsSuccessStatusCode)
            {
                throw new ContainerHttpException(Resource.GetString(nameof(Strings.RegistryPushFailed)), putResponse.RequestMessage?.RequestUri?.ToString(), manifestJson);
            }
            _logger.LogInformation("Uploaded manifest to {0}", RegistryName);

            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Uploading tag {0} to {1}", destination.Tag, RegistryName);
            var putTagResponse = await API.Manifest.PutAsync(destination.Repository, destination.Tag, manifestContent, cancellationToken).ConfigureAwait(false);

            if (!putTagResponse.IsSuccessStatusCode)
            {
                throw new ContainerHttpException(Resource.GetString(nameof(Strings.RegistryPushFailed)), putTagResponse.RequestMessage?.RequestUri?.ToString(), manifestJson);
            }

            _logger.LogInformation("Uploaded tag {0} to {1}", destination.Tag, RegistryName);
        }
    }

    /// <summary>
    /// Upload a blob to the registry using chunks. The chunks are uploaded sequentially, and the size of the chunks is determined by the UploadInformation derived from the 'Start' call
    /// </summary>
    internal async Task<FinalizeUploadInformation> UploadBlobChunkedAsync(Stream contents, StartUploadInformation uploadInfo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Uri patchUri = uploadInfo.UploadUri;
        contents.Seek(0, SeekOrigin.Begin);

        byte[] chunkBackingStore = new byte[EffectiveChunkSize(uploadInfo.RegistryDeclaredChunkSize, IsAmazonECRRegistry)];

        int chunkCount = 0;
        int chunkStart = 0;
        // chunked upload retries inspired from regclient -
        // we allow up to N retries across the whole operation, and
        // successes 'reclaim' retry attempts for future failures.
        int retryMax = 10;
        int retryCount = 0;

        _logger.LogTrace("Uploading {0} bytes of content in chunks of {1} bytes.", contents.Length, chunkBackingStore.Length);

        while (contents.Position < contents.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogTrace("Processing next chunk because current position {0} < content size {1}, chunk size: {2}.", contents.Position, contents.Length, chunkBackingStore.Length);

            var (content, bytesRead) = await ReadChunk().ConfigureAwait(false);

            _logger.LogTrace("Uploading {0} bytes of content at {1}", bytesRead, patchUri);

            HttpResponseMessage patchResponse = await API.Blob.Upload.UploadChunkAsync(patchUri, content, cancellationToken).ConfigureAwait(false);

            _logger.LogTrace("Received status code '{0}' from upload.", patchResponse.StatusCode);

            // central state machine -
            // successes move to the next chunk,
            // failures are retried up to retryMax times,
            // or else we fail the whole operation.
            (patchResponse.StatusCode switch
            {
                HttpStatusCode.Accepted => ProcessSuccessfulChunk(patchResponse),
                // known quirk - AWS ECR sends 201 Created instead of 202 Accepted
                HttpStatusCode.Created when IsAmazonECRRegistry => ProcessSuccessfulChunk(patchResponse),
                _ when retryCount < retryMax => await CheckReadAmountAndRetry(patchUri, cancellationToken).ConfigureAwait(false),
                _ => await LogError(patchResponse, patchUri, cancellationToken).ConfigureAwait(false)
            })();

            // handles all the logic for reading a new chunk from the stream, including setting content type.
            async Task<(ByteArrayContent, int)> ReadChunk()
            {
                int bytesRead = await contents.ReadAsync(chunkBackingStore, cancellationToken).ConfigureAwait(false);
                ByteArrayContent content = new(chunkBackingStore, offset: 0, count: bytesRead);
                content.Headers.ContentLength = bytesRead;

                _logger.LogTrace("Read {0} bytes from content.", bytesRead);

                // manual because ACR throws an error with the .NET type {"Range":"bytes 0-84521/*","Reason":"the Content-Range header format is invalid"}
                //    content.Headers.Add("Content-Range", $"0-{contents.Length - 1}");
                Debug.Assert(content.Headers.TryAddWithoutValidation("Content-Range", $"{chunkStart}-{chunkStart + bytesRead - 1}"));

                return new(content, bytesRead);
            }

            // for a successful chunk, we decrement retries (if any) and read the amount written by the server + next location
            Action ProcessSuccessfulChunk(HttpResponseMessage response) => () =>
            {
                if (retryCount > 0)
                {
                    retryCount -= 1;
                }
                chunkCount += 1;
                UpdateStateFromResponse(response);
            };

            // responses can tell us a) where to upload the next chunk, and b) how much of the last chunk was written.
            // we use this data to update our internal state before each new iteration.
            void UpdateStateFromResponse(HttpResponseMessage response)
            {
                var amountSent = response.ParseRangeAmount();
                patchUri = response.GetNextLocation();
                if (amountSent is not null)
                {
                    chunkStart = amountSent.Value + 1;
                    // what the server actually read/processed may not align with the bytesRead from the content originally,
                    // so seek to that position to ensure no gaps
                    contents.Seek(chunkStart, SeekOrigin.Begin);
                }
                else
                {
                    chunkStart += bytesRead;
                }
                _logger.LogTrace("Next position of content to read is {0}.", chunkStart);
            }

            async Task<Action> LogError(HttpResponseMessage response, Uri patchUri, CancellationToken cancellationToken)
            {
                _logger.LogTrace("Max number of retries reached: {0}", retryMax);
                await response.LogHttpResponse(_logger, cancellationToken).ConfigureAwait(false);
                string errorMessage = Resource.FormatString(nameof(Strings.BlobUploadFailed), $"PATCH {patchUri}", response.StatusCode);
                return () => throw new ApplicationException(errorMessage);
            }

            // when an error occurs, we can check on the state of the upload from the registry. notably this gives us the same
            // location and range information as the success, so we can use this for our iteration processing.
            async Task<Action> CheckReadAmountAndRetry(Uri patchUri, CancellationToken cancellationToken)
            {
                retryCount += 1;
                _logger.LogTrace("Checking the size of the content uploaded to server at {0}", patchUri);
                HttpResponseMessage getResponse = await API.Blob.Upload.GetStatusAsync(patchUri, cancellationToken).ConfigureAwait(false);

                //the current logic
                // - if SC is NoContent
                //    - if Range header is there and second part is >0, we use that range
                //    - if Range is 0-0, 0--1, or not there we assumed the chunk was uploaded fully
                // - if SC is not NoContent
                //    - we assume that chunk was not uploaded at all and retry
                
                // two cases handled by same status code check here:
                // * error response (4xx/5xx) from server
                // * non-spec-compliant but incorrect (2xx) from server
                if (getResponse.StatusCode == HttpStatusCode.NoContent)
                {
                    _logger.LogTrace("Size of the content upload to server: {0}", getResponse.ParseRangeAmount());
                    UpdateStateFromResponse(getResponse);
                }
                else
                {
                    _logger.LogTrace("Failed to read position from server {0}, attempting upload from beginning.", getResponse.StatusCode);
                    // reset back to previous chunk so we can retry
                    contents.Seek(chunkStart, SeekOrigin.Begin);
                }
                return () => { };
            }
        }
        return new(patchUri);
    }

    private static string? CheckIfRidExistsInGraph(RuntimeGraph graphForManifestList, IEnumerable<string> leafRids, string userRid) => leafRids.FirstOrDefault(leaf => graphForManifestList.AreCompatible(leaf, userRid));

    private static string? CreateRidForPlatform(PlatformInformation platform)
    {
        // we only support linux and windows containers explicitly, so anything else we should skip past.
        // there are theoretically other platforms/architectures that Docker supports (s390x?), but we are
        // deliberately ignoring them without clear user signal.
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
            _ => null
        };

        if (osPart is null || platformPart is null) return null;
        return $"{osPart}{versionPart ?? ""}-{platformPart}";
    }

    private static string DeriveRegistryName(Uri baseUri)
    {
        var port = baseUri.Port == -1 ? string.Empty : $":{baseUri.Port}";
        if (baseUri.OriginalString.EndsWith(port, ignoreCase: true, culture: null))
        {
            // the port was part of the original assignment, so it's ok to consider it part of the 'name
            return baseUri.GetComponents(UriComponents.HostAndPort, UriFormat.Unescaped);
        }
        else
        {
            // the port was not part of the original assignment, so it's not part of the 'name'
            return baseUri.GetComponents(UriComponents.Host, UriFormat.Unescaped);
        }
    }
    /// <summary>
    /// Computes the effective chunk size to use for the upload
    /// </summary>
    /// <remarks>
    /// The chunk size is determined by the following rules:
    /// We compare the registry's expressed chunk size with any user-provided chunk size (via env var).
    /// If both are set, we use the smaller of the two.
    /// If only one is set, we use that one.
    /// If either is zero, we use the other.
    /// If neither is set, we use the default chunk size.
    /// Finally, AWS ECR has a min size that we use to override the above if it would be below that min size.
    /// </remarks>
    private static int EffectiveChunkSize(int? registryChunkSize, bool isAWS)
    {

        var result =
            (registryChunkSize, s_chunkedUploadSizeBytes) switch
            {
                (0, int u) => u,
                (int r, 0) => r,
                (int r, int u) => Math.Min(r, u),
                (int r, null) => r,
                (null, int u) => u,
                (null, null) => s_defaultChunkSizeBytes
            };

        if (isAWS)
        {
            // AWS ECR requires a min chunk size of 5MB for all chunks except the last.
            return Math.Max(result, s_fiveMegs);
        }
        else
        {
            return result;
        }
    }

    private static RuntimeGraph GetRuntimeGraphForDotNet(string ridGraphPath) => JsonRuntimeFormat.ReadRuntimeGraph(ridGraphPath);

    private void AddRidAndDescendantsToSet(HashSet<RuntimeDescription> runtimeDescriptionSet, string rid, RuntimeGraph dotnetRuntimeGraph)
    {
        var R = dotnetRuntimeGraph.Runtimes[rid];
        runtimeDescriptionSet.Add(R);
        foreach (var r in R.InheritedRuntimes) AddRidAndDescendantsToSet(runtimeDescriptionSet, r, dotnetRuntimeGraph);
    }

    private (IReadOnlyDictionary<string, PlatformSpecificManifest>, RuntimeGraph) ConstructRuntimeGraphForManifestList(ManifestListV2 manifestList, RuntimeGraph dotnetRuntimeGraph)
    {
        var ridDict = new Dictionary<string, PlatformSpecificManifest>();
        var runtimeDescriptionSet = new HashSet<RuntimeDescription>();
        foreach (var manifest in manifestList.manifests)
        {
            if (CreateRidForPlatform(manifest.platform) is { } rid)
            {
                if (ridDict.TryAdd(rid, manifest))
                {
                    AddRidAndDescendantsToSet(runtimeDescriptionSet, rid, dotnetRuntimeGraph);
                }
            }
        }

        var graph = new RuntimeGraph(runtimeDescriptionSet);
        return (ridDict, graph);
    }

    private async Task<ImageBuilder> PickBestImageFromManifestListAsync(
        string repositoryName,
        string reference,
        ManifestListV2 manifestList,
        string runtimeIdentifier,
        string runtimeIdentifierGraphPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var runtimeGraph = GetRuntimeGraphForDotNet(runtimeIdentifierGraphPath);
        var (ridDict, graphForManifestList) = ConstructRuntimeGraphForManifestList(manifestList, runtimeGraph);
        var bestManifestRid = CheckIfRidExistsInGraph(graphForManifestList, ridDict.Keys, runtimeIdentifier);
        if (bestManifestRid is null)
        {
            throw new BaseImageNotFoundException(runtimeIdentifier, repositoryName, reference, graphForManifestList.Runtimes.Keys);
        }
        var matchingManifest = ridDict[bestManifestRid];
        var manifestResponse = await API.Manifest.GetAsync(repositoryName, matchingManifest.digest, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        return await ReadSingleImageAsync(
            repositoryName,
            await manifestResponse.Content.ReadFromJsonAsync<ManifestV2>(cancellationToken: cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ImageBuilder> ReadSingleImageAsync(string repositoryName, ManifestV2 manifest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var config = manifest.Config;
        string configSha = config.digest;

        var blobResponse = await API.Blob.GetAsync(repositoryName, configSha, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        JsonNode? configDoc = JsonNode.Parse(await blobResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        Debug.Assert(configDoc is not null);

        cancellationToken.ThrowIfCancellationRequested();
        return new ImageBuilder(manifest, new ImageConfig(configDoc));
    }
    /// <summary>
    /// Orchestrates an upload of a blob. Starts the upload, sends the content, and finishes the upload.
    /// </summary>
    private async Task UploadBlobAsync(string repository, string digest, Stream contents, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (await API.Blob.ExistsAsync(repository, digest, cancellationToken).ConfigureAwait(false))
        {
            // Already there!
            return;
        }

        // Three steps to this process:
        // * start an upload session
        cancellationToken.ThrowIfCancellationRequested();
        var uploadInfo = await API.Blob.Upload.StartAsync(repository, cancellationToken).ConfigureAwait(false);

        _logger.LogTrace("Started upload session for {0} to {1} with chunk size {2}", digest, uploadInfo.UploadUri, EffectiveChunkSize(uploadInfo.RegistryDeclaredChunkSize, IsAmazonECRRegistry));
        // * upload the blob
        cancellationToken.ThrowIfCancellationRequested();
        var finalizeInformation = await UploadBlobContentsAsync(repository, digest, contents, uploadInfo, cancellationToken).ConfigureAwait(false);
        _logger.LogTrace("Uploaded content for {0}", digest);
        // * finish the upload session
        cancellationToken.ThrowIfCancellationRequested();
        await API.Blob.Upload.CompleteAsync(finalizeInformation.UploadUri, digest, cancellationToken).ConfigureAwait(false);
        _logger.LogTrace("Finalized upload session for {0}", digest);
    }
    /// <summary>
    /// Uploads the contents of the stream, either in an atomic PUT or in chunks.
    /// </summary>
    /// <remarks>
    /// Generally, we try to upload the blob in an atomic PUT. If the registry does not support this, we fall back to chunked uploads.
    /// If the atomic PUT fails, we fall back to chunked uploads.
    /// If the registry provides a chunk max and that max is less than the content length, then we use chunked uploads.
    /// </remarks>
    private async Task<FinalizeUploadInformation> UploadBlobContentsAsync(string repository, string digest, Stream contents, StartUploadInformation uploadInfo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogTrace("Attempting to upload whole blob, content length: {0}.", contents.Length);
        try
        {
            return await API.Blob.Upload.UploadAtomicallyAsync(uploadInfo.UploadUri, contents, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogTrace("Errored while uploading whole blob: {0}.\nRetrying with chunked upload. Content length: {1}, chunk size: {2}.", ex, contents.Length, uploadInfo.RegistryDeclaredChunkSize);
            contents.Seek(0, SeekOrigin.Begin);
            return await UploadBlobChunkedAsync(contents, uploadInfo, cancellationToken).ConfigureAwait(false);
        }
    }
}
