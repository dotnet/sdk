// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

internal class DefaultManifestOperations : IManifestOperations
{
    private readonly Uri _baseUri;
    private readonly HttpClient _client;
    private readonly ILogger _logger;
    private readonly string _registryName;

    internal DefaultManifestOperations(Uri baseUri, string registryName, HttpClient client, ILogger logger)
    {
        _baseUri = baseUri;
        _client = client;
        _logger = logger;
        _registryName = registryName;
    }

    public async Task<bool> ExistsAsync(string repositoryName, string reference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, new Uri(_baseUri, $"/v2/{repositoryName}/manifests/{reference}")).AcceptManifestFormats();
        using HttpResponseMessage response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return response.StatusCode switch
        {
            HttpStatusCode.OK => true,
            _ when (int)response.StatusCode >= 500 => await LogAndThrowContainerHttpException<bool>(response, cancellationToken).ConfigureAwait(false),
            _ => false,
        };
    }

    public async Task<ManifestResponse> GetAsync(string repositoryName, string reference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseUri, $"/v2/{repositoryName}/manifests/{reference}")).AcceptManifestFormats();
        using HttpResponseMessage response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is not HttpStatusCode.OK)
        {
            return response.StatusCode switch
            {
                HttpStatusCode.NotFound => throw new RepositoryNotFoundException(_registryName, repositoryName, reference),
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => throw new UnableToAccessRepositoryException(_registryName, repositoryName),
                _ => await LogAndThrowContainerHttpException<ManifestResponse>(response, cancellationToken).ConfigureAwait(false)
            };
        }

        ReadOnlyMemory<byte> content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        string? contentDigest =
            response.Headers.TryGetValues("Docker-Content-Digest", out IEnumerable<string>? values)
                ? values.FirstOrDefault()
                : null;

        string? knownDigest = DigestUtils.TryParseDigest(reference, out _, out _)
            ? reference
            : contentDigest;

        // SAFETY: OCI registry specification does not require the Docker-Content-Digest header.
        // Content fetched by tag can only be validated when the header is present.
        // See https://github.com/opencontainers/distribution-spec/blob/main/spec.md#pulling-manifests
        if (knownDigest is not null)
        {
            DigestUtils.ValidateDigestContent(knownDigest, content.Span);
        }

        return new ManifestResponse(
            Content: content,
            KnownDigest: knownDigest,
            MediaType: response.Content.Headers.ContentType?.MediaType
        );
    }

    public async Task PutAsync(string repositoryName, string reference, string manifestJson, string mediaType, CancellationToken cancellationToken)
    {
        HttpContent manifestUploadContent = new StringContent(manifestJson);
        manifestUploadContent.Headers.ContentType = new MediaTypeHeaderValue(mediaType);

        HttpResponseMessage putResponse = await _client.PutAsync(new Uri(_baseUri, $"/v2/{repositoryName}/manifests/{reference}"), manifestUploadContent, cancellationToken).ConfigureAwait(false);

        if (!putResponse.IsSuccessStatusCode)
        {
            await putResponse.LogHttpResponseAsync(_logger, cancellationToken).ConfigureAwait(false);
            throw new ContainerHttpException(Resource.FormatString(nameof(Strings.RegistryPushFailed), putResponse.StatusCode), putResponse.RequestMessage?.RequestUri?.ToString(), putResponse.StatusCode);
        }
    }

    private async Task<T> LogAndThrowContainerHttpException<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await response.LogHttpResponseAsync(_logger, cancellationToken).ConfigureAwait(false);
        throw new ContainerHttpException(Resource.GetString(nameof(Strings.RegistryPullFailed)), response.RequestMessage?.RequestUri?.ToString(), response.StatusCode);
    }
}
