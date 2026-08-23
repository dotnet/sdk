// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry.Remote.Auth;

namespace Microsoft.NET.Build.Containers;

internal sealed class OrasRepositoryFactory(Uri baseUri, IClient client, RegistrySettings settings)
{
    private const int DefaultChunkSizeBytes = 64 * 1024;

    public IRepository Create(string repositoryName) => new Repository(new RepositoryOptions
    {
        BlobUploadChunkSize = settings.ChunkedUploadSizeBytes ?? DefaultChunkSizeBytes,
        BlobUploadMode = settings.ForceChunkedUpload ?
            BlobUploadMode.Chunked :
            BlobUploadMode.MonolithicWithChunkedFallback,
        Client = client,
        Reference = Reference.Parse($"{baseUri.Authority}/{repositoryName}"),
    });
}
