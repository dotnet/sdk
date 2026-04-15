// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Net;
using System.Net.Http;
using Microsoft.NET.Sdk.Publish.Tasks.MsDeploy;
using Microsoft.NET.Sdk.Publish.Tasks.Properties;
using Moq;

namespace Microsoft.NET.Sdk.Publish.Tasks.OneDeploy.Tests;

/// <summary>
/// Unit Tests for <see cref="OneDeploy"/>
/// </summary>
public partial class OneDeployTests
{
    private const string Username = "someUser";
    private const string NotShareableValue = "PLACEHOLDER";
    private const string PublishUrl = "https://mysite.scm.azurewebsites.net";
    private const string UserAgentName = "websdk"; // as OneDeploy.UserAgentName
    private const string DeploymentUrl = $@"{PublishUrl}/api/deployments/056f49ce-fcd7-497c-929b-d74bc6f8905e";
    private const string DeploymentLogUrl = $@"{DeploymentUrl}/log";
    private const string DefaultApiPath = "api/publish"; // as OneDeploy.OneDeployApiPath
    private const string DefaultQueryParam = "RemoteBuild=false"; // as OneDeploy.OneDeployQueryParam

    private static readonly Uri OneDeployUri = new UriBuilder(PublishUrl)
    {
        Path = DefaultApiPath,
        Query = DefaultQueryParam
    }.Uri;

    private static string _fileToPublish;
    public static string FileToPublish
    {
        get
        {
            if (_fileToPublish == null)
            {
                string codebase = typeof(OneDeployTests).Assembly.Location;
                string assemblyPath = new Uri(codebase, UriKind.Absolute).LocalPath;
                string baseDirectory = Path.GetDirectoryName(assemblyPath);
                _fileToPublish = Path.Combine(baseDirectory, Path.Combine("Resources", "TestPublishContents.zip"));
            }

            return _fileToPublish;
        }
    }

    [Theory]
    [InlineData(DeploymentStatus.Success, HttpStatusCode.OK, true)]
    [InlineData(DeploymentStatus.Success, HttpStatusCode.Accepted, true)]
    [InlineData(DeploymentStatus.PartialSuccess, HttpStatusCode.OK, true)]
    [InlineData(DeploymentStatus.PartialSuccess, HttpStatusCode.Accepted, true)]
    [InlineData(DeploymentStatus.Failed, HttpStatusCode.OK, false)]
    [InlineData(DeploymentStatus.Failed, HttpStatusCode.Accepted, false)]
    [InlineData(DeploymentStatus.Conflict, HttpStatusCode.OK, false)]
    [InlineData(DeploymentStatus.Conflict, HttpStatusCode.Accepted, false)]
    [InlineData(DeploymentStatus.Cancelled, HttpStatusCode.OK, false)]
    [InlineData(DeploymentStatus.Cancelled, HttpStatusCode.Accepted, false)]
    [InlineData(DeploymentStatus.Unknown, HttpStatusCode.OK, false)]
    [InlineData(DeploymentStatus.Unknown, HttpStatusCode.Accepted, false)]

    public async Task OneDeploy_Execute_Completes(DeploymentStatus deployStatus, HttpStatusCode statusCode, bool expectedResult)
    {
        // Arrange
        var httpClientMock = GetHttpClientMock(statusCode);

        var deploymentStatusServiceMock = GetDeploymentStatusServiceMock(httpClientMock.Object, deployStatus);

        // set messages to log according to result
        var taskLoggerMock = new Mock<ITaskLogger>();
        taskLoggerMock.Setup(l => l.LogMessage(Build.Framework.MessageImportance.High, string.Format(Resources.ONEDEPLOY_PublishingOneDeploy, FileToPublish, OneDeployUri.AbsoluteUri.ToString())));
        taskLoggerMock.Setup(l => l.LogMessage(string.Format(Resources.ONEDEPLOY_Uploaded, FileToPublish)));

        if (expectedResult)
        {
            taskLoggerMock.Setup(l => l.LogMessage(Build.Framework.MessageImportance.High, Resources.ONEDEPLOY_Success));
        }
        else
        {
            var failedDeployMsg = string.Format(Resources.ONEDEPLOY_FailedWithLogs, FileToPublish, OneDeployUri.AbsoluteUri, deployStatus, DeploymentLogUrl);
            taskLoggerMock.Setup(l => l.LogError(failedDeployMsg));
        }

        var oneDeployTask = new OneDeploy(taskLoggerMock.Object);

        // Act
        var result = await oneDeployTask.OneDeployAsync(
            FileToPublish, Username, NotShareableValue, PublishUrl, $"{UserAgentName}/8.0", webjobName: null, webjobType: null, httpClientMock.Object, deploymentStatusServiceMock.Object, CancellationToken.None);

        // Assert: deployment operation runs to completion with expected result
        Assert.Equal(expectedResult, result);

        httpClientMock.VerifyAll();
        deploymentStatusServiceMock.VerifyAll();
        taskLoggerMock.VerifyAll();
    }

    [Theory]
    [InlineData("not-a-location-url")]
    [InlineData(null)]
    public async Task OneDeploy_Execute_Deploy_Location_Missing(string invalidLocationHeaderValue)
    {
        // Arrange
        var httpClientMock = new Mock<IHttpClient>();

        // Request
        HttpRequestMessage requestMessage = new();
        httpClientMock.Setup(hc => hc.DefaultRequestHeaders).Returns(requestMessage.Headers);

        // Response
        HttpResponseMessage responseMessage = new(HttpStatusCode.OK);
        if (invalidLocationHeaderValue is not null)
        {
            responseMessage.Headers.Add("Location", invalidLocationHeaderValue);
        }

        // PostAsync()
        httpClientMock.Setup(hc => hc.PostAsync(OneDeployUri, It.IsAny<StreamContent>())).ReturnsAsync(responseMessage);

        var deploymentStatusServiceMock = new Mock<IDeploymentStatusService<DeploymentResponse>>();

        // set messages to log according to result
        var taskLoggerMock = new Mock<ITaskLogger>();
        taskLoggerMock.Setup(l => l.LogMessage(Build.Framework.MessageImportance.High, string.Format(Resources.ONEDEPLOY_PublishingOneDeploy, FileToPublish, OneDeployUri.AbsoluteUri.ToString())));
        taskLoggerMock.Setup(l => l.LogMessage(string.Format(Resources.ONEDEPLOY_Uploaded, FileToPublish)));

        var oneDeployTask = new OneDeploy(taskLoggerMock.Object);

        // Act
        var result = await oneDeployTask.OneDeployAsync(
            FileToPublish, Username, NotShareableValue, PublishUrl, $"{UserAgentName}/8.0", webjobName: null, webjobType: null, httpClientMock.Object, deploymentStatusServiceMock.Object, CancellationToken.None);

        // Assert: deployment operation runs to completion, without polling the deployment because 'Location' header was not found in response
        Assert.True(result);

        httpClientMock.VerifyAll();
        deploymentStatusServiceMock.VerifyAll();
        taskLoggerMock.VerifyAll();
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task OneDeploy_Execute_HttpResponse_Fail(HttpStatusCode statusCode)
    {
        // Arrange
        var httpClientMock = GetHttpClientMock(statusCode, deployLocationHeader: null);

        var deploymentStatusServiceMock = new Mock<IDeploymentStatusService<DeploymentResponse>>();

        // set messages to log
        var taskLoggerMock = new Mock<ITaskLogger>();
        taskLoggerMock.Setup(l => l.LogMessage(Build.Framework.MessageImportance.High, string.Format(Resources.ONEDEPLOY_PublishingOneDeploy, FileToPublish, OneDeployUri.AbsoluteUri.ToString())));
        taskLoggerMock.Setup(l => l.LogError(string.Format(Resources.ONEDEPLOY_FailedDeployRequest, OneDeployUri.AbsoluteUri.ToString(), statusCode)));

        var oneDeployTask = new OneDeploy(taskLoggerMock.Object);

        // Act
        var result = await oneDeployTask.OneDeployAsync(
            FileToPublish, Username, NotShareableValue, PublishUrl, $"{UserAgentName}/8.0", webjobName: null, webjobType: null, httpClientMock.Object, deploymentStatusServiceMock.Object, CancellationToken.None);

        // Assert: deployment operation fails because HTTP POST request to upload the package returns a failed HTTP Response
        Assert.False(result);

        httpClientMock.VerifyAll();
        deploymentStatusServiceMock.VerifyAll();
        taskLoggerMock.VerifyAll();
    }

    [Theory]
    [InlineData("not-valid-url")]
    [InlineData("")]
    [InlineData(null)]
    public async Task OneDeploy_Execute_PublishUrl_Invalid(string invalidUrl)
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
            FileToPublish, Username, NotShareableValue, invalidUrl, $"{UserAgentName}/8.0", webjobName: null, webjobType: null, httpClientMock.Object, deploymentStatusServiceMock.Object, CancellationToken.None);

        // Assert: deployment operation fails because 'PublishUrl' is not valid
        Assert.False(result);

        httpClientMock.VerifyAll();
        deploymentStatusServiceMock.VerifyAll();
        taskLoggerMock.VerifyAll();
    }

    [Theory]
    [InlineData("z:\\Missing\\Directory\\File")]
    [InlineData("")]
    [InlineData(null)]
    public async Task OneDeploy_Execute_FileToPublish_Missing(string invalidFileToPublish)
    {
        // Arrange
        var httpClientMock = new Mock<IHttpClient>();

        var deploymentStatusServiceMock = new Mock<IDeploymentStatusService<DeploymentResponse>>();

        // set messages to log
        var taskLoggerMock = new Mock<ITaskLogger>();
        taskLoggerMock.Setup(l => l.LogError(Resources.ONEDEPLOY_FileToPublish_NotFound));

        var oneDeployTask = new OneDeploy(taskLoggerMock.Object);

        // Act
        var result = await oneDeployTask.OneDeployAsync(
            invalidFileToPublish, Username, NotShareableValue, PublishUrl, $"{UserAgentName}/8.0", webjobName: null, webjobType: null, httpClientMock.Object, deploymentStatusServiceMock.Object, CancellationToken.None);

        // Assert: deployment operation fails because 'FileToPublishPath' is not valid
        Assert.False(result);

        httpClientMock.VerifyAll();
        deploymentStatusServiceMock.VerifyAll();
        taskLoggerMock.VerifyAll();
    }

    [Theory]
    [InlineData(Username, "")]
    [InlineData(Username, null)]
    [InlineData("", "")]
    [InlineData(null, null)]
    public async Task OneDeploy_Execute_Credentials_Missing_Args(string invalidUsername, string invalidPassword)
    {
        // Arrange
        var httpClientMock = new Mock<IHttpClient>();

        var deploymentStatusServiceMock = new Mock<IDeploymentStatusService<DeploymentResponse>>();

        // set messages to log
        var taskLoggerMock = new Mock<ITaskLogger>();
        taskLoggerMock.Setup(l => l.LogError(Resources.ONEDEPLOY_FailedToRetrieveCredentials));

        var oneDeployTask = new OneDeploy(taskLoggerMock.Object);

        // Act
        var result = await oneDeployTask.OneDeployAsync(
            FileToPublish, invalidUsername, invalidPassword, PublishUrl, $"{UserAgentName}/8.0", webjobName: null, webjobType: null, httpClientMock.Object, deploymentStatusServiceMock.Object, CancellationToken.None);

        // Assert: deployment operation fails because 'Username' and/or 'Password' is
        // not valid nor the could be retrieved from Task HostObject
        Assert.False(result);

        httpClientMock.VerifyAll();
        deploymentStatusServiceMock.VerifyAll();
        taskLoggerMock.VerifyAll();
    }

    [Fact]
    public async Task OneDeploy_Execute_Credentials_From_TaskHostObject()
    {
        // Arrange
        var httpClientMock = GetHttpClientMock(HttpStatusCode.OK);

        var deploymentStatusServiceMock = GetDeploymentStatusServiceMock(httpClientMock.Object, DeploymentStatus.PartialSuccess);

        // set messages to log according to result
        var taskLoggerMock = new Mock<ITaskLogger>();
        taskLoggerMock.Setup(l => l.LogMessage(Build.Framework.MessageImportance.High, string.Format(Resources.ONEDEPLOY_PublishingOneDeploy, FileToPublish, OneDeployUri.AbsoluteUri.ToString())));
        taskLoggerMock.Setup(l => l.LogMessage(string.Format(Resources.ONEDEPLOY_Uploaded, FileToPublish)));
        taskLoggerMock.Setup(l => l.LogMessage(Build.Framework.MessageImportance.High, Resources.ONEDEPLOY_Success));

        var oneDeployTask = new OneDeploy(taskLoggerMock.Object);

        var msbuildHostObject = new VSMsDeployTaskHostObject();
        msbuildHostObject.AddCredentialTaskItemIfExists(Username, NotShareableValue);
        oneDeployTask.HostObject = msbuildHostObject;

        // Act
        var result = await oneDeployTask.OneDeployAsync(
            FileToPublish, Username, NotShareableValue, PublishUrl, $"{UserAgentName}/8.0", webjobName: null, webjobType: null, httpClientMock.Object, deploymentStatusServiceMock.Object, CancellationToken.None);

        // Assert: deployment operation runs to completion
        // obtaining the credentials from the Task HostObject
        Assert.True(result);

        httpClientMock.VerifyAll();
        deploymentStatusServiceMock.VerifyAll();
        taskLoggerMock.VerifyAll();
    }

    private Mock<IHttpClient> GetHttpClientMock(
        HttpStatusCode statusCode,
        string deployLocationHeader = DeploymentUrl)
    {
        var httpClientMock = new Mock<IHttpClient>();

        // Request
        HttpRequestMessage requestMessage = new();
        httpClientMock
            .Setup(hc => hc.DefaultRequestHeaders)
            .Returns(requestMessage.Headers);

        // Response
        HttpResponseMessage responseMessage = new(statusCode);
        if (!string.IsNullOrEmpty(deployLocationHeader))
        {
            responseMessage.Headers.Add("Location", deployLocationHeader);
        }

        // PostAsync()
        httpClientMock
            .Setup(hc => hc.PostAsync(OneDeployUri, It.IsAny<StreamContent>()))
            .ReturnsAsync(responseMessage);

        return httpClientMock;
    }

    [Fact]
    public void Constructor_OK()
    {
        var oneDeploy = new OneDeploy();

        // no-arg ctor (as invoked by MSBuild) instantiate instance
        Assert.NotNull(oneDeploy);
    }

    private Mock<IDeploymentStatusService<DeploymentResponse>> GetDeploymentStatusServiceMock(
        IHttpClient httpClient,
        DeploymentStatus status = DeploymentStatus.Success,
        string statusText = null)
    {
        var deploymentResponse = new DeploymentResponse()
        {
            Status = status,
            StatusText = statusText,
            LogUrl = DeploymentLogUrl
        };

        var statusServiceMock = new Mock<IDeploymentStatusService<DeploymentResponse>>();

        statusServiceMock
            .Setup(s => s.PollDeploymentAsync(httpClient, DeploymentUrl, Username, NotShareableValue, $"{UserAgentName}/8.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(deploymentResponse);

        return statusServiceMock;
    }
}
