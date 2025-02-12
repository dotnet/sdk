// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.NET.Sdk.Publish.Tasks.MsDeploy;
using Microsoft.NET.Sdk.Publish.Tasks.Properties;

namespace Microsoft.NET.Sdk.Publish.Tasks.OneDeploy;

/// <summary>
/// 'OneDeploy' publish task.
/// </summary>
public partial class OneDeploy : Task
{
    private const string DefaultRequestContentType = "application/zip";
    private const string UserAgentName = "websdk";
    private const string OneDeployApiPath = "api/publish";
    private const string OneDeployQueryParam = "RemoteBuild=false";

    private readonly ITaskLogger _taskLogger;

    public OneDeploy()
    {
        // logger is enabled by default
        _taskLogger = new TaskLogger(Log, true);
    }

    // Test constructor
    internal OneDeploy(ITaskLogger taskLogger)
    {
        _taskLogger = taskLogger;
    }

    [Required]
    public string FileToPublishPath { get; set; }

    public string PublishUrl { get; set; }

    [Required]
    public string Username { get; set; }

    public string Password { get; set; }

    [Required]
    public string UserAgentVersion { get; set; }

    /// <inheritdoc/>
    public override bool Execute()
    {
        var deployTask = OneDeployAsync(FileToPublishPath, Username, Password, PublishUrl, UserAgentVersion, WebJobName, WebJobType);
        deployTask.Wait();

        return deployTask.Result;
    }

    public async Task<bool> OneDeployAsync(
        string fileToPublishPath,
        string username,
        string password,
        string url,
        string userAgentVersion,
        string webjobName = null,
        string webjobType = null,
        CancellationToken cancellationToken = default)
    {
        using DefaultHttpClient httpClient = new();

        var userAgent = $"{UserAgentName}/{userAgentVersion}";
        var deployStatusService = new OneDeployStatusService(_taskLogger);

        return await OneDeployAsync(
            fileToPublishPath, username, password, url, userAgent, webjobName, webjobType, httpClient, deployStatusService, cancellationToken);
    }

    internal async Task<bool> OneDeployAsync(
        string fileToPublishPath,
        string username,
        string password,
        string url,
        string userAgent,
        string webjobName,
        string webjobType,
        IHttpClient httpClient,
        IDeploymentStatusService<DeploymentResponse> deploymentStatusService,
        CancellationToken cancellationToken = default)
    {
        // missing credentials
        if (string.IsNullOrEmpty(password) && !GetCredentialsFromTask(out username, out password))
        {
            _taskLogger.LogError(Resources.ONEDEPLOY_FailedToRetrieveCredentials);
            return false;
        }

        // missing file to publish
        if (!File.Exists(fileToPublishPath))
        {
            _taskLogger.LogError(Resources.ONEDEPLOY_FileToPublish_NotFound);
            return false;
        }

        // 'PublishUrl' must be valid
        if (!GetPublishUrl(url, webjobName, webjobType, out var oneDeployPublishUri))
        {
            _taskLogger.LogError(string.Format(Resources.ONEDEPLOY_InvalidPublishUrl, url));
            return false;
        }

        _taskLogger.LogMessage(
            MessageImportance.High,
            string.Format(Resources.ONEDEPLOY_PublishingOneDeploy, fileToPublishPath, oneDeployPublishUri.ToString()));

        var fileToPublishStream = File.OpenRead(fileToPublishPath);

        // push package to target instance
        var response = await DeployAsync(
            httpClient, oneDeployPublishUri, username, password, userAgent, fileToPublishPath, webjobName, webjobType, fileToPublishStream, cancellationToken);

        // if push failed, finish operation
        if (!response.IsResponseSuccessful())
        {
            var responseText = await response.GetTextResponseAsync(cancellationToken);

            var errorMessage = !string.IsNullOrEmpty(responseText)
                ? string.Format(Resources.ONEDEPLOY_FailedDeployRequest_With_ResponseText, oneDeployPublishUri, response?.StatusCode, responseText)
                : string.Format(Resources.ONEDEPLOY_FailedDeployRequest, oneDeployPublishUri, response?.StatusCode);

            _taskLogger.LogError(errorMessage);
            return false;
        }

        _taskLogger.LogMessage(string.Format(Resources.ONEDEPLOY_Uploaded, fileToPublishPath));

        // monitor deployment (if available)
        var deploymentUrl = response.GetHeader("Location").FirstOrDefault();
        if (!string.IsNullOrEmpty(deploymentUrl) && Uri.TryCreate(deploymentUrl, UriKind.Absolute, out var _))
        {
            var deploymentResponse = await deploymentStatusService.PollDeploymentAsync(httpClient, deploymentUrl, username, password, userAgent, cancellationToken);

            if (deploymentResponse.IsSuccessfulResponse())
            {
                _taskLogger.LogMessage(MessageImportance.High, Resources.ONEDEPLOY_Success);
                return true;
            }

            if (deploymentResponse.IsFailedResponse())
            {
                _taskLogger.LogError(string.Format(Resources.ONEDEPLOY_FailedWithLogs,
                    fileToPublishPath,
                    oneDeployPublishUri,
                    deploymentResponse.Status ?? DeploymentStatus.Unknown,
                    deploymentResponse.GetLogUrlWithId()));

                return false;
            }
        }

        // no follow-up deployment response (as in WebJobs); assume deployment worked
        _taskLogger.LogMessage(MessageImportance.High, Resources.ONEDEPLOY_Success);
        return true;
    }

    private bool GetCredentialsFromTask(out string user, out string password)
    {
        VSHostObject hostObj = new(HostObject as IEnumerable<ITaskItem>);

        return hostObj.ExtractCredentials(out user, out password);
    }

    private Task<IHttpResponse> DeployAsync(
        IHttpClient httpClient,
        Uri publishUri,
        string username,
        string password,
        string userAgent,
        string fileToPublish,
        string webjobName,
        string webjobType,
        FileStream fileToPublishStream,
        CancellationToken cancellationToken)
    {
        return IsWebJobProject(webjobName, webjobType)
           ? DeployWebJobAsync(httpClient, publishUri, username, password, userAgent, fileToPublish, fileToPublishStream, cancellationToken)
           : DefaultDeployAsync(httpClient, publishUri, username, password, userAgent, fileToPublishStream, cancellationToken);
    }

    private bool GetPublishUrl(string publishUrl, string webjobName, string webjobType, out Uri publishUri)
    {
        publishUri = null;

        if (string.IsNullOrEmpty(publishUrl) ||
            !Uri.TryCreate(publishUrl, UriKind.Absolute, out var _))
        {
            return false;
        }

        return IsWebJobProject(webjobName, webjobType)
            ? GetWebJobPublishUri(publishUrl, webjobName, webjobType, out publishUri)
            : GetDefaultPublishUri(publishUrl, out publishUri);
    }

    private async Task<IHttpResponse> DefaultDeployAsync(
        IHttpClient httpClient,
        Uri publishUri,
        string username,
        string password,
        string userAgent,
        FileStream fileToPublishStream,
        CancellationToken cancellationToken)
    {
        var response = await httpClient.PostRequestAsync(
            publishUri, username, password, DefaultRequestContentType, userAgent, Encoding.UTF8, fileToPublishStream);

        return response;
    }

    private bool GetDefaultPublishUri(string publishUrl, out Uri publishUri)
    {
        publishUri = new UriBuilder(publishUrl)
        {
            Path = OneDeployApiPath,
            Query = OneDeployQueryParam,
            Port = -1
        }.Uri;

        return Uri.TryCreate(publishUri.ToString(), UriKind.Absolute, out var _);
    }
}
