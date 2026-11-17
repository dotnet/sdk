// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry;

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class RegistryTests : IDisposable
{
    private readonly TestLoggerFactory _loggerFactory;

    public TestContext TestContext { get; }

    public RegistryTests(TestContext testContext)
    {
        TestContext = testContext;
        _loggerFactory = new TestLoggerFactory(testContext);
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
    }

    [DataRow("us-south1-docker.pkg.dev", true)]
    [DataRow("us.gcr.io", false)]
    [TestMethod]
    public void CheckIfGoogleArtifactRegistry(string registryName, bool expectedIsGoogleArtifactRegistry)
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(CheckIfGoogleArtifactRegistry));
        Registry registry = new(registryName, logger, RegistryMode.Push);
        Assert.AreEqual(expectedIsGoogleArtifactRegistry, registry.IsGoogleArtifactRegistry);
    }

    [TestMethod]
    public void DockerIoAlias()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(DockerIoAlias));
        Registry registry = new("docker.io", logger, RegistryMode.Push);
        Assert.IsTrue(registry.IsDockerHub);
        Assert.AreEqual("docker.io", registry.RegistryName);
        Assert.AreEqual("registry-1.docker.io", registry.BaseUri.Host);
    }

    [TestMethod]
    public async Task PushAsync_SkipsBlobUploadsByDefaultWhenManifestAlreadyExists()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(PushAsync_SkipsBlobUploadsByDefaultWhenManifestAlreadyExists));
        const string repository = "test/repository";
        const string manifestDigest = "sha256:manifest";
        string[] tags = ["latest", "stable"];

        Mock<IManifestStore> manifests = new(MockBehavior.Strict);
        manifests
            .Setup(m => m.ResolveAsync(manifestDigest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Descriptor { MediaType = MediaType.ImageManifest, Digest = manifestDigest });
        foreach (string tag in tags)
        {
            manifests
                .Setup(m => m.PushAsync(
                    It.Is<Descriptor>(d => d.MediaType == MediaType.ImageManifest),
                    It.IsAny<Stream>(),
                    tag,
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        Mock<IRepository> repositoryClient = new(MockBehavior.Strict);
        repositoryClient.SetupGet(r => r.Manifests).Returns(manifests.Object);
        Mock<IRepositoryFactory> repositoryFactory = new(MockBehavior.Strict);
        repositoryFactory.Setup(f => f.Create(repository)).Returns(repositoryClient.Object);
        Registry registry = new("example.com", logger, repositoryFactory.Object);
        BuiltImage image = new()
        {
            Config = "{}",
            ImageDigest = "sha256:config",
            Manifest = "{}",
            ManifestDigest = manifestDigest,
            ManifestMediaType = MediaType.ImageManifest,
            Layers = [new Descriptor { MediaType = MediaType.ImageLayerGzip, Size = 123, Digest = "sha256:layer" }],
            OS = "linux",
            Architecture = "amd64",
        };
        SourceImageReference source = new(registry, "base/image", "latest", null);
        DestinationImageReference destination = new(registry, repository, tags);

        await registry.PushAsync(image, source, destination, CancellationToken.None);

        manifests.Verify(m => m.ResolveAsync(manifestDigest, It.IsAny<CancellationToken>()), Times.Once);
        foreach (string tag in tags)
        {
            manifests.Verify(m => m.PushAsync(
                It.Is<Descriptor>(d => d.MediaType == MediaType.ImageManifest),
                It.IsAny<Stream>(),
                tag,
                It.IsAny<CancellationToken>()), Times.Once);
        }
        repositoryClient.VerifyGet(r => r.Blobs, Times.Never);
    }

    [TestMethod]
    public async Task PushAsync_DoesNotCheckManifestWhenNoCacheIsEnabled()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(PushAsync_DoesNotCheckManifestWhenNoCacheIsEnabled));
        const string repository = "test/repository";
        const string configDigest = "sha256:config";

        Mock<IBlobStore> blobs = new(MockBehavior.Strict);
        blobs
            .Setup(b => b.ExistsAsync(It.Is<Descriptor>(d => d.Digest == configDigest), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        Mock<IManifestStore> manifests = new(MockBehavior.Strict);
        manifests
            .Setup(m => m.PushAsync(
                It.Is<Descriptor>(d => d.MediaType == MediaType.ImageManifest),
                It.IsAny<Stream>(),
                "latest",
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Mock<IRepository> repositoryClient = new(MockBehavior.Strict);
        repositoryClient.SetupGet(r => r.Blobs).Returns(blobs.Object);
        repositoryClient.SetupGet(r => r.Manifests).Returns(manifests.Object);
        Mock<IRepositoryFactory> repositoryFactory = new(MockBehavior.Strict);
        repositoryFactory.Setup(f => f.Create(repository)).Returns(repositoryClient.Object);

        Registry registry = new("example.com", logger, repositoryFactory.Object);
        BuiltImage image = new()
        {
            Config = "{}",
            ImageDigest = configDigest,
            Manifest = "{}",
            ManifestDigest = "sha256:manifest",
            ManifestMediaType = MediaType.ImageManifest,
            Layers = [],
            OS = "linux",
            Architecture = "amd64",
        };
        SourceImageReference source = new(registry, "base/image", "latest", null);
        DestinationImageReference destination = new(registry, repository, ["latest"]);

        await registry.PushAsync(image, source, destination, noCache: true, CancellationToken.None);

        manifests.Verify(m => m.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        blobs.Verify(b => b.ExistsAsync(It.Is<Descriptor>(d => d.Digest == configDigest), It.IsAny<CancellationToken>()), Times.Once);
        manifests.Verify(m => m.PushAsync(
            It.Is<Descriptor>(d => d.MediaType == MediaType.ImageManifest),
            It.IsAny<Stream>(),
            "latest",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task PushAsync_UploadsLayersInParallelForAmazonECR()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(PushAsync_UploadsLayersInParallelForAmazonECR));
        const string repository = "msp/test-repository";
        const string configDigest = "sha256:config";
        Descriptor[] layers =
        [
            new() { MediaType = MediaType.ImageLayerGzip, Size = 1, Digest = "sha256:layer1" },
            new() { MediaType = MediaType.ImageLayerGzip, Size = 1, Digest = "sha256:layer2" },
        ];

        TaskCompletionSource bothMountsStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseMounts = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int concurrentMounts = 0;
        int maximumConcurrentMounts = 0;

        Mock<IBlobStore> blobs = new(MockBehavior.Strict);
        blobs
            .Setup(b => b.ExistsAsync(It.IsAny<Descriptor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Descriptor descriptor, CancellationToken _) => descriptor.Digest == configDigest);
        Mock<IManifestStore> manifests = new(MockBehavior.Strict);
        manifests
            .Setup(m => m.PushAsync(
                It.IsAny<Descriptor>(),
                It.IsAny<Stream>(),
                "latest",
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Mock<IRepository> repositoryClient = new(MockBehavior.Strict);
        repositoryClient.SetupGet(r => r.Blobs).Returns(blobs.Object);
        repositoryClient.SetupGet(r => r.Manifests).Returns(manifests.Object);
        repositoryClient
            .Setup(r => r.MountAsync(
                It.IsAny<Descriptor>(),
                "base/image",
                It.IsAny<Func<CancellationToken, Task<Stream>>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Descriptor, string, Func<CancellationToken, Task<Stream>>, CancellationToken>(async (_, _, _, _) =>
            {
                int currentMounts = Interlocked.Increment(ref concurrentMounts);
                Interlocked.Exchange(ref maximumConcurrentMounts, Math.Max(Volatile.Read(ref maximumConcurrentMounts), currentMounts));
                if (currentMounts == layers.Length)
                {
                    bothMountsStarted.TrySetResult();
                }

                await releaseMounts.Task;
                Interlocked.Decrement(ref concurrentMounts);
            });

        Mock<IRepositoryFactory> repositoryFactory = new(MockBehavior.Strict);
        repositoryFactory.Setup(f => f.Create(repository)).Returns(repositoryClient.Object);

        Registry registry = new("123456789012.dkr.ecr.eu-west-1.amazonaws.com", logger, repositoryFactory.Object);
        BuiltImage image = new()
        {
            Config = "{}",
            ImageDigest = configDigest,
            Manifest = "{}",
            ManifestDigest = "sha256:manifest",
            ManifestMediaType = MediaType.ImageManifest,
            Layers = layers,
            OS = "linux",
            Architecture = "amd64",
        };
        SourceImageReference source = new(registry, "base/image", "latest", null);
        DestinationImageReference destination = new(registry, repository, ["latest"]);

        Task push = registry.PushAsync(image, source, destination, noCache: true, TestContext.CancellationToken);
        try
        {
            await bothMountsStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        }
        finally
        {
            releaseMounts.TrySetResult();
        }
        await push;

        Assert.AreEqual(layers.Length, maximumConcurrentMounts);
    }

    [TestMethod]
    public async Task PushLayerAsync_UsesRegistryBlobOperations()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(PushLayerAsync_UsesRegistryBlobOperations));
        const string repository = "testRepo";
        Descriptor descriptor = new()
        {
            MediaType = "application/octet-stream",
            Digest = "sha256:fafafafafafafafafafafafafafafafa",
            Size = 1000,
        };
        Mock<Layer> layer = new(MockBehavior.Strict);
        layer.Setup(l => l.OpenBackingFile()).Returns(new MemoryStream(new byte[1000]));
        layer.Setup(l => l.Descriptor).Returns(descriptor);

        Mock<IBlobStore> blobs = new(MockBehavior.Strict);
        blobs.Setup(b => b.ExistsAsync(descriptor, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        blobs.Setup(b => b.PushAsync(descriptor, It.IsAny<Stream>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        Mock<IRepository> repositoryClient = new(MockBehavior.Strict);
        repositoryClient.SetupGet(r => r.Blobs).Returns(blobs.Object);
        Mock<IRepositoryFactory> repositoryFactory = new(MockBehavior.Strict);
        repositoryFactory.Setup(f => f.Create(repository)).Returns(repositoryClient.Object);

        Registry registry = new("public.ecr.aws", logger, repositoryFactory.Object);
        await registry.PushLayerAsync(layer.Object, repository, CancellationToken.None);

        blobs.Verify(b => b.ExistsAsync(descriptor, It.IsAny<CancellationToken>()), Times.Once);
        blobs.Verify(b => b.PushAsync(descriptor, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    [Ignore("https://github.com/dotnet/sdk/issues/42820")]
    [DataRow(true, true, true)]
    [DataRow(false, true, true)]
    [DataRow(true, false, true)]
    [DataRow(false, false, true)]
    [DataRow(false, false, false)]
    public async Task InsecureRegistry(bool isInsecureRegistry, bool serverIsHttps, bool httpServerCloseAbortive)
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(InsecureRegistry));

        // Start a dummy HTTP server that response with 200 OK.
        using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        IPEndPoint endpoint = (listener.LocalEndpoint as IPEndPoint)!;
        Uri registryUri = new Uri($"https://{endpoint.Address}:{endpoint.Port}");
        SslServerAuthenticationOptions? sslOptions = null!;
        if (serverIsHttps)
        {
            var key = RSA.Create(2048);
            var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            X509Certificate2 serverCertificate = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(1));

            // https://stackoverflow.com/questions/72096812/loading-x509certificate2-from-pem-file-results-in-no-credentials-are-available/72101855#72101855
            serverCertificate = X509CertificateLoader.LoadPkcs12(serverCertificate.Export(X509ContentType.Pfx), password: "");

            sslOptions = new SslServerAuthenticationOptions()
            {
                ServerCertificate = serverCertificate,
                ClientCertificateRequired = false
            };
        }
        _ = Task.Run(async () =>
        {
            while (true)
            {
                using TcpClient client = await listener.AcceptTcpClientAsync(TestContext.CancellationToken);
                try
                {
                    using Stream stream = serverIsHttps ? new SslStream(client.GetStream(), leaveInnerStreamOpen: false) : client.GetStream();
                    if (stream is SslStream sslStream)
                    {
                        await sslStream.AuthenticateAsServerAsync(sslOptions!, TestContext.CancellationToken);
                    }
                    byte[] buffer = new byte[10];
                    await stream.ReadAtLeastAsync(buffer, buffer.Length, cancellationToken: TestContext.CancellationToken); // Wait for the request.
                    // Repond if we see '/v2/' in the buffer (since we expect that as part of the request path).
                    if (buffer.AsSpan().IndexOf("/v2/"u8) != 0)
                    {
                        await stream.WriteAsync("HTTP/1.0 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray(), TestContext.CancellationToken);
                    }
                    else
                    {
                        if (httpServerCloseAbortive)
                        {
                            client.GetStream().Close(timeout: 0);
                        }
                    }
                }
                catch
                { }
            }
        }, TestContext.CancellationToken);

        RegistrySettings settings = new()
        {
            IsInsecure = isInsecureRegistry
        };
        Registry registry = new(registryUri, logger, RegistryMode.Pull, settings: settings);

        // Make a request.
        Task getManifest = registry.GetImageManifestAsync(repositoryName: "dotnet/runtime", reference: "latest", runtimeIdentifier: "linux-x64", manifestPicker: null!, cancellationToken: default!);

        if (isInsecureRegistry)
        {
            // Falls back to http (when serverIsHttps is false) or ignores https certificate errors (when serverIsHttps is true).
            // Results in throwing: CONTAINER2003: The manifest for dotnet/runtime:latest from registry hwas an unknown type.
            await Assert.ThrowsExactlyAsync<NotImplementedException>(() => getManifest);
        }
        else
        {
            // Does not fall back and throws HttpRequestException with SecureConnectionError.
            Exception? exception = await Assert.ThrowsAsync<Exception>(() => getManifest);
            try
            {
                // The registry client may reach its retry limit and throw an ApplicationException.
                if (exception is ApplicationException)
                {
                    // Find the exception for the first failed attempt.
                    exception = (exception.InnerException as AggregateException)?.InnerExceptions.FirstOrDefault();
                    Assert.IsNotNull(exception);
                }

                HttpRequestException requestException = Assert.IsExactInstanceOfType<HttpRequestException>(exception);
                Assert.AreEqual(HttpRequestError.SecureConnectionError, requestException.HttpRequestError);

                // The FallbackToHttpMessageHandler should fall back (if this registry was configured as insecure).
                Assert.IsTrue(FallbackToHttpMessageHandler.ShouldAttemptFallbackToHttp(requestException));
            }
            catch
            {
                // Log a message describing the exception.
                StringBuilder sb = new();
                sb.AppendLine("Exception is not fallback exception:");
                while (exception != null)
                {
                    switch (exception)
                    {
                        case SocketException socketException:
                            sb.AppendLine($"{nameof(SocketException)}({socketException.SocketErrorCode}) - {exception.Message}");
                            break;
                        case HttpRequestException requestException:
                            sb.AppendLine($"{nameof(HttpRequestException)}({requestException.HttpRequestError}) - {exception.Message}");
                            break;
                        default:
                            sb.AppendLine($"{exception.GetType().Name} - {exception.Message}");
                            break;
                    }

                    exception = exception.InnerException;
                }
                logger.LogError(sb.ToString());

                throw;
            }
        }
    }

    [DataRow("localhost", null, true)]
    [DataRow("localhost:5000", null, true)]
    [DataRow("public.ecr.aws", null, false)]
    [DataRow("public.ecr.aws", "public.ecr.aws", true)]
    [DataRow("public.ecr.aws", "Public.ecr.aws", true)] // ignore case
    [DataRow("public.ecr.aws", "public.ecr.aws;docker.io", true)] // multiple registries
    [DataRow("public.ecr.aws", ";public.ecr.aws ;  docker.io ", true)] // ignore whitespace
    [DataRow("public.ecr.aws", "public.ecr.aws2;docker.io ", false)] // full name match
    [TestMethod]
    public void IsRegistryInsecure(string registryName, string? insecureRegistriesEnvvar, bool expectedInsecure)
    {
        var environment = new Dictionary<string, string>();
        if (insecureRegistriesEnvvar is not null)
        {
            environment["DOTNET_CONTAINER_INSECURE_REGISTRIES"] = insecureRegistriesEnvvar;
        }

        var registrySettings = new RegistrySettings(registryName, new MockEnvironmentProvider(environment));

        Assert.AreEqual(expectedInsecure, registrySettings.IsInsecure);
    }

    [TestMethod]
    [DataRow("DOTNET_CONTAINER_DEBUG_REGISTRY_FORCE_CHUNKED_UPLOAD")]
    [DataRow("SDK_CONTAINER_DEBUG_REGISTRY_FORCE_CHUNKED_UPLOAD")]
    public void ForceChunkedUploadEnvironmentVariablesAreSupported(string variable)
    {
        var registrySettings = new RegistrySettings(environment: new MockEnvironmentProvider(
            new Dictionary<string, string> { [variable] = "true" }));

        Assert.IsTrue(registrySettings.ForceChunkedUpload);
    }

    [TestMethod]
    [DataRow("DOTNET_CONTAINER_REGISTRY_CHUNKED_UPLOAD_SIZE_BYTES")]
    [DataRow("SDK_CONTAINER_REGISTRY_CHUNKED_UPLOAD_SIZE_BYTES")]
    public void ChunkedUploadSizeEnvironmentVariablesAreSupported(string variable)
    {
        var registrySettings = new RegistrySettings(environment: new MockEnvironmentProvider(
            new Dictionary<string, string> { [variable] = "1048576" }));

        Assert.AreEqual(1024 * 1024, registrySettings.ChunkedUploadSizeBytes);
    }

    [TestMethod]
    public async Task DownloadBlobAsync_RetriesOnFailure()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger(nameof(DownloadBlobAsync_RetriesOnFailure));

        var repoName = "testRepo";
        var descriptor = new Descriptor
        {
            MediaType = MediaType.ImageLayerGzip,
            Digest = "sha256:039058c6f2c0cb492c533b0a4d14ef77cc0f78abccced5287d84a1a2011cfb81",
            Size = 1234,
        };
        var cancellationToken = CancellationToken.None;

        var blobs = new Mock<IBlobStore>(MockBehavior.Strict);
        blobs
            .SetupSequence(b => b.FetchAsync(descriptor, cancellationToken))
            .ThrowsAsync(new Exception("Simulated failure 1")) // First attempt fails
            .ThrowsAsync(new Exception("Simulated failure 2")) // Second attempt fails
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 })); // Third attempt succeeds
        var repositoryClient = new Mock<IRepository>(MockBehavior.Strict);
        repositoryClient.SetupGet(r => r.Blobs).Returns(blobs.Object);
        var repositoryFactory = new Mock<IRepositoryFactory>(MockBehavior.Strict);
        repositoryFactory.Setup(f => f.Create(repoName)).Returns(repositoryClient.Object);

        Registry registry = new(repoName, logger, repositoryFactory.Object, null, () => TimeSpan.Zero);

        string? result = null;
        try
        {
            // Act
            result = await registry.DownloadBlobAsync(repoName, descriptor, cancellationToken);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(File.Exists(result)); // Ensure the file was successfully downloaded
            blobs.Verify(b => b.FetchAsync(descriptor, cancellationToken), Times.Exactly(3)); // Verify retries
        }
        finally
        {
            // Cleanup
            if (result != null)
            {
                File.Delete(result);
            }
        }
    }

    [TestMethod]
    public async Task DownloadBlobAsync_ThrowsAfterMaxRetries()
    {
        // Arrange
        var logger = _loggerFactory.CreateLogger(nameof(DownloadBlobAsync_ThrowsAfterMaxRetries));

        var repoName = "testRepo";
        var descriptor = new Descriptor
        {
            MediaType = MediaType.ImageLayerGzip,
            Digest = "sha256:c5098cc7c2a2ad9bfc66e4c4cb242683a578e9d8f25fd8730b289dd5667916ad",
            Size = 1234,
        };
        var cancellationToken = CancellationToken.None;

        var blobs = new Mock<IBlobStore>(MockBehavior.Strict);
        // Simulate 5 failures (assuming your retry logic attempts 5 times before throwing)
        blobs
            .SetupSequence(b => b.FetchAsync(descriptor, cancellationToken))
            .ThrowsAsync(new Exception("Simulated failure 1"))
            .ThrowsAsync(new Exception("Simulated failure 2"))
            .ThrowsAsync(new Exception("Simulated failure 3"))
            .ThrowsAsync(new Exception("Simulated failure 4"))
            .ThrowsAsync(new Exception("Simulated failure 5"));
        var repositoryClient = new Mock<IRepository>(MockBehavior.Strict);
        repositoryClient.SetupGet(r => r.Blobs).Returns(blobs.Object);
        var repositoryFactory = new Mock<IRepositoryFactory>(MockBehavior.Strict);
        repositoryFactory.Setup(f => f.Create(repoName)).Returns(repositoryClient.Object);

        Registry registry = new(repoName, logger, repositoryFactory.Object, null, () => TimeSpan.Zero);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<UnableToDownloadFromRepositoryException>(async () =>
        {
            await registry.DownloadBlobAsync(repoName, descriptor, cancellationToken);
        });

        blobs.Verify(b => b.FetchAsync(descriptor, cancellationToken), Times.Exactly(5));
    }

    [TestMethod]
    [DataRow(OrasProject.Oras.Docker.MediaType.ManifestList)]
    [DataRow(MediaType.ImageIndex)]
    public async Task GetManifestListAcceptsBothIndexFormats(string mediaType)
    {
        byte[] json = "{\"schemaVersion\":2,\"manifests\":[]}"u8.ToArray();
        Descriptor descriptor = Descriptor.Create(json, mediaType);
        var registry = RegistryWithManifest(descriptor, json);
        var index = await registry.GetManifestListAsync("app", "latest", TestContext.CancellationToken);
        Assert.IsNotNull(index);
        Assert.IsEmpty(index.Manifests);
    }

    [TestMethod]
    public async Task GetManifestListRejectsTamperedContent()
    {
        byte[] json = "{\"schemaVersion\":2,\"manifests\":[]}"u8.ToArray();
        Descriptor descriptor = Descriptor.Create(json, MediaType.ImageIndex);
        byte[] tampered = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(json).Replace(":2", ":3", StringComparison.Ordinal));
        var registry = RegistryWithManifest(descriptor, tampered);
        await Assert.ThrowsExactlyAsync<OrasProject.Oras.Content.Exceptions.MismatchedDigestException>(() =>
            registry.GetManifestListAsync("app", "latest", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task GetManifestListRejectsOversizedMetadataBeforeReading()
    {
        Descriptor descriptor = new() { Digest = "sha256:unused", Size = (long)int.MaxValue + 1, MediaType = MediaType.ImageIndex };
        var registry = RegistryWithManifest(descriptor, []);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
            registry.GetManifestListAsync("app", "latest", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task GetImageManifestRejectsTamperedConfig()
    {
        byte[] config = "{\"architecture\":\"amd64\",\"os\":\"linux\"}"u8.ToArray();
        Descriptor configDescriptor = Descriptor.Create(config, MediaType.ImageConfig);
        var manifest = new Manifest { SchemaVersion = 2, Config = configDescriptor, Layers = [] };
        byte[] json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(manifest);
        Descriptor descriptor = Descriptor.Create(json, MediaType.ImageManifest);
        var manifests = new Mock<IManifestStore>(MockBehavior.Strict);
        manifests.Setup(m => m.FetchAsync("latest", It.IsAny<CancellationToken>()))
            .ReturnsAsync((descriptor, new MemoryStream(json)));
        var blobs = new Mock<IBlobStore>(MockBehavior.Strict);
        blobs.Setup(b => b.FetchAsync(It.Is<Descriptor>(d => d.Digest == configDescriptor.Digest), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(config).Replace("amd64", "arm64", StringComparison.Ordinal))));
        var repository = new Mock<IRepository>(MockBehavior.Strict);
        repository.SetupGet(r => r.Manifests).Returns(manifests.Object);
        repository.SetupGet(r => r.Blobs).Returns(blobs.Object);
        var factory = new Mock<IRepositoryFactory>(MockBehavior.Strict);
        factory.Setup(f => f.Create("app")).Returns(repository.Object);
        var registry = new Registry("example.com", _loggerFactory.CreateLogger("tampered-config"), factory.Object);
        await Assert.ThrowsExactlyAsync<OrasProject.Oras.Content.Exceptions.MismatchedDigestException>(() =>
            registry.GetImageManifestAsync("app", "latest", "linux-x64", Mock.Of<IManifestPicker>(), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task DownloadBlobPreservesCancellationOnLastAttempt()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
        Descriptor descriptor = Descriptor.Create(bytes, MediaType.ImageLayerGzip);
        using var cancellation = new CancellationTokenSource();
        int calls = 0;
        var blobs = new Mock<IBlobStore>(MockBehavior.Strict);
        blobs.Setup(b => b.FetchAsync(descriptor, It.IsAny<CancellationToken>())).Returns(() =>
        {
            if (++calls == 5)
            {
                cancellation.Cancel();
                return Task.FromCanceled<Stream>(cancellation.Token);
            }
            return Task.FromException<Stream>(new IOException("interrupted download"));
        });
        var repository = new Mock<IRepository>(MockBehavior.Strict);
        repository.SetupGet(r => r.Blobs).Returns(blobs.Object);
        var factory = new Mock<IRepositoryFactory>(MockBehavior.Strict);
        factory.Setup(f => f.Create("app")).Returns(repository.Object);
        var registry = new Registry("example.com", _loggerFactory.CreateLogger("canceled-download"), factory.Object, retryDelayProvider: () => TimeSpan.Zero);
        await Assert.ThrowsAsync<OperationCanceledException>(() => registry.DownloadBlobAsync("app", descriptor, cancellation.Token));
        Assert.AreEqual(5, calls);
    }

    [TestMethod]
    [DataRow("DOTNET_CONTAINER_REGISTRY_CHUNKED_UPLOAD_SIZE_BYTES", "0")]
    [DataRow("SDK_CONTAINER_REGISTRY_CHUNKED_UPLOAD_SIZE_BYTES", "-1")]
    public void InvalidChunkSettingReportsTheEnvironmentVariable(string variable, string value)
    {
        var error = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RegistrySettings(environment:
            new MockEnvironmentProvider(new Dictionary<string, string> { [variable] = value })));
        Assert.Contains(variable, error.Message);
    }

    [TestMethod]
    public async Task CachedBlobDoesNotKeepTheFileOpen()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
        Descriptor descriptor = Descriptor.Create(bytes, MediaType.ImageLayerGzip);
        string path = ContentStore.PathForDescriptor(descriptor);
        await File.WriteAllBytesAsync(path, bytes, TestContext.CancellationToken);
        try
        {
            var registry = new Registry("example.com", _loggerFactory.CreateLogger("cached-blob"), Mock.Of<IRepositoryFactory>());
            Assert.AreEqual(path, await registry.DownloadBlobAsync("app", descriptor, TestContext.CancellationToken));
            using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.AreEqual(bytes.Length, exclusive.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [DataRow(true, HttpStatusCode.Forbidden, false)]
    [DataRow(true, HttpStatusCode.MethodNotAllowed, false)]
    [DataRow(false, HttpStatusCode.Forbidden, false)]
    [DataRow(true, HttpStatusCode.Accepted, false)]
    [DataRow(true, HttpStatusCode.Accepted, true)]
    public async Task PushLayersFallsBackFromMountWithoutRetryingUploadFailures(
        bool sameRegistry, HttpStatusCode mountStatus, bool rejectUpload)
    {
        byte[] layer = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
        Descriptor descriptor = Descriptor.Create(layer, MediaType.ImageLayerGzip);
        Descriptor config = Descriptor.Create("{}"u8.ToArray(), MediaType.ImageConfig);
        string sourceHost = sameRegistry ? "destination.example.com" : "source.example.com";
        int mounts = 0, downloads = 0, uploads = 0;
        var methods = new List<string>();
        using var client = new HttpClient(new MountTestHandler(async (request, token) =>
        {
            string path = request.RequestUri!.AbsolutePath;
            methods.Add(request.Method.Method);
            var response = new HttpResponseMessage(HttpStatusCode.Created) { RequestMessage = request };
            if (request.Method == HttpMethod.Head)
            {
                response.StatusCode = path.EndsWith(config.Digest, StringComparison.Ordinal) ? HttpStatusCode.OK : HttpStatusCode.NotFound;
                response.Content = new ByteArrayContent("{}"u8.ToArray());
            }
            else if (request.Method == HttpMethod.Get)
            {
                Assert.AreEqual(sourceHost, request.RequestUri.Host);
                Assert.AreEqual($"/v2/base/image/blobs/{descriptor.Digest}", path);
                downloads++;
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new ByteArrayContent(layer);
            }
            else if (request.Method == HttpMethod.Post)
            {
                bool mounting = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query)["mount"] is not null;
                if (mounting)
                {
                    Assert.IsTrue(sameRegistry, "A mount must never be attempted across registries.");
                    mounts++;
                }
                response.StatusCode = mounting ? mountStatus : HttpStatusCode.Accepted;
                response.Headers.Location = new Uri("/upload", UriKind.Relative);
            }
            else if (request.Method == HttpMethod.Patch)
            {
                uploads++;
                byte[] uploaded = await request.Content!.ReadAsByteArrayAsync(token);
                Assert.AreSequenceEqual(layer, uploaded);
                response.StatusCode = rejectUpload ? HttpStatusCode.Forbidden : HttpStatusCode.Accepted;
                response.Headers.Location = new Uri("/upload", UriKind.Relative);
            }
            return response;
        }));
        var destinationFactory = new OrasRepositoryFactory(new Uri("http://destination.example.com"),
            new OrasProject.Oras.Registry.Remote.PlainClient(client), new RegistrySettings { ForceChunkedUpload = true });
        var sourceFactory = new OrasRepositoryFactory(new Uri($"http://{sourceHost}"),
            new OrasProject.Oras.Registry.Remote.PlainClient(client), new RegistrySettings());
        var destinationRegistry = new Registry(new Uri("http://destination.example.com"), _loggerFactory.CreateLogger("mount-destination"), destinationFactory);
        var sourceRegistry = new Registry(new Uri($"http://{sourceHost}"), _loggerFactory.CreateLogger("mount-source"), sourceFactory);
        string manifest = System.Text.Json.JsonSerializer.Serialize(new Manifest { SchemaVersion = 2, Config = config, Layers = [descriptor] });
        var image = new BuiltImage
        {
            Config = "{}",
            ImageDigest = config.Digest,
            Manifest = manifest,
            ManifestDigest = DigestUtils.ComputeSha256Digest(manifest),
            ManifestMediaType = MediaType.ImageManifest,
            Layers = [descriptor],
            OS = "linux",
            Architecture = "amd64",
        };
        try
        {
            Task push = destinationRegistry.PushAsync(image,
                new SourceImageReference(sourceRegistry, "base/image", "latest", null),
                new DestinationImageReference(destinationRegistry, "app", ["latest"]),
                noCache: true, TestContext.CancellationToken);
            if (rejectUpload)
            {
                await Assert.ThrowsExactlyAsync<UnableToAccessRepositoryException>(() => push);
            }
            else
            {
                await push;
            }
            Assert.AreEqual(sameRegistry ? 1 : 0, mounts);
            Assert.AreEqual(1, downloads);
            Assert.AreEqual(1, uploads);
            Assert.AreEqual(sameRegistry && mountStatus != HttpStatusCode.Accepted ? 2 : 1, methods.Count(method => method == "POST"));
        }
        finally
        {
            File.Delete(ContentStore.PathForDescriptor(descriptor));
        }
    }

    private sealed class MountTestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request, cancellationToken);
    }

    private Registry RegistryWithManifest(Descriptor descriptor, byte[] content)
    {
        var manifests = new Mock<IManifestStore>(MockBehavior.Strict);
        manifests.Setup(m => m.FetchAsync("latest", It.IsAny<CancellationToken>()))
            .ReturnsAsync((descriptor, new MemoryStream(content)));
        var repository = new Mock<IRepository>(MockBehavior.Strict);
        repository.SetupGet(r => r.Manifests).Returns(manifests.Object);
        var factory = new Mock<IRepositoryFactory>(MockBehavior.Strict);
        factory.Setup(f => f.Create("app")).Returns(repository.Object);
        return new Registry("example.com", _loggerFactory.CreateLogger("metadata"), factory.Object);
    }

    private class MockEnvironmentProvider : IEnvironmentProvider
    {
        private readonly IDictionary<string, string> _environmentVariables;

        public MockEnvironmentProvider(IDictionary<string, string> environmentVariables)
        {
            _environmentVariables = environmentVariables;
        }

        public bool GetEnvironmentVariableAsBool(string name, bool defaultValue)
        {
            string? str = GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(str))
            {
                return defaultValue;
            }

            switch (str.ToLowerInvariant())
            {
                case "true":
                case "1":
                case "yes":
                    return true;
                case "false":
                case "0":
                case "no":
                    return false;
                default:
                    return defaultValue;
            }
        }

        public string? GetEnvironmentVariable(string name)
        {
            string? value;
            _environmentVariables.TryGetValue(name, out value);
            return value;
        }

        public string? GetEnvironmentVariable(string variable, EnvironmentVariableTarget target)
            => GetEnvironmentVariable(variable);

        public int? GetEnvironmentVariableAsNullableInt(string variable)
        {
            if (GetEnvironmentVariable(variable) is string strValue && int.TryParse(strValue, out int intValue))
            {
                return intValue;
            }

            return null;
        }

        public void SetEnvironmentVariable(string variable, string value, EnvironmentVariableTarget target)
            => throw new NotImplementedException();

        public IEnumerable<string> ExecutableExtensions
            => throw new NotImplementedException();

        public string GetCommandPath(string commandName, params string[] extensions)
            => throw new NotImplementedException();

        public string GetCommandPathFromRootPath(string rootPath, string commandName, params string[] extensions)
            => throw new NotImplementedException();

        public string GetCommandPathFromRootPath(string rootPath, string commandName, IEnumerable<string> extensions)
            => throw new NotImplementedException();

        public bool TryGetEnvironmentVariable(string name, [NotNullWhen(true)] out string? value) => _environmentVariables.TryGetValue(name, out value!);

        public bool TryGetEnvironmentVariableAsBool(string name, [NotNullWhen(true)] out bool value)
        {
            if (TryGetEnvironmentVariable(name, out string? strValue) && bool.TryParse(strValue, out bool boolValue))
            {
                value = boolValue;
                return true;
            }
            else
            {
                value = false;
                return false;
            }
        }

        public bool TryGetEnvironmentVariableAsInt(string name, [NotNullWhen(true)] out int value)
        {
            if (TryGetEnvironmentVariable(name, out string? strValue) && int.TryParse(strValue, out int intValue))
            {
                value = intValue;
                return true;
            }
            else
            {
                value = 0;
                return false;
            }
        }
    }
}
