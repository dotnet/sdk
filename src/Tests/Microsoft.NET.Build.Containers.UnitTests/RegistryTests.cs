// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Moq;
using System.Net;
using Xunit;

namespace Microsoft.NET.Build.Containers.UnitTests
{
    public class RegistryTests
    {
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
            Registry registry = new Registry(ContainerHelpers.TryExpandRegistryToUri(registryName));
            Assert.Equal(isECR, registry.IsAmazonECRRegistry);
        }

        [InlineData("us-south1-docker.pkg.dev", true)]
        [InlineData("us.gcr.io", false)]
        [Theory]
        public void CheckIfGoogleArtifactRegistry(string registryName, bool isECR)
        {
            Registry registry = new Registry(ContainerHelpers.TryExpandRegistryToUri(registryName));
            Assert.Equal(isECR, registry.IsGoogleArtifactRegistry);
        }

        [Fact]
        public async Task RegistriesThatProvideNoUploadSizeAttemptFullUpload()
        {
            var repoName = "testRepo";
            var layerDigest = "sha256:fafafafafafafafafafafafafafafafa";
            var mockLayer = new Mock<Layer>(MockBehavior.Strict);
            mockLayer
                .Setup(l => l.OpenBackingFile()).Returns(new MemoryStream(new byte[1000]));
            mockLayer
                .Setup(l => l.Descriptor).Returns(new Descriptor("blah", layerDigest, 1234));

            var uploadPath = new Uri("/uploads/foo/12345", UriKind.Relative);
            var api = new Mock<IRegistryAPI>(MockBehavior.Strict);
            api.Setup(api => api.Blob.ExistsAsync(repoName, layerDigest, It.IsAny<CancellationToken>())).Returns(Task.FromResult(false));
            api.Setup(api => api.Blob.Upload.StartAsync(repoName, It.IsAny<CancellationToken>())).Returns(Task.FromResult(new StartUploadInformation(null, uploadPath)));
            api.Setup(api => api.Blob.Upload.UploadAtomicallyAsync(uploadPath, It.IsAny<Stream>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(new FinalizeUploadInformation(uploadPath)));
            api.Setup(api => api.Blob.Upload.CompleteAsync(uploadPath, layerDigest, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            api.Verify(api => api.Blob.Upload.UploadChunkAsync(uploadPath, It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Never());

            Registry registry = new Registry(ContainerHelpers.TryExpandRegistryToUri("public.ecr.aws"), api.Object);
            await registry.PushAsync(mockLayer.Object, repoName, _ => { }, CancellationToken.None);
        }
    }
}
