// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.Registry;

internal class DefaultManifestOperations : IManifestOperations
{
    private HttpClient Client { get; }
    public DefaultManifestOperations(HttpClient client)
    {
        Client = client;
    }

    public async Task<HttpResponseMessage> GetAsync(string repositoryName, string reference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/v2/{repositoryName}/manifests/{reference}")).AcceptManifestFormats();
        var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return response;
    }

    public async Task<HttpResponseMessage> PutAsync(string repositoryName, string reference, HttpContent content, CancellationToken cancellationToken)
    {
        return await Client.PutAsync(new Uri($"/v2/{repositoryName}/manifests/{reference}"), content, cancellationToken).ConfigureAwait(false);
    }
}
