// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;
using System.Net.Sockets;
using Moq;

namespace Microsoft.NET.Build.Containers.UnitTests;

public class RegistryTests : IDisposable
{
    private ITestOutputHelper _testOutput;
    private readonly TestLoggerFactory _loggerFactory;

    public RegistryTests(ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
        _loggerFactory = new TestLoggerFactory(testOutput);
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
    }

    [InlineData("us-south1-docker.pkg.dev", true)]
    [InlineData("us.gcr.io", false)]
    [Theory]
    public void CheckIfGoogleArtifactRegistry(string registryName, bool isECR)
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(CheckIfGoogleArtifactRegistry));
        Registry registry = new(registryName, logger, RegistryMode.Push);
        Assert.Equal(isECR, registry.IsGoogleArtifactRegistry);
    }

    [Fact]
    public void DockerIoAlias()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(DockerIoAlias));
        Registry registry = new("docker.io", logger, RegistryMode.Push);
        Assert.True(registry.IsDockerHub);
        Assert.Equal("docker.io", registry.RegistryName);
        Assert.Equal("registry-1.docker.io", registry.BaseUri.Host);
    }

    [Fact]
    public async Task RegistriesThatProvideNoUploadSizeAttemptFullUpload()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(RegistriesThatProvideNoUploadSizeAttemptFullUpload));
        var repoName = "testRepo";
        var layerDigest = "sha256:fafafafafafafafafafafafafafafafa";
        var mockLayer = new Mock<Layer>(MockBehavior.Strict);
        mockLayer
            .Setup(l => l.OpenBackingFile()).Returns(new MemoryStream(new byte[1000]));
        mockLayer
            .Setup(l => l.Descriptor).Returns(new Descriptor("blah", layerDigest, 1234));

        var uploadPath = new Uri("/uploads/foo/12345", UriKind.Relative);
        var api = new Mock<IRegistryAPI>(MockBehavior.Loose);
        api.Setup(api => api.Blob.ExistsAsync(repoName, layerDigest, It.IsAny<CancellationToken>())).Returns(Task.FromResult(false));
        api.Setup(api => api.Blob.Upload.StartAsync(repoName, It.IsAny<CancellationToken>())).Returns(Task.FromResult(new StartUploadInformation(uploadPath)));
        api.Setup(api => api.Blob.Upload.UploadAtomicallyAsync(uploadPath, It.IsAny<Stream>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(new FinalizeUploadInformation(uploadPath)));

        Registry registry = new("public.ecr.aws", logger, api.Object);
        await registry.PushLayerAsync(mockLayer.Object, repoName, CancellationToken.None);

        api.Verify(api => api.Blob.Upload.UploadChunkAsync(uploadPath, It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Never());
        api.Verify(api => api.Blob.Upload.UploadAtomicallyAsync(uploadPath, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task RegistriesThatProvideUploadSizePrefersFullUploadWhenChunkSizeIsLowerThanContentLength()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(RegistriesThatProvideUploadSizePrefersFullUploadWhenChunkSizeIsLowerThanContentLength));
        var repoName = "testRepo";
        var layerDigest = "sha256:fafafafafafafafafafafafafafafafa";
        var mockLayer = new Mock<Layer>(MockBehavior.Strict);
        var chunkSizeLessThanContentLength = 10000;
        var registryUri = new Uri("https://public.ecr.aws");;
        mockLayer
            .Setup(l => l.OpenBackingFile()).Returns(new MemoryStream(new byte[100000]));
        mockLayer
            .Setup(l => l.Descriptor).Returns(new Descriptor("blah", layerDigest, 1234));

        var uploadPath = new Uri("/uploads/foo/12345", UriKind.Relative);
        var absoluteUploadUri = new Uri(registryUri, uploadPath);
        var api = new Mock<IRegistryAPI>(MockBehavior.Loose);
        var uploadedCount = 0;
        api.Setup(api => api.Blob.ExistsAsync(repoName, layerDigest, It.IsAny<CancellationToken>())).Returns(Task.FromResult(false));
        api.Setup(api => api.Blob.Upload.StartAsync(repoName, It.IsAny<CancellationToken>())).Returns(Task.FromResult(new StartUploadInformation(uploadPath)));
        api.Setup(api => api.Blob.Upload.UploadAtomicallyAsync(uploadPath, It.IsAny<Stream>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(new FinalizeUploadInformation(uploadPath)));
        api.Setup(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())).Returns(() =>
        {
            uploadedCount += chunkSizeLessThanContentLength;
            return Task.FromResult(ChunkUploadSuccessful(absoluteUploadUri, uploadPath, uploadedCount));
        });

        Registry registry = new(registryUri, logger, api.Object);
        await registry.PushLayerAsync(mockLayer.Object, repoName, CancellationToken.None);

        api.Verify(api => api.Blob.Upload.UploadAtomicallyAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
        api.Verify(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegistriesThatFailAtomicUploadFallbackToChunked()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(RegistriesThatFailAtomicUploadFallbackToChunked));
        var repoName = "testRepo";
        var layerDigest = "sha256:fafafafafafafafafafafafafafafafa";
        var mockLayer = new Mock<Layer>(MockBehavior.Strict);
        var contentLength = 100000;
        var chunkSizeLessThanContentLength = 100000;
        var registryUri = new Uri("https://public.ecr.aws");;
        mockLayer
            .Setup(l => l.OpenBackingFile()).Returns(new MemoryStream(new byte[contentLength]));
        mockLayer
            .Setup(l => l.Descriptor).Returns(new Descriptor("blah", layerDigest, 1234));

        var uploadPath = new Uri("/uploads/foo/12345", UriKind.Relative);
        var absoluteUploadUri = new Uri(registryUri, uploadPath);
        var api = new Mock<IRegistryAPI>(MockBehavior.Loose);
        var uploadedCount = 0;
        api.Setup(api => api.Blob.ExistsAsync(repoName, layerDigest, It.IsAny<CancellationToken>())).Returns(Task.FromResult(false));
        api.Setup(api => api.Blob.Upload.StartAsync(repoName, It.IsAny<CancellationToken>())).Returns(Task.FromResult(new StartUploadInformation(uploadPath)));
        api.Setup(api => api.Blob.Upload.UploadAtomicallyAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<Stream>(), It.IsAny<CancellationToken>())).Throws(new Exception("Server-side shutdown the thing"));
        api.Setup(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())).Returns(() =>
        {
            uploadedCount += chunkSizeLessThanContentLength;
            return Task.FromResult(ChunkUploadSuccessful(absoluteUploadUri, uploadPath, uploadedCount));
        });

        Registry registry = new(registryUri, logger, api.Object);
        await registry.PushLayerAsync(mockLayer.Object, repoName, CancellationToken.None);

        api.Verify(api => api.Blob.Upload.UploadAtomicallyAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once());
        api.Verify(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Exactly(contentLength / chunkSizeLessThanContentLength));
    }

    [Fact]
    public async Task ChunkedUploadCalculatesChunksCorrectly()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(RegistriesThatFailAtomicUploadFallbackToChunked));
        var repoName = "testRepo";
        var layerDigest = "sha256:fafafafafafafafafafafafafafafafa";
        var mockLayer = new Mock<Layer>(MockBehavior.Strict);
        var contentLength = 1000000;
        var chunkSize = 100000;
        var registryUri = new Uri("https://public.ecr.aws");;
        mockLayer
            .Setup(l => l.OpenBackingFile()).Returns(new MemoryStream(new byte[contentLength]));
        mockLayer
            .Setup(l => l.Descriptor).Returns(new Descriptor("blah", layerDigest, 1234));

        var uploadPath = new Uri("/uploads/foo/12345", UriKind.Relative);
        var absoluteUploadUri = new Uri(registryUri, uploadPath);
        var api = new Mock<IRegistryAPI>(MockBehavior.Loose);
        var uploadedCount = 0;
        api.Setup(api => api.Blob.ExistsAsync(repoName, layerDigest, It.IsAny<CancellationToken>())).Returns(Task.FromResult(false));
        api.Setup(api => api.Blob.Upload.StartAsync(repoName, It.IsAny<CancellationToken>())).Returns(Task.FromResult(new StartUploadInformation(uploadPath)));
        api.Setup(api => api.Blob.Upload.UploadAtomicallyAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<Stream>(), It.IsAny<CancellationToken>())).Throws(new Exception("Server-side shutdown the thing"));
        api.Setup(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())).Returns(() =>
        {
            uploadedCount += chunkSize;
            return Task.FromResult(ChunkUploadSuccessful(absoluteUploadUri, uploadPath, uploadedCount));
        });

        RegistrySettings settings = new()
        {
            ParallelUploadEnabled = false,
            ForceChunkedUpload = false,
            ChunkedUploadSizeBytes = chunkSize,
        };

        Registry registry = new(registryUri, logger, api.Object, settings);
        await registry.PushLayerAsync(mockLayer.Object, repoName, CancellationToken.None);

        api.Verify(api => api.Blob.Upload.UploadAtomicallyAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once());
        api.Verify(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Exactly(10));
    }

    [Fact]
    public async Task PushAsync_Logging()
    {
        using TestLoggerFactory loggerFactory = new(_testOutput);
        List<(LogLevel, string)> loggedMessages = new();
        loggerFactory.AddProvider(new InMemoryLoggerProvider(loggedMessages));
        ILogger logger = loggerFactory.CreateLogger(nameof(PushAsync_Logging));

        var repoName = "testRepo";
        var layerDigest = "sha256:fafafafafafafafafafafafafafafafa";
        var mockLayer = new Mock<Layer>(MockBehavior.Strict);
        mockLayer
            .Setup(l => l.OpenBackingFile()).Returns(new MemoryStream(new byte[1000]));
        mockLayer
            .Setup(l => l.Descriptor).Returns(new Descriptor("blah", layerDigest, 1234));

        var uploadPath = new Uri("/uploads/foo/12345", UriKind.Relative);
        var api = new Mock<IRegistryAPI>(MockBehavior.Loose);
        api.Setup(api => api.Blob.ExistsAsync(repoName, layerDigest, It.IsAny<CancellationToken>())).Returns(Task.FromResult(false));
        api.Setup(api => api.Blob.Upload.StartAsync(repoName, It.IsAny<CancellationToken>())).Returns(Task.FromResult(new StartUploadInformation(uploadPath)));
        api.Setup(api => api.Blob.Upload.UploadAtomicallyAsync(uploadPath, It.IsAny<Stream>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(new FinalizeUploadInformation(uploadPath)));

        Registry registry = new("public.ecr.aws", logger, api.Object);
        await registry.PushLayerAsync(mockLayer.Object, repoName, CancellationToken.None);

        Assert.NotEmpty(loggedMessages);
        Assert.True(loggedMessages.All(m => m.Item1 == LogLevel.Trace));
        var messages = loggedMessages.Select(m => m.Item2).ToList();
        Assert.Contains(messages, m => m == "Started upload session for sha256:fafafafafafafafafafafafafafafafa");
        Assert.Contains(messages, m => m == "Finalized upload session for sha256:fafafafafafafafafafafafafafafafa");
    }

    [Fact]
    public async Task PushAsync_ForceChunkedUpload()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(PushAsync_ForceChunkedUpload));

        string repoName = "testRepo";
        string layerDigest = "sha256:fafafafafafafafafafafafafafafafa";
        Mock<Layer> mockLayer = new(MockBehavior.Strict);
        int contentLength = 1000000;
        int chunkSize = 100000;
        var registryUri = new Uri("https://public.ecr.aws");;
        mockLayer
            .Setup(l => l.OpenBackingFile()).Returns(new MemoryStream(new byte[contentLength]));
        mockLayer
            .Setup(l => l.Descriptor).Returns(new Descriptor("blah", layerDigest, 1234));

        Uri uploadPath = new("/uploads/foo/12345", UriKind.Relative);
        Uri absoluteUploadUri = new(registryUri, uploadPath);
        Mock<IRegistryAPI> api = new(MockBehavior.Loose);
        int uploadedCount = 0;
        api.Setup(api => api.Blob.ExistsAsync(repoName, layerDigest, It.IsAny<CancellationToken>())).Returns(Task.FromResult(false));
        api.Setup(api => api.Blob.Upload.StartAsync(repoName, It.IsAny<CancellationToken>())).Returns(Task.FromResult(new StartUploadInformation(uploadPath)));
        api.Setup(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())).Returns(() =>
        {
            uploadedCount += chunkSize;
            return Task.FromResult(ChunkUploadSuccessful(absoluteUploadUri, uploadPath, uploadedCount));
        });

        RegistrySettings settings = new()
        {
            ParallelUploadEnabled = false,
            ForceChunkedUpload = true,
            ChunkedUploadSizeBytes = chunkSize,
        };

        Registry registry = new(registryUri, logger, api.Object, settings);
        await registry.PushLayerAsync(mockLayer.Object, repoName, CancellationToken.None);

        api.Verify(api => api.Blob.Upload.UploadAtomicallyAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never());
        api.Verify(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Exactly(10));
    }

    [Fact]
    public async Task CanParseRegistryDeclaredChunkSize_FromRange()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(CanParseRegistryDeclaredChunkSize_FromRange));
        string repoName = "testRepo";

        Mock<HttpClient> client = new(MockBehavior.Loose);
        HttpResponseMessage httpResponse = new()
        {
            StatusCode = HttpStatusCode.Accepted,
        };
        httpResponse.Headers.Add("Range", "0-100000");
        httpResponse.Headers.Location = new Uri("https://my-registy.com/v2/testRepo/blobs/uploads/");
        client.Setup(client => client.SendAsync(It.Is<HttpRequestMessage>(m => m.RequestUri == new Uri("https://my-registy.com/v2/testRepo/blobs/uploads/")), It.IsAny<CancellationToken>())).Returns(Task.FromResult(httpResponse));

        HttpClient finalClient = client.Object;
        DefaultBlobUploadOperations operations = new(new Uri("https://my-registy.com"), finalClient, logger);
        StartUploadInformation result = await operations.StartAsync(repoName, CancellationToken.None);

        Assert.Equal("https://my-registy.com/v2/testRepo/blobs/uploads/", result.UploadUri.AbsoluteUri);
    }

    [Fact]
    public async Task CanParseRegistryDeclaredChunkSize_FromOCIChunkMinLength()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(CanParseRegistryDeclaredChunkSize_FromOCIChunkMinLength));
        string repoName = "testRepo";

        Mock<HttpClient> client = new(MockBehavior.Loose);
        HttpResponseMessage httpResponse = new()
        {
            StatusCode = HttpStatusCode.Accepted,
        };
        httpResponse.Headers.Add("OCI-Chunk-Min-Length", "100000");
        httpResponse.Headers.Location = new Uri("https://my-registy.com/v2/testRepo/blobs/uploads/");
        client.Setup(client => client.SendAsync(It.Is<HttpRequestMessage>(m => m.RequestUri == new Uri("https://my-registy.com/v2/testRepo/blobs/uploads/")), It.IsAny<CancellationToken>())).Returns(Task.FromResult(httpResponse));

        HttpClient finalClient = client.Object;
        DefaultBlobUploadOperations operations = new(new Uri("https://my-registy.com"), finalClient, logger);
        StartUploadInformation result = await operations.StartAsync(repoName, CancellationToken.None);

        Assert.Equal("https://my-registy.com/v2/testRepo/blobs/uploads/", result.UploadUri.AbsoluteUri);
    }

    [Fact]
    public async Task CanParseRegistryDeclaredChunkSize_None()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(CanParseRegistryDeclaredChunkSize_None));
        string repoName = "testRepo";

        Mock<HttpClient> client = new(MockBehavior.Loose);
        HttpResponseMessage httpResponse = new()
        {
            StatusCode = HttpStatusCode.Accepted,
        };
        httpResponse.Headers.Location = new Uri("https://my-registy.com/v2/testRepo/blobs/uploads/");
        client.Setup(client => client.SendAsync(It.Is<HttpRequestMessage>(m => m.RequestUri == new Uri("https://my-registy.com/v2/testRepo/blobs/uploads/")), It.IsAny<CancellationToken>())).Returns(Task.FromResult(httpResponse));

        HttpClient finalClient = client.Object;
        DefaultBlobUploadOperations operations = new(new Uri("https://my-registy.com"), finalClient, logger);
        StartUploadInformation result = await operations.StartAsync(repoName, CancellationToken.None);

        Assert.Equal("https://my-registy.com/v2/testRepo/blobs/uploads/", result.UploadUri.AbsoluteUri);
    }

    [Fact]
    public async Task UploadBlobChunkedAsync_NormalFlow()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(UploadBlobChunkedAsync_NormalFlow));
        var registryUri = new Uri("https://public.ecr.aws");;

        int contentLength = 50000000;
        int chunkSize = 10000000;

        Stream testStream = new MemoryStream(new byte[contentLength]);

        Uri uploadPath = new("/uploads/foo/12345", UriKind.Relative);
        Uri absoluteUploadUri = new(registryUri, uploadPath);
        Mock<IRegistryAPI> api = new(MockBehavior.Loose);
        int uploadedCount = 0;
        api.Setup(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())).Returns(() =>
        {
            uploadedCount += chunkSize;
            return Task.FromResult(ChunkUploadSuccessful(absoluteUploadUri, uploadPath, uploadedCount));
        });

        RegistrySettings settings = new()
        {
            ParallelUploadEnabled = false,
            ForceChunkedUpload = false,
            ChunkedUploadSizeBytes = chunkSize,
        };

        Registry registry = new(registryUri, logger, api.Object, settings);
        await registry.UploadBlobChunkedAsync(testStream, new StartUploadInformation(absoluteUploadUri), CancellationToken.None);

        api.Verify(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Exactly(5));
    }

    [Fact]
    public async Task UploadBlobChunkedAsync_Failure()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(UploadBlobChunkedAsync_NormalFlow));
        var registryUri = new Uri("https://public.ecr.aws");;

        int contentLength = 50000000;
        int chunkSize = 10000000;

        Stream testStream = new MemoryStream(new byte[contentLength]);

        Uri uploadPath = new("/uploads/foo/12345", UriKind.Relative);
        Uri absoluteUploadUri = new(registryUri, uploadPath);
        Mock<IRegistryAPI> api = new(MockBehavior.Loose);

        Exception preparedException = new ApplicationException(Resource.FormatString(nameof(Strings.BlobUploadFailed), $"PATCH <uri>", HttpStatusCode.InternalServerError));

        api.Setup(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())).Returns(() =>
        {
            throw preparedException;
        });

        RegistrySettings settings = new()
        {
            ParallelUploadEnabled = false,
            ForceChunkedUpload = false,
            ChunkedUploadSizeBytes = chunkSize,
        };

        Registry registry = new(registryUri, logger, api.Object, settings);
        ApplicationException receivedException = await Assert.ThrowsAsync<ApplicationException>(() => registry.UploadBlobChunkedAsync(testStream, new StartUploadInformation(absoluteUploadUri), CancellationToken.None));

        Assert.Equal(preparedException, receivedException);

        api.Verify(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
    }

    [InlineData(true, true, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, false, true)]
    [InlineData(false, false, false)]
    [Theory]
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
                using TcpClient client = await listener.AcceptTcpClientAsync();
                try
                {
                    using Stream stream = serverIsHttps ? new SslStream(client.GetStream(), leaveInnerStreamOpen: false) : client.GetStream();
                    if (stream is SslStream sslStream)
                    {
                        await sslStream.AuthenticateAsServerAsync(sslOptions!, default(CancellationToken));
                    }
                    byte[] buffer = new byte[10];
                    await stream.ReadAtLeastAsync(buffer, buffer.Length); // Wait for the request.
                    // Repond if we see '/v2/' in the buffer (since we expect that as part of the request path).
                    if (buffer.AsSpan().IndexOf("/v2/"u8) != 0)
                    {
                        await stream.WriteAsync("HTTP/1.0 200 OK\r\nContent-Length: 0\r\n\r\n"u8.ToArray());
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
        });

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
            await Assert.ThrowsAsync<NotImplementedException>(() => getManifest);
        }
        else
        {
            // Does not fall back and throws HttpRequestException with SecureConnectionError.
            Exception? exception = await Assert.ThrowsAnyAsync<Exception>(() => getManifest);
            try
            {
                // The AuthHandshakeMessageHandler may reach its retry limit and throw an ApplicationException.
                if (exception is ApplicationException)
                {
                    // Find the exception for the first failed attempt.
                    exception = (exception.InnerException as AggregateException)?.InnerExceptions.FirstOrDefault();
                    Assert.NotNull(exception);
                }

                Assert.IsType<HttpRequestException>(exception);
                HttpRequestException requestException = (HttpRequestException)exception;
                Assert.Equal(HttpRequestError.SecureConnectionError, requestException.HttpRequestError);

                // The FallbackToHttpMessageHandler should fall back (if this registry was configured as insecure).
                Assert.True(FallbackToHttpMessageHandler.ShouldAttemptFallbackToHttp(requestException));
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

    [InlineData("localhost", null, true)]
    [InlineData("localhost:5000", null, true)]
    [InlineData("public.ecr.aws", null, false)]
    [InlineData("public.ecr.aws", "public.ecr.aws", true)]
    [InlineData("public.ecr.aws", "Public.ecr.aws", true)] // ignore case
    [InlineData("public.ecr.aws", "public.ecr.aws;docker.io", true)] // multiple registries
    [InlineData("public.ecr.aws", ";public.ecr.aws ;  docker.io ", true)] // ignore whitespace
    [InlineData("public.ecr.aws", "public.ecr.aws2;docker.io ", false)] // full name match
    [Theory]
    public void IsRegistryInsecure(string registryName, string? insecureRegistriesEnvvar, bool expectedInsecure)
    {
        var environment = new Dictionary<string, string>();
        if (insecureRegistriesEnvvar is not null)
        {
            environment["DOTNET_CONTAINER_INSECURE_REGISTRIES"] = insecureRegistriesEnvvar;
        }

        var registrySettings = new RegistrySettings(registryName, new MockEnvironmentProvider(environment));

        Assert.Equal(expectedInsecure, registrySettings.IsInsecure);
    }

    private static NextChunkUploadInformation ChunkUploadSuccessful(Uri requestUri, Uri uploadUrl, int? contentLength, HttpStatusCode code = HttpStatusCode.Accepted)
    {
        return new(uploadUrl);
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
    }
}
