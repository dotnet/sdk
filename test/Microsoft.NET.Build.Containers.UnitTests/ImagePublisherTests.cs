// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http.Headers;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.NET.Build.Containers.LocalDaemons;
using Microsoft.NET.Build.Containers.Tasks;
using Moq;

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class ImagePublisherTests
{
    public TestContext TestContext { get; set; } = default!;

    /// <summary>
    /// Verifies that authentication rejected during a remote push or a local archive's base-layer
    /// download is reported as CONTAINER1019, for both single-image and image-index publication.
    /// </summary>
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task PublishImage_ReportsUnexpectedAuthOrigin(bool localArchive, bool multiArch)
    {
        // Model a registry request that ends at another origin with a Basic challenge.
        string registryName = $"publish-auth-test-{Guid.NewGuid():N}.invalid";
        Uri registryUri = new($"https://{registryName}");
        Uri redirectedUri = new("https://storage.invalid/v2/");
        var transport = new RedirectedChallengeHandler(redirectedUri);
        using var client = new HttpClient(new AuthHandshakeMessageHandler(
            registryName, registryUri, false, transport, NullLogger.Instance, localArchive ? RegistryMode.Pull : RegistryMode.Push));
        var api = new Mock<IRegistryAPI>(MockBehavior.Strict);
        api.SetupGet(a => a.Manifest).Returns(new DefaultManifestOperations(registryUri, registryName, client, NullLogger.Instance));
        api.SetupGet(a => a.Blob).Returns(new DefaultBlobOperations(registryUri, registryName, client, NullLogger.Instance));
        var registry = new Registry(registryName, NullLogger.Instance, api.Object, retryDelayProvider: () => TimeSpan.Zero);
        var source = new SourceImageReference(registry, "base/image", "latest", null);
        BuiltImage image = new()
        {
            Config = "{}",
            ImageDigest = "sha256:config",
            Manifest = "{}",
            ManifestDigest = "sha256:manifest",
            ManifestMediaType = SchemaTypes.OciManifestV1,
            Layers = [new ManifestLayer(SchemaTypes.OciLayerGzipV1, 123, $"sha256:{Guid.NewGuid():N}{Guid.NewGuid():N}", null)],
            OS = "linux",
            Architecture = "amd64",
        };
        var errors = new List<BuildErrorEventArgs>();
        var engine = new Mock<IBuildEngine>();
        engine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>())).Callback<BuildErrorEventArgs>(errors.Add);
        using var task = new CreateNewImage { BuildEngine = engine.Object };
        string archivePath = Path.Combine(Path.GetTempPath(), $"publish-auth-test-{Guid.NewGuid():N}.tar.gz");
        try
        {
            // Remote publication reaches the manifest endpoint; archive publication first
            // downloads a base layer. Both paths must preserve the authentication diagnostic.
            DestinationImageReference destination = localArchive
                ? new(new ArchiveFileRegistry(archivePath), "test/image", ["latest"])
                : new(registry, "test/image", ["latest"]);
            var telemetry = new Telemetry(source, destination, task.Log);
            if (multiArch)
            {
                MultiArchImage index = new()
                {
                    ImageIndex = "{}",
                    ImageIndexMediaType = SchemaTypes.OciImageIndexV1,
                    Images = [image],
                };
                await ImagePublisher.PublishImageAsync(index, source, destination, task.Log, telemetry, TestContext.CancellationToken);
            }
            else
            {
                await ImagePublisher.PublishImageAsync(image, source, destination, false, task.Log, telemetry, TestContext.CancellationToken);
            }

            // Assert the structured MSBuild error code, not just a code embedded in its message.
            Assert.HasCount(1, errors);
            Assert.AreEqual("CONTAINER1019", errors[0].Code);
            Assert.Contains(registryUri.GetLeftPart(UriPartial.Authority), errors[0].Message!);
            Assert.Contains(redirectedUri.GetLeftPart(UriPartial.Authority), errors[0].Message!);
            Assert.AreEqual(1, transport.RequestCount);
        }
        finally
        {
            File.Delete(archivePath);
        }
    }

    private sealed class RedirectedChallengeHandler(Uri redirectedUri) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            Assert.IsNull(request.Headers.Authorization);
            request.RequestUri = redirectedUri;
            var response = new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = request };
            response.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue("Basic", "realm=\"registry\""));
            return Task.FromResult(response);
        }
    }
}
