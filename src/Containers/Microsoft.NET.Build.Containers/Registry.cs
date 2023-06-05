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

public enum ProgressMessageLevel {
    Info,
    Trace
}

public record struct LogMessage (ProgressMessageLevel level, string messageFormat, object[] formatArgs) {
    public static LogMessage Info(string messageFormat, params object[] formatArgs) => new LogMessage(ProgressMessageLevel.Info, messageFormat,  formatArgs);
    public static LogMessage Trace(string messageFormat, params object[] formatArgs) => new LogMessage(ProgressMessageLevel.Trace, messageFormat,  formatArgs);
};

// represents the raw API that an external registry implements.

internal interface IManifestOperations {
    public Task<HttpResponseMessage> GetAsync(string repositoryName, string reference, CancellationToken cancellationToken);
    public Task<HttpResponseMessage> PutAsync(string repositoryName, string reference, HttpContent content, CancellationToken cancellationToken);
}

/// <summary>
/// Data derived from the 'start upload' call that is used to determine how perform the upload.
/// </summary>
internal record StartUploadInformation(int? registryDeclaredChunkSize, UriBuilder uploadUri);

/// <summary>
/// Captures the data needed to finalize an upload
/// </summary>
internal record FinalizeUploadInformation(UriBuilder uploadUri);

internal interface IBlobUploadOperations {
    public Task<StartUploadInformation> StartAsync(string repositoryName, CancellationToken cancellationToken);

    /// <summary>
    /// Uploads a chunk of data to the registry. The chunk size is determined by the registry.
    /// </summary>
    /// <remarks>
    /// Note that unlike other operations, this method uses a full URI. This is because we are data-driven entirely by the registry, no path-patterns to follow.
    /// </remarks>
    public Task<HttpResponseMessage> UploadChunkAsync(Uri uploadUri, HttpContent content, CancellationToken cancellationToken);

    /// <summary>
    /// Uploads a stream of data to the registry atomically.
    /// </summary>
    /// <remarks>
    /// Note that unlike other operations, this method uses a full URI. This is because we are data-driven entirely by the registry, no path-patterns to follow.
    /// This method is also implemented the same as UploadChunkAsync, and is here for semantic reasons only.
    /// </remarks>
    public Task<FinalizeUploadInformation> UploadAtomicallyAsync(Uri uploadUri, Stream content, CancellationToken cancellationToken);

    /// <summary>
    /// Check on the status of an upload operation.
    /// </summary>
    /// <remarks>
    /// Note that unlike other operations, this method uses a full URI. This is because we are data-driven entirely by the registry, no path-patterns to follow.
    /// </remarks>
    public Task<HttpResponseMessage> GetStatusAsync(Uri uploadUri, CancellationToken cancellationToken);

    public Task<HttpResponseMessage> CompleteAsync(Uri uploadUri, string digest, CancellationToken cancellationToken);

    public Task<bool> TryMount(string destinationRepository, string sourceRepository, string digest);
}

internal interface IBlobOperations {
    public Task<HttpResponseMessage> GetAsync(string repositoryName, string digest, CancellationToken cancellationToken);
    public Task<bool> ExistsAsync(string repositoryName, string digest, CancellationToken cancellationToken);
    public IBlobUploadOperations Upload { get; }
}

internal interface IRegistryAPI {
    public IManifestOperations Manifest { get; }
    public IBlobOperations Blob { get; }
}

internal static class HttpExtensions {

    public static HttpRequestMessage AcceptManifestFormats(this HttpRequestMessage request )
    {
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new("application/json"));
        request.Headers.Accept.Add(new(SchemaTypes.DockerManifestListV2));
        request.Headers.Accept.Add(new(SchemaTypes.DockerManifestV2));
        return request;
    }

    /// <summary>
    /// servers tell us the total Range they've processed via the range headers. we use this to determine where
    /// the next chunk should start.
    /// </summary>
    public static int? ParseRangeAmount(this HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Range", out var rangeValues))
        {
            var range = rangeValues.First();
            var parts = range.Split('-', 2);
            if (parts.Length == 2 && int.TryParse(parts[1], out var amountRead))
            {
                // github returns a 0 range, this leads to bad behavior
                if (amountRead <= 0)
                {
                    return null;
                }
                return amountRead;
            }
        }
        return null;
    }

    /// <summary>
    /// the OCI-Chunk-Min-Length header can be returned on the start of a blob upload. this tells us the minimum
    /// chunk size the server will accept. we use this to help determine if we should chunk or not.
    /// </summary>
    public static int? ParseOCIChunkMinSizeAmount(this HttpResponseMessage response)
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

    /// <summary>
    /// servers send the Location header on each response, which tells us where to send the next chunk.
    /// </summary>
    public static UriBuilder GetNextLocation(this HttpResponseMessage response)
    {
        if (response.Headers.Location is { IsAbsoluteUri: true })
        {
            return new UriBuilder(response.Headers.Location);
        }
        else
        {
            // if we don't trim the BaseUri and relative Uri of slashes, you can get invalid urls.
            // Uri constructor does this on our behalf.
            return new UriBuilder(new Uri(response.RequestMessage!.RequestUri!, response.Headers.Location?.OriginalString ?? ""));
        }
    }

    public static bool IsAmazonECRRegistry(this Uri uri) {
        // If this the registry is to public ECR the name will contain "public.ecr.aws".
        if (uri.Authority.Contains("public.ecr.aws"))
        {
            return true;
        }

        // If the registry is to a private ECR the registry will start with an account id which is a 12 digit number and will container either
        // ".ecr." or ".ecr-" if pushed to a FIPS endpoint.
        var accountId = uri.Authority.Split('.')[0];
        if ((uri.Authority.Contains(".ecr.") || uri.Authority.Contains(".ecr-")) && accountId.Length == 12 && long.TryParse(accountId, out _))
        {
            return true;
        }

        return false;
    }
}

internal class DefaultManifestOperations: IManifestOperations {
    private HttpClient Client { get; }
    public DefaultManifestOperations(HttpClient client) {
        Client = client;
    }

    public async Task<HttpResponseMessage> GetAsync(string repositoryName, string reference, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/v2/{repositoryName}/manifests/{reference}")).AcceptManifestFormats();
        var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return response;
    }

    public async Task<HttpResponseMessage> PutAsync(string repositoryName, string reference, HttpContent content, CancellationToken cancellationToken)
    {
        return await Client.PutAsync(new Uri($"/v2/{repositoryName}/manifests/{reference}"), content, cancellationToken).ConfigureAwait(false);
    }
}

internal class DefaultBlobOperations: IBlobOperations {
    private HttpClient Client { get; }
    public DefaultBlobOperations(HttpClient client) {
        Client = client;
        Upload = new DefaultBlobUploadOperations(Client);
    }

    public IBlobUploadOperations Upload { get; }

    public async Task<HttpResponseMessage> GetAsync(string repositoryName, string digest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/v2/{repositoryName}/blobs/{digest}"));
        var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return response;
    }

    public async Task<bool> ExistsAsync(string repositoryName, string digest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HttpResponseMessage response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Head, new Uri($"/v2/{repositoryName}/blobs/{digest}")), cancellationToken).ConfigureAwait(false);
        return response.StatusCode == HttpStatusCode.OK;
    }
}

internal class DefaultBlobUploadOperations: IBlobUploadOperations
{
    private HttpClient Client { get; }
    public DefaultBlobUploadOperations(HttpClient client) {
        Client = client;
    }

    public async Task<StartUploadInformation> StartAsync(string repositoryName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Uri startUploadUri = new Uri($"/v2/{repositoryName}/blobs/uploads/");

        HttpResponseMessage pushResponse = await Client.PostAsync(startUploadUri, content: null, cancellationToken).ConfigureAwait(false);

        if (pushResponse.StatusCode != HttpStatusCode.Accepted)
        {
            var headers = pushResponse.Headers.ToString();
            var detail = await pushResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            string errorMessage = Resource.FormatString(nameof(Strings.BlobUploadFailed), $"POST {startUploadUri}", pushResponse.StatusCode, headers + Environment.NewLine + detail);
            throw new ApplicationException(errorMessage);
        }
        cancellationToken.ThrowIfCancellationRequested();
        var chunkSize = pushResponse.ParseRangeAmount() ?? pushResponse.ParseOCIChunkMinSizeAmount();
        var location = pushResponse.GetNextLocation();
        return new(chunkSize, location);
    }

    public async Task<HttpResponseMessage> UploadChunkAsync(Uri uploadUri, HttpContent content, CancellationToken token)
    {
        return await Client.PatchAsync(uploadUri, content, token).ConfigureAwait(false);
    }

    public async Task<FinalizeUploadInformation> UploadAtomicallyAsync(Uri uploadUri, Stream contents, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        StreamContent content = new StreamContent(contents);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Headers.ContentLength = contents.Length;
        var patchResponse = await Client.PatchAsync(uploadUri, content, token).ConfigureAwait(false);

        token.ThrowIfCancellationRequested();
        // Fail the upload if the response code is not Accepted (202) or if uploading to Amazon ECR which returns back Created (201).
        if (!(patchResponse.StatusCode == HttpStatusCode.Accepted || (uploadUri.IsAmazonECRRegistry() && patchResponse.StatusCode == HttpStatusCode.Created)))
        {
            var headers = patchResponse.Headers.ToString();
            var detail = await patchResponse.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            string errorMessage = Resource.FormatString(nameof(Strings.BlobUploadFailed), $"Whole PATCH {uploadUri}", patchResponse.StatusCode, headers + Environment.NewLine + detail);
            throw new ApplicationException(errorMessage);
        }
        return new(patchResponse.GetNextLocation());
    }

    public async Task<HttpResponseMessage> GetStatusAsync(Uri uploadUri, CancellationToken token)
    {
        return await Client.GetAsync(uploadUri, token).ConfigureAwait(false);
    }

    public async Task<HttpResponseMessage> CompleteAsync(Uri uploadUri, string digest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // PUT with digest to finalize
        UriBuilder builder = new(uploadUri);
        builder.Query += $"&digest={Uri.EscapeDataString(digest)}";
        var putUri = builder.Uri;
        HttpResponseMessage finalizeResponse = await Client.PutAsync(putUri, null, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (finalizeResponse.StatusCode != HttpStatusCode.Created)
        {
            var headers = finalizeResponse.Headers.ToString();
            var detail = await finalizeResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            string errorMessage = Resource.FormatString(nameof(Strings.BlobUploadFailed), $"PUT {putUri}", finalizeResponse.StatusCode, headers + Environment.NewLine + detail);
            throw new ApplicationException(errorMessage);
        }

        return finalizeResponse;
    }

    public async Task<bool> TryMount(string destinationRepository, string sourceRepository, string digest)
    {
        // Blob wasn't there; can we tell the server to get it from the base image?
        HttpResponseMessage pushResponse = await Client.PostAsync(new Uri($"/v2/{destinationRepository}/blobs/uploads/?mount={digest}&from={sourceRepository}"), content: null).ConfigureAwait(false);
        return pushResponse.StatusCode == HttpStatusCode.Created;
    }
}

internal class DefaultRegistryAPI : IRegistryAPI {

    private HttpClient Client { get; }

    public DefaultRegistryAPI(Uri baseUri) {
        var isAmazonECRRegistry = baseUri.IsAmazonECRRegistry();
        Client = CreateClient(baseUri, isAmazonECRRegistry: isAmazonECRRegistry);
        Manifest = new DefaultManifestOperations(Client);
        Blob = new DefaultBlobOperations(Client);
    }

    public IManifestOperations Manifest { get; }
    public IBlobOperations Blob { get; }

    private HttpClient CreateClient(Uri baseUri, bool isAmazonECRRegistry = false)
    {
        HttpMessageHandler clientHandler = new AuthHandshakeMessageHandler(new SocketsHttpHandler()
        {
            PooledConnectionLifetime = TimeSpan.FromMilliseconds(10 /* total guess */),
            // disabling cookies prevents CSRF tokens from being sent - some servers send these and
            // can't handle them being sent back - specifically, Harbor does this.
            // golang client libraries disable cookies as well for this reason.
            UseCookies = false
        });
        if (isAmazonECRRegistry)
        {
            clientHandler = new AmazonECRMessageHandler(clientHandler);
        }

        HttpClient client = new(clientHandler);
        client.BaseAddress = baseUri;
        client.DefaultRequestHeaders.Add("User-Agent", $".NET Container Library v{Constants.Version}");

        return client;
    }
}


internal static class SchemaTypes {
    public const string DockerManifestV2 = "application/vnd.docker.distribution.manifest.v2+json";
    public const string DockerManifestListV2 = "application/vnd.docker.distribution.manifest.list.v2+json";
    public const string DockerContainerV1 = "application/vnd.docker.container.image.v1+json";
}

internal sealed class Registry
{
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
    private static readonly bool s_parallelUploadEnabled = Env.GetEnvironmentVariableAsBool(ContainerHelpers.ParallelUploadEnabled, defaultValue: true);

    private static readonly int s_defaultChunkSizeBytes = 1024 * 64;

    private static readonly int s_fiveMegs = 5_242_880;

    /// <summary>
    /// The name of the registry, which is the host name, optionally followed by a colon and the port number.
    /// This is used in user-facing error messages, and it should match what the user would manually enter as
    /// part of Docker commands like `docker login`.
    /// </summary>
    public string RegistryName { get; init; }

    private IRegistryAPI API { get; }

    public Registry(Uri baseUri)
    {
        BaseUri = baseUri;
        RegistryName = DeriveRegistryName(baseUri);
        API = new DefaultRegistryAPI(BaseUri);
    }

    /// <summary>
    /// Creates a new registry instance with the specified base URI and API for testing purposes
    /// </summary>
    internal Registry(Uri baseUri, IRegistryAPI api)
    {
        BaseUri = baseUri;
        RegistryName = DeriveRegistryName(baseUri);
        API = api;
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
    /// Check to see if the registry is for Amazon Elastic Container Registry (ECR).
    /// </summary>
    public bool IsAmazonECRRegistry
    {
        get => BaseUri.IsAmazonECRRegistry();
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
    public bool IsGoogleArtifactRegistry
    {
        get => RegistryName.EndsWith("-docker.pkg.dev", StringComparison.Ordinal);
    }

    /// <summary>
    /// Pushing to ECR uses a much larger chunk size. To avoid getting too many socket disconnects trying to do too many
    /// parallel uploads be more conservative and upload one layer at a time.
    /// </summary>
    private bool SupportsParallelUploads => !IsAmazonECRRegistry && s_parallelUploadEnabled;

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

    private static string? CheckIfRidExistsInGraph(RuntimeGraph graphForManifestList, IEnumerable<string> leafRids, string userRid) => leafRids.FirstOrDefault(leaf => graphForManifestList.AreCompatible(leaf, userRid));

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

    public async Task PushAsync(Layer layer, string repository, Action<LogMessage> logProgressMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string digest = layer.Descriptor.Digest;

        using (FileStream contents = File.OpenRead(layer.BackingFile))
        {
            await UploadBlobAsync(repository, digest, contents, logProgressMessage, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Upload a blob to the registry using chunks. The chunks are uploaded sequentially, and the size of the chunks is determined by the UploadInformation derived from the 'Start' call
    /// </summary>
    private async Task<FinalizeUploadInformation> UploadBlobChunkedAsync(string repository, string digest, Stream contents, StartUploadInformation uploadInfo, Action<LogMessage> logProgressMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Uri patchUri = uploadInfo.uploadUri.Uri;
        contents.Seek(0, SeekOrigin.Begin);

        byte[] chunkBackingStore = new byte[EffectiveChunkSize(uploadInfo.registryDeclaredChunkSize, IsAmazonECRRegistry)];

        int chunkCount = 0;
        int chunkStart = 0;
        // chunked upload retries inspired from regclient -
        // we allow up to N retries across the whole operation, and
        // successes 'reclaim' retry attempts for future failures.
        int retryMax = 10;
        int retryCount = 0;

        while (contents.Position < contents.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (content, bytesRead) = await ReadChunk();
            HttpResponseMessage patchResponse = await API.Blob.Upload.UploadChunkAsync(patchUri, content, cancellationToken).ConfigureAwait(false);

            // central state machine -
            // successes move to the next chunk,
            // failures are retried up to retryMax times,
            // or else we fail the whole operation.
            (patchResponse.StatusCode switch
            {
                HttpStatusCode.Accepted => ProcessSuccessfulChunk(patchResponse),
                // known quirk - AWS ECR sends 201 Created instead of 202 Accepted
                HttpStatusCode.Created when IsAmazonECRRegistry => ProcessSuccessfulChunk(patchResponse, ignoreRange: true),
                _ when retryCount < retryMax => await CheckReadAmountAndRetry(patchUri, cancellationToken).ConfigureAwait(false),
                _ => await LogError(patchResponse, patchUri, cancellationToken).ConfigureAwait(false)
            })();

            // handles all the logic for reading a new chunk from the stream, including setting content type.
            async Task<(ByteArrayContent, int)> ReadChunk()
            {
                logProgressMessage(LogMessage.Trace("Processing chunk because ({0} < {1})", contents.Position, contents.Length));

                int bytesRead = await contents.ReadAsync(chunkBackingStore, cancellationToken).ConfigureAwait(false);
                ByteArrayContent content = new(chunkBackingStore, offset: 0, count: bytesRead);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Headers.ContentLength = bytesRead;

                // manual because ACR throws an error with the .NET type {"Range":"bytes 0-84521/*","Reason":"the Content-Range header format is invalid"}
                //    content.Headers.Add("Content-Range", $"0-{contents.Length - 1}");
                Debug.Assert(content.Headers.TryAddWithoutValidation("Content-Range", $"{chunkStart}-{chunkStart + bytesRead - 1}"));

                return new(content, bytesRead);
            }

            // for a successful chunk, we decrement retries (if any) and read the amount written by the server + next location
            Action ProcessSuccessfulChunk(HttpResponseMessage response, bool ignoreRange = false) => () =>
            {
                if (retryCount > 0)
                {
                    retryCount -= 1;
                }
                chunkCount += 1;
                UpdateStateFromResponse(response, ignoreRange);
            };

            // responses can tell us a) where to upload the next chunk, and b) how much of the last chunk was written.
            // we use this data to update our internal state before each new iteration.
            void UpdateStateFromResponse(HttpResponseMessage response, bool ignoreRange)
            {
                var amountSent = ignoreRange ? null : response.ParseRangeAmount();
                patchUri = response.GetNextLocation().Uri;
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
            }

            static async Task<Action> LogError(HttpResponseMessage response, Uri patchUri, CancellationToken cancellationToken)
            {
                var headers = response.Headers.ToString();
                var detail = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                string errorMessage = Resource.FormatString(nameof(Strings.BlobUploadFailed), $"Chunked PATCH {patchUri}", response.StatusCode, headers + Environment.NewLine + detail);
                return () => throw new ApplicationException(errorMessage);
            }

            // when an error occurs, we can check on the state of the upload from the registry. notably this gives us the same
            // location and range information as the success, so we can use this for our iteration processing.
            async Task<Action> CheckReadAmountAndRetry(Uri patchUri, CancellationToken cancellationToken)
            {
                retryCount += 1;
                var getResponse = await API.Blob.Upload.GetStatusAsync(patchUri, cancellationToken).ConfigureAwait(false);
                if (!getResponse.IsSuccessStatusCode || getResponse.StatusCode != HttpStatusCode.NoContent)
                {
                    // reset back to previous chunk so we can retry
                    contents.Seek(chunkStart, SeekOrigin.Begin);
                }
                else
                {
                    UpdateStateFromResponse(getResponse, false);
                }
                return () => { };
            }
        }
        return new(new(patchUri));
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

    /// <summary>
    /// Uploads the contents of the stream, either in an atomic PUT or in chunks.
    /// </summary>
    /// <remarks>
    /// Generally, we try to upload the blob in an atomic PUT. If the registry does not support this, we fall back to chunked uploads.
    /// If the atomic PUT fails, we fall back to chunked uploads.
    /// If the registry provides a chunk max and that max is less than the content length, then we use chunked uploads.
    /// </remarks>
    private async Task<FinalizeUploadInformation> UploadBlobContentsAsync(string repository, string digest, Stream contents, StartUploadInformation uploadInfo, Action<LogMessage> logProgressMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (uploadInfo.registryDeclaredChunkSize is null || uploadInfo.registryDeclaredChunkSize >= contents.Length)
        {
            logProgressMessage(LogMessage.Trace("Chunk size undetected or was greater than content length of {0}, attempting to upload whole blob.", contents.Length));
            try
            {
                return await API.Blob.Upload.UploadAtomicallyAsync(uploadInfo.uploadUri.Uri, contents, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logProgressMessage(LogMessage.Trace("Errored while uploading whole blob: {0}.\nRetrying with chunked upload.", ex));
                contents.Seek(0, SeekOrigin.Begin);
                return await UploadBlobChunkedAsync(repository, digest, contents, uploadInfo, logProgressMessage, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            logProgressMessage(LogMessage.Trace("Chunk size was smaller than content length of {0}, uploading chunks.", contents.Length));
            return await UploadBlobChunkedAsync(repository, digest, contents, uploadInfo, logProgressMessage, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Orchestrates an upload of a blob. Starts the upload, sends the content, and finishes the upload.
    /// </summary>
    private async Task UploadBlobAsync(string repository, string digest, Stream contents, Action<LogMessage> logProgressMessage, CancellationToken cancellationToken)
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

        logProgressMessage(LogMessage.Trace("Started upload session for {0} to {1} with chunk size {2}", digest, uploadInfo.uploadUri, EffectiveChunkSize(uploadInfo.registryDeclaredChunkSize, IsAmazonECRRegistry)));
        // * upload the blob
        cancellationToken.ThrowIfCancellationRequested();
        var finalizeInformation = await UploadBlobContentsAsync(repository, digest, contents, uploadInfo, logProgressMessage, cancellationToken).ConfigureAwait(false);
        logProgressMessage(LogMessage.Trace("Uploaded content for {0}", digest));
        // * finish the upload session
        cancellationToken.ThrowIfCancellationRequested();
        await API.Blob.Upload.CompleteAsync(finalizeInformation.uploadUri.Uri, digest, cancellationToken).ConfigureAwait(false);
        logProgressMessage(LogMessage.Trace("Finalized upload session for {0}", digest));
    }

    /// <summary>
    /// Pushes a built image to a destination, attempting to mount the layer from the source if possible.
    /// Otherwise we push the image layers, manifest, and config locally using atomic or chunked uploads.
    /// </summary>
    public async Task PushAsync(BuiltImage builtImage, ImageReference source, ImageReference destination, Action<LogMessage> logProgressMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Registry destinationRegistry = destination.Registry!;

        await PushLayers().ConfigureAwait(false);
        await PushConfig().ConfigureAwait(false);
        await PushManifest().ConfigureAwait(false);


        async Task PushLayers()
        {
            Func<Descriptor, Task> uploadLayerFunc = async (descriptor) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            string digest = descriptor.Digest;

            logProgressMessage(LogMessage.Info("Uploading layer {0} to {1}", digest, destinationRegistry.RegistryName));
            if (await API.Blob.ExistsAsync(destination.Repository, digest, cancellationToken).ConfigureAwait(false))
            {
                logProgressMessage(LogMessage.Info("Layer {0} already existed", digest));
                return;
            }

            if (await API.Blob.Upload.TryMount(destination.Repository, source.Repository, digest))
            {
                // The blob wasn't already available in another namespace, so fall back to explicitly uploading it
                if (source.Registry is { } sourceRegistry)
                {
                    // Ensure the blob is available locally
                    await sourceRegistry.DownloadBlobAsync(source.Repository, descriptor, cancellationToken).ConfigureAwait(false);
                    // Then push it to the destination registry
                    await destinationRegistry.PushAsync(Layer.FromDescriptor(descriptor), destination.Repository, logProgressMessage, cancellationToken).ConfigureAwait(false);
                    logProgressMessage(LogMessage.Info("Finished uploading layer {0} to {1}", digest, destinationRegistry.RegistryName));
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
                logProgressMessage(LogMessage.Info("Uploading config to registry at blob {0}", configDigest));
                await UploadBlobAsync(destination.Repository, configDigest, stringStream, logProgressMessage, cancellationToken).ConfigureAwait(false);
                logProgressMessage(LogMessage.Info("Uploaded config to registry"));
            }
            cancellationToken.ThrowIfCancellationRequested();
        }

        async Task PushManifest()
        {
            var manifestDigest = builtImage.Manifest.GetDigest();
            logProgressMessage(LogMessage.Info("Uploading manifest to registry {0} as blob {1}", RegistryName, manifestDigest));
            string manifestJson = JsonSerializer.SerializeToNode(builtImage.Manifest)?.ToJsonString() ?? "";
            StringContent manifestContent = new(manifestJson);
            manifestContent.Headers.ContentType = new MediaTypeHeaderValue(SchemaTypes.DockerManifestV2);

            var putResponse = await API.Manifest.PutAsync(destination.Repository, manifestDigest, manifestContent, cancellationToken).ConfigureAwait(false);

            if (!putResponse.IsSuccessStatusCode)
            {
                throw new ContainerHttpException(Resource.GetString(nameof(Strings.RegistryPushFailed)), putResponse.RequestMessage?.RequestUri?.ToString(), manifestJson);
            }
            logProgressMessage(LogMessage.Info("Uploaded manifest to {0}", RegistryName));

            cancellationToken.ThrowIfCancellationRequested();

            logProgressMessage(LogMessage.Info("Uploading tag {0} to {1}", destination.Tag, RegistryName));
            var putTagResponse = await API.Manifest.PutAsync(destination.Repository, destination.Tag, manifestContent, cancellationToken).ConfigureAwait(false);

            if (!putTagResponse.IsSuccessStatusCode)
            {
                throw new ContainerHttpException(Resource.GetString(nameof(Strings.RegistryPushFailed)), putTagResponse.RequestMessage?.RequestUri?.ToString(), manifestJson);
            }

            logProgressMessage(LogMessage.Info("Uploaded tag {0} to {1}", destination.Tag, RegistryName));
        }
    }
}
