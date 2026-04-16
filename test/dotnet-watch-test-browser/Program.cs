using System;
using System.Buffers;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

if (args is not [var urlArg])
{
    Console.Error.WriteLine();
    return -1;
}

Log($"Test browser opened at '{urlArg}'.");

var url = new Uri(urlArg, UriKind.Absolute);

var (webSocketUrls, publicKey) = await GetWebSocketUrlsAndPublicKey(url);

var secret = RandomNumberGenerator.GetBytes(32);
var encryptedSecret = GetEncryptedSecret(publicKey, secret);

using var webSocket = await OpenWebSocket(webSocketUrls, encryptedSecret);
var buffer = new byte[8 * 1024];

while (await TryReceiveMessageAsync(webSocket, message => Log($"Received: {Encoding.UTF8.GetString(message)}")))
{
}

Log("WebSocket closed");

return 0;

static async Task<WebSocket> OpenWebSocket(string[] urls, string encryptedSecret)
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
            Log($"Error connecting to '{url}': {e.Message}");
        }
    }

    throw new InvalidOperationException("Unable to establish a connection.");
}

static async ValueTask<bool> TryReceiveMessageAsync(WebSocket socket, Action<ReadOnlySpan<byte>> receiver)
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

static async Task<(string[] url, string key)> GetWebSocketUrlsAndPublicKey(Uri baseUrl)
{
    var refreshScriptUrl = new Uri(baseUrl, "/_framework/aspnetcore-browser-refresh.js");

    Log($"Fetching: {refreshScriptUrl}");

    using var httpClient = new HttpClient();
    var content = await httpClient.GetStringAsync(refreshScriptUrl);

    Log($"Request for '{refreshScriptUrl}' succeeded");
    var webSocketUrl = GetWebSocketUrls(content);
    var key = GetSharedSecretKey(content);

    Log($"WebSocket urls are '{string.Join(',', webSocketUrl)}'.");
    Log($"Key is '{key}'.");

    return (webSocketUrl, key);
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
