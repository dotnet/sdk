// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;

namespace Microsoft.NET.Build.Containers.Registry;

internal class DefaultBlobOperations : IBlobOperations
{
    private HttpClient Client { get; }
    public DefaultBlobOperations(HttpClient client)
    {
        Client = client;
        Upload = new DefaultBlobUploadOperations(Client);
    }

    public IBlobUploadOperations Upload { get; }

    public async Task<HttpResponseMessage> GetAsync(string repositoryName, string digest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(Client.BaseAddress!, $"/v2/{repositoryName}/blobs/{digest}"));
        var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return response;
    }

    public async Task<bool> ExistsAsync(string repositoryName, string digest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HttpResponseMessage response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Head, new Uri(Client.BaseAddress!, $"/v2/{repositoryName}/blobs/{digest}")), cancellationToken).ConfigureAwait(false);
        return response.StatusCode == HttpStatusCode.OK;
    }
}
