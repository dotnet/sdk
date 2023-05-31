// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Build.Containers.Resources;
using NuGet.RuntimeModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.NET.Build.Containers;

internal sealed class Registry
{
    private const string DockerManifestV2 = "application/vnd.docker.distribution.manifest.v2+json";
    private const string DockerManifestListV2 = "application/vnd.docker.distribution.manifest.list.v2+json";
    private const string DockerContainerV1 = "application/vnd.docker.container.image.v1+json";

    /// <summary>
    /// Whether we should upload blobs via chunked upload (enabled by default, but disabled for certain registries in conjunction with the explicit support check below).
    /// </summary>
    /// <remarks>
    /// Relates to https://github.com/dotnet/sdk-container-builds/pull/383#issuecomment-1466408853
    /// </remarks>
    private static readonly bool s_chunkedUploadEnabled = Env.GetEnvironmentVariableAsBool(ContainerHelpers.ChunkedUploadEnabled, defaultValue: true);

    /// <summary>
    /// When chunking is enabled, allows explicit control over the size of the chunks uploaded
    /// </summary>
    /// <remarks>
    /// Our default of 64KB is very conservative, so raising this to 1MB or more can speed up layer uploads reasonably well.
    /// </remarks>
    private static readonly int? s_chunkedUploadSizeBytes = Env.GetEnvironmentVariableAsNullableInt(ContainerHelpers.ChunkedUploadSizeBytes);

    /// <summary>
    /// Whether we should upload blobs in parallel (enabled by default, but disabled for certain registries in conjunction with the explicit support check below).
    /// </summary>
    /// <remarks>
    /// Enabling this can swamp some registries, so this is an escape hatch.
    /// </remarks>
    private static readonly bool s_parallelUploadEnabled =  Env.GetEnvironmentVariableAsBool(ContainerHelpers.ParallelUploadEnabled, defaultValue: true);

    private static readonly int s_defaultChunkSizeBytes = 1024 * 64;

    /// <summary>
    /// The name of the registry, which is the host name, optionally followed by a colon and the port number.
    /// This is used in user-facing error messages, and it should match what the user would manually enter as
    /// part of Docker commands like `docker login`.
    /// </summary>
    public string RegistryName { get; init; }

    public Registry(Uri baseUri)
    {
        BaseUri = baseUri;
        RegistryName = DeriveRegistryName(baseUri);
        _client = CreateClient();
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

    public Uri BaseUri { get; }

    /// <summary>
    /// The max chunk size for patch blob uploads.
    /// </summary>
    /// <remarks>
    /// This varies by registry target, for example Amazon Elastic Container Registry requires 5MB chunks for all but the last chunk.
    /// </remarks>
    public int MaxChunkSizeBytes => s_chunkedUploadSizeBytes ?? s_defaultChunkSizeBytes;

    /// <summary>
    /// Check to see if the registry is for Amazon Elastic Container Registry (ECR).
    /// </summary>
    public bool IsAmazonECRRegistry
    {
        get
        {
            // If this the registry is to public ECR the name will contain "public.ecr.aws".
            if (RegistryName.Contains("public.ecr.aws"))
            {
                return true;
            }

            // If the registry is to a private ECR the registry will start with an account id which is a 12 digit number and will container either
            // ".ecr." or ".ecr-" if pushed to a FIPS endpoint.
            var accountId = RegistryName.Split('.')[0];
            if ((RegistryName.Contains(".ecr.") || RegistryName.Contains(".ecr-")) && accountId.Length == 12 && long.TryParse(accountId, out _))
            {
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Check to see if the registry is GitHub Packages, which always uses ghcr.io.
    /// </summary>
    public bool IsGithubPackageRegistry => RegistryName.StartsWith("ghcr.io", StringComparison.Ordinal);

    /// <summary>
    /// Check to see if the registry is Docker Hub, which uses two well-known domains.
    /// </summary>
    public bool IsDockerHub => RegistryName.Equals("registry-1.docker.io", StringComparison.Ordinal) || RegistryName.Equals("registry.hub.docker.com", StringComparison.Ordinal);

    /// <summary>
    /// Check to see if the registry is for Google Artifact Registry.
    /// </summary>
    /// <remarks>
    /// Google Artifact Registry locations (one for each availability zone) are of the form "ZONE-docker.pkg.dev".
    /// </remarks>
    public bool IsGoogleArtifactRegistry {
        get => RegistryName.EndsWith("-docker.pkg.dev", StringComparison.Ordinal);
    }

    /// <summary>
    /// Google Artifact Registry doesn't support chunked upload, but Amazon ECR, GitHub Packages, and DockerHub do. We want the capability check to be agnostic to the target.
    /// </summary>
    private bool SupportsChunkedUpload => (!IsGoogleArtifactRegistry || IsAmazonECRRegistry || IsGithubPackageRegistry || IsDockerHub) && s_chunkedUploadEnabled;

    /// <summary>
    /// Pushing to ECR uses a much larger chunk size. To avoid getting too many socket disconnects trying to do too many
    /// parallel uploads be more conservative and upload one layer at a time.
    /// </summary>
    private bool SupportsParallelUploads => !IsAmazonECRRegistry && s_parallelUploadEnabled;

    public async Task<ImageBuilder> GetImageManifestAsync(string repositoryName, string reference, string runtimeIdentifier, string runtimeIdentifierGraphPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var initialManifestResponse = await GetManifestAsync(repositoryName, reference, cancellationToken).ConfigureAwait(false);

        return initialManifestResponse.Content.Headers.ContentType?.MediaType switch
        {
            DockerManifestV2 => await ReadSingleImageAsync(
                repositoryName,
                await initialManifestResponse.Content.ReadFromJsonAsync<ManifestV2>(cancellationToken: cancellationToken).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false),
            DockerManifestListV2 => await PickBestImageFromManifestListAsync(
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

    private async Task<ImageBuilder> ReadSingleImageAsync(string repositoryName, ManifestV2 manifest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var config = manifest.Config;
        string configSha = config.digest;

        var blobResponse = await GetBlobAsync(repositoryName, configSha, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        JsonNode? configDoc = JsonNode.Parse(await blobResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        Debug.Assert(configDoc is not null);

        cancellationToken.ThrowIfCancellationRequested();
        return new ImageBuilder(manifest, new ImageConfig(configDoc));
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
        if (bestManifestRid is null) {
            throw new BaseImageNotFoundException(runtimeIdentifier, repositoryName, reference, graphForManifestList.Runtimes.Keys);
        }
        var matchingManifest = ridDict[bestManifestRid];
        var manifestResponse = await GetManifestAsync(repositoryName, matchingManifest.digest, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        return await ReadSingleImageAsync(
            repositoryName,
            await manifestResponse.Content.ReadFromJsonAsync<ManifestV2>(cancellationToken: cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> GetManifestAsync(string repositoryName, string reference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var client = GetClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseUri, $"/v2/{repositoryName}/manifests/{reference}"));
        AddDockerFormatsAcceptHeader(request);
        var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return response;
    }

    private async Task<HttpResponseMessage> GetBlobAsync(string repositoryName, string digest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var client = GetClient();
        using var request =
            new HttpRequestMessage(HttpMethod.Get, new Uri(BaseUri, $"/v2/{repositoryName}/blobs/{digest}"));
        AddDockerFormatsAcceptHeader(request);
        var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return response;
    }

    private static string? CheckIfRidExistsInGraph(RuntimeGraph graphForManifestList, IEnumerable<string> leafRids, string userRid) => leafRids.FirstOrDefault(leaf => graphForManifestList.AreCompatible(leaf, userRid));

    private (IReadOnlyDictionary<string, PlatformSpecificManifest>, RuntimeGraph) ConstructRuntimeGraphForManifestList(ManifestListV2 manifestList, RuntimeGraph dotnetRuntimeGraph)
    {
        var ridDict = new Dictionary<string, PlatformSpecificManifest>();
        var runtimeDescriptionSet = new HashSet<RuntimeDescription>();
        foreach (var manifest in manifestList.manifests) {
            if (CreateRidForPlatform(manifest.platform) is { } rid)
            {
                if (ridDict.TryAdd(rid, manifest)) {
                    AddRidAndDescendantsToSet(runtimeDescriptionSet, rid, dotnetRuntimeGraph);
                }
            }
        }

        var graph = new RuntimeGraph(runtimeDescriptionSet);
        return (ridDict, graph);
    }

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
            [var major, .. ] => major,
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

    private static RuntimeGraph GetRuntimeGraphForDotNet(string ridGraphPath) => JsonRuntimeFormat.ReadRuntimeGraph(ridGraphPath);

    private void AddRidAndDescendantsToSet(HashSet<RuntimeDescription> runtimeDescriptionSet, string rid, RuntimeGraph dotnetRuntimeGraph)
    {
        var R = dotnetRuntimeGraph.Runtimes[rid];
        runtimeDescriptionSet.Add(R);
        foreach (var r in R.InheritedRuntimes) AddRidAndDescendantsToSet(runtimeDescriptionSet, r, dotnetRuntimeGraph);
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

        HttpClient client = GetClient();

        using var request = new HttpRequestMessage(HttpMethod.Get,
            new Uri(BaseUri, $"/v2/{repository}/blobs/{descriptor.Digest}"));
        AddDockerFormatsAcceptHeader(request);
        var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

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

    public async Task PushAsync(Layer layer, string repository, Action<string> logProgressMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string digest = layer.Descriptor.Digest;

        using (FileStream contents = File.OpenRead(layer.BackingFile))
        {
            await UploadBlobAsync(repository, digest, contents, logProgressMessage, cancellationToken).ConfigureAwait(false);
        }
    }

    private record UploadFinalizeInformation(UriBuilder uploadUri, ByteArrayContent? content, string digest);

    private async Task<UploadFinalizeInformation> UploadBlobChunkedAsync(string repository, Stream contents, HttpClient client, UploadInformation uploadInfo, Action<string> logProgressMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Uri patchUri = uploadInfo.uploadUri.Uri;
        contents.Seek(0, SeekOrigin.Begin);

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] chunkBackingStore = new byte[uploadInfo.EffectiveChunkSize];

        int chunkCount = 0;
        int chunkStart = 0;
        int retryCount = 0;

        while (contents.Position < contents.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // if ((contents.Length - contents.Position) < uploadInfo.EffectiveChunkSize)
            // {
            //     logProgressMessage($"Delaying final chunk because ({contents.Length} - {contents.Position}) < {uploadInfo.EffectiveChunkSize})");
            //     // we're at the final chunk - we should emit that chunk as the final PUT for AWS compat
            //     var (finalContent, _) = await ReadChunk();
            //     return new(new(patchUri), finalContent);
            // }

            var (content, bytesRead) = await ReadChunk();
            HttpResponseMessage patchResponse = await client.PatchAsync(patchUri, content, cancellationToken).ConfigureAwait(false);

            (patchResponse.StatusCode switch {
                HttpStatusCode.Accepted => ProcessSuccessfulChunk(patchResponse),
                HttpStatusCode.Created when IsAmazonECRRegistry => ProcessSuccessfulChunk(patchResponse, ignoreRange: true),
                _ when retryCount < 10 => await CheckReadAmountAndRetry(patchUri, cancellationToken).ConfigureAwait(false),
                _ => await LogError(patchResponse, patchUri, cancellationToken).ConfigureAwait(false)
            })();

            async Task<(ByteArrayContent, int)> ReadChunk()
            {
                logProgressMessage($"Processing chunk because ({contents.Position} < {contents.Length})");
                int bytesRead = await contents.ReadAsync(chunkBackingStore, cancellationToken).ConfigureAwait(false);
                hash.AppendData(chunkBackingStore, 0, bytesRead);
                ByteArrayContent content = new (chunkBackingStore, offset: 0, count: bytesRead);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Headers.ContentLength = bytesRead;
                // manual because ACR throws an error with the .NET type {"Range":"bytes 0-84521/*","Reason":"the Content-Range header format is invalid"}
                //    content.Headers.Add("Content-Range", $"0-{contents.Length - 1}");
                Debug.Assert(content.Headers.TryAddWithoutValidation("Content-Range", $"{chunkStart}-{chunkStart + bytesRead - 1}"));
                return new(content, bytesRead);
            }

            // for a successful chunk, we decrement retries (if any) and read the amount written by the server + next location
            Action ProcessSuccessfulChunk(HttpResponseMessage response, bool ignoreRange = false) => () => {
                if (retryCount > 0) {
                    retryCount -= 1;
                }
                chunkCount += 1;
                UpdateStateFromResponse(response, ignoreRange);
            };

            // responses can tell us a) where to upload the next chunk, and b) how much of the last chunk was written.
            // we use this data to update our internal state before each new iteration.
            void UpdateStateFromResponse(HttpResponseMessage response, bool ignoreRange)
            {
                var amountSent = ignoreRange ? null : ParseRangeAmount(response);
                patchUri = GetNextLocation(response).Uri;
                if (amountSent is not null)
                {
                    chunkStart = amountSent.Value + 1;
                    // may not align with the bytesRead, so seek to that
                    contents.Seek(chunkStart, SeekOrigin.Begin);
                }
                else
                {
                    chunkStart += bytesRead;
                }
            }

            static async Task<Action> LogError(HttpResponseMessage response, Uri patchUri, CancellationToken cancellationToken) {
                var headers = response.Headers.ToString();
                var detail = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                string errorMessage = Resource.FormatString(nameof(Strings.BlobUploadFailed), $"Chunked PATCH {patchUri}", response.StatusCode, headers + Environment.NewLine + detail);
                return () => throw new ApplicationException(errorMessage);
            }

            // when an error occurs, we can check on the state of the upload from the registry. notably this gives us the same
            // location and range information as the success, so we can use this for our iteration processing.
            async Task<Action> CheckReadAmountAndRetry(Uri patchUri, CancellationToken cancellationToken) {
                retryCount += 1;
                var getResponse = await client.GetAsync(patchUri, cancellationToken).ConfigureAwait(false);
                if (!getResponse.IsSuccessStatusCode) {
                    // reset back to previous chunk so we can retry
                    contents.Seek(chunkStart, SeekOrigin.Begin);
                }
                else {
                    UpdateStateFromResponse(getResponse, false);
                }
                return () => { };
            }
        }
        var digest = $"sha256:{Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()}";
        return new(new(patchUri), null, digest);
    }

    // servers tell us the total Range they've processed via the range headers. we use this to determine where
    // the next chunk should start.
    private static int? ParseRangeAmount(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Range", out var rangeValues))
        {
            var range = rangeValues.First();
            var parts = range.Split('-', 2);
            if (parts.Length == 2 && int.TryParse(parts[1], out var amountRead))
            {
                // github returns a 0 range, this leads to bad behavior
                if (amountRead == 0) {
                    return null;
                }
                return amountRead;
            }
        }
        return null;
    }

    private static int? ParseOCIChunkMinSizeAmount(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("OCI-Chunk-Min-Length", out var minLengthValues))
        {
            var minLength = minLengthValues.First();
            if (int.TryParse(minLength, out var amountRead))
            {
                return amountRead;
            }
        }
        return null;
    }

    private UriBuilder GetNextLocation(HttpResponseMessage response) {
        if (response.Headers.Location is {IsAbsoluteUri: true })
        {
            return new UriBuilder(response.Headers.Location);
        }
        else
        {
            // if we don't trim the BaseUri and relative Uri of slashes, you can get invalid urls.
            // Uri constructor does this on our behalf.
            return new UriBuilder(new Uri(BaseUri, response.Headers.Location?.OriginalString ?? ""));
        }
    }

    private async Task<UploadFinalizeInformation> UploadBlobWholeAsync(string repository, string digest, Stream contents, HttpClient client, UriBuilder uploadUri, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StreamContent content = new StreamContent(contents);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Headers.ContentLength = contents.Length;
        HttpResponseMessage patchResponse = await client.PatchAsync(uploadUri.Uri, content, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        // Fail the upload if the response code is not Accepted (202) or if uploading to Amazon ECR which returns back Created (201).
        if (!(patchResponse.StatusCode == HttpStatusCode.Accepted || (IsAmazonECRRegistry && patchResponse.StatusCode == HttpStatusCode.Created)))
        {
            var headers = patchResponse.Headers.ToString();
            var detail = await patchResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            string errorMessage = Resource.FormatString(nameof(Strings.BlobUploadFailed), $"Whole PATCH {uploadUri}", patchResponse.StatusCode, headers + Environment.NewLine + detail);
            throw new ApplicationException(errorMessage);
        }
        return new(GetNextLocation(patchResponse), null, digest);
    }

    private record UploadInformation(int? chunkSize, UriBuilder uploadUri)
    {
        public int EffectiveChunkSize => EffectiveChunkSize(chunkSize);
    }

    private async Task<UploadInformation> StartUploadSessionAsync(string repository, string digest, HttpClient client, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Uri startUploadUri = new Uri(BaseUri, $"/v2/{repository}/blobs/uploads/");

        HttpResponseMessage pushResponse = await client.PostAsync(startUploadUri, content: null, cancellationToken).ConfigureAwait(false);

        if (pushResponse.StatusCode != HttpStatusCode.Accepted)
        {
            var headers = pushResponse.Headers.ToString();
            var detail = await pushResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            string errorMessage = Resource.FormatString(nameof(Strings.BlobUploadFailed), $"POST {startUploadUri}", pushResponse.StatusCode, headers + Environment.NewLine + detail);
            throw new ApplicationException(errorMessage);
        }
        cancellationToken.ThrowIfCancellationRequested();
        var chunkSize = ParseRangeAmount(pushResponse) ?? ParseOCIChunkMinSizeAmount(pushResponse);
        return new(chunkSize, GetNextLocation(pushResponse));
    }

    private static int EffectiveChunkSize(int? registryChunkSize)
    {
        var result =
            (registryChunkSize, s_chunkedUploadSizeBytes) switch {
                (0, int u) => u,
                (int r, 0) => r,
                (int r, int u) => Math.Min(r, u),
                (int r, null) => r,
                (null, int u) => u,
                (null, null) => s_defaultChunkSizeBytes
            };
        return result;
    }

    private async Task<UploadFinalizeInformation> UploadBlobContentsAsync(string repository, string digest, Stream contents, HttpClient client, UploadInformation uploadInfo, Action<string> logProgressMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (uploadInfo.EffectiveChunkSize > contents.Length)
        {
            logProgressMessage($"Chunk size was greater than content length of {contents.Length}, uploading whole blob.");
            try
            {
                return await UploadBlobWholeAsync(repository, digest, contents, client, uploadInfo.uploadUri, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logProgressMessage($"Errored while uploading whole blob: {ex.Message}.\nRetrying with chunked upload.");
                contents.Seek(0, SeekOrigin.Begin);
                return await UploadBlobChunkedAsync(repository, contents, client, uploadInfo, logProgressMessage, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            logProgressMessage($"Chunk size was smaller than content length of {contents.Length}, uploading chunks.");
            return await UploadBlobChunkedAsync(repository, contents, client, uploadInfo, logProgressMessage, cancellationToken).ConfigureAwait(false);
        }
    }


    private static async Task FinishUploadSessionAsync(HttpClient client, UploadFinalizeInformation finalizeInformation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UriBuilder uploadUri = finalizeInformation.uploadUri;
        // PUT with digest to finalize
        uploadUri.Query += $"&digest={Uri.EscapeDataString(finalizeInformation.digest)}";
        var putUri = uploadUri.Uri;
        HttpResponseMessage finalizeResponse = await client.PutAsync(putUri, finalizeInformation.content, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (finalizeResponse.StatusCode != HttpStatusCode.Created)
        {
            var headers = finalizeResponse.Headers.ToString();
            var detail = await finalizeResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            string errorMessage = Resource.FormatString(nameof(Strings.BlobUploadFailed), $"PUT {putUri}", finalizeResponse.StatusCode, headers + Environment.NewLine + detail);
            throw new ApplicationException(errorMessage);
        }
    }

    /// <summary>
    /// Tries to upload a blob to a registry using the best available mechanism:
    /// * mount
    /// * full upload
    /// * chunked upload
    /// </summary>
    private async Task UploadBlobAsync(string repository, string digest, Stream contents, Action<string> logProgressMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HttpClient client = GetClient();


        if (await BlobAlreadyUploadedAsync(repository, digest, client, cancellationToken).ConfigureAwait(false))
        {
            // Already there!
            return;
        }

        // Three steps to this process:
        // * start an upload session
        cancellationToken.ThrowIfCancellationRequested();
        var uploadInfo = await StartUploadSessionAsync(repository, digest, client, cancellationToken).ConfigureAwait(false);
        logProgressMessage($"Started upload session for {digest} to {uploadInfo.uploadUri} with chunk size '{uploadInfo.EffectiveChunkSize}'");
        // * upload the blob
        cancellationToken.ThrowIfCancellationRequested();
        var finalizeInformation = await UploadBlobContentsAsync(repository, digest, contents, client, uploadInfo, logProgressMessage, cancellationToken).ConfigureAwait(false);
        logProgressMessage($"Uploaded content for {digest}");
        // * finish the upload session
        cancellationToken.ThrowIfCancellationRequested();
        logProgressMessage($"Computed digest for '{digest}' was '{finalizeInformation.digest}'");
        await FinishUploadSessionAsync(client, finalizeInformation, cancellationToken).ConfigureAwait(false);
        logProgressMessage($"Finalized upload session for {digest}");
    }

    private async Task<bool> BlobAlreadyUploadedAsync(string repository, string digest, HttpClient client, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, new Uri(BaseUri, $"/v2/{repository}/blobs/{digest}")), cancellationToken).ConfigureAwait(false);

        return response.StatusCode == HttpStatusCode.OK;
    }

    private readonly HttpClient _client;

    private HttpClient GetClient()
    {
        return _client;
    }

    private HttpClient CreateClient()
    {
        HttpMessageHandler clientHandler = new AuthHandshakeMessageHandler(new SocketsHttpHandler() { PooledConnectionLifetime = TimeSpan.FromMilliseconds(10 /* total guess */), UseCookies = false });
        if(IsAmazonECRRegistry)
        {
            clientHandler = new AmazonECRMessageHandler(clientHandler);
        }

        HttpClient client = new(clientHandler);

        client.DefaultRequestHeaders.Add("User-Agent", $".NET Container Library v{Constants.Version}");

        return client;
    }

    private static void AddDockerFormatsAcceptHeader(HttpRequestMessage request)
    {
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new("application/json"));
        request.Headers.Accept.Add(new(DockerManifestListV2));
        request.Headers.Accept.Add(new(DockerManifestV2));
        request.Headers.Accept.Add(new(DockerContainerV1));
    }

    public async Task PushAsync(BuiltImage builtImage, ImageReference source, ImageReference destination, Action<string> logProgressMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HttpClient client = GetClient();
        Registry destinationRegistry = destination.Registry!;

        Func<Descriptor, Task> uploadLayerFunc = async (descriptor) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            string digest = descriptor.Digest;

            logProgressMessage($"Uploading layer {digest} to {destinationRegistry.RegistryName}");
            if (await destinationRegistry.BlobAlreadyUploadedAsync(destination.Repository, digest, client, cancellationToken).ConfigureAwait(false))
            {
                logProgressMessage($"Layer {digest} already existed");
                return;
            }

            // Blob wasn't there; can we tell the server to get it from the base image?
            HttpResponseMessage pushResponse = await client.PostAsync(new Uri(destinationRegistry.BaseUri, $"/v2/{destination.Repository}/blobs/uploads/?mount={digest}&from={source.Repository}"), content: null).ConfigureAwait(false);

            if (pushResponse.StatusCode != HttpStatusCode.Created)
            {
                // The blob wasn't already available in another namespace, so fall back to explicitly uploading it

                if (source.Registry is { } sourceRegistry)
                {
                    // Ensure the blob is available locally
                    await sourceRegistry.DownloadBlobAsync(source.Repository, descriptor, cancellationToken).ConfigureAwait(false);
                    // Then push it to the destination registry
                    await destinationRegistry.PushAsync(Layer.FromDescriptor(descriptor), destination.Repository, logProgressMessage, cancellationToken).ConfigureAwait(false);
                    logProgressMessage($"Finished uploading layer {digest} to {destinationRegistry.RegistryName}");
                }
                else {
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
            foreach(var descriptor in builtImage.LayerDescriptors)
            {
                await uploadLayerFunc(descriptor).ConfigureAwait(false);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        using (MemoryStream stringStream = new MemoryStream(Encoding.UTF8.GetBytes(builtImage.Config)))
        {
            var configDigest = builtImage.ImageDigest;
            logProgressMessage($"Uploading config to registry at blob {configDigest}");
            await UploadBlobAsync(destination.Repository, configDigest, stringStream, logProgressMessage, cancellationToken).ConfigureAwait(false);
            logProgressMessage($"Uploaded config to registry");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var manifestDigest = builtImage.Manifest.GetDigest();
        logProgressMessage($"Uploading manifest to registry {RegistryName} as blob {manifestDigest}");
        string jsonString = JsonSerializer.SerializeToNode(builtImage.Manifest)?.ToJsonString() ?? "";
        HttpContent manifestUploadContent = new StringContent(jsonString);
        manifestUploadContent.Headers.ContentType = new MediaTypeHeaderValue(DockerManifestV2);
        var putResponse = await client.PutAsync(new Uri(BaseUri, $"/v2/{destination.Repository}/manifests/{manifestDigest}"), manifestUploadContent, cancellationToken).ConfigureAwait(false);

        if (!putResponse.IsSuccessStatusCode)
        {
            throw new ContainerHttpException(Resource.GetString(nameof(Strings.RegistryPushFailed)), putResponse.RequestMessage?.RequestUri?.ToString(), jsonString);
        }
        logProgressMessage($"Uploaded manifest to {RegistryName}");

        cancellationToken.ThrowIfCancellationRequested();
        logProgressMessage($"Uploading tag {destination.Tag} to {RegistryName}");
        var putResponse2 = await client.PutAsync(new Uri(BaseUri, $"/v2/{destination.Repository}/manifests/{destination.Tag}"), manifestUploadContent, cancellationToken).ConfigureAwait(false);

        if (!putResponse2.IsSuccessStatusCode)
        {
            throw new ContainerHttpException(Resource.GetString(nameof(Strings.RegistryPushFailed)), putResponse2.RequestMessage?.RequestUri?.ToString(), jsonString);
        }

        logProgressMessage($"Uploaded tag {destination.Tag} to {RegistryName}");
    }
}
