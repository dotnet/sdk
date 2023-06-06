// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.Registry;

internal interface IBlobOperations
{
    public Task<HttpResponseMessage> GetAsync(string repositoryName, string digest, CancellationToken cancellationToken);
    public Task<bool> ExistsAsync(string repositoryName, string digest, CancellationToken cancellationToken);
    public IBlobUploadOperations Upload { get; }
}
