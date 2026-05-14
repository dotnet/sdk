// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Net;
using System.Net.Http;
using System.Security.Policy;
using Microsoft.NET.Sdk.Publish.Tasks.Properties;
using Moq;

namespace Microsoft.NET.Sdk.Publish.Tasks.OneDeploy.Tests;

public partial class OneDeployTests
{
    private const string WebJobName = "TestWebJob";
    private const string ContinuousWebJob = "Continuous"; // as OneDeploy.WebJob.ContinuousWebJobType
    private const string TriggeredWebJob = "Triggered"; // as OneDeploy.WebJob.TriggeredWebJobType
    private const string ContinuousApiPath = "api/continuouswebjobs"; // as OneDeploy.WebJob.ContinuousWebJobApiPath
    private const string TriggeredApiPath = "api/triggeredwebjobs"; // as OneDeploy.WebJob.TriggeredWebJobsApiPath
    private const string PutErrorResponseMessage = "Missing run.sh file";

    private static readonly Uri OneDeploy_WebJob_Continuous_Uri = new UriBuilder(PublishUrl)
    {
        Path = $"{ContinuousApiPath}/{WebJobName}",
    }.Uri;

    private static readonly Uri OneDeploy_WebJob_Triggered_Uri = new UriBuilder(PublishUrl)
    {
        Path = $"{TriggeredApiPath}/{WebJobName}",
    }.Uri;

    [Theory]
    [InlineData(DeploymentStatus.Success, HttpStatusCode.OK, ContinuousWebJob, true)]
    [InlineData(DeploymentStatus.Success, HttpStatusCode.OK, TriggeredWebJob, true)]
    [InlineData(DeploymentStatus.Failed, HttpStatusCode.BadRequest, ContinuousWebJob, false)]
    [InlineData(DeploymentStatus.Failed, HttpStatusCode.NotFound, TriggeredWebJob, false)]
    public async Task OneDeploy_WebJob_Execute_Completes(
        DeploymentStatus deployStatus, HttpStatusCode statusCode, string webJobType, bool expectedResult)
    {
        // Arrange
        var publishUri = GetWebJobUri(webJobType);
        var httpClientMock = GetWebJobHttpClientMock(publishUri, statusCode, deployStatus);

        var deploymentStatusServiceMock = new Mock<IDeploymentStatusService<DeploymentResponse>>();

        // set messages to log according to result
        var taskLoggerMock = new Mock<ITaskLogger>();
        taskLoggerMock.Setup(l => l.LogMessage(Build.Framework.MessageImportance.High, string.Format(Resources.ONEDEPLOY_PublishingOneDeploy, FileToPublish, publishUri.AbsoluteUri.ToString())));

        if (expectedResult)
        {
            taskLoggerMock.Setup(l => l.LogMessage(string.Format(Resources.ONEDEPLOY_Uploaded, FileToPublish)));
            taskLoggerMock.Setup(l => l.LogMessage(Build.Framework.MessageImportance.High, Resources.ONEDEPLOY_Success));
        }
        else
        {
            var failedDeployMsg = string.Format(Resources.ONEDEPLOY_FailedDeployRequest_With_ResponseText, publishUri.AbsoluteUri, statusCode, PutErrorResponseMessage);
            taskLoggerMock.Setup(l => l.LogError(failedDeployMsg));
        }

        var oneDeployTask = new OneDeploy(taskLoggerMock.Object);

        // Act
        var result = await oneDeployTask.OneDeployAsync(
            FileToPublish, Username, NotShareableValue, PublishUrl, $"{UserAgentName}/8.0", WebJobName, webJobType, httpClientMock.Object, deploymentStatusServiceMock.Object, CancellationToken.None);

        // Assert: WebJob deployment operation runs to completion with expected result
        Assert.Equal(expectedResult, result);

        httpClientMock.VerifyAll();
        deploymentStatusServiceMock.VerifyAll();
        taskLoggerMock.VerifyAll();
    }

    [Theory]
    [InlineData("not-valid-url", ContinuousWebJob)]
    [InlineData("not-valid-url", TriggeredWebJob)]
    [InlineData("", ContinuousWebJob)]
    [InlineData("", TriggeredWebJob)]
    [InlineData(null, ContinuousWebJob)]
    [InlineData(null, TriggeredWebJob)]
    public async Task OneDeploy_WebJob_PublishUrl_Invalid(string invalidUrl, string webjobType)
    {
        // Arrange
        var httpClientMock = new Mock<IHttpClient>();

        var deploymentStatusServiceMock = new Mock<IDeploymentStatusService<DeploymentResponse>>();

        // set messages to log
        var taskLoggerMock = new Mock<ITaskLogger>();
        taskLoggerMock.Setup(l => l.LogError(string.Format(Resources.ONEDEPLOY_InvalidPublishUrl, invalidUrl)));

        var oneDeployTask = new OneDeploy(taskLoggerMock.Object);

        // Act
        var result = await oneDeployTask.OneDeployAsync(
            FileToPublish, Username, NotShareableValue, invalidUrl, $"{UserAgentName}/8.0", WebJobName, webjobType, httpClientMock.Object, deploymentStatusServiceMock.Object, CancellationToken.None);

        // Assert: deployment operation fails because 'PublishUrl' is not valid
        Assert.False(result);

        httpClientMock.VerifyAll();
        deploymentStatusServiceMock.VerifyAll();
        taskLoggerMock.VerifyAll();
    }

    [Theory]
    [InlineData("", ContinuousWebJob)]
    [InlineData("", TriggeredWebJob)]
    [InlineData(null, ContinuousWebJob)]
    [InlineData(null, TriggeredWebJob)]
    [InlineData(WebJobName, "NotValidType")]
    [InlineData(WebJobName, "")]
    [InlineData(WebJobName, null)]
    [InlineData("", "")]
    [InlineData(null, null)]
    public async Task OneDeploy_WebJob_Missing_NameOrType(string webjobName, string webjobType)
    {
        // Arrange
        var httpClientMock = new Mock<IHttpClient>();

        // Request
        HttpRequestMessage requestMessage = new();
        httpClientMock.Setup(hc => hc.DefaultRequestHeaders).Returns(requestMessage.Headers);

        // PostAsync()
        httpClientMock
            .Setup(hc => hc.PostAsync(OneDeployUri, It.IsAny<StreamContent>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadGateway)
                {
                    Content = new StringContent(PutErrorResponseMessage)
                }
            );

        var deploymentStatusServiceMock = new Mock<IDeploymentStatusService<DeploymentResponse>>();

        // set messages to log
        var taskLoggerMock = new Mock<ITaskLogger>();
        taskLoggerMock.Setup(l => l.LogMessage(Build.Framework.MessageImportance.High, string.Format(Resources.ONEDEPLOY_PublishingOneDeploy, FileToPublish, OneDeployUri.AbsoluteUri.ToString())));
        taskLoggerMock.Setup(l => l.LogError(string.Format(Resources.ONEDEPLOY_FailedDeployRequest_With_ResponseText, OneDeployUri.AbsoluteUri, HttpStatusCode.BadGateway, PutErrorResponseMessage)));

        var oneDeployTask = new OneDeploy(taskLoggerMock.Object);

        // Act
        var result = await oneDeployTask.OneDeployAsync(
            FileToPublish, Username, NotShareableValue, PublishUrl, $"{UserAgentName}/8.0", webjobName, webjobType, httpClientMock.Object, deploymentStatusServiceMock.Object, CancellationToken.None);

        // Assert: deployment operation fails because since 'WebJobName' and/or 'WebJobType' is invalid, so we calculate the
        // default OneDeploy URI ('<site_scm_ulr>/api/publish'), which target instance does not recognized as valid
        Assert.False(result);

        httpClientMock.VerifyAll();
        deploymentStatusServiceMock.VerifyAll();
        taskLoggerMock.VerifyAll();
    }

    private Mock<IHttpClient> GetWebJobHttpClientMock(
        Uri publishUri,
        HttpStatusCode statusCode,
        DeploymentStatus deploymentStatus)
    {
        var httpClientMock = new Mock<IHttpClient>();

        // Request
        HttpRequestMessage requestMessage = new();
        httpClientMock
            .Setup(hc => hc.DefaultRequestHeaders)
            .Returns(requestMessage.Headers);

        // Response
        HttpResponseMessage responseMessage = new(statusCode);
        if (deploymentStatus.IsFailedStatus())
        {
            responseMessage.Content = new StringContent(PutErrorResponseMessage);
        }

        // PutAsync()
        httpClientMock
            .Setup(hc => hc.PutAsync(publishUri, It.IsAny<StreamContent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(responseMessage);

        return httpClientMock;
    }

    private Uri GetWebJobUri(string webJobType)
    {
        return webJobType switch
        {
            TriggeredWebJob => OneDeploy_WebJob_Triggered_Uri,
            ContinuousWebJob
                or _ => OneDeploy_WebJob_Continuous_Uri
        };
    }
}
