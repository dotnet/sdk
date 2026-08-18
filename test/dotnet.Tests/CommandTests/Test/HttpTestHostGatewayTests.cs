// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http.Headers;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.IPC;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public sealed class HttpTestHostGatewayTests
{
    private const string BrowserOrigin = "http://127.0.0.1:5000";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Post_AuthenticatedFrame_RoundTripsProtocolResponse()
    {
        HandshakeMessage? receivedHandshake = null;
        using var gateway = new HttpTestHostGateway(
            request =>
            {
                receivedHandshake = Assert.IsInstanceOfType<HandshakeMessage>(request);
                return Task.FromResult<IResponse>(new HandshakeMessage(new Dictionary<byte, string>
                {
                    [HandshakeMessagePropertyNames.SupportedProtocolVersions] = "1.3.0",
                }));
            },
            TestContext.CancellationToken,
            BrowserOrigin);

        var requestHandshake = new HandshakeMessage(new Dictionary<byte, string>
        {
            [HandshakeMessagePropertyNames.SupportedProtocolVersions] = "1.0.0;1.3.0",
        });

        using HttpResponseMessage response = await SendAsync(gateway, requestHandshake, gateway.Token);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.AreEqual(BrowserOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.IsNotNull(receivedHandshake);

        var responseHandshake = Assert.IsInstanceOfType<HandshakeMessage>(
            Deserialize(await response.Content.ReadAsByteArrayAsync(TestContext.CancellationToken)));
        Assert.AreEqual(
            "1.3.0",
            responseHandshake.Properties[HandshakeMessagePropertyNames.SupportedProtocolVersions]);
    }

    [TestMethod]
    public async Task Post_InvalidToken_IsRejectedBeforeDispatch()
    {
        bool dispatched = false;
        using var gateway = new HttpTestHostGateway(
            _ =>
            {
                dispatched = true;
                return Task.FromResult<IResponse>(VoidResponse.CachedInstance);
            },
            TestContext.CancellationToken,
            BrowserOrigin);

        using HttpResponseMessage response = await SendAsync(
            gateway,
            new HandshakeMessage([]),
            token: "invalid-token");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.AreEqual(BrowserOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.IsFalse(dispatched);
    }

    [TestMethod]
    public async Task Options_WithoutAuthorization_ReturnsCorsAndPrivateNetworkHeaders()
    {
        using var gateway = new HttpTestHostGateway(
            _ => Task.FromResult<IResponse>(VoidResponse.CachedInstance),
            TestContext.CancellationToken,
            BrowserOrigin);
        using var request = new HttpRequestMessage(HttpMethod.Options, gateway.Endpoint);
        request.Headers.Add("Origin", BrowserOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        request.Headers.Add("Access-Control-Request-Private-Network", "true");

        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        Assert.AreEqual(BrowserOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.AreEqual("POST", response.Headers.GetValues("Access-Control-Allow-Methods").Single());
        Assert.AreEqual("Authorization, Content-Type", response.Headers.GetValues("Access-Control-Allow-Headers").Single());
        Assert.AreEqual("true", response.Headers.GetValues("Access-Control-Allow-Private-Network").Single());
    }

    [TestMethod]
    public async Task Options_FirstBrowserOriginPinsCorsPolicy()
    {
        using var gateway = new HttpTestHostGateway(
            _ => Task.FromResult<IResponse>(VoidResponse.CachedInstance),
            TestContext.CancellationToken);
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        using var firstRequest = new HttpRequestMessage(HttpMethod.Options, gateway.Endpoint);
        firstRequest.Headers.Add("Origin", BrowserOrigin);

        using HttpResponseMessage firstResponse = await client.SendAsync(firstRequest, TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.NoContent, firstResponse.StatusCode);
        Assert.AreEqual(BrowserOrigin, firstResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());

        using var secondRequest = new HttpRequestMessage(HttpMethod.Options, gateway.Endpoint);
        secondRequest.Headers.Add("Origin", "http://127.0.0.1:5001");
        using HttpResponseMessage secondResponse = await client.SendAsync(secondRequest, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.Forbidden, secondResponse.StatusCode);
        Assert.IsFalse(secondResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [TestMethod]
    public async Task Options_WrongPathCannotPinCorsPolicy()
    {
        using var gateway = new HttpTestHostGateway(
            _ => Task.FromResult<IResponse>(VoidResponse.CachedInstance),
            TestContext.CancellationToken);
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        using var wrongPathRequest = new HttpRequestMessage(
            HttpMethod.Options,
            new Uri(gateway.Endpoint, "wrong-path"));
        wrongPathRequest.Headers.Add("Origin", "http://127.0.0.1:5001");

        using HttpResponseMessage wrongPathResponse = await client.SendAsync(wrongPathRequest, TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.NotFound, wrongPathResponse.StatusCode);

        using var validRequest = new HttpRequestMessage(HttpMethod.Options, gateway.Endpoint);
        validRequest.Headers.Add("Origin", BrowserOrigin);
        using HttpResponseMessage validResponse = await client.SendAsync(validRequest, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NoContent, validResponse.StatusCode);
        Assert.AreEqual(BrowserOrigin, validResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [TestMethod]
    public async Task Post_MalformedFrame_ReturnsBadRequest()
    {
        using var gateway = new HttpTestHostGateway(
            _ => Task.FromResult<IResponse>(VoidResponse.CachedInstance),
            TestContext.CancellationToken,
            BrowserOrigin);
        using var request = CreateRequest(gateway, gateway.Token, [1, 2, 3, 4]);
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task Post_CorruptMessage_DoesNotStopGateway()
    {
        int dispatchedRequests = 0;
        using var gateway = new HttpTestHostGateway(
            _ =>
            {
                dispatchedRequests++;
                return Task.FromResult<IResponse>(VoidResponse.CachedInstance);
            },
            TestContext.CancellationToken,
            BrowserOrigin);
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        byte[] corruptHandshakeFrame =
        [
            4, 0, 0, 0,
            9, 0, 0, 0,
        ];
        using var corruptRequest = CreateRequest(gateway, gateway.Token, corruptHandshakeFrame);

        using HttpResponseMessage corruptResponse = await client.SendAsync(corruptRequest, TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.BadRequest, corruptResponse.StatusCode);

        using var validRequest = CreateRequest(
            gateway,
            gateway.Token,
            Serialize(new HandshakeMessage([])));
        using HttpResponseMessage validResponse = await client.SendAsync(validRequest, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, validResponse.StatusCode);
        Assert.AreEqual(1, dispatchedRequests);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpTestHostGateway gateway,
        object message,
        string token)
    {
        using var request = CreateRequest(gateway, token, Serialize(message));
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        try
        {
            return await client.SendAsync(request, TestContext.CancellationToken);
        }
        finally
        {
            client.Dispose();
        }
    }

    private static HttpRequestMessage CreateRequest(
        HttpTestHostGateway gateway,
        string token,
        byte[] frame)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, gateway.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Origin", BrowserOrigin);
        request.Content = new ByteArrayContent(frame);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream")
        {
            CharSet = "utf-8",
        };
        return request;
    }

    private static byte[] Serialize(object message)
    {
        var serializer = new ProtocolMessageSerializer();
        serializer.RegisterAllSerializers();
        return serializer.Serialize(message);
    }

    private static object Deserialize(byte[] frame)
    {
        var serializer = new ProtocolMessageSerializer();
        serializer.RegisterAllSerializers();
        return serializer.Deserialize(frame, skipUnknownMessages: false);
    }
}
