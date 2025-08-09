// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Represents registry v2 API, manifest operations.
/// </summary>
/// <remarks>
/// https://docs.docker.com/registry/spec/api/#manifest
/// </remarks>
internal interface IManifestOperations
{

    /// <summary>
    /// Gets a manifest from the registry.
    /// </summary>
    /// <param name="repositoryName">The name of the repository to get the manifest from.</param>
    /// <param name="reference">A tag reference to reference this manifest by.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    public Task<HttpResponseMessage> GetAsync(string repositoryName, string reference, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a manifest from the registry.
    /// </summary>
    /// <param name="repositoryName">The name of the repository to get the manifest from.</param>
    /// <param name="digest">The digest of the manifest exactly.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    public Task<HttpResponseMessage> GetAsync(string repositoryName, Digest digest, CancellationToken cancellationToken);

    /// <summary>
    /// Puts a manifest into the registry.
    /// </summary>
    /// <param name="repositoryName">The name of the repository to put the manifest into.</param>
    /// <param name="digest">Either a tag reference or a digest string to reference this manifest by.</param>
    /// <param name="manifest">The manifest to put into the registry.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns></returns>
    public Task PutAsync<T>(string repositoryName, Digest digest, T manifest, CancellationToken cancellationToken) where T : IManifest;
    /// <summary>
    /// Puts a manifest into the registry.
    /// </summary>
    /// <param name="repositoryName">The name of the repository to put the manifest into.</param>
    /// <param name="reference">A tag reference to reference this manifest by.</param>
    /// <param name="manifest">The manifest to put into the registry.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns></returns>
    public Task PutAsync<T>(string repositoryName, string reference, T manifest, CancellationToken cancellationToken) where T : IManifest;
}
