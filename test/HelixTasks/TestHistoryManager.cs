// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.SdkCustomHelix.Sdk;

/// <summary>
/// Queries Azure DevOps for historical test execution durations from the last
/// successful CI build. Used to inform time-based Helix work item scheduling.
/// </summary>
internal sealed class TestHistoryManager
{
    private readonly string _projectUri;
    private readonly string _accessToken;
    private readonly int _definitionId;
    private readonly string _targetBranch;
    private readonly string? _phaseName;
    private readonly TaskLoggingHelper? _log;

    /// <summary>
    /// Creates a new TestHistoryManager.
    /// </summary>
    /// <param name="projectUri">AzDO project URI (e.g. "https://dev.azure.com/dnceng/public").</param>
    /// <param name="accessToken">Pipeline access token for AzDO REST API.</param>
    /// <param name="definitionId">Pipeline definition ID to query builds for.</param>
    /// <param name="targetBranch">Branch name (without refs/heads/) to look for successful builds.</param>
    /// <param name="phaseName">Optional phase/stage name to filter test runs by.</param>
    /// <param name="log">Optional MSBuild logger for diagnostics.</param>
    public TestHistoryManager(
        string projectUri,
        string accessToken,
        int definitionId,
        string targetBranch,
        string? phaseName = null,
        TaskLoggingHelper? log = null)
    {
        _projectUri = projectUri;
        _accessToken = accessToken;
        _definitionId = definitionId;
        _targetBranch = targetBranch;
        _phaseName = phaseName;
        _log = log;
    }

    /// <summary>
    /// Fetches per-test-method durations from the last successful CI build.
    /// Returns null if history cannot be obtained (triggers count-based fallback).
    /// </summary>
    public async Task<Dictionary<string, TestExecutionInfo>?> GetTestHistoryAsync(CancellationToken ct = default)
    {
        try
        {
            using var client = new AzdoClient(_projectUri, _accessToken);

            // 1. Find the last successful build
            var build = await client.GetLastSuccessfulBuildAsync(_definitionId, _targetBranch, ct);
            if (build is null)
            {
                _log?.LogMessage("TestHistoryManager: No successful build found for branch '{0}' or 'main'.", _targetBranch);
                return null;
            }

            _log?.LogMessage("TestHistoryManager: Using build {0} (#{1}) from {2}.",
                build.Id, build.BuildNumber, build.SourceBranch);

            // 2. Get test runs for that build
            var testRuns = await client.GetTestRunsAsync(build.Id, ct);
            if (testRuns.Count == 0)
            {
                _log?.LogMessage("TestHistoryManager: No test runs found for build {0}.", build.Id);
                return null;
            }

            // Filter by phase name if specified
            if (!string.IsNullOrEmpty(_phaseName))
            {
                testRuns = testRuns
                    .Where(r => r.Name.Contains(_phaseName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (testRuns.Count == 0)
                {
                    _log?.LogMessage("TestHistoryManager: No test runs matching phase '{0}'.", _phaseName);
                    return null;
                }
            }

            // 3. Aggregate test results across all matching runs
            var history = new Dictionary<string, TestExecutionInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var run in testRuns)
            {
                var results = await client.GetTestResultsAsync(run.Id, ct);
                foreach (var result in results)
                {
                    if (string.IsNullOrEmpty(result.AutomatedTestName))
                        continue;

                    // Use the longest duration if a test appears in multiple runs
                    var duration = TimeSpan.FromMilliseconds(result.DurationInMs);
                    if (history.TryGetValue(result.AutomatedTestName, out var existing))
                    {
                        if (duration > existing.Duration)
                        {
                            history[result.AutomatedTestName] = new TestExecutionInfo(duration);
                        }
                    }
                    else
                    {
                        history[result.AutomatedTestName] = new TestExecutionInfo(duration);
                    }
                }
            }

            _log?.LogMessage("TestHistoryManager: Retrieved duration data for {0} test methods.", history.Count);
            return history.Count > 0 ? history : null;
        }
        catch (Exception ex)
        {
            _log?.LogWarning("TestHistoryManager: Failed to retrieve test history (will fall back to count-based scheduling): {0}", ex.Message);
            return null;
        }
    }
}

/// <summary>
/// Historical execution info for a single test method.
/// </summary>
internal readonly record struct TestExecutionInfo(TimeSpan Duration);
