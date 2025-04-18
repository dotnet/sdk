// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Publish.Tasks.OneDeploy;

/// <summary>
/// 'OneDeploy' publish task for WebJobs.
/// </summary>
public partial class OneDeploy
{
    private const string ContinuousWebJobType = "Continuous";
    private const string TriggeredWebJobType = "Triggered";
    private const string ContinuousWebJobApiPath = "continuouswebjobs";
    private const string TriggeredWebJobsApiPath = "triggeredwebjobs";

    public string? WebJobName { get; set; }

    public string? WebJobType { get; set; }

    /// <summary>
    /// Whether the name is a non-empty value and type is either 'Continuous' or 'Triggered'.
    /// </summary>
    private bool IsWebJobProject(string? webjobName, string? webjobType) =>
        !string.IsNullOrEmpty(webjobName) &&
        (string.Equals(ContinuousWebJobType, webjobType, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(TriggeredWebJobType, webjobType, StringComparison.OrdinalIgnoreCase));

    private async Task<IHttpResponse?> DeployWebJobAsync(
        IHttpClient httpClient,
        Uri publishUri,
        string? username,
        string? password,
        string? userAgent,
        string? fileToPublish,
        FileStream fileToPublishStream,
        CancellationToken cancellationToken)
    {
        var response = await httpClient.PutRequestAsync(
            publishUri, username, password, DefaultRequestContentType, userAgent, Path.GetFileName(fileToPublish), Encoding.UTF8, fileToPublishStream, cancellationToken);

        return response;
    }

    private bool GetWebJobPublishUri(string publishUrl, string? webjobName, string? webjobType, out Uri publishUri)
    {
        // action path differs by WebJob type
        var path = string.Equals(ContinuousWebJobType, webjobType, StringComparison.OrdinalIgnoreCase)
            ? ContinuousWebJobApiPath
            : TriggeredWebJobsApiPath;

        // Either:
        //   {publishUrl}/api/continuouswebjobs/{WebJobName}
        //   {publishUrl}/api/triggeredwebjobs/{WebJobName}
        publishUri = new UriBuilder(publishUrl)
        {
            Path = $"api/{path}/{webjobName}",
            Port = -1
        }.Uri;

        return Uri.TryCreate(publishUri.ToString(), UriKind.Absolute, out _);
    }
}
