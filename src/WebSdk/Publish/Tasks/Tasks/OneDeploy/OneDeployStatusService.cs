// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;
using Microsoft.NET.Sdk.Publish.Tasks.Properties;

namespace Microsoft.NET.Sdk.Publish.Tasks.OneDeploy;

internal class OneDeployStatusService(ITaskLogger taskLogger = null) : IDeploymentStatusService<DeploymentResponse>
{
    private static readonly TimeSpan s_maximumWaitForResult = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan s_refreshDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan s_retryDelay = TimeSpan.FromSeconds(1);
    private static readonly int s_retryCount = 3;

    private readonly ITaskLogger _taskLogger = taskLogger;

    /// <inheritdoc/>
    public async Task<DeploymentResponse> PollDeploymentAsync(
        IHttpClient httpClient,
        string url,
        string user,
        string password,
        string userAgent,
        CancellationToken cancellationToken)
    {
        _taskLogger?.LogMessage(Resources.DeploymentStatus_Polling);

        if (httpClient is null || cancellationToken.IsCancellationRequested)
        {
            return DeploymentResponse.s_unknownResponse;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var _))
        {
            _taskLogger?.LogError(string.Format(Resources.DeploymentStatus_InvalidPollingUrl, url));
            return DeploymentResponse.s_unknownResponse;
        }

        var maxWaitForResultTokenSource = new CancellationTokenSource(s_maximumWaitForResult);
        var retryTokenSource = CancellationTokenSource.CreateLinkedTokenSource(maxWaitForResultTokenSource.Token, cancellationToken);
        var retryToken = retryTokenSource.Token;

        DeploymentResponse deploymentResponse = null;
        DeploymentStatus deployStatus = DeploymentStatus.Pending;

        try
        {
            retryTokenSource.CancelAfter(s_maximumWaitForResult);

            while (!retryToken.IsCancellationRequested && !deployStatus.IsTerminatingStatus())
            {
                deploymentResponse = await httpClient.RetryGetRequestAsync<DeploymentResponse>(
                        url, user, password, userAgent, s_retryCount, s_retryDelay, retryToken);

                // set to 'Unknown' if no response is returned
                deploymentResponse ??= DeploymentResponse.s_unknownResponse;

                deployStatus = deploymentResponse is not null && deploymentResponse.Status is not null
                    ? deploymentResponse.Status.Value
                    : DeploymentStatus.Unknown;

                _taskLogger?.LogMessage(deploymentResponse.ToString());

                await System.Threading.Tasks.Task.Delay(s_refreshDelay, retryToken);
            }
        }
        catch (Exception ex)
        {
            if (ex is TaskCanceledException)
            {
                // 'maxWaitTimeForResult' has passed; no 'terminating' status was obtained
            }
            else if (ex is HttpRequestException)
            {
                // HTTP GET request threw; return last known response
                return deploymentResponse;
            }
        }

        return deploymentResponse ?? DeploymentResponse.s_unknownResponse;
    }
}
