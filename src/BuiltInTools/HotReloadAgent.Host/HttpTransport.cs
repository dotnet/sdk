// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.HotReload;

/// <summary>
/// HTTP-based client for hot reload communication.
/// Used for mobile platforms (Android, iOS, MacCatalyst) where named pipes don't work over the network.
/// </summary>
internal sealed class HttpTransport(string baseUrl, Action<string> log, int connectionTimeoutMS) : Transport(log)
{
    // Use infinite timeout for the HttpClient since polling can take arbitrarily long.
    // The connectionTimeoutMS is used for the initial connect via CancellationToken.
    private readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri(baseUrl),
        Timeout = Timeout.InfiniteTimeSpan
    };

    public override void Dispose()
        => _httpClient.Dispose();

    public override string DisplayName
        => $"HTTP {baseUrl}";

    public override async ValueTask SendAsync(IResponse response, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();

        if (response.Type != ResponseType.InitializationResponse)
        {
            await stream.WriteAsync((byte)response.Type, cancellationToken);
        }

        await response.WriteAsync(stream, cancellationToken);
        stream.Position = 0;

        var url = response.Type == ResponseType.InitializationResponse ? "connect" : "httpMessage";
        Log($"POST {url} ({response.Type}, {stream.Length} bytes)");

        using var cancellationSourceWithTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // only apply the timeout for the initial connection:
        if (response.Type == ResponseType.InitializationResponse)
        {
            cancellationSourceWithTimeout.CancelAfter(connectionTimeoutMS);
        }

        try
        {
            using var content = new StreamContent(stream);
            using var httpMessage = await _httpClient.PostAsync(url, content, cancellationSourceWithTimeout.Token);

            httpMessage.EnsureSuccessStatusCode();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Failed to connect in {connectionTimeoutMS}ms.");
        }
    }

    public override async ValueTask<RequestStream> ReceiveAsync(CancellationToken cancellationToken)
    {
        // Poll for updates by sending a request to /poll
        // The server will hold the connection open until there's an update to send
        while (true)
        {
            const string url = "poll";
            Log($"GET {url}");

            using var httpMessage = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            try
            {
                httpMessage.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException e)
            {
                Log(e.Message);

                // retry:
                await Task.Delay(100, cancellationToken);
                continue;
            }

            return new RequestStream(await httpMessage.Content.ReadAsStreamAsync(cancellationToken), disposeOnCompletion: true);
        }
    }
}
