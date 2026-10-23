// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Represents registry v2 API, blob operations.
/// </summary>
/// <remarks>
/// https://docs.docker.com/registry/spec/api/#blob
/// </remarks>
internal interface IBlobOperations
{
    public IBlobUploadOperations Upload { get; }

    public Task<bool> ExistsAsync(string repositoryName, string digest, CancellationToken cancellationToken);

    public Task<JsonNode> GetJsonAsync(string repositoryName, string digest, CancellationToken cancellationToken);

    /// <summary>
    /// Fetches the blob for <paramref name="digest"/> as a stream.
    /// </summary>
    /// <remarks>
    /// Callers are responsible for checking stream contents against <paramref name="digest"/>.
    /// <see cref="StreamExtensions.CopyToAndVerifyAsync"/> exists to help with this.
    /// </remarks>
    public Task<Stream> GetUnvalidatedStreamAsync(string repositoryName, string digest, CancellationToken cancellationToken);
}
