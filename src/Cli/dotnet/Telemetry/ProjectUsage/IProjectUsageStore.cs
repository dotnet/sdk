// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Telemetry.ProjectUsage;

/// <summary>
/// Persists project usage data, aggregated per UTC day and per project.
/// </summary>
internal interface IProjectUsageStore
{
    /// <summary>Merges the records into the stored data for <paramref name="day"/>.</summary>
    void Merge(DateOnly day, IEnumerable<ProjectUsageRecord> records);

    /// <summary>Returns the days that have stored data, in ascending order.</summary>
    IReadOnlyList<DateOnly> GetDays();

    IReadOnlyList<ProjectUsageRecord> ReadDay(DateOnly day);

    void DeleteProject(DateOnly day, string projectId);

    void DeleteDay(DateOnly day);
}
