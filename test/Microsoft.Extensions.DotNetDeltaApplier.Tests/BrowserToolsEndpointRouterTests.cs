// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Microsoft.DotNet.HotReload.UnitTests;

[TestClass]
public class BrowserToolsEndpointRouterTests
{
    private static readonly Guid s_sessionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [TestMethod]
    public async Task Session_Get_ReturnsDescriptorAndReflectsGenerationReset()
    {
        var store = new BrowserToolsUpdateStore();
        var initialGeneration = store.GenerationId;
        using var server = new TestBrowserRefreshServer();
        var router = CreateRouter(store, server);

        var (initialContext, initialBody) = await InvokeAsync(
            router,
            HttpMethods.Get,
            BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.SessionPath);

        AssertJsonResponse(initialContext, initialBody);
        using var initialDocument = JsonDocument.Parse(initialBody);
        AssertSessionDescriptor(initialDocument.RootElement, initialGeneration);

        var newGeneration = store.Reset();

        var (resetContext, resetBody) = await InvokeAsync(
            router,
            HttpMethods.Get,
            BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.SessionPath);

        Assert.AreNotEqual(initialGeneration, newGeneration);
        AssertJsonResponse(resetContext, resetBody);
        using var resetDocument = JsonDocument.Parse(resetBody);
        AssertSessionDescriptor(resetDocument.RootElement, newGeneration);
    }

    [TestMethod]
    public async Task Updates_CurrentGeneration_ReturnsOrderedBatches()
    {
        var store = new BrowserToolsUpdateStore();
        var generation = store.GenerationId;
        var firstModuleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondModuleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        store.Append(CreateBatch(generation, updateId: 0, firstModuleId, deltaSeed: 10));
        store.Append(CreateBatch(generation, updateId: 2, secondModuleId, deltaSeed: 20));
        using var server = new TestBrowserRefreshServer();
        var router = CreateRouter(store, server);

        var (context, body) = await InvokeAsync(
            router,
            HttpMethods.Get,
            $"{BrowserToolsProtocol.RoutePrefix}{BrowserToolsProtocol.UpdatesPath}/{generation:D}.json");

        AssertJsonResponse(context, body);
        using var document = JsonDocument.Parse(body);
        var batches = document.RootElement;
        Assert.AreEqual(JsonValueKind.Array, batches.ValueKind);
        Assert.AreEqual(2, batches.GetArrayLength());
        AssertBatch(batches[0], generation, updateId: 0, firstModuleId, deltaSeed: 10);
        AssertBatch(batches[1], generation, updateId: 2, secondModuleId, deltaSeed: 20);
    }

    [TestMethod]
    public async Task Updates_StaleGeneration_ReturnsConflictWithoutBody()
    {
        var store = new BrowserToolsUpdateStore();
        var staleGeneration = store.GenerationId;
        store.Append(CreateBatch(
            staleGeneration,
            updateId: 0,
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            deltaSeed: 30));
        store.Reset();
        using var server = new TestBrowserRefreshServer();
        var router = CreateRouter(store, server);

        var (context, body) = await InvokeAsync(
            router,
            HttpMethods.Get,
            $"{BrowserToolsProtocol.RoutePrefix}{BrowserToolsProtocol.UpdatesPath}/{staleGeneration:D}.json");

        AssertEmptyResponse(context, body, StatusCodes.Status409Conflict);
    }

    [TestMethod]
    public async Task ClearCache_Get_ReturnsNoContentAndClearSiteData()
    {
        var store = new BrowserToolsUpdateStore();
        using var server = new TestBrowserRefreshServer();
        var router = CreateRouter(store, server);

        var (context, body) = await InvokeAsync(
            router,
            HttpMethods.Get,
            BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.ClearCachePath);

        AssertEmptyResponse(context, body, StatusCodes.Status204NoContent);
        Assert.AreEqual("\"cache\"", context.Response.Headers["Clear-Site-Data"].ToString());
    }

    [TestMethod]
    [DataRow("POST")]
    [DataRow("PUT")]
    [DataRow("DELETE")]
    [DataRow("HEAD")]
    public async Task KnownEndpoint_NonGetMethod_ReturnsMethodNotAllowed(string method)
    {
        var store = new BrowserToolsUpdateStore();
        using var server = new TestBrowserRefreshServer();
        var router = CreateRouter(store, server);

        var (context, body) = await InvokeAsync(
            router,
            method,
            BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.SessionPath);

        AssertEmptyResponse(context, body, StatusCodes.Status405MethodNotAllowed);
    }

    [TestMethod]
    [DataRow("/updates/not-a-guid.json")]
    [DataRow("/updates/44444444-4444-4444-4444-444444444444")]
    [DataRow("/updates/55555555-5555-5555-5555-555555555555.json/extra")]
    [DataRow("/unknown")]
    public async Task UnknownOrMalformedGet_ReturnsNotFound(string route)
    {
        var store = new BrowserToolsUpdateStore();
        using var server = new TestBrowserRefreshServer();
        var router = CreateRouter(store, server);

        var (context, body) = await InvokeAsync(
            router,
            HttpMethods.Get,
            BrowserToolsProtocol.RoutePrefix + route);

        AssertEmptyResponse(context, body, StatusCodes.Status404NotFound);
    }

    [TestMethod]
    public async Task Connect_NonWebSocketRequest_ReturnsBadRequest()
    {
        var store = new BrowserToolsUpdateStore();
        using var server = new TestBrowserRefreshServer();
        var router = CreateRouter(store, server);

        var (context, body) = await InvokeAsync(
            router,
            HttpMethods.Get,
            BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.ConnectPath);

        AssertEmptyResponse(context, body, StatusCodes.Status400BadRequest);
    }

    [TestMethod]
    public async Task Connect_WebSocketWithoutSharedSecret_ReturnsBadRequest()
    {
        var store = new BrowserToolsUpdateStore();
        using var server = new TestBrowserRefreshServer();
        var router = CreateRouter(store, server);
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpWebSocketFeature>(new TestWebSocketFeature());
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.ConnectPath;
        context.Response.Body = new MemoryStream();

        await router.HandleAsync(context);

        AssertEmptyResponse(context, [], StatusCodes.Status400BadRequest);
    }

    private static BrowserToolsEndpointRouter CreateRouter(
        BrowserToolsUpdateStore store,
        TestBrowserRefreshServer server)
        => new(s_sessionId, "public-key", store, server);

    private static async Task<(DefaultHttpContext Context, byte[] Body)> InvokeAsync(
        BrowserToolsEndpointRouter router,
        string method,
        string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        await router.HandleAsync(context);

        context.Response.Body.Position = 0;
        return (context, ((MemoryStream)context.Response.Body).ToArray());
    }

    private static BrowserToolsUpdateBatch CreateBatch(Guid generationId, int updateId, Guid moduleId, byte deltaSeed)
        => new(
            generationId,
            updateId,
            ImmutableArray.Create(
                new BrowserToolsManagedCodeUpdate(
                    moduleId,
                    [deltaSeed, (byte)(deltaSeed + 1)],
                    [(byte)(deltaSeed + 2), (byte)(deltaSeed + 3)],
                    [(byte)(deltaSeed + 4)],
                    [deltaSeed, deltaSeed + 100])));

    private static void AssertSessionDescriptor(JsonElement descriptor, Guid expectedGeneration)
    {
        Assert.AreEqual(JsonValueKind.Object, descriptor.ValueKind);
        Assert.AreSequenceEqual(
            ["generationId", "protocolVersion", "publicKey", "sessionId"],
            descriptor.EnumerateObject().Select(static property => property.Name).Order(StringComparer.Ordinal));
        Assert.AreEqual(1, descriptor.GetProperty("protocolVersion").GetInt32());
        Assert.AreEqual(s_sessionId, descriptor.GetProperty("sessionId").GetGuid());
        Assert.AreEqual(expectedGeneration, descriptor.GetProperty("generationId").GetGuid());
        Assert.AreEqual("public-key", descriptor.GetProperty("publicKey").GetString());
    }

    private static void AssertBatch(
        JsonElement batch,
        Guid expectedGeneration,
        int updateId,
        Guid expectedModuleId,
        byte deltaSeed)
    {
        Assert.AreEqual(expectedGeneration, batch.GetProperty("generationId").GetGuid());
        Assert.AreEqual(updateId, batch.GetProperty("updateId").GetInt32());

        var deltas = batch.GetProperty("deltas");
        Assert.AreEqual(JsonValueKind.Array, deltas.ValueKind);
        Assert.AreEqual(1, deltas.GetArrayLength());

        var delta = deltas[0];
        Assert.AreEqual(expectedModuleId, delta.GetProperty("moduleId").GetGuid());
        Assert.AreSequenceEqual(
            [deltaSeed, (byte)(deltaSeed + 1)],
            delta.GetProperty("metadataDelta").GetBytesFromBase64());
        Assert.AreSequenceEqual(
            [(byte)(deltaSeed + 2), (byte)(deltaSeed + 3)],
            delta.GetProperty("ilDelta").GetBytesFromBase64());
        Assert.AreSequenceEqual(
            [(byte)(deltaSeed + 4)],
            delta.GetProperty("pdbDelta").GetBytesFromBase64());
        Assert.AreSequenceEqual(
            [deltaSeed, deltaSeed + 100],
            delta.GetProperty("updatedTypes").EnumerateArray().Select(static item => item.GetInt32()));
    }

    private static void AssertJsonResponse(DefaultHttpContext context, byte[] body)
    {
        AssertResponse(context, StatusCodes.Status200OK);
        Assert.AreEqual("application/json", context.Response.ContentType);
        Assert.AreEqual((long?)body.Length, context.Response.ContentLength);
        Assert.IsNotEmpty(body);
    }

    private static void AssertEmptyResponse(DefaultHttpContext context, byte[] body, int expectedStatusCode)
    {
        AssertResponse(context, expectedStatusCode);
        Assert.IsEmpty(body);
    }

    private static void AssertResponse(DefaultHttpContext context, int expectedStatusCode)
    {
        Assert.AreEqual(expectedStatusCode, context.Response.StatusCode);
        Assert.AreEqual("no-store", context.Response.Headers.CacheControl.ToString());
    }

    private sealed class TestWebSocketFeature : IHttpWebSocketFeature
    {
        public bool IsWebSocketRequest => true;

        public Task<WebSocket> AcceptAsync(WebSocketAcceptContext context)
            => throw new InvalidOperationException("The unauthenticated WebSocket must be rejected before acceptance.");
    }
}
