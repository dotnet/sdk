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
    public Task<bool> ExistsAsync(string repositoryName, Descriptor descriptor, CancellationToken cancellationToken);

    public Task<JsonNode> GetJsonAsync(string repositoryName, Descriptor descriptor, CancellationToken cancellationToken);

    public Task<Stream> GetStreamAsync(string repositoryName, Descriptor descriptor, CancellationToken cancellationToken);

    public Task PushAsync(string repositoryName, Descriptor descriptor, Stream content, CancellationToken cancellationToken);

    public Task MountAsync(
        string destinationRepository,
        string sourceRepository,
        Descriptor descriptor,
        Func<CancellationToken, Task<Stream>> getContent,
        CancellationToken cancellationToken);
}
