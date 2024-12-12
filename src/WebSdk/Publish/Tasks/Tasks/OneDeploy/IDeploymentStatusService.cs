// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Publish.Tasks.OneDeploy;

/// <summary>
/// A service that monitors a deployment operation.
/// </summary>
internal interface IDeploymentStatusService<T>
{
    /// <summary>
    /// Polls a deployment operation status.
    /// </summary>
    /// <typeparam name="T">deployment response type</typeparam>
    /// <param name="httpClient">HTTP client</param>
    /// <param name="url">URL endpoint to poll the deployment</param>
    /// <param name="user">user name</param>
    /// <param name="password">user password</param>
    /// <param name="userAgent">'UserAgent' header value</param>
    /// <param name="cancellation">cancellation token</param>
    /// <returns>the resulting deployment response</returns>
    Task<T?> PollDeploymentAsync(IHttpClient httpClient, string? url, string? user, string? password, string userAgent, CancellationToken cancellation);
}
