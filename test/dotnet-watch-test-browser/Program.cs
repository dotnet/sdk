// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;

if (args is not [var urlArg])
{
    Console.Error.WriteLine();
    return -1;
}

Log($"Test browser opened at '{urlArg}'.");

var url = new Uri(urlArg, UriKind.Absolute);

var (webSocketUrl, publicKey) = await GetWebSocketUrlAndPublicKey(url);

var secret = RandomNumberGenerator.GetBytes(32);
var encryptedSecret = GetEncryptedSecret(publicKey, secret);

while (true)
{
    using var webSocket = await OpenWebSocket(webSocketUrl, encryptedSecret);
    var buffer = new byte[8 * 1024];

    while (await TryReceiveMessageAsync(
        webSocket,
        message => Log($"Received: {Encoding.UTF8.GetString(message)}")))
    {
    }

    await WaitForBrowserToolsRouteAsync(url);
    Log("""Received: {"type":"Reload"}""");
}

static async Task<WebSocket> OpenWebSocket(string url, string encryptedSecret)
{
    var webSocket = new ClientWebSocket();
    webSocket.Options.AddSubProtocol(Uri.EscapeDataString(encryptedSecret));
    await webSocket.ConnectAsync(new Uri(url), CancellationToken.None);
    return webSocket;
}

static async ValueTask<bool> TryReceiveMessageAsync(
    WebSocket socket,
    Action<ReadOnlySpan<byte>> receiver)
{
    var writer = new ArrayBufferWriter<byte>(initialCapacity: 1024);

    while (true)
    {
        ValueWebSocketReceiveResult result;
        var data = writer.GetMemory();
        try
        {
            result = await socket.ReceiveAsync(data, CancellationToken.None);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Log($"Failed to receive response: {e.Message}");
            return false;
        }

        if (result.MessageType == WebSocketMessageType.Close)
        {
            return false;
        }

        writer.Advance(result.Count);
        if (result.EndOfMessage)
        {
            break;
        }
    }

    receiver(writer.WrittenSpan);
    return true;
}

static async Task<(string url, string key)> GetWebSocketUrlAndPublicKey(Uri baseUrl)
{
    var sessionUrl = new Uri(baseUrl, "/_framework/dotnet-browser-tools/session.json");
    using var httpClient = new HttpClient();
    using var sessionResponse = await httpClient.GetAsync(sessionUrl);
    sessionResponse.EnsureSuccessStatusCode();
    Log($"Request for '{sessionUrl}' succeeded");
    using var session = JsonDocument.Parse(await sessionResponse.Content.ReadAsStreamAsync());
    var webSocketUrl = new UriBuilder(baseUrl)
    {
        Scheme = baseUrl.Scheme == Uri.UriSchemeHttps ? Uri.UriSchemeWss : Uri.UriSchemeWs,
        Path = "/_framework/dotnet-browser-tools/connect",
        Query = string.Empty,
    }.Uri.AbsoluteUri;
    var publicKey = session.RootElement.GetProperty("publicKey").GetString() ??
        throw new InvalidOperationException("Browser tools session did not contain a public key.");

    Log($"WebSocket url is '{webSocketUrl}'.");
    Log($"Key is '{publicKey}'.");
    return (webSocketUrl, publicKey);
}

static async Task WaitForBrowserToolsRouteAsync(Uri baseUrl)
{
    var sessionUrl = new Uri(baseUrl, "/_framework/dotnet-browser-tools/session.json");
    using var httpClient = new HttpClient();

    while (true)
    {
        try
        {
            using var response = await httpClient.GetAsync(sessionUrl);
            if (response.IsSuccessStatusCode)
            {
                return;
            }
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"Waiting for browser tools route: {e.Message}");
        }

        await Task.Delay(100);
    }
}

// Equivalent to the browser tools client's shared-secret encryption:
static string GetEncryptedSecret(string key, byte[] secret)
{
    using var rsa = RSA.Create();
    rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(key), out _);
    return Convert.ToBase64String(rsa.Encrypt(secret, RSAEncryptionPadding.OaepSHA256));
}

static void Log(string message)
    => Console.WriteLine($"🧪 {message}");
