// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;

namespace Microsoft.DotNet.Watch.UnitTests;

/// <summary>
/// Minimal HTTP client that hides the difference between HttpClient (.NET) and HttpWebRequest (.NET Framework).
/// Non-success status codes are returned, not thrown.
/// </summary>
internal static class TestHttpClient
{
    public static Task<TestHttpResponse> GetAsync(Uri baseAddress, string path, CancellationToken cancellationToken)
        => SendAsync("GET", new Uri(baseAddress, path), cancellationToken);

    public static Task<TestHttpResponse> PostAsync(Uri baseAddress, string path, CancellationToken cancellationToken)
        => SendAsync("POST", new Uri(baseAddress, path), cancellationToken);

#if NET
    private static async Task<TestHttpResponse> SendAsync(string method, Uri uri, CancellationToken cancellationToken)
    {
        using var client = new HttpClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), uri);
        using var response = await client.SendAsync(request, cancellationToken);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in response.Headers.Concat(response.Content.Headers))
        {
            headers[header.Key] = string.Join(", ", header.Value);
        }

        return new TestHttpResponse(
            response.StatusCode,
            response.Content.Headers.ContentType?.MediaType,
            headers,
            await response.Content.ReadAsStringAsync(cancellationToken));
    }
#else
    private static async Task<TestHttpResponse> SendAsync(string method, Uri uri, CancellationToken cancellationToken)
    {
        var request = (HttpWebRequest)WebRequest.Create(uri);
        request.Method = method;
        if (method != "GET")
        {
            request.ContentLength = 0;
        }

        using var registration = cancellationToken.Register(request.Abort);

        HttpWebResponse response;
        try
        {
            response = (HttpWebResponse)await request.GetResponseAsync();
        }
        catch (WebException e) when (e.Response is HttpWebResponse errorResponse)
        {
            response = errorResponse;
        }

        using (response)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in response.Headers.AllKeys)
            {
                headers[name] = response.Headers[name];
            }

            string body;
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                body = await reader.ReadToEndAsync();
            }

            var contentType = response.ContentType;
            var mediaType = string.IsNullOrEmpty(contentType) ? null : contentType.Split(';')[0].Trim();

            return new TestHttpResponse(response.StatusCode, mediaType, headers, body);
        }
    }
#endif

    internal sealed class TestHttpResponse(
        HttpStatusCode statusCode,
        string? mediaType,
        IReadOnlyDictionary<string, string> headers,
        string body)
    {
        public HttpStatusCode StatusCode { get; } = statusCode;
        public string? MediaType { get; } = mediaType;
        public IReadOnlyDictionary<string, string> Headers { get; } = headers;
        public string Body { get; } = body;

        public string? GetHeader(string name)
            => Headers.TryGetValue(name, out var value) ? value : null;
    }

}
