// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.Registry;

// represents the raw API that an external registry implements.

internal interface IManifestOperations
{
    public Task<HttpResponseMessage> GetAsync(string repositoryName, string reference, CancellationToken cancellationToken);
    public Task<HttpResponseMessage> PutAsync(string repositoryName, string reference, HttpContent content, CancellationToken cancellationToken);
}
