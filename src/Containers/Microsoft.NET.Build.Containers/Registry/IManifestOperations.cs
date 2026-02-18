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
    public Task<HttpResponseMessage> GetAsync(string repositoryName, string reference, CancellationToken cancellationToken);

    public Task PutAsync(string repositoryName, string reference, string manifestListJson, string mediaType, CancellationToken cancellationToken);
}
