// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Represents registry v2 API, manifest operations.
/// </summary>
/// <remarks>
/// https://docs.docker.com/registry/spec/api/#manifest
/// </remarks>
internal interface IManifestOperations
{
    public Task<bool> ExistsAsync(string repositoryName, string reference, CancellationToken cancellationToken);

    /// <summary>
    /// Fetches the manifest for <paramref name="tagOrDigest"/>, validating its content against the
    /// requested digest and the Docker-Content-Digest response header when either is present.
    /// </summary>
    public Task<ManifestResponse> GetAsync(string repositoryPath, string tagOrDigest, CancellationToken cancellationToken);

    public Task PutAsync(string repositoryName, string reference, string manifestListJson, string mediaType, CancellationToken cancellationToken);
}
