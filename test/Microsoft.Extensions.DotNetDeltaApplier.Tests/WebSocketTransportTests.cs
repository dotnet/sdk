// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.HotReload.UnitTests;

[TestClass]
public class WebSocketTransportTests
{
    private static readonly WebSocketConfig Config = new(0, securePort: null, hostName: null, additionalAllowedOrigins: []);

    private sealed class BuildOutputs : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "mobile-hot-reload", Guid.NewGuid().ToString("N"));
        private readonly string _keyDirectory;
        public SharedSecretBuildOutputs Outputs { get; }

        public BuildOutputs()
        {
            _keyDirectory = Path.Combine(_directory, SharedSecretBuildOutputs.DirectoryName);
            Directory.CreateDirectory(_keyDirectory);
            Outputs = new SharedSecretBuildOutputs(_keyDirectory, SharedSecretBuildOutputs.WebSocketPublicKeyFileName, SharedSecretBuildOutputs.WebSocketPrivateKeyFileName);
        }

        public SharedSecretProvider WriteKey()
        {
            var key = new SharedSecretProvider();
            var p = key.RSA.ExportParameters(includePrivateParameters: true);
            var publicKey = key.GetPublicKey();
            File.WriteAllText(Path.Combine(_keyDirectory, SharedSecretBuildOutputs.WebSocketPublicKeyFileName), JsonSerializer.Serialize(new
            {
                version = 1, algorithm = "RSA-OAEP-SHA256", format = "SubjectPublicKeyInfo", publicKey
            }));
            File.WriteAllText(Path.Combine(_keyDirectory, SharedSecretBuildOutputs.WebSocketPrivateKeyFileName), JsonSerializer.Serialize(new
            {
                version = 1, algorithm = "RSA-OAEP-SHA256", format = "RSAParameters", publicKey,
                parameters = new { modulus = p.Modulus, exponent = p.Exponent, d = p.D, p = p.P, q = p.Q, dp = p.DP, dq = p.DQ, inverseQ = p.InverseQ }
            }));
            return key;
        }

        public void Dispose() => Directory.Delete(_directory, recursive: true);
    }

    [TestMethod]
    public async Task AuthenticatesWithBuildKeyBeforeSendingInitialization()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var outputs = new BuildOutputs();
        using var key = outputs.WriteKey();
        using var server = await WebSocketClientTransport.CreateAsync(Config, outputs.Outputs, NullLogger.Instance, cts.Token);
        var environment = new Dictionary<string, string>();
        server.ConfigureEnvironment(environment);
        Assert.HasCount(1, environment);

        using var agent = new WebSocketTransport(environment[AgentEnvironmentVariables.DotNetWatchHotReloadWebSocketEndpoint], key.GetPublicKey(), _ => { }, 5000);
        await agent.SendAsync(new ClientInitializationResponse(Environment.ProcessId, "Baseline"), cts.Token);
        await server.WaitForConnectionAsync(cts.Token);
        using var response = await server.ReadAsync(cts.Token);
        Assert.IsNotNull(response);
        Assert.AreEqual(ResponseType.InitializationResponse, response.Value.Type);
        var initialization = await ClientInitializationResponse.ReadAsync(response.Value.Data, cts.Token);
        Assert.AreEqual(Environment.ProcessId, initialization.ProcessId);
        Assert.AreEqual("Baseline", initialization.Capabilities);
    }

    [TestMethod]
    public async Task LoadsCurrentBuildKeyWhenAgentConnects()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var outputs = new BuildOutputs();
        using var originalKey = outputs.WriteKey();
        using var server = await WebSocketClientTransport.CreateAsync(Config, outputs.Outputs, NullLogger.Instance, cts.Token);
        using var currentKey = outputs.WriteKey();
        var environment = new Dictionary<string, string>();
        server.ConfigureEnvironment(environment);
        using var agent = new WebSocketTransport(environment[AgentEnvironmentVariables.DotNetWatchHotReloadWebSocketEndpoint], currentKey.GetPublicKey(), _ => { }, 5000);
        await agent.SendAsync(new ClientInitializationResponse(Environment.ProcessId, "Baseline"), cts.Token);
        await server.WaitForConnectionAsync(cts.Token);
    }

    [TestMethod]
    public async Task AdditionalConnectionDoesNotReplaceActiveAgent()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var outputs = new BuildOutputs();
        using var key = outputs.WriteKey();
        using var server = await WebSocketClientTransport.CreateAsync(Config, outputs.Outputs, NullLogger.Instance, cts.Token);
        var environment = new Dictionary<string, string>();
        server.ConfigureEnvironment(environment);
        var url = new Uri(environment[AgentEnvironmentVariables.DotNetWatchHotReloadWebSocketEndpoint]);
        using var agent = new WebSocketTransport(url.ToString(), key.GetPublicKey(), _ => { }, 5000);
        await agent.SendAsync(new ClientInitializationResponse(Environment.ProcessId, "Baseline"), cts.Token);
        await server.WaitForConnectionAsync(cts.Token);

        using var second = new ClientWebSocket();
        second.Options.SetRequestHeader("Origin", "ws://localhost");
        second.Options.CollectHttpResponseDetails = true;
        second.Options.AddSubProtocol(WebUtility.UrlEncode(
            Convert.ToBase64String(key.RSA.Encrypt(new byte[32], RSAEncryptionPadding.OaepSHA256))));
        await Assert.ThrowsExactlyAsync<WebSocketException>(() => second.ConnectAsync(url, cts.Token));
        Assert.AreEqual(HttpStatusCode.Conflict, second.HttpStatusCode);

        using var response = await server.ReadAsync(cts.Token);
        Assert.IsNotNull(response);
        Assert.AreEqual(ResponseType.InitializationResponse, response.Value.Type);
    }

    [TestMethod]
    [DataRow(null, HttpStatusCode.Unauthorized)]
    [DataRow("invalid", HttpStatusCode.Unauthorized)]
    [DataRow("another-key", HttpStatusCode.Unauthorized)]
    [DataRow("missing-build-output", HttpStatusCode.InternalServerError)]
    public async Task RejectsInvalidHandshakeBeforeUpgrade(string? credential, HttpStatusCode expectedStatus)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var outputs = new BuildOutputs();
        if (credential != "missing-build-output")
        {
            outputs.WriteKey().Dispose();
        }

        using var server = await WebSocketClientTransport.CreateAsync(Config, outputs.Outputs, NullLogger.Instance, cts.Token);
        var environment = new Dictionary<string, string>();
        server.ConfigureEnvironment(environment);
        using var socket = new ClientWebSocket();
        socket.Options.CollectHttpResponseDetails = true;
        socket.Options.SetRequestHeader("Origin", "ws://localhost");
        if (credential != null)
        {
            using var anotherKey = new SharedSecretProvider();
            socket.Options.AddSubProtocol(credential is "another-key" or "missing-build-output"
                ? WebUtility.UrlEncode(Convert.ToBase64String(anotherKey.RSA.Encrypt(new byte[32], RSAEncryptionPadding.OaepSHA256)))
                : credential);
        }

        await Assert.ThrowsExactlyAsync<WebSocketException>(
            () => socket.ConnectAsync(new Uri(environment[AgentEnvironmentVariables.DotNetWatchHotReloadWebSocketEndpoint]), cts.Token));
        Assert.AreEqual(expectedStatus, socket.HttpStatusCode);
    }

    [TestMethod]
    [DataRow("wrong-secret")]
    [DataRow("binary")]
    [DataRow("oversized")]
    [DataRow("close")]
    [DataRow("noncanonical")]
    public async Task RejectsProviderWithoutCorrectEcho(string response)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var key = new SharedSecretProvider();
        var initializationReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = await KestrelWebSocketServer.StartServerAsync(Config, async context =>
        {
            using var socket = await context.WebSockets.AcceptWebSocketAsync(context.WebSockets.WebSocketRequestedProtocols.Single());
            if (response == "close")
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", cts.Token);
            }
            else
            {
                var echo = response == "oversized" ? new string('x', 100) : "wrong-secret";
                if (response == "noncanonical")
                {
                    var encoded = key.DecryptSecret(WebUtility.UrlDecode(context.WebSockets.WebSocketRequestedProtocols.Single())).ToCharArray();
                    const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
                    // Change an unused padding bit without changing the decoded 32-byte secret.
                    encoded[^2] = alphabet[alphabet.IndexOf(encoded[^2]) + 1];
                    echo = new string(encoded);
                }

                await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(echo)),
                    response == "binary" ? WebSocketMessageType.Binary : WebSocketMessageType.Text, true, cts.Token);
            }

            try
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(new byte[1024]), cts.Token);
                initializationReceived.SetResult(result.MessageType == WebSocketMessageType.Binary);
            }
            catch (WebSocketException)
            {
                initializationReceived.SetResult(false);
            }
        }, cts.Token);

        using (var agent = new WebSocketTransport(server.ServerUrls[0], key.GetPublicKey(), _ => { }, 5000))
        {
            await Assert.ThrowsExactlyAsync<InvalidDataException>(
                async () => await agent.SendAsync(new ClientInitializationResponse(Environment.ProcessId, "Baseline"), cts.Token));
        }

        Assert.IsFalse(await initializationReceived.Task.WaitAsync(cts.Token));
    }

    [TestMethod]
    public async Task AcceptsFragmentedEcho()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var key = new SharedSecretProvider();
        var initializationReceived = new TaskCompletionSource<WebSocketMessageType>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = await KestrelWebSocketServer.StartServerAsync(Config, async context =>
        {
            var protocol = context.WebSockets.WebSocketRequestedProtocols.Single();
            var secret = Encoding.UTF8.GetBytes(key.DecryptSecret(WebUtility.UrlDecode(protocol)));
            using var socket = await context.WebSockets.AcceptWebSocketAsync(protocol);
            await socket.SendAsync(new ArraySegment<byte>(secret, 0, 10), WebSocketMessageType.Text, false, cts.Token);
            await socket.SendAsync(new ArraySegment<byte>(secret, 10, secret.Length - 10), WebSocketMessageType.Text, true, cts.Token);
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(new byte[1024]), cts.Token);
            initializationReceived.SetResult(result.MessageType);
        }, cts.Token);

        using var agent = new WebSocketTransport(server.ServerUrls[0], key.GetPublicKey(), _ => { }, 5000);
        await agent.SendAsync(new ClientInitializationResponse(Environment.ProcessId, "Baseline"), cts.Token);
        Assert.AreEqual(WebSocketMessageType.Binary, await initializationReceived.Task.WaitAsync(cts.Token));
    }

    [TestMethod]
    public async Task MissingEchoUsesConnectionTimeout()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var key = new SharedSecretProvider();
        using var server = await KestrelWebSocketServer.StartServerAsync(Config, async context =>
        {
            using var socket = await context.WebSockets.AcceptWebSocketAsync(context.WebSockets.WebSocketRequestedProtocols.Single());
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
            }
        }, cts.Token);

        using var agent = new WebSocketTransport(server.ServerUrls[0], key.GetPublicKey(), _ => { }, 1000);
        await Assert.ThrowsExactlyAsync<TimeoutException>(
            async () => await agent.SendAsync(new ClientInitializationResponse(Environment.ProcessId, "Baseline"), cts.Token));
    }
}
