// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.NET.Sdk.Publish.Tasks.Properties;
using Moq;

namespace Microsoft.NET.Sdk.Publish.Tasks.OneDeploy.Tests;

/// <summary>
/// Unit Tests for <see cref="OneDeployStatusService"/>
/// </summary>
public class OneDeployStatusServiceTests
{
    private const string Username = "someUser";
    private const string NotShareableValue = "PLACEHOLDER";
    private const string UserAgent = "websdk/8.0"; // as OneDeploy.UserAgentName
    private const string PublishUrl = "https://mysite.scm.azurewebsites.net";
    private const string DeploymentId = "056f49ce-fcd7-497c-929b-d74bc6f8905e";

    private static readonly Uri DeploymentUri = new UriBuilder($@"{PublishUrl}/api/deployments/{DeploymentId}").Uri;

    [Theory]
    [InlineData(HttpStatusCode.OK, DeploymentStatus.Success)]
    [InlineData(HttpStatusCode.Accepted, DeploymentStatus.Success)]
    [InlineData(HttpStatusCode.OK, DeploymentStatus.PartialSuccess)]
    [InlineData(HttpStatusCode.Accepted, DeploymentStatus.PartialSuccess)]
    [InlineData(HttpStatusCode.OK, DeploymentStatus.Failed)]
    [InlineData(HttpStatusCode.Accepted, DeploymentStatus.Failed)]
    [InlineData(HttpStatusCode.OK, DeploymentStatus.Conflict)]
    [InlineData(HttpStatusCode.Accepted, DeploymentStatus.Conflict)]
    [InlineData(HttpStatusCode.OK, DeploymentStatus.Cancelled)]
    [InlineData(HttpStatusCode.Accepted, DeploymentStatus.Cancelled)]
    [InlineData(HttpStatusCode.OK, DeploymentStatus.Unknown)]
    [InlineData(HttpStatusCode.Accepted, DeploymentStatus.Unknown)]
    public async Task PollDeployment_Completes(HttpStatusCode statusCode, DeploymentStatus expectedDeploymentStatus)
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var taskLoggerMock = new Mock<ITaskLogger>();
        taskLoggerMock.Setup(l => l.LogMessage(Resources.DeploymentStatus_Polling));
        taskLoggerMock.Setup(l => l.LogMessage(string.Format(Resources.DeploymentStatus, expectedDeploymentStatus)));

        var deploymentResponse = new DeploymentResponse();
        if (expectedDeploymentStatus != DeploymentStatus.Unknown)
        {
            deploymentResponse.Id = DeploymentId;
            deploymentResponse.Status = expectedDeploymentStatus;
        }

        var httpClientMock = GetHttpClientMock(statusCode, deploymentResponse);

        var oneDeployStatusService = new OneDeployStatusService(taskLoggerMock.Object);

        // Act
        var result = await oneDeployStatusService.PollDeploymentAsync(httpClientMock.Object, DeploymentUri.AbsoluteUri, Username, NotShareableValue, UserAgent, cancellationToken);

        // Assert: poll deployment status runs to completion, resulting in the given expectedDeploymentStatus
        Assert.Equal(expectedDeploymentStatus, deploymentResponse.Status);

        taskLoggerMock.VerifyAll();
        httpClientMock.VerifyAll();
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task PollDeployment_HttpResponse_Fail(HttpStatusCode statusCode)
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var taskLoggerMock = new Mock<ITaskLogger>();
        taskLoggerMock.Setup(l => l.LogMessage(Resources.DeploymentStatus_Polling));
        taskLoggerMock.Setup(l => l.LogMessage(string.Format(Resources.DeploymentStatus, DeploymentStatus.Unknown)));

        var httpClientMock = GetHttpClientMock(statusCode, null);

        var oneDeployStatusService = new OneDeployStatusService(taskLoggerMock.Object);

        // Act
        var result = await oneDeployStatusService.PollDeploymentAsync(httpClientMock.Object, DeploymentUri.AbsoluteUri, Username, NotShareableValue, UserAgent, cancellationToken);

        // Assert: poll deployment status runs to completion, resulting in 'Unknown' status because failed HTTP Response Status code
        Assert.Equal(DeploymentStatus.Unknown, result.Status);

        taskLoggerMock.VerifyAll();
        httpClientMock.VerifyAll();
    }

    [Fact]
    public async Task PollDeployment_Completes_No_Logger()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var deploymentResponse = new DeploymentResponse()
        {
            Id = DeploymentId,
            Status = DeploymentStatus.Success
        };

        var httpClientMock = GetHttpClientMock(HttpStatusCode.OK, deploymentResponse);

        var oneDeployStatusService = new OneDeployStatusService();

        // Act
        var result = await oneDeployStatusService.PollDeploymentAsync(httpClientMock.Object, DeploymentUri.AbsoluteUri, Username, NotShareableValue, UserAgent, cancellationToken);

        // Assert: poll deployment status runs to completion with NULL ITaskLogger
        Assert.Equal(DeploymentStatus.Success, deploymentResponse.Status);

        httpClientMock.VerifyAll();
    }

    [Fact]
    public async Task PollDeployment_Halted()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var taskLoggerMock = new Mock<ITaskLogger>();
        taskLoggerMock.Setup(l => l.LogMessage(Resources.DeploymentStatus_Polling));

        var httpClientMock = new Mock<IHttpClient>();

        var oneDeployStatusService = new OneDeployStatusService(taskLoggerMock.Object);

        // Act
        cancellationTokenSource.Cancel();
        var result = await oneDeployStatusService.PollDeploymentAsync(httpClientMock.Object, DeploymentUri.AbsoluteUri, Username, NotShareableValue, UserAgent, cancellationToken);

        // Assert: deployment status won't poll for deployment as 'CancellationToken' is already cancelled
        Assert.Equal(DeploymentStatus.Unknown, result.Status);

        taskLoggerMock.VerifyAll();
        httpClientMock.VerifyAll();
    }

    [Theory]
    [InlineData("not-valid-url")]
    [InlineData("")]
    [InlineData(null)]
    public async Task PollDeployment_InvalidURL(string invalidUrl)
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var taskLoggerMock = new Mock<ITaskLogger>();
        taskLoggerMock.Setup(l => l.LogMessage(Resources.DeploymentStatus_Polling));
        taskLoggerMock.Setup(l => l.LogError(string.Format(Resources.DeploymentStatus_InvalidPollingUrl, invalidUrl)));

        var httpClientMock = new Mock<IHttpClient>();

        var oneDeployStatusService = new OneDeployStatusService(taskLoggerMock.Object);

        // Act
        var result = await oneDeployStatusService.PollDeploymentAsync(httpClientMock.Object, invalidUrl, Username, NotShareableValue, UserAgent, cancellationToken);

        // Assert: deployment status won't poll for deployment because given polling URL is invalid
        Assert.Equal(DeploymentStatus.Unknown, result.Status);

        taskLoggerMock.VerifyAll();
        httpClientMock.VerifyAll();
    }

    [Fact]
    public async Task PollDeployment_Missing_HttpClient()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var taskLoggerMock = new Mock<ITaskLogger>();
        taskLoggerMock.Setup(l => l.LogMessage(Resources.DeploymentStatus_Polling));

        var oneDeployStatusService = new OneDeployStatusService(taskLoggerMock.Object);

        // Act
        var result = await oneDeployStatusService.PollDeploymentAsync(null, DeploymentUri.AbsoluteUri, Username, NotShareableValue, UserAgent, cancellationToken);

        // Assert: deployment status won't poll for deployment because IHttpClient is NULL
        Assert.Equal(DeploymentStatus.Unknown, result.Status);

        taskLoggerMock.VerifyAll();
    }

    [Fact]
    public void Constructor_OK()
    {
        var oneDeployStatusService = new OneDeployStatusService();

        // no-arg ctor instantiate instance
        Assert.NotNull(oneDeployStatusService);
    }

    private Mock<IHttpClient> GetHttpClientMock(
        HttpStatusCode statusCode,
        DeploymentResponse deploymentResponse)
    {
        var httpClientMock = new Mock<IHttpClient>();

        // Request
        HttpRequestMessage requestMessage = new();
        httpClientMock
            .Setup(hc => hc.DefaultRequestHeaders)
            .Returns(requestMessage.Headers);

        // Response
        HttpContent responseContent = null;
        string deploymentResponseJson = null;
        if (deploymentResponse is not null)
        {
            deploymentResponseJson = JsonSerializer.Serialize(deploymentResponse);
            responseContent = new StringContent(deploymentResponseJson, Encoding.UTF8, "application/json");
        }

        HttpResponseMessage responseMessage = new(statusCode);
        if (responseContent is not null)
        {
            responseMessage.Content = responseContent;
        }

        // GetAsync()
        httpClientMock
            .Setup(hc => hc.GetAsync(DeploymentUri, It.IsAny<CancellationToken>()))
            .ReturnsAsync(responseMessage);

        return httpClientMock;
    }
}
