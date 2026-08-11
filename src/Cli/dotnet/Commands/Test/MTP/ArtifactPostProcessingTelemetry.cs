// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Test;

/// <summary>
/// Reports one event per <c>dotnet test</c> run that planned at least one artifact post-processing
/// job. Runs that plan nothing are silent, because the interesting population is the runs where
/// post-processing actually had work to do.
/// </summary>
internal static class ArtifactPostProcessingTelemetry
{
    private const string EventName = "test/artifact-post-processing";

    /// <summary>
    /// Replaces any kind or extension outside the well-known sets below. Artifact kinds and file
    /// extensions are chosen by whoever wrote the producing extension, so an in-house post-processor
    /// can carry a product or team name. Bucketing everything unrecognized still answers what the
    /// event exists to answer — how often post-processing runs, and on which shipped formats.
    /// </summary>
    private const string OtherValue = "other";

    private static readonly HashSet<string> WellKnownKinds = new(StringComparer.Ordinal)
    {
        "microsoft.testing.trx",
        "microsoft.codecoverage",
        "coverlet.cobertura",
        "junit.report",
        "nunit.report",
    };

    private static readonly HashSet<string> WellKnownExtensions = new(StringComparer.Ordinal)
    {
        ".trx",
        ".coverage",
        ".cobertura",
        ".xml",
        ".json",
    };

    public static void TrackPostProcessing(
        ArtifactPostProcessingPlan plan,
        int executedJobs,
        int failedJobs,
        TimeSpan duration)
        => TelemetryEventEntry.TrackEvent(
            EventName,
            CreateProperties(plan, executedJobs, failedJobs, duration));

    internal static Dictionary<string, string?> CreateProperties(
        ArtifactPostProcessingPlan plan,
        int executedJobs,
        int failedJobs,
        TimeSpan duration)
    {
        ArtifactPostProcessingGroup[] groups = [.. plan.Jobs.SelectMany(job => job.Groups)];

        return new Dictionary<string, string?>
        {
            ["jobs_planned"] = plan.Jobs.Count.ToString(CultureInfo.InvariantCulture),
            ["jobs_executed"] = executedJobs.ToString(CultureInfo.InvariantCulture),
            ["jobs_failed"] = failedJobs.ToString(CultureInfo.InvariantCulture),
            ["artifact_count"] = groups.Sum(group => group.Artifacts.Count).ToString(CultureInfo.InvariantCulture),
            ["kinds"] = Join(groups.Where(group => group.IsKind).Select(group => group.Key), WellKnownKinds),
            ["extensions"] = Join(groups.Where(group => !group.IsKind).Select(group => group.Key), WellKnownExtensions),
            ["duration_ms"] = ((long)duration.TotalMilliseconds).ToString(CultureInfo.InvariantCulture),
        };
    }

    private static string Join(IEnumerable<string> values, HashSet<string> wellKnownValues)
        => string.Join(
            ';',
            values
                .Select(value => wellKnownValues.Contains(value) ? value : OtherValue)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));
}
