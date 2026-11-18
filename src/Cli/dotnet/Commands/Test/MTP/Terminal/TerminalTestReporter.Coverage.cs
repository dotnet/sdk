// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

internal sealed partial class TerminalTestReporter
{
    private readonly Lock _coverageLock = new();
    private readonly Dictionary<CoverageSummaryKey, ApplicationCoverageSummary> _coverageSummaries = [];
    private readonly Dictionary<string, MergedCoverageSummary> _mergedCoverageSummaries = new(FileUtilities.PathComparer);
    private readonly Dictionary<CoverageInputOrigin, (int Started, int Ended)> _coverageTestSessions = [];
    private readonly Dictionary<string, HashSet<CoverageInputOrigin>> _coverageInputOrigins = new(FileUtilities.PathComparer);
    private bool _coverageWasTruncated;

    internal void TestSessionStarted(string executionId, string sessionUid, string? instanceId = null)
    {
        lock (_coverageLock)
        {
            var key = new CoverageInputOrigin(executionId, instanceId, sessionUid);
            (int started, int ended) = _coverageTestSessions.GetValueOrDefault(key);
            _coverageTestSessions[key] = (started + 1, ended);
        }
    }

    internal void TestSessionEnded(string executionId, string sessionUid, string? instanceId = null)
    {
        lock (_coverageLock)
        {
            var key = new CoverageInputOrigin(executionId, instanceId, sessionUid);
            (int started, int ended) = _coverageTestSessions.GetValueOrDefault(key);
            _coverageTestSessions[key] = (started, ended + 1);
        }
    }

    internal void CoverageArtifactReplaced(string path)
    {
        lock (_coverageLock)
        {
            _mergedCoverageSummaries.Remove(path);
        }
    }

    private void RecordCoverageInputOrigin(string path, string? executionId, string? instanceId, string? sessionUid)
    {
        lock (_coverageLock)
        {
            if (!_coverageInputOrigins.TryGetValue(path, out HashSet<CoverageInputOrigin>? origins))
            {
                origins = [];
                _coverageInputOrigins.Add(path, origins);
            }

            origins.Add(new CoverageInputOrigin(executionId, instanceId, sessionUid));
        }
    }

    private void RecordCoverageApplicationExit(int exitCode)
    {
        // Aggregate and ignore policies can erase an individual application's truncation code.
        if (exitCode is ExitCode.TestSessionAborted or ExitCode.TestExecutionStoppedForMaxFailedTests or ExitCode.TestExecutionStoppedAtDeadline)
        {
            MarkCoverageIncomplete();
        }
    }

    internal void MarkCoverageIncomplete()
    {
        lock (_coverageLock)
        {
            _coverageWasTruncated = true;
        }
    }

    internal void CoverageSummaryAdded(string assembly, string executionId, string instanceId, CoverageSummaryMessage[] summaries)
    {
        foreach (CoverageSummaryMessage summary in summaries)
        {
            if (!IsValidCoverageSummary(summary, merged: false))
            {
                WriteWarningMessage(CliCommandStrings.InvalidCoverageSummary);
                continue;
            }

            var key = new CoverageSummaryKey(
                executionId, instanceId, summary.SessionUid!, summary.ProducerId!,
                summary.ScopeLevel!.Value, summary.ScopeName, summary.Metric!.Value, summary.CustomMetricName);
            lock (_coverageLock)
            {
                _coverageSummaries[key] = new ApplicationCoverageSummary(assembly, summary);
            }
        }
    }

    internal void MergedCoverageSummaryAdded(
        string path,
        IReadOnlySet<string> inputPaths,
        bool complete,
        CoverageSummaryMessage[] summaries)
    {
        CoverageArtifactReplaced(path);
        Dictionary<(string ProducerId, int Metric, string? CustomMetricName), CoverageSummaryMessage> metrics = [];
        foreach (CoverageSummaryMessage summary in summaries)
        {
            if (!IsValidCoverageSummary(summary, merged: true))
            {
                WriteWarningMessage(CliCommandStrings.InvalidCoverageSummary);
                return;
            }

            metrics[(summary.ProducerId!, summary.Metric!.Value, summary.CustomMetricName)] = summary;
        }

        lock (_coverageLock)
        {
            _mergedCoverageSummaries[path] = new MergedCoverageSummary(
                path, new HashSet<string>(inputPaths, FileUtilities.PathComparer), complete, [.. metrics.Values]);
        }
    }

    private static bool IsValidCoverageSummary(CoverageSummaryMessage summary, bool merged)
        => !string.IsNullOrWhiteSpace(summary.ProducerId)
            && summary.Metric is >= 0
            && summary.CoveredCount is >= 0
            && summary.CoverableCount is >= 0
            && summary.CoveredCount <= summary.CoverableCount
            && (summary.Metric == (int)CoverageMetric.Custom
                ? !string.IsNullOrWhiteSpace(summary.CustomMetricName)
                : summary.CustomMetricName is null)
            && (merged ? summary.SessionUid is null : !string.IsNullOrWhiteSpace(summary.SessionUid))
            && (summary.ScopeLevel == 0
                ? summary.ScopeName is null
                : !merged && summary.ScopeLevel == 1 && !string.IsNullOrWhiteSpace(summary.ScopeName));

    private void AppendCoverageSummary(ITerminal terminal, int? exitCode)
    {
        KeyValuePair<CoverageSummaryKey, ApplicationCoverageSummary>[] applications;
        MergedCoverageSummary[] merged;
        MergedCoverageSummary[] completeMerges;
        bool singleMeasurementSource;
        bool wasTruncated;
        bool completeLifecycle;
        Dictionary<string, IReadOnlySet<string>> testInstances = _assemblies.ToDictionary(
            entry => entry.Key, entry => entry.Value.GetInstanceIds(), StringComparer.Ordinal);
        lock (_coverageLock)
        {
            applications = [.. _coverageSummaries
                .OrderBy(entry => entry.Value.Assembly, FileUtilities.PathComparer)
                .ThenBy(entry => entry.Key.ExecutionId, StringComparer.Ordinal)
                .ThenBy(entry => entry.Key.InstanceId, StringComparer.Ordinal)
                .ThenBy(entry => entry.Key.SessionUid, StringComparer.Ordinal)
                .ThenBy(entry => entry.Key.ScopeLevel)
                .ThenBy(entry => entry.Key.ScopeName, StringComparer.Ordinal)
                .ThenBy(entry => entry.Key.Metric)
                .ThenBy(entry => entry.Key.CustomMetricName, StringComparer.Ordinal)
                .ThenBy(entry => entry.Key.ProducerId, StringComparer.Ordinal)];
            merged = [.. _mergedCoverageSummaries.Values.OrderBy(summary => summary.Path, FileUtilities.PathComparer)];
            wasTruncated = _coverageWasTruncated;
            Dictionary<string, HashSet<string>> testSessions = _coverageTestSessions.Keys
                .GroupBy(session => session.ExecutionId!, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(session => session.SessionUid!).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
            HashSet<CoverageInputOrigin> expectedSources = [];
            completeLifecycle = _coverageTestSessions.Values.All(session => session.Started > 0 && session.Started == session.Ended);
            foreach (CoverageInputOrigin source in _coverageTestSessions.Keys)
            {
                if (TryResolveCoverageOrigin(source, testInstances, testSessions) is { } resolved)
                {
                    expectedSources.Add(resolved);
                }
                else
                {
                    completeLifecycle = false;
                }
            }

            Dictionary<string, HashSet<string>> observedInstances = expectedSources
                .GroupBy(source => source.ExecutionId!, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(source => source.InstanceId!).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
            completeLifecycle &= testInstances.All(entry =>
                observedInstances.TryGetValue(entry.Key, out HashSet<string>? observed) && entry.Value.SetEquals(observed));
            foreach (CoverageSummaryKey key in applications.Select(entry => entry.Key))
            {
                var measured = new CoverageInputOrigin(key.ExecutionId, key.InstanceId, key.SessionUid);
                if (TryResolveCoverageOrigin(measured, testInstances, testSessions) is not { } resolved
                    || !expectedSources.Contains(resolved))
                {
                    completeLifecycle = false;
                }
            }

            CoverageInputOrigin[] measurementSources =
            [
                .. applications.Select(entry => new CoverageInputOrigin(entry.Key.ExecutionId, entry.Key.InstanceId, entry.Key.SessionUid)).Distinct(),
            ];
            singleMeasurementSource = expectedSources.Count == 1
                && testInstances.Count == 1
                && testInstances.Values.Single().Count == 1
                && measurementSources.Length == 1
                && TryResolveCoverageOrigin(measurementSources[0], testInstances, testSessions) is { } singleSource
                && expectedSources.Contains(singleSource);
            completeMerges =
            [
                .. merged.Where(summary => summary.Complete && CoversAllCoverageSources(summary.InputPaths, expectedSources, testInstances, testSessions)),
            ];
        }

        bool canReportTotal = !_wasCancelled
            && !wasTruncated
            && exitCode is not null
                and not ExitCode.TestSessionAborted
                and not ExitCode.TestExecutionStoppedForMaxFailedTests
                and not ExitCode.TestExecutionStoppedAtDeadline
            && !HasHandshakeFailure
            && completeLifecycle
            && !_assemblies.IsEmpty;
        bool useMergedTotal = canReportTotal && completeMerges.Length == 1;
        bool useApplicationTotal = canReportTotal
            && merged.Length == 0
            && singleMeasurementSource;
        List<(string Scope, CoverageSummaryMessage Summary)> rows = [];

        if (useMergedTotal)
        {
            rows.AddRange(completeMerges[0].Summaries.Select(summary => (CliCommandStrings.CoverageScopeOverall, summary)));
        }
        else
        {
            foreach ((CoverageSummaryKey key, ApplicationCoverageSummary application) in applications)
            {
                string scope = key.ScopeLevel == 0
                    ? useApplicationTotal
                        ? CliCommandStrings.CoverageScopeOverall
                        : $"{application.Assembly} ({key.InstanceId}|{key.SessionUid})"
                    : $"{application.Assembly}: {key.ScopeName} ({key.InstanceId}|{key.SessionUid})";
                rows.Add((scope, application.Summary));
            }

            foreach (MergedCoverageSummary summary in merged)
            {
                string scope = string.Format(CultureInfo.CurrentCulture, CliCommandStrings.CoverageMergedArtifact, summary.Path);
                rows.AddRange(summary.Summaries.Select(metric => (scope, metric)));
            }
        }

        if (rows.Count == 0)
        {
            return;
        }

        terminal.AppendLine();
        terminal.AppendLine($"{SingleIndentation}{CliCommandStrings.CodeCoverageSummary}");
        var metricCounts = rows.GroupBy(row => (row.Scope, row.Summary.Metric, row.Summary.CustomMetricName))
            .ToDictionary(group => group.Key, group => group.Count());
        foreach ((string scope, CoverageSummaryMessage summary) in rows)
        {
            string metric = GetCoverageMetricLabel(summary);
            if (metricCounts[(scope, summary.Metric, summary.CustomMetricName)] > 1)
            {
                metric = string.Format(
                    CultureInfo.CurrentCulture, CliCommandStrings.CoverageMetricWithProducer,
                    metric, NormalizeCoverageLabel(summary.ProducerId!));
            }

            string value = summary.CoverableCount > 0
                ? $"{(100m * summary.CoveredCount!.Value / summary.CoverableCount!.Value).ToString("F1", CultureInfo.InvariantCulture)}%"
                : CliCommandStrings.CoverageNoData;
            terminal.AppendLine($"{DoubleIndentation}{NormalizeCoverageLabel(scope)} - {metric}: {value}");
        }
    }

    private bool CoversAllCoverageSources(
        IReadOnlySet<string> inputPaths,
        IReadOnlySet<CoverageInputOrigin> expectedSources,
        IReadOnlyDictionary<string, IReadOnlySet<string>> testInstances,
        IReadOnlyDictionary<string, HashSet<string>> testSessions)
    {
        HashSet<CoverageInputOrigin> coveredSources = [];

        foreach (string inputPath in inputPaths)
        {
            if (!_coverageInputOrigins.TryGetValue(inputPath, out HashSet<CoverageInputOrigin>? origins) || origins.Count == 0)
            {
                return false;
            }

            CoverageInputOrigin? resolvedOrigin = null;
            foreach (CoverageInputOrigin origin in origins)
            {
                CoverageInputOrigin? normalized = TryResolveCoverageOrigin(origin, testInstances, testSessions);
                if (normalized is null || !expectedSources.Contains(normalized))
                {
                    return false;
                }

                if (resolvedOrigin is not null && resolvedOrigin != normalized)
                {
                    // A reused path contains only its last contents, not both sessions' data.
                    return false;
                }

                resolvedOrigin = normalized;
            }

            if (resolvedOrigin is null)
            {
                return false;
            }

            coveredSources.Add(resolvedOrigin);
        }

        return inputPaths.Count > 0
            && expectedSources.Count > 0
            && expectedSources.SetEquals(coveredSources);
    }

    private static CoverageInputOrigin? TryResolveCoverageOrigin(
        CoverageInputOrigin origin,
        IReadOnlyDictionary<string, IReadOnlySet<string>> testInstances,
        IReadOnlyDictionary<string, HashSet<string>> testSessions)
    {
        if (origin.ExecutionId is not { } executionId
            || !testInstances.TryGetValue(executionId, out IReadOnlySet<string>? instances)
            || !testSessions.TryGetValue(executionId, out HashSet<string>? sessions))
        {
            return null;
        }

        // Controller/legacy messages can omit child identity. Only a genuinely single-source
        // execution permits inference; matching separate instance/session projections is not proof.
        string? instanceId = origin.InstanceId is { } reportedInstance && instances.Contains(reportedInstance)
            ? reportedInstance
            : instances.Count == 1 ? instances.Single() : null;
        string? sessionUid = !string.IsNullOrEmpty(origin.SessionUid)
            ? sessions.Contains(origin.SessionUid) ? origin.SessionUid : null
            : sessions.Count == 1 ? sessions.Single() : null;
        return instanceId is not null && sessionUid is not null
            ? new CoverageInputOrigin(executionId, instanceId, sessionUid)
            : null;
    }

    private static string GetCoverageMetricLabel(CoverageSummaryMessage summary)
        => (CoverageMetric)summary.Metric!.Value switch
        {
            CoverageMetric.Line => CliCommandStrings.CoverageMetricLine,
            CoverageMetric.Statement => CliCommandStrings.CoverageMetricStatement,
            CoverageMetric.Branch => CliCommandStrings.CoverageMetricBranch,
            CoverageMetric.Method => CliCommandStrings.CoverageMetricMethod,
            CoverageMetric.Function => CliCommandStrings.CoverageMetricFunction,
            CoverageMetric.Block => CliCommandStrings.CoverageMetricBlock,
            CoverageMetric.Instruction => CliCommandStrings.CoverageMetricInstruction,
            CoverageMetric.Region => CliCommandStrings.CoverageMetricRegion,
            CoverageMetric.Class => CliCommandStrings.CoverageMetricClass,
            CoverageMetric.Condition => CliCommandStrings.CoverageMetricCondition,
            CoverageMetric.Complexity => CliCommandStrings.CoverageMetricComplexity,
            CoverageMetric.Custom => NormalizeCoverageLabel(summary.CustomMetricName!),
            _ => summary.Metric.Value.ToString(CultureInfo.InvariantCulture),
        };

    private static string NormalizeCoverageLabel(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (char character in text)
        {
            if (char.IsControl(character)
                || char.GetUnicodeCategory(character) is UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
            {
                builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private void ClearCoverageSummaries()
    {
        lock (_coverageLock)
        {
            _coverageSummaries.Clear();
            _mergedCoverageSummaries.Clear();
            _coverageTestSessions.Clear();
            _coverageInputOrigins.Clear();
            _coverageWasTruncated = false;
        }
    }

    private readonly record struct CoverageSummaryKey(
        string ExecutionId,
        string InstanceId,
        string SessionUid,
        string ProducerId,
        int ScopeLevel,
        string? ScopeName,
        int Metric,
        string? CustomMetricName);

    private sealed record ApplicationCoverageSummary(string Assembly, CoverageSummaryMessage Summary);

    private sealed record CoverageInputOrigin(string? ExecutionId, string? InstanceId, string? SessionUid);

    private sealed record MergedCoverageSummary(
        string Path,
        IReadOnlySet<string> InputPaths,
        bool Complete,
        CoverageSummaryMessage[] Summaries);
}
