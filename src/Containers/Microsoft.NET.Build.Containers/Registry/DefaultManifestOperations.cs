﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

    public async Task<HttpResponseMessage> GetAsync(string repositoryName, string reference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseUri, $"/v2/{repositoryName}/manifests/{reference}")).AcceptManifestFormats();
        HttpResponseMessage response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return response.StatusCode switch
        {
            HttpStatusCode.OK => response,
            HttpStatusCode.NotFound => throw new RepositoryNotFoundException(_registryName, repositoryName, reference),
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => throw new UnableToAccessRepositoryException(_registryName, repositoryName),
            _ => await LogAndThrowContainerHttpException<HttpResponseMessage>(response, cancellationToken).ConfigureAwait(false)
        };
    }
        public async Task<HttpResponseMessage> GetAsync(string repositoryName, Digest digest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseUri, $"/v2/{repositoryName}/manifests/{digest.ToUriString()}")).AcceptManifestFormats();
        HttpResponseMessage response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return response.StatusCode switch
        {
            HttpStatusCode.OK => response,
            HttpStatusCode.NotFound => throw new RepositoryNotFoundException(_registryName, repositoryName, digest.ToString()),
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => throw new UnableToAccessRepositoryException(_registryName, repositoryName),
            _ => await LogAndThrowContainerHttpException<HttpResponseMessage>(response, cancellationToken).ConfigureAwait(false)
        };
    }

    public async Task PutAsync<T>(string repositoryName, string reference, T manifest, CancellationToken cancellationToken) where T : IManifest
    {
        JsonContent manifestUploadContent = JsonContent.Create(manifest, mediaType: new MediaTypeHeaderValue(manifest.MediaType!));
        HttpResponseMessage putResponse = await _client.PutAsync(new Uri(_baseUri, $"/v2/{repositoryName}/manifests/{reference}"), manifestUploadContent, cancellationToken).ConfigureAwait(false);

        if (!putResponse.IsSuccessStatusCode)
        {
            await putResponse.LogHttpResponseAsync(_logger, cancellationToken).ConfigureAwait(false);
            throw new ContainerHttpException(Resource.FormatString(nameof(Strings.RegistryPushFailed), putResponse.StatusCode), putResponse.RequestMessage?.RequestUri?.ToString(), putResponse.StatusCode);
        }
    }
        public async Task PutAsync<T>(string repositoryName, Digest digest, T manifest, CancellationToken cancellationToken) where T : IManifest
    {
        JsonContent manifestUploadContent = JsonContent.Create(manifest, mediaType: new MediaTypeHeaderValue(manifest.MediaType!));
        HttpResponseMessage putResponse = await _client.PutAsync(new Uri(_baseUri, $"/v2/{repositoryName}/manifests/{digest.ToUriString()}"), manifestUploadContent, cancellationToken).ConfigureAwait(false);

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
