// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

if (args is not [var urlArg])
{
    Console.Error.WriteLine();
    return -1;
}

Log($"Test browser opened at '{urlArg}'.");

var url = new Uri(urlArg, UriKind.Absolute);

var (webSocketUrls, publicKey, reconnectThroughApplication) = await GetWebSocketUrlsAndPublicKey(url);

var secret = RandomNumberGenerator.GetBytes(32);
var encryptedSecret = GetEncryptedSecret(publicKey, secret);

while (true)
{
    using var webSocket = await OpenWebSocket(webSocketUrls, encryptedSecret, logFailures: !reconnectThroughApplication);
    var buffer = new byte[8 * 1024];

    while (await TryReceiveMessageAsync(
        webSocket,
        message => Log($"Received: {Encoding.UTF8.GetString(message)}"),
        logFailures: !reconnectThroughApplication))
    {
    }

    if (!reconnectThroughApplication)
    {
        Log("WebSocket closed");
        break;
    }

    await WaitForBrowserToolsRouteAsync(url);
    Log("""Received: {"type":"Reload"}""");
}

return 0;

static async Task<WebSocket> OpenWebSocket(string[] urls, string encryptedSecret, bool logFailures)
{
    foreach (var url in urls)
    {
        try
        {
            var webSocket = new ClientWebSocket();
            webSocket.Options.AddSubProtocol(Uri.EscapeDataString(encryptedSecret));
            await webSocket.ConnectAsync(new Uri(url), CancellationToken.None);
            return webSocket;
        }
        catch (Exception e)
        {
            if (logFailures)
            {
                Log($"Error connecting to '{url}': {e.Message}");
            }
        }
    }

    throw new InvalidOperationException("Unable to establish a connection.");
}

static async ValueTask<bool> TryReceiveMessageAsync(
    WebSocket socket,
    Action<ReadOnlySpan<byte>> receiver,
    bool logFailures)
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
            if (logFailures)
            {
                Log($"Failed to receive response: {e.Message}");
            }
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

static async Task<(string[] url, string key, bool reconnectThroughApplication)> GetWebSocketUrlsAndPublicKey(Uri baseUrl)
{
    var sessionUrl = new Uri(baseUrl, "/_framework/dotnet-browser-tools/session.json");
    using var httpClient = new HttpClient();
    using var sessionResponse = await httpClient.GetAsync(sessionUrl);
    if (sessionResponse.IsSuccessStatusCode)
    {
        Log($"Request for '{sessionUrl}' succeeded");
        using var session = JsonDocument.Parse(await sessionResponse.Content.ReadAsStreamAsync());
        var modernWebSocketUrl = new UriBuilder(baseUrl)
        {
            Scheme = baseUrl.Scheme == Uri.UriSchemeHttps ? Uri.UriSchemeWss : Uri.UriSchemeWs,
            Path = "/_framework/dotnet-browser-tools/connect",
            Query = string.Empty,
        }.Uri.AbsoluteUri;
        var publicKey = session.RootElement.GetProperty("publicKey").GetString() ??
            throw new InvalidOperationException("Browser tools session did not contain a public key.");

        Log($"WebSocket url is '{modernWebSocketUrl}'.");
        Log($"Key is '{publicKey}'.");
        return ([modernWebSocketUrl], publicKey, reconnectThroughApplication: true);
    }

    if (sessionResponse.StatusCode != System.Net.HttpStatusCode.NotFound)
    {
        sessionResponse.EnsureSuccessStatusCode();
    }

    var refreshScriptUrl = new Uri(baseUrl, "/_framework/aspnetcore-browser-refresh.js");

    Log($"Fetching: {refreshScriptUrl}");

    var content = await httpClient.GetStringAsync(refreshScriptUrl);

    Log($"Request for '{refreshScriptUrl}' succeeded");
    var webSocketUrl = GetWebSocketUrls(content);
    var key = GetSharedSecretKey(content);

    Log($"WebSocket urls are '{string.Join(',', webSocketUrl)}'.");
    Log($"Key is '{key}'.");

    return (webSocketUrl, key, reconnectThroughApplication: false);
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

static string[] GetWebSocketUrls(string refreshScript)
{
    var pattern = "const webSocketUrls = '([^']+)'";

    var match = Regex.Match(refreshScript, pattern);
    if (!match.Success)
    {
        throw new InvalidOperationException($"Can't find web socket URL pattern in the script: {pattern}{Environment.NewLine}{refreshScript}");
    }

    return match.Groups[1].Value.Split(",");
}

static string GetSharedSecretKey(string refreshScript)
{
    var pattern = @"const sharedSecret = await getSecret\('([^']+)'\)";

    var match = Regex.Match(refreshScript, pattern);
    if (!match.Success)
    {
        throw new InvalidOperationException($"Can't find web socket shared secret pattern in the script: {pattern}{Environment.NewLine}{refreshScript}");
    }

    return match.Groups[1].Value;
}

// Equivalent to getSecret function in WebSocketScriptInjection.js:
static string GetEncryptedSecret(string key, byte[] secret)
{
    using var rsa = RSA.Create();
    rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(key), out _);
    return Convert.ToBase64String(rsa.Encrypt(secret, RSAEncryptionPadding.OaepSHA256));
}

static void Log(string message)
    => Console.WriteLine($"🧪 {message}");
