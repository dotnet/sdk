// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;
using Microsoft.NET.Sdk.Publish.Tasks.Properties;

namespace Microsoft.NET.Sdk.Publish.Tasks.OneDeploy;

/// <summary>
/// Represents the JSON response of a deployment operation.
/// </summary>
internal class DeploymentResponse
{
    internal static readonly DeploymentResponse s_unknownResponse = new()
    {
        Status = DeploymentStatus.Unknown,
    };

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("end_time")]
    public string? EndTime { get; set; }

    [JsonPropertyName("log_url")]
    public string? LogUrl { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("status")]
    public DeploymentStatus? Status { get; set; } = DeploymentStatus.Unknown;

    [JsonPropertyName("site_name")]
    public string? SiteName { get; set; }

    [JsonPropertyName("status_text")]
    public string? StatusText { get; set; }

    [JsonPropertyName("start_time")]
    public string? StartTime { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.IsNullOrEmpty(StatusText)
            ? string.Format(Resources.DeploymentStatus, Status)
            : string.Format(Resources.DeploymentStatusWithText, Status, StatusText);
    }
}

/// <summary>
/// Extension methods for <see cref="DeploymentResponse"/>.
/// </summary>
internal static class DeploymentResponseExtensions
{
    internal static bool IsSuccessfulResponse(this DeploymentResponse response)
    {
        return response is not null
            && response.Status is not null
            && response.Status.Value.IsSuccessfulStatus();
    }

    internal static bool IsFailedResponse(this DeploymentResponse response)
    {
        return response is null
            || response.Status is null
            || response.Status.Value.IsFailedStatus();
    }

    public static string? GetLogUrlWithId(this DeploymentResponse deploymentResponse)
    {
        if (deploymentResponse is null
            || string.IsNullOrEmpty(deploymentResponse.LogUrl)
            || string.IsNullOrEmpty(deploymentResponse.Id))
        {
            return deploymentResponse?.LogUrl;
        }

        try
        {
            Uri logUrl = new(deploymentResponse.LogUrl);
            string pathAndQuery = logUrl.PathAndQuery;

            // try to replace '../latest/log' with '../{deploymentResponse.Id}/log'
            if (!string.IsNullOrEmpty(pathAndQuery))
            {
                string[] pathAndQueryParts = pathAndQuery.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                string?[] pathWithIdParts = new string[pathAndQueryParts.Length];

                for (int i = pathAndQueryParts.Length - 1; i >= 0; i--)
                {
                    if (string.Equals("latest", pathAndQueryParts[i], StringComparison.Ordinal))
                    {
                        pathWithIdParts[i] = deploymentResponse.Id;
                        continue;
                    }

                    pathWithIdParts[i] = pathAndQueryParts[i].Trim();
                }

                return new UriBuilder()
                {
                    Scheme = logUrl.Scheme,
                    Host = logUrl.Host,
                    Path = string.Join("/", pathWithIdParts)
                }.ToString();
            }
        }
        catch
        {
            // do nothing
        }

        return deploymentResponse.LogUrl;
    }
}
