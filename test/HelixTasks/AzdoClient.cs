// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.DotNet.SdkCustomHelix.Sdk;

/// <summary>
/// Lightweight REST client for Azure DevOps test history queries.
/// Replaces the heavy Microsoft.TeamFoundationServer.Client package.
/// </summary>
internal sealed class AzdoClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _projectUri; // e.g. "https://dev.azure.com/dnceng/public"

    public AzdoClient(string projectUri, string accessToken)
    {
        _projectUri = projectUri.TrimEnd('/');
        _httpClient = new HttpClient();

        if (!string.IsNullOrEmpty(accessToken))
        {
            var encoded = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{accessToken}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encoded);
        }
    }

    /// <summary>
    /// Gets the last successful CI build for the given pipeline definition on the specified branch.
    /// Falls back to "refs/heads/main" if no build is found on the target branch.
    /// </summary>
    public async Task<AzdoBuild?> GetLastSuccessfulBuildAsync(int definitionId, string targetBranch, CancellationToken ct = default)
    {
        var build = await GetLastSuccessfulBuildOnBranchAsync(definitionId, $"refs/heads/{targetBranch}", ct);
        if (build is null && targetBranch != "main")
        {
            build = await GetLastSuccessfulBuildOnBranchAsync(definitionId, "refs/heads/main", ct);
        }
        return build;
    }

    private async Task<AzdoBuild?> GetLastSuccessfulBuildOnBranchAsync(int definitionId, string branchName, CancellationToken ct)
    {
        var url = $"{_projectUri}/_apis/build/builds?definitions={definitionId}&branchName={Uri.EscapeDataString(branchName)}&resultFilter=succeeded&statusFilter=completed&$top=1&api-version=7.0";
        var response = await GetJsonAsync<AzdoListResponse<AzdoBuild>>(url, ct);
        return response?.Value?.FirstOrDefault();
    }

    /// <summary>
    /// Gets all test runs associated with a build.
    /// </summary>
    public async Task<List<AzdoTestRun>> GetTestRunsAsync(int buildId, CancellationToken ct = default)
    {
        var url = $"{_projectUri}/_apis/test/runs?buildUri=vstfs:///Build/Build/{buildId}&api-version=7.0";
        var response = await GetJsonAsync<AzdoListResponse<AzdoTestRun>>(url, ct);
        return response?.Value ?? new List<AzdoTestRun>();
    }

    /// <summary>
    /// Gets test results for a specific test run, with pagination support.
    /// </summary>
    public async Task<List<AzdoTestResult>> GetTestResultsAsync(int runId, CancellationToken ct = default)
    {
        var allResults = new List<AzdoTestResult>();
        int top = 1000;
        int skip = 0;

        while (true)
        {
            var url = $"{_projectUri}/_apis/test/Runs/{runId}/results?$top={top}&$skip={skip}&api-version=7.0";
            var response = await GetJsonAsync<AzdoListResponse<AzdoTestResult>>(url, ct);
            if (response?.Value is null || response.Value.Count == 0)
                break;

            allResults.AddRange(response.Value);
            if (response.Value.Count < top)
                break;

            skip += top;
        }

        return allResults;
    }

    private async Task<T?> GetJsonAsync<T>(string url, CancellationToken ct) where T : class
    {
        using var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public void Dispose() => _httpClient.Dispose();
}

#region AzDO API Models

internal sealed class AzdoListResponse<T>
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("value")]
    public List<T>? Value { get; set; }
}

internal sealed class AzdoBuild
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("buildNumber")]
    public string BuildNumber { get; set; } = "";

    [JsonPropertyName("sourceBranch")]
    public string SourceBranch { get; set; } = "";
}

internal sealed class AzdoTestRun
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("totalTests")]
    public int TotalTests { get; set; }

    [JsonPropertyName("passedTests")]
    public int PassedTests { get; set; }
}

internal sealed class AzdoTestResult
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("testCaseTitle")]
    public string TestCaseTitle { get; set; } = "";

    [JsonPropertyName("automatedTestName")]
    public string AutomatedTestName { get; set; } = "";

    [JsonPropertyName("outcome")]
    public string Outcome { get; set; } = "";

    [JsonPropertyName("durationInMs")]
    public double DurationInMs { get; set; }

    [JsonPropertyName("subResults")]
    public List<AzdoSubResult>? SubResults { get; set; }
}

internal sealed class AzdoSubResult
{
    [JsonPropertyName("durationInMs")]
    public double DurationInMs { get; set; }

    [JsonPropertyName("outcome")]
    public string Outcome { get; set; } = "";
}

#endregion
