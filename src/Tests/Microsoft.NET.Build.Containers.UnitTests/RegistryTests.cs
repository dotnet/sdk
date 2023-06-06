// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Registry;
using Microsoft.NET.TestFramework;
using Moq;
using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Containers.UnitTests
{
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


        [InlineData("public.ecr.aws", true)]
        [InlineData("123412341234.dkr.ecr.us-west-2.amazonaws.com", true)]
        [InlineData("123412341234.dkr.ecr-fips.us-west-2.amazonaws.com", true)]
        [InlineData("notvalid.dkr.ecr.us-west-2.amazonaws.com", false)]
        [InlineData("1111.dkr.ecr.us-west-2.amazonaws.com", false)]
        [InlineData("mcr.microsoft.com", false)]
        [InlineData("localhost", false)]
        [InlineData("hub", false)]
        [Theory]
        public void CheckIfAmazonECR(string registryName, bool isECR)
        {
            ILogger logger = _loggerFactory.CreateLogger(nameof(CheckIfAmazonECR));
            RegistryManager registry = new RegistryManager(ContainerHelpers.TryExpandRegistryToUri(registryName), logger);
            Assert.Equal(isECR, registry.IsAmazonECRRegistry);
        }

        [InlineData("us-south1-docker.pkg.dev", true)]
        [InlineData("us.gcr.io", false)]
        [Theory]
        public void CheckIfGoogleArtifactRegistry(string registryName, bool isECR)
        {
            ILogger logger = _loggerFactory.CreateLogger(nameof(CheckIfGoogleArtifactRegistry));
            RegistryManager registry = new RegistryManager(ContainerHelpers.TryExpandRegistryToUri(registryName), logger);
            Assert.Equal(isECR, registry.IsGoogleArtifactRegistry);
        }

        [Fact]
        public async Task RegistriesThatProvideNoUploadSizeAttemptFullUpload()
        {
            ILogger logger = _loggerFactory.CreateLogger(nameof(CheckIfAmazonECR));
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
            api.Setup(api => api.Blob.Upload.StartAsync(repoName, It.IsAny<CancellationToken>())).Returns(Task.FromResult(new StartUploadInformation(null, uploadPath)));
            api.Setup(api => api.Blob.Upload.UploadAtomicallyAsync(uploadPath, It.IsAny<Stream>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(new FinalizeUploadInformation(uploadPath)));

            RegistryManager registry = new(ContainerHelpers.TryExpandRegistryToUri("public.ecr.aws"), api.Object, logger);
            await registry.PushAsync(mockLayer.Object, repoName, CancellationToken.None);

            api.Verify(api => api.Blob.Upload.UploadChunkAsync(uploadPath, It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Never());
            api.Verify(api => api.Blob.Upload.UploadAtomicallyAsync(uploadPath, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task RegistriesThatProvideUploadSizeSkipFullUploadWhenChunkSizeIsLowerThanContentLength()
        {
            ILogger logger = _loggerFactory.CreateLogger(nameof(CheckIfAmazonECR));
            var repoName = "testRepo";
            var layerDigest = "sha256:fafafafafafafafafafafafafafafafa";
            var mockLayer = new Mock<Layer>(MockBehavior.Strict);
            var contentLength = 100000;
            var chunkSizeLessThanContentLength = 10000;
            var registryUri = ContainerHelpers.TryExpandRegistryToUri("public.ecr.aws");
            mockLayer
                .Setup(l => l.OpenBackingFile()).Returns(new MemoryStream(new byte[100000]));
            mockLayer
                .Setup(l => l.Descriptor).Returns(new Descriptor("blah", layerDigest, 1234));

            var uploadPath = new Uri("/uploads/foo/12345", UriKind.Relative);
            var absoluteUploadUri = new Uri(registryUri, uploadPath);
            var api = new Mock<IRegistryAPI>(MockBehavior.Loose);
            var uploadedCount = 0;
            api.Setup(api => api.Blob.ExistsAsync(repoName, layerDigest, It.IsAny<CancellationToken>())).Returns(Task.FromResult(false));
            api.Setup(api => api.Blob.Upload.StartAsync(repoName, It.IsAny<CancellationToken>())).Returns(Task.FromResult(new StartUploadInformation(chunkSizeLessThanContentLength, uploadPath)));

            api.Setup(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())).Returns(() => {
                uploadedCount += chunkSizeLessThanContentLength;
                return Task.FromResult(ChunkUploadSuccessful(absoluteUploadUri, uploadPath, uploadedCount));
            });

            RegistryManager registry = new(registryUri, api.Object, logger);
            await registry.PushAsync(mockLayer.Object, repoName, CancellationToken.None);

            api.Verify(api => api.Blob.Upload.UploadAtomicallyAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never());
            api.Verify(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Exactly(contentLength/chunkSizeLessThanContentLength));
        }

        private static HttpResponseMessage ChunkUploadSuccessful(Uri requestUri, Uri uploadUrl, int? contentLength)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Accepted);
            response.RequestMessage = new HttpRequestMessage(HttpMethod.Patch, requestUri);
            response.Headers.Location = uploadUrl;
            if (contentLength is int len) response.Headers.Add("Range", $"0-{len - 1}");
            return response;
        }
    }
}
