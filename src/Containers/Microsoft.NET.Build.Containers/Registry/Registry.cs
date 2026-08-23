// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;
using NuGet.RuntimeModel;
using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry.Remote.Exceptions;

namespace Microsoft.NET.Build.Containers;

internal interface IManifestPicker
{
    public Descriptor? PickBestManifestForRid(IReadOnlyDictionary<string, Descriptor> manifestList, string runtimeIdentifier);
}

internal sealed class RidGraphManifestPicker : IManifestPicker
{
    private readonly RuntimeGraph _runtimeGraph;

    public RidGraphManifestPicker(string runtimeIdentifierGraphPath)
    {
        _runtimeGraph = GetRuntimeGraphForDotNet(runtimeIdentifierGraphPath);
    }
    public Descriptor? PickBestManifestForRid(IReadOnlyDictionary<string, Descriptor> ridManifestDict, string runtimeIdentifier)
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
    private const int MaxDownloadRetries = 5;
    private readonly Func<TimeSpan> _retryDelayProvider;

    private readonly ILogger _logger;
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly RegistrySettings _settings;

    /// <summary>
    /// The name of the registry, which is the host name, optionally followed by a colon and the port number.
    /// This is used in user-facing error messages, and it should match what the user would manually enter as
    /// part of Docker commands like `docker login`.
    /// </summary>
    public string RegistryName { get; }

    internal Registry(string registryName, ILogger logger, IRepositoryFactory repositoryFactory, RegistrySettings? settings = null, Func<TimeSpan>? retryDelayProvider = null) :
        this(new Uri($"https://{registryName}"), logger, repositoryFactory, settings, retryDelayProvider)
    { }

    internal Registry(string registryName, ILogger logger, RegistryMode mode, RegistrySettings? settings = null) :
        this(new Uri($"https://{registryName}"), logger, new RepositoryFactoryProvider(mode), settings)
    { }

    internal Registry(Uri baseUri, ILogger logger, IRepositoryFactory repositoryFactory, RegistrySettings? settings = null, Func<TimeSpan>? retryDelayProvider = null) :
        this(baseUri, logger, new RepositoryFactoryProvider(repositoryFactory), settings, retryDelayProvider)
    { }

    internal Registry(Uri baseUri, ILogger logger, RegistryMode mode, RegistrySettings? settings = null) :
        this(baseUri, logger, new RepositoryFactoryProvider(mode), settings)
    { }

    private Registry(Uri baseUri, ILogger logger, RepositoryFactoryProvider factory, RegistrySettings? settings = null, Func<TimeSpan>? retryDelayProvider = null)
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
        _repositoryFactory = factory.Create(RegistryName, BaseUri, logger, _settings);

        _retryDelayProvider = retryDelayProvider ?? (() => TimeSpan.FromSeconds(1));
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

    public async Task<ImageBuilder> GetImageManifestAsync(string repositoryName, string reference, string runtimeIdentifier, IManifestPicker manifestPicker, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        (Descriptor initialDescriptor, Stream initialManifestStream) = await FetchManifestAsync(repositoryName, reference, cancellationToken).ConfigureAwait(false);
        await using (initialManifestStream.ConfigureAwait(false))
        {
            return initialDescriptor.MediaType switch
            {
                OrasProject.Oras.Docker.MediaType.Manifest or MediaType.ImageManifest => await ReadSingleManifest().ConfigureAwait(false),
                OrasProject.Oras.Docker.MediaType.ManifestList or MediaType.ImageIndex => await PickBestImageFromIndexAsync(
                    repositoryName,
                    reference,
                    await JsonSerializer.DeserializeAsync<OrasProject.Oras.Oci.Index>(initialManifestStream, cancellationToken: cancellationToken).ConfigureAwait(false),
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

            async Task<ImageBuilder> ReadSingleManifest()
            {
                Manifest manifest = await JsonSerializer.DeserializeAsync<Manifest>(initialManifestStream, cancellationToken: cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidDataException("The image manifest contained invalid JSON.");
                return await ReadSingleImageAsync(
                    repositoryName,
                    manifest,
                    initialDescriptor.Digest,
                    initialDescriptor.MediaType,
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    internal async Task<OrasProject.Oras.Oci.Index?> GetManifestListAsync(string repositoryName, string reference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        (Descriptor descriptor, Stream content) = await FetchManifestAsync(repositoryName, reference, cancellationToken).ConfigureAwait(false);
        await using (content.ConfigureAwait(false))
        {
            return descriptor.MediaType == OrasProject.Oras.Docker.MediaType.ManifestList
                ? await JsonSerializer.DeserializeAsync<OrasProject.Oras.Oci.Index>(content, cancellationToken: cancellationToken).ConfigureAwait(false)
                : null;
        }
    }

    private async Task<ImageBuilder> ReadSingleImageAsync(string repositoryName, Manifest manifest, string manifestDigest, string manifestMediaType, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        JsonNode configDoc = await FetchBlobJsonAsync(repositoryName, manifest.Config, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        // Manifest.MediaType can be null, so we also provide the media type returned with the manifest.
        return new ImageBuilder(manifest, manifestDigest, manifest.MediaType ?? manifestMediaType, new ImageConfig(configDoc), _logger);
    }

    private static IReadOnlyDictionary<string, Descriptor> GetManifestsByRid(IList<Descriptor> manifestList)
    {
        var ridDict = new Dictionary<string, Descriptor>();
        foreach (var manifest in manifestList)
        {
            if (manifest.Platform is not null && CreateRidForPlatform(manifest.Platform) is { } rid)
            {
                ridDict.TryAdd(rid, manifest);
            }
        }

        return ridDict;
    }

    private static string? CreateRidForPlatform(Platform platform)
    {
        // we only support linux and windows containers explicitly, so anything else we should skip past.
        var osPart = platform.Os switch
        {
            "linux" => "linux",
            "windows" => "win",
            _ => null
        };
        // TODO: this part needs a lot of work, the RID graph isn't super precise here and version numbers (especially on windows) are _whack_
        // TODO: we _may_ need OS-specific version parsing. Need to do more research on what the field looks like across more manifest lists.
        var versionPart = platform.OsVersion?.Split('.') switch
        {
            [var major, ..] => major,
            _ => null
        };
        var platformPart = platform.Architecture switch
        {
            "amd64" => "x64",
            "x386" => "x86",
            "arm" => $"arm{(platform.Variant != "v7" ? platform.Variant : "")}",
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

    private async Task<ImageBuilder> PickBestImageFromIndexAsync(
        string repositoryName,
        string reference,
        OrasProject.Oras.Oci.Index? index,
        string runtimeIdentifier,
        IManifestPicker manifestPicker,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (index is null)
        {
            throw new BaseImageNotFoundException(runtimeIdentifier, repositoryName, reference, []);
        }

        var ridManifestDict = GetManifestsByRid(index.Manifests);
        if (manifestPicker.PickBestManifestForRid(ridManifestDict, runtimeIdentifier) is Descriptor matchingManifest)
        {
            return await ReadImageFromManifest(
                repositoryName,
                reference,
                matchingManifest.Digest,
                matchingManifest.MediaType,
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
        (Descriptor _, Stream content) = await FetchManifestAsync(repositoryName, manifestDigest, cancellationToken).ConfigureAwait(false);
        await using (content.ConfigureAwait(false))
        {
            Manifest? manifest = await JsonSerializer.DeserializeAsync<Manifest>(content, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (manifest is null) throw new BaseImageNotFoundException(runtimeIdentifier, repositoryName, reference, rids);
            DigestUtils.ValidateDigest(manifestDigest);
            return await ReadSingleImageAsync(
                repositoryName,
                manifest,
                manifestDigest,
                mediaType,
                cancellationToken).ConfigureAwait(false);
        }
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

        try
        {
            var fileStream = File.OpenRead(localPath);

            var actualHash = SHA256.HashData(fileStream);
            var expectedHash = DigestUtils.GetEncodedValue(descriptor.Digest);
            InvalidDigestException.ThrowIfMismatched(expectedHash, actualHash);

            return localPath;
        }
        catch (DirectoryNotFoundException)
        {
            // Cache miss
        }
        catch (FileNotFoundException)
        {
            // Cache miss
        }
        catch (InvalidDigestException exception)
        {
            // Incorrect digest
            _logger.LogTrace(
                "Digest validation failed for cached blob {1} ({2}), redownloading from registry.",
                localPath, exception.Message);
        }

        string tempTarballPath = ContentStore.GetTempFile();

        int retryCount = 0;
        while (retryCount < MaxDownloadRetries)
        {
            try
            {
                // No local copy, so download one
                using Stream responseStream = await FetchBlobAsync(repository, descriptor, cancellationToken).ConfigureAwait(false);

                using (FileStream fs = File.Create(tempTarballPath))
                {
                    await responseStream
                        .CopyToAndVerifyAsync(fs, descriptor.Digest, cancellationToken)
                        .ConfigureAwait(false);
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
            await UploadBlobAsync(repository, layer.Descriptor, contents, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task UploadBlobAsync(string repository, Descriptor descriptor, Stream contents, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (await BlobExistsAsync(repository, descriptor, cancellationToken).ConfigureAwait(false))
        {
            // Already there!
            _logger.LogInformation(Strings.Registry_LayerExists, descriptor.Digest);
            return;
        }

        await PushBlobAsync(repository, descriptor, contents, cancellationToken).ConfigureAwait(false);
        _logger.LogTrace("Uploaded content for {0}", descriptor.Digest);
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
            await PushManifestAsync(destinationImageReference.Repository, tag, multiArchImage.ImageIndex, multiArchImage.ImageIndexMediaType, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(Strings.Registry_TagUploaded, tag, RegistryName);
        }
    }

    public Task PushAsync(BuiltImage builtImage, SourceImageReference source, DestinationImageReference destination, CancellationToken cancellationToken)
        => PushAsync(builtImage, source, destination, noCache: false, cancellationToken);

    public Task PushAsync(BuiltImage builtImage, SourceImageReference source, DestinationImageReference destination, bool noCache, CancellationToken cancellationToken)
        => PushAsync(builtImage, source, destination, pushTags: true, noCache, cancellationToken);

    private async Task PushAsync(BuiltImage builtImage, SourceImageReference source, DestinationImageReference destination, bool pushTags, bool noCache, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Registry destinationRegistry = destination.RemoteRegistry!;

        bool manifestExists = !noCache &&
            await ManifestExistsAsync(destination.Repository, builtImage.ManifestDigest, cancellationToken).ConfigureAwait(false);

        if (manifestExists)
        {
            _logger.LogInformation(Strings.Registry_ManifestExists, builtImage.ManifestDigest, destination.Repository);
        }

        Func<Descriptor, Task> uploadLayerFunc = async (descriptor) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            string digest = descriptor.Digest;

            _logger.LogInformation(Strings.Registry_LayerUploadStarted, digest, destinationRegistry.RegistryName);
            if (await BlobExistsAsync(destination.Repository, descriptor, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogInformation(Strings.Registry_LayerExists, digest);
                return;
            }

            if (source.Registry is { } sourceRegistry)
            {
                await MountBlobAsync(
                    destination.Repository,
                    source.Repository,
                    descriptor,
                    async token =>
                    {
                        string localPath = await sourceRegistry.DownloadBlobAsync(source.Repository, descriptor, token).ConfigureAwait(false);
                        return File.OpenRead(localPath);
                    },
                    cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(Strings.Registry_LayerUploaded, digest, destinationRegistry.RegistryName);
            }
            else
            {
                throw new NotImplementedException(Resource.GetString(nameof(Strings.MissingLinkToRegistry)));
            }
        };

        if (!manifestExists)
        {
            if (_settings.ParallelUploadEnabled)
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
                var configDigest = builtImage.ImageDigest!;
                Descriptor configDescriptor = new()
                {
                    MediaType = builtImage.ManifestMediaType == OrasProject.Oras.Docker.MediaType.Manifest ? OrasProject.Oras.Docker.MediaType.Config : MediaType.ImageConfig,
                    Digest = configDigest,
                    Size = stringStream.Length,
                };
                _logger.LogInformation(Strings.Registry_ConfigUploadStarted, configDigest);
                await UploadBlobAsync(destination.Repository, configDescriptor, stringStream, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(Strings.Registry_ConfigUploaded);
            }
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
                await PushManifestAsync(destination.Repository, tag, builtImage.Manifest, builtImage.ManifestMediaType, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(Strings.Registry_TagUploaded, tag, RegistryName);
            }
        }
        else if (!manifestExists)
        {
            _logger.LogInformation(Strings.Registry_ManifestUploadStarted, RegistryName, builtImage.ManifestDigest);
            await PushManifestAsync(destination.Repository, builtImage.ManifestDigest, builtImage.Manifest, builtImage.ManifestMediaType, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(Strings.Registry_ManifestUploaded, RegistryName);
        }
    }

    private async Task<bool> ManifestExistsAsync(string repositoryName, string reference, CancellationToken cancellationToken)
    {
        try
        {
            await _repositoryFactory.Create(repositoryName).Manifests.ResolveAsync(reference, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (NotFoundException)
        {
            return false;
        }
        catch (ResponseException e) when (e.StatusCode is not null && (int)e.StatusCode >= 500)
        {
            throw CreateContainerHttpException(Strings.RegistryPullFailed, e);
        }
        catch (ResponseException)
        {
            return false;
        }
    }

    private async Task<(Descriptor Descriptor, Stream Content)> FetchManifestAsync(
        string repositoryName,
        string reference,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _repositoryFactory.Create(repositoryName).Manifests.FetchAsync(reference, cancellationToken).ConfigureAwait(false);
        }
        catch (NotFoundException)
        {
            throw new RepositoryNotFoundException(RegistryName, repositoryName, reference);
        }
        catch (ResponseException e) when (e.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new UnableToAccessRepositoryException(RegistryName, repositoryName);
        }
        catch (ResponseException e)
        {
            throw CreateContainerHttpException(Strings.RegistryPullFailed, e);
        }
    }

    private async Task<JsonNode> FetchBlobJsonAsync(string repositoryName, Descriptor descriptor, CancellationToken cancellationToken)
    {
        await using Stream stream = await FetchBlobAsync(repositoryName, descriptor, cancellationToken).ConfigureAwait(false);
        return await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException($"Blob '{descriptor.Digest}' contained invalid JSON.");
    }

    private async Task<Stream> FetchBlobAsync(string repositoryName, Descriptor descriptor, CancellationToken cancellationToken)
    {
        try
        {
            return await _repositoryFactory.Create(repositoryName).Blobs.FetchAsync(descriptor, cancellationToken).ConfigureAwait(false);
        }
        catch (ResponseException e) when (e.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new UnableToAccessRepositoryException(RegistryName, repositoryName);
        }
        catch (ResponseException e)
        {
            _logger.LogTrace(e, "ORAS blob pull failed.");
            throw CreateContainerHttpException(Strings.RegistryPullFailed, e);
        }
    }

    private async Task<bool> BlobExistsAsync(string repositoryName, Descriptor descriptor, CancellationToken cancellationToken)
    {
        try
        {
            return await _repositoryFactory.Create(repositoryName).Blobs.ExistsAsync(descriptor, cancellationToken).ConfigureAwait(false);
        }
        catch (ResponseException e) when (e.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new UnableToAccessRepositoryException(RegistryName, repositoryName);
        }
        catch (ResponseException e)
        {
            _logger.LogTrace(e, "ORAS blob existence check failed.");
            throw CreateContainerHttpException(Strings.RegistryPullFailed, e);
        }
    }

    private async Task PushBlobAsync(string repositoryName, Descriptor descriptor, Stream content, CancellationToken cancellationToken)
    {
        try
        {
            await _repositoryFactory.Create(repositoryName).Blobs.PushAsync(descriptor, content, cancellationToken).ConfigureAwait(false);
        }
        catch (ResponseException e) when (e.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new UnableToAccessRepositoryException(RegistryName, repositoryName);
        }
        catch (ResponseException e)
        {
            _logger.LogTrace(e, "ORAS blob push failed.");
            throw CreateContainerHttpException(Strings.RegistryPullFailed, e);
        }
    }

    private async Task MountBlobAsync(
        string destinationRepository,
        string sourceRepository,
        Descriptor descriptor,
        Func<CancellationToken, Task<Stream>> getContent,
        CancellationToken cancellationToken)
    {
        try
        {
            await _repositoryFactory.Create(destinationRepository).MountAsync(
                descriptor,
                sourceRepository,
                getContent,
                cancellationToken).ConfigureAwait(false);
        }
        catch (ResponseException e) when (e.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new UnableToAccessRepositoryException(RegistryName, destinationRepository);
        }
        catch (ResponseException e)
        {
            _logger.LogTrace(e, "ORAS blob mount failed.");
            throw CreateContainerHttpException(Strings.RegistryPullFailed, e);
        }
    }

    private async Task PushManifestAsync(
        string repositoryName,
        string reference,
        string manifestJson,
        string mediaType,
        CancellationToken cancellationToken)
    {
        byte[] manifestBytes = Encoding.UTF8.GetBytes(manifestJson);
        Descriptor descriptor = Descriptor.Create(manifestBytes, mediaType);
        using MemoryStream content = new(manifestBytes, writable: false);
        try
        {
            await _repositoryFactory.Create(repositoryName).Manifests.PushAsync(descriptor, content, reference, cancellationToken).ConfigureAwait(false);
        }
        catch (ResponseException e) when (e.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new UnableToAccessRepositoryException(RegistryName, repositoryName);
        }
        catch (ResponseException e)
        {
            _logger.LogTrace(e, "ORAS manifest push failed.");
            throw CreateContainerHttpException(Resource.FormatString(nameof(Strings.RegistryPushFailed), e.StatusCode), e);
        }
    }

    private static ContainerHttpException CreateContainerHttpException(string message, ResponseException exception) =>
        new(message, exception.RequestUri?.ToString(), exception.StatusCode);

    private readonly ref struct RepositoryFactoryProvider
    {
        private readonly IRepositoryFactory? _repositoryFactory;
        private readonly RegistryMode? _mode;

        public RepositoryFactoryProvider(IRepositoryFactory repositoryFactory)
        {
            _repositoryFactory = repositoryFactory;
        }

        public RepositoryFactoryProvider(RegistryMode mode)
        {
            _mode = mode;
        }

        public IRepositoryFactory Create(string registryName, Uri baseUri, ILogger logger, RegistrySettings settings)
        {
            return _repositoryFactory ?? new OrasRepositoryFactory(registryName, baseUri, settings, logger, _mode!.Value);
        }
    }
}
