// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;
using System.Net.Http.Headers;

namespace Microsoft.NET.Sdk.Publish.Tasks;

internal class DefaultHttpClient : IHttpClient, IDisposable
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    /// <inheritdoc/>
    public HttpRequestHeaders DefaultRequestHeaders => _httpClient.DefaultRequestHeaders;

    /// <inheritdoc/>
    public Task<HttpResponseMessage> PostAsync(Uri uri, StreamContent content)
    {
        return _httpClient.PostAsync(uri, content);
    }

    /// <inheritdoc/>
    public Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken cancellationToken)
    {
        return _httpClient.GetAsync(uri, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<HttpResponseMessage> PutAsync(Uri uri, StreamContent content, CancellationToken cancellationToken)
    {
        return _httpClient.PutAsync(uri, content, cancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
