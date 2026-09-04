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

// The configuration module is part of the application's build output. Reading the public key from it
// - rather than from the provider - is what makes authenticating the provider meaningful.
var (configUrl, publicKey) = await GetConfigurationAsync(url);

var webSocketUrl = new UriBuilder(url)
{
    Scheme = url.Scheme == Uri.UriSchemeHttps ? Uri.UriSchemeWss : Uri.UriSchemeWs,
    Path = "/_framework/dotnet-browser-tools/connect",
    Query = string.Empty,
}.Uri.AbsoluteUri;

Log($"WebSocket url is '{webSocketUrl}'.");
Log($"Key is '{publicKey}'.");

var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
var encryptedSecret = GetEncryptedSecret(publicKey, secret);

while (true)
{
    using var webSocket = await OpenWebSocket(webSocketUrl, encryptedSecret);

    while (await TryReceiveMessageAsync(webSocket, message =>
    {
        var text = Encoding.UTF8.GetString(message);

        // The provider replays the current snapshot as part of the connection handshake and only
        // releases live messages on this connection once the browser acknowledges it. Report it
        // separately so that 'Received' keeps meaning 'live message'.
        if (TryGetSessionInitializationUpdateCount(text, out var updateCount))
        {
            Log($"Session initialized with {updateCount} update(s).");
            return true;
        }

        Log($"Received: {text}");
        return RequiresAcknowledgement(text);
    }))
    {
    }

    await WaitForApplicationAsync(configUrl);
    Log("""Received: {"type":"Reload"}""");
}

static async Task<WebSocket> OpenWebSocket(string url, string encryptedSecret)
{
    var webSocket = new ClientWebSocket();
    webSocket.Options.AddSubProtocol(Uri.EscapeDataString(encryptedSecret));
    await webSocket.ConnectAsync(new Uri(url), CancellationToken.None);
    return webSocket;
}

// The provider withholds live messages until the session initialization message is acknowledged and
// expects an acknowledgement for each update batch. All other messages are one way.
static bool TryGetSessionInitializationUpdateCount(string message, out int updateCount)
{
    using var document = JsonDocument.Parse(message);
    if (document.RootElement.TryGetProperty("type", out var type) &&
        type.GetString() == "InitializeSession")
    {
        updateCount = document.RootElement.TryGetProperty("updates", out var updates)
            ? updates.GetArrayLength()
            : 0;

        return true;
    }

    updateCount = 0;
    return false;
}

static bool RequiresAcknowledgement(string message)
{
    using var document = JsonDocument.Parse(message);
    return document.RootElement.TryGetProperty("type", out var type) &&
        type.GetString() is "ApplyManagedCodeUpdates";
}

static async Task AcknowledgeAsync(WebSocket socket)
{
    var response = Encoding.UTF8.GetBytes("""{"success":true,"log":[]}""");
    try
    {
        await socket.SendAsync(response, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
    }
    catch (Exception e) when (e is not OperationCanceledException)
    {
        Log($"Failed to acknowledge: {e.Message}");
    }
}

static async ValueTask<bool> TryReceiveMessageAsync(WebSocket socket, Func<byte[], bool> receiver)
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

    if (receiver(writer.WrittenSpan.ToArray()))
    {
        await AcknowledgeAsync(socket);
    }

    return true;
}

static async Task<(Uri configUrl, string key)> GetConfigurationAsync(Uri baseUrl)
{
    using var httpClient = new HttpClient();

    foreach (var candidate in GetConfigurationUrls(baseUrl))
    {
        using var response = await httpClient.GetAsync(candidate);
        if (!response.IsSuccessStatusCode)
        {
            continue;
        }

        Log($"Request for '{candidate}' succeeded");
        var content = await response.Content.ReadAsStringAsync();
        return (candidate, ParsePublicKey(content));
    }

    throw new InvalidOperationException("The application does not host a browser tools configuration module.");
}

static IEnumerable<Uri> GetConfigurationUrls(Uri baseUrl)
{
    yield return new Uri(baseUrl, "_framework/Microsoft.NET.Sdk.Web.DotNetWatch.BrowserTools.Config.js");
    yield return new Uri(baseUrl, "_framework/Microsoft.NET.Sdk.WebAssembly.DotNetWatch.BrowserTools.Config.js");
}

static string ParsePublicKey(string moduleContent)
{
    var match = Regex.Match(moduleContent, @"publicKey:\s*'(?<key>[^']*)'");
    return match.Success
        ? match.Groups["key"].Value
        : throw new InvalidOperationException("The browser tools configuration module does not contain a public key.");
}

static async Task WaitForApplicationAsync(Uri configUrl)
{
    using var httpClient = new HttpClient();

    while (true)
    {
        try
        {
            using var response = await httpClient.GetAsync(configUrl);
            if (response.IsSuccessStatusCode)
            {
                return;
            }
        }
        catch (HttpRequestException e)
        {
            Log($"Waiting for the application to return: {e.Message}");
        }

        await Task.Delay(100);
    }
}

// Equivalent to the browser tools client's shared-secret encryption:
static string GetEncryptedSecret(string key, string secret)
{
    using var rsa = RSA.Create();
    rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(key), out _);
    return Convert.ToBase64String(rsa.Encrypt(Convert.FromBase64String(secret), RSAEncryptionPadding.OaepSHA256));
}

static void Log(string message)
    => Console.WriteLine($"🧪 {message}");
