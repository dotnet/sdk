// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;
using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Registry.Remote.Exceptions;
using OrasDescriptor = OrasProject.Oras.Oci.Descriptor;

namespace Microsoft.NET.Build.Containers;

internal sealed class DefaultManifestOperations : IManifestOperations
{
    private readonly OrasRepositoryFactory _repositoryFactory;
    private readonly ILogger _logger;
    private readonly string _registryName;

    internal DefaultManifestOperations(OrasRepositoryFactory repositoryFactory, string registryName, ILogger logger)
    {
        _repositoryFactory = repositoryFactory;
        _logger = logger;
        _registryName = registryName;
    }

    public async Task<bool> ExistsAsync(string repositoryName, string reference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
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

    public async Task<HttpResponseMessage> GetAsync(string repositoryName, string reference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            (OrasDescriptor descriptor, Stream stream) = await _repositoryFactory.Create(repositoryName).Manifests.FetchAsync(reference, cancellationToken).ConfigureAwait(false);
            HttpResponseMessage response = new(System.Net.HttpStatusCode.OK)
            {
                Content = new StreamContent(stream),
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(descriptor.MediaType);
            response.Content.Headers.ContentLength = descriptor.Size;
            response.Headers.TryAddWithoutValidation("Docker-Content-Digest", descriptor.Digest);
            return response;
        }
        catch (NotFoundException)
        {
            throw new RepositoryNotFoundException(_registryName, repositoryName, reference);
        }
        catch (ResponseException e) when (e.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            throw new UnableToAccessRepositoryException(_registryName, repositoryName);
        }
        catch (ResponseException e)
        {
            throw CreateContainerHttpException(Strings.RegistryPullFailed, e);
        }
    }

    public async Task PutAsync(string repositoryName, string reference, string manifestJson, string mediaType, CancellationToken cancellationToken)
    {
        byte[] manifestBytes = Encoding.UTF8.GetBytes(manifestJson);
        OrasDescriptor descriptor = OrasDescriptor.Create(manifestBytes, mediaType);
        using MemoryStream content = new(manifestBytes, writable: false);
        try
        {
            await _repositoryFactory.Create(repositoryName).Manifests.PushAsync(descriptor, content, reference, cancellationToken).ConfigureAwait(false);
        }
        catch (ResponseException e) when (e.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            throw new UnableToAccessRepositoryException(_registryName, repositoryName);
        }
        catch (ResponseException e)
        {
            _logger.LogTrace(e, "ORAS manifest push failed.");
            throw CreateContainerHttpException(Resource.FormatString(nameof(Strings.RegistryPushFailed), e.StatusCode), e);
        }
    }

    private static ContainerHttpException CreateContainerHttpException(string message, ResponseException exception)
        => new(message, exception.RequestUri?.ToString(), exception.StatusCode);
}
