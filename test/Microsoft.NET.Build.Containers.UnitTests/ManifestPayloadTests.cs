// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class ManifestPayloadTests
{
    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MultipleTagsReceiveIdenticalAnnotatedUtf8Payloads(bool imageIndex)
    {
        const string json = """{"schemaVersion":2,"annotations":{"example.title":"café 🚀"}}""";
        byte[] expected = Encoding.UTF8.GetBytes(json);
        string mediaType = imageIndex ? SchemaTypes.OciImageIndexV1 : SchemaTypes.OciManifestV1;
        string[] tags = ["latest", "stable", "release"];
        var uploads = new List<string>();
        using var client = new HttpClient(new PayloadHandler(async (request, token) =>
        {
            if (request.Method == HttpMethod.Head)
            {
                // Existing manifests still need each requested tag published.
                return new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request };
            }
            Assert.AreEqual(HttpMethod.Put, request.Method);
            uploads.Add(request.RequestUri!.Segments[^1]);
            Assert.AreEqual(mediaType, request.Content!.Headers.ContentType!.ToString());
            Assert.AreEqual(expected.Length, request.Content.Headers.ContentLength);
            Assert.AreSequenceEqual(expected, await request.Content.ReadAsByteArrayAsync(token));
            return new HttpResponseMessage(HttpStatusCode.Created) { RequestMessage = request };
        }));
        var api = new Mock<IRegistryAPI>(MockBehavior.Strict);
        api.SetupGet(a => a.Manifest).Returns(new DefaultManifestOperations(
            new Uri("https://registry.example.com"), "registry.example.com", client, NullLogger.Instance));
        var registry = new Registry("registry.example.com", NullLogger.Instance, api.Object);
        var source = new SourceImageReference(registry, "base/image", "latest", null);
        var destination = new DestinationImageReference(registry, "app", tags);
        if (imageIndex)
        {
            await registry.PushManifestListAsync(new MultiArchImage
            {
                ImageIndex = json,
                ImageIndexMediaType = mediaType,
            }, source, destination, TestContext.CancellationToken);
        }
        else
        {
            await registry.PushAsync(new BuiltImage
            {
                Config = "{}",
                Manifest = json,
                ManifestDigest = DigestUtils.ComputeSha256Digest(json),
                ManifestMediaType = mediaType,
                Layers = [],
                OS = "linux",
                Architecture = "amd64",
            }, source, destination, TestContext.CancellationToken);
        }
        Assert.AreSequenceEqual(tags, uploads);
        api.VerifyGet(a => a.Blob, Times.Never);
    }

    [TestMethod]
    public async Task ManifestUploadPreservesMemorySliceAndReportsFailure()
    {
        const string json = """{"schemaVersion":2,"annotations":{"example.title":"café"}}""";
        byte[] bytes = Encoding.UTF8.GetBytes("prefix" + json + "suffix");
        ReadOnlyMemory<byte> payload = bytes.AsMemory(6, Encoding.UTF8.GetByteCount(json));
        int requests = 0;
        using var client = new HttpClient(new PayloadHandler(async (request, token) =>
        {
            requests++;
            Assert.AreEqual("/v2/app/manifests/latest", request.RequestUri!.AbsolutePath);
            using var document = JsonDocument.Parse(await request.Content!.ReadAsByteArrayAsync(token));
            Assert.AreEqual("café", document.RootElement.GetProperty("annotations").GetProperty("example.title").GetString());
            return new HttpResponseMessage(HttpStatusCode.BadRequest) { RequestMessage = request };
        }));
        var operations = new DefaultManifestOperations(new Uri("https://registry.example.com"),
            "registry.example.com", client, NullLogger.Instance);
        var error = await Assert.ThrowsExactlyAsync<ContainerHttpException>(() =>
            operations.PutAsync("app", "latest", payload, SchemaTypes.OciManifestV1, TestContext.CancellationToken));
        Assert.Contains("HTTP status code: BadRequest", error.Message);
        Assert.AreEqual(1, requests);
    }

    private sealed class PayloadHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request, cancellationToken);
    }
}
