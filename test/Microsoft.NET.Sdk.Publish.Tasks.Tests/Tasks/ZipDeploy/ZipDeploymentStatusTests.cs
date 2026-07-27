// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Net;
using System.Net.Http;
using System.Text.Json;
using Moq;

namespace Microsoft.NET.Sdk.Publish.Tasks.ZipDeploy.Tests
{
    [TestClass]
    public class ZipDeploymentStatusTests
    {
        private const string UserAgentName = "websdk";
        private const string UserAgentVersion = "1.0";
        private const string userName = "deploymentUser";
        private const string password = "[PLACEHOLDER]";


        [TestMethod]
        [DataRow(HttpStatusCode.Forbidden, DeployStatus.Unknown)]
        [DataRow(HttpStatusCode.NotFound, DeployStatus.Unknown)]
        [DataRow(HttpStatusCode.RequestTimeout, DeployStatus.Unknown)]
        [DataRow(HttpStatusCode.InternalServerError, DeployStatus.Unknown)]
        public async Task PollDeploymentStatusTest_ForErrorResponses(HttpStatusCode responseStatusCode, DeployStatus expectedDeployStatus)
        {
            // Arrange
            string deployUrl = "https://sitename.scm.azurewebsites.net/DeploymentStatus?Id=knownId";
            Action<Mock<IHttpClient>, bool> verifyStep = (client, result) =>
            {
                client.Verify(c => c.GetAsync(
                It.Is<Uri>(uri => string.Equals(uri.AbsoluteUri, deployUrl, StringComparison.Ordinal)), It.IsAny<CancellationToken>()));
                Assert.AreEqual($"{UserAgentName}/{UserAgentVersion}", client.Object.DefaultRequestHeaders.GetValues("User-Agent").FirstOrDefault());
                Assert.IsTrue(result);
            };

            Mock<IHttpClient> client = new();
            HttpRequestMessage requestMessage = new();
            client.Setup(x => x.DefaultRequestHeaders).Returns(requestMessage.Headers);
            client.Setup(c => c.GetAsync(new Uri(deployUrl, UriKind.RelativeOrAbsolute), It.IsAny<CancellationToken>())).Returns(() =>
            {
                return Task.FromResult(new HttpResponseMessage(responseStatusCode));
            });
            ZipDeploymentStatus deploymentStatus = new(client.Object, $"{UserAgentName}/{UserAgentVersion}", null, false);

            // Act
            var actualdeployStatus = await deploymentStatus.PollDeploymentStatusAsync(deployUrl, userName, password);

            // Assert
            verifyStep(client, expectedDeployStatus == actualdeployStatus.Status);
        }

        [TestMethod]
        [DataRow(HttpStatusCode.OK, DeployStatus.Success)]
        [DataRow(HttpStatusCode.Accepted, DeployStatus.Success)]
        [DataRow(HttpStatusCode.OK, DeployStatus.Failed)]
        [DataRow(HttpStatusCode.Accepted, DeployStatus.Failed)]
        [DataRow(HttpStatusCode.OK, DeployStatus.Unknown)]
        [DataRow(HttpStatusCode.Accepted, DeployStatus.Unknown)]
        public async Task PollDeploymentStatusTest_ForValidResponses(HttpStatusCode responseStatusCode, DeployStatus expectedDeployStatus)
        {
            // Arrange
            string deployUrl = "https://sitename.scm.azurewebsites.net/DeploymentStatus?Id=knownId";
            Action<Mock<IHttpClient>, bool> verifyStep = (client, result) =>
            {
                client.Verify(c => c.GetAsync(
                It.Is<Uri>(uri => string.Equals(uri.AbsoluteUri, deployUrl, StringComparison.Ordinal)), It.IsAny<CancellationToken>()));
                Assert.AreEqual($"{UserAgentName}/{UserAgentVersion}", client.Object.DefaultRequestHeaders.GetValues("User-Agent").FirstOrDefault());
                Assert.IsTrue(result);
            };

            var deploymentResponse = new DeploymentResponse()
            {
                Id = "20a106ca-3797-4dbb",
                Status = expectedDeployStatus,
                LogUrl = "https://mywebapp.scm.azurewebsites.net/api/deployments/latest/log",
            };

            Mock<IHttpClient> client = new();
            HttpRequestMessage requestMessage = new();
            client.Setup(x => x.DefaultRequestHeaders).Returns(requestMessage.Headers);
            client.Setup(c => c.GetAsync(new Uri(deployUrl, UriKind.RelativeOrAbsolute), It.IsAny<CancellationToken>())).Returns(() =>
            {
                string statusJson = JsonSerializer.Serialize(deploymentResponse);

                HttpContent httpContent = new StringContent(statusJson, Encoding.UTF8, "application/json");
                HttpResponseMessage responseMessage = new(responseStatusCode)
                {
                    Content = httpContent
                };
                return Task.FromResult(responseMessage);
            });
            ZipDeploymentStatus deploymentStatus = new(client.Object, $"{UserAgentName}/{UserAgentVersion}", null, false);

            // Act
            var actualdeployStatus = await deploymentStatus.PollDeploymentStatusAsync(deployUrl, userName, password);

            // Assert
            verifyStep(client, expectedDeployStatus == actualdeployStatus.Status);
        }

        [TestMethod]
        [DataRow("https://mywebapp.scm.azurewebsites.net/api/deployments/latest/log", "id_1", "https://mywebapp.scm.azurewebsites.net/api/deployments/id_1/log")]
        [DataRow("https://mywebapp.scm.azurewebsites.net/api/deployments/latest/log", "", "https://mywebapp.scm.azurewebsites.net/api/deployments/latest/log")]
        [DataRow("https://mywebapp.scm.azurewebsites.net/api/deployments/latest/log", null, "https://mywebapp.scm.azurewebsites.net/api/deployments/latest/log")]
        [DataRow("https://mywebapp.scm.azurewebsites.net/api/deployments/latest", "id_2", "https://mywebapp.scm.azurewebsites.net/api/deployments/id_2")]
        [DataRow("https://mywebapp.scm.azurewebsites.net/api/deployments/latest/diagnostics/log", "11223344", "https://mywebapp.scm.azurewebsites.net/api/deployments/11223344/diagnostics/log")]
        [DataRow("https://latest.scm.azurewebsites.net/api/deployments/latest/log", "11223344", "https://latest.scm.azurewebsites.net/api/deployments/11223344/log")]
        [DataRow("https://latest.scm.azurewebsites.net/api/deployments/log", "11223344", "https://latest.scm.azurewebsites.net/api/deployments/log")]
        [DataRow("https://latest.scm.azurewebsites.net/api/latest/deployments/latest/log", "11223344", "https://latest.scm.azurewebsites.net/api/11223344/deployments/11223344/log")]
        [DataRow("", "id_2", "")]
        [DataRow(null, "id_2", null)]
        [DataRow("MyWebSiteNotAsUrl", "id_2", "MyWebSiteNotAsUrl")]
        [DataRow("MyWebSiteNotAsUrl", null, "MyWebSiteNotAsUrl")]
        [DataRow(null, null, null)]
        public void TestLogUrlId(string url, string id, string expectedUrl)
        {
            DeploymentResponse deploymentResponse = null;

            if (!string.IsNullOrEmpty(url)
                || !string.IsNullOrEmpty(id)
                || !string.IsNullOrEmpty(expectedUrl))
            {
                deploymentResponse = new()
                {
                    Id = id,
                    Status = DeployStatus.Success,
                    LogUrl = url,
                };
            }

            Assert.AreEqual(expectedUrl, deploymentResponse?.GetLogUrlWithId());
        }
    }
}
