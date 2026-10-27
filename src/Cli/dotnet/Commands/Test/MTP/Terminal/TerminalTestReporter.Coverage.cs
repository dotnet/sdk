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
        IReadOnlySet<string> inputExecutionIds,
        bool complete,
        CoverageSummaryMessage[] summaries)
    {
        if (summaries.Any(summary => !IsValidCoverageSummary(summary, merged: true)))
        {
            WriteWarningMessage(CliCommandStrings.InvalidCoverageSummary);
            return;
        }

        lock (_coverageLock)
        {
            _mergedCoverageSummaries[path] = new MergedCoverageSummary(
                path, new HashSet<string>(inputExecutionIds, StringComparer.Ordinal), complete, [.. summaries]);
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
        }

        bool canReportTotal = !_wasCancelled
            && exitCode is not null
                and not ExitCode.TestSessionAborted
                and not ExitCode.TestExecutionStoppedForMaxFailedTests
                and not ExitCode.TestExecutionStoppedAtDeadline
            && !HasHandshakeFailure
            && !_assemblies.IsEmpty;
        MergedCoverageSummary[] completeMerges =
        [
            .. merged.Where(summary => summary.Complete && _assemblies.Keys.All(summary.InputExecutionIds.Contains)),
        ];
        bool useMergedTotal = canReportTotal && completeMerges.Length == 1;
        bool useApplicationTotal = canReportTotal
            && merged.Length == 0
            && _assemblies.Count == 1
            && applications.Select(entry => (entry.Key.ExecutionId, entry.Key.InstanceId, entry.Key.SessionUid)).Distinct().Count() == 1;
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
                        : $"{application.Assembly} ({key.SessionUid})"
                    : $"{application.Assembly}: {key.ScopeName}";
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

    private sealed record MergedCoverageSummary(
        string Path,
        IReadOnlySet<string> InputExecutionIds,
        bool Complete,
        CoverageSummaryMessage[] Summaries);
}
