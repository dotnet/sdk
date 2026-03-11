// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;
using System.Net.Http.Headers;

namespace Microsoft.NET.Sdk.Publish.Tasks;

/// <summary>
/// Sends HTTP requests.
/// </summary>
public interface IHttpClient
{
    /// <summary>
    /// The Headers of a request.
    /// </summary>
    HttpRequestHeaders DefaultRequestHeaders { get; }

    /// <summary>
    /// Sends an HTTP POST request.
    /// </summary>
    /// <param name="uri">URI to send the request to</param>
    /// <param name="content">request payload</param>
    /// <returns>response of request</returns>
    Task<HttpResponseMessage> PostAsync(Uri uri, StreamContent content);

    /// <summary>
    /// Sends an HTTP GET request.
    /// </summary>
    /// <param name="uri">URI to send the request to</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>response of the request</returns>
    Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken cancellationToken);

    /// <summary>
    /// Sends an HTTP PUT request.
    /// </summary>
    /// <param name="uri">URI to send the request to</param>
    /// <param name="content">request payload</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>response of request</returns>
    Task<HttpResponseMessage> PutAsync(Uri uri, StreamContent content, CancellationToken cancellationToken);
}
