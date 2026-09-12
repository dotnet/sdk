// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;
using OrasProject.Oras.Registry.Remote.Exceptions;
using OrasDescriptor = OrasProject.Oras.Oci.Descriptor;

namespace Microsoft.NET.Build.Containers;

internal sealed class DefaultBlobOperations(
    OrasRepositoryFactory repositoryFactory,
    string registryName,
    ILogger logger) : IBlobOperations
{
    public async Task<bool> ExistsAsync(string repositoryName, Descriptor descriptor, CancellationToken cancellationToken)
    {
        try
        {
            return await repositoryFactory.Create(repositoryName).Blobs.ExistsAsync(ToOrasDescriptor(descriptor), cancellationToken).ConfigureAwait(false);
        }
        catch (ResponseException e) when (e.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new UnableToAccessRepositoryException(registryName, repositoryName);
        }
        catch (ResponseException e)
        {
            logger.LogTrace(e, "ORAS blob existence check failed.");
            throw CreateContainerHttpException(e);
        }
    }

    public async Task<JsonNode> GetJsonAsync(string repositoryName, Descriptor descriptor, CancellationToken cancellationToken)
    {
        await using Stream stream = await GetStreamAsync(repositoryName, descriptor, cancellationToken).ConfigureAwait(false);
        return await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException($"Blob '{descriptor.Digest}' contained invalid JSON.");
    }

    public async Task<Stream> GetStreamAsync(string repositoryName, Descriptor descriptor, CancellationToken cancellationToken)
    {
        try
        {
            return await repositoryFactory.Create(repositoryName).Blobs.FetchAsync(ToOrasDescriptor(descriptor), cancellationToken).ConfigureAwait(false);
        }
        catch (ResponseException e) when (e.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new UnableToAccessRepositoryException(registryName, repositoryName);
        }
        catch (ResponseException e)
        {
            logger.LogTrace(e, "ORAS blob pull failed.");
            throw CreateContainerHttpException(e);
        }
    }

    public async Task PushAsync(string repositoryName, Descriptor descriptor, Stream content, CancellationToken cancellationToken)
    {
        try
        {
            await repositoryFactory.Create(repositoryName).Blobs.PushAsync(ToOrasDescriptor(descriptor), content, cancellationToken).ConfigureAwait(false);
        }
        catch (ResponseException e) when (e.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new UnableToAccessRepositoryException(registryName, repositoryName);
        }
        catch (ResponseException e)
        {
            logger.LogTrace(e, "ORAS blob push failed.");
            throw CreateContainerHttpException(e);
        }
    }

    public async Task MountAsync(
        string destinationRepository,
        string sourceRepository,
        Descriptor descriptor,
        Func<CancellationToken, Task<Stream>> getContent,
        CancellationToken cancellationToken)
    {
        try
        {
            await repositoryFactory.Create(destinationRepository).MountAsync(
                ToOrasDescriptor(descriptor),
                sourceRepository,
                getContent,
                cancellationToken).ConfigureAwait(false);
        }
        catch (ResponseException e) when (e.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new UnableToAccessRepositoryException(registryName, destinationRepository);
        }
        catch (ResponseException e)
        {
            logger.LogTrace(e, "ORAS blob mount failed.");
            throw CreateContainerHttpException(e);
        }
    }

    private static OrasDescriptor ToOrasDescriptor(Descriptor descriptor) => new()
    {
        MediaType = descriptor.MediaType,
        Digest = descriptor.Digest,
        Size = descriptor.Size,
    };

    private static ContainerHttpException CreateContainerHttpException(ResponseException exception)
        => new(Resource.GetString(nameof(Strings.RegistryPullFailed)), exception.RequestUri?.ToString(), exception.StatusCode);
}
