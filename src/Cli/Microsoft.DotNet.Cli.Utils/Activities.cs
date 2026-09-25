// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

using System.Diagnostics.Metrics;

namespace Microsoft.DotNet.Cli.Utils;

/// <summary>
/// Contains helpers for working with <see cref="Activity">Activities</see> in the .NET CLI.
/// </summary>
public static class Activities
{
    /// <summary>
    /// The main entrypoint for creating <see cref="Activity">Activities</see> in the .NET CLI.
    /// All activities created in the CLI should use this <see cref="ActivitySource"/>, to allow
    /// consumers to easily filter and trace CLI activities.
    /// </summary>
    public static ActivitySource Source { get; } = new("dotnet-cli", Product.Version);

    private static readonly Meter s_meter = new(Source.Name, Product.Version);
    private static readonly Histogram<double> s_activityDuration = s_meter.CreateHistogram<double>(
        "dotnet.cli.activity.duration",
        unit: "s",
        description: "Duration of a dotnet CLI activity.");

    private static readonly ActivityListener s_metricsListener = new()
    {
        ShouldListenTo = source => source.Name == Source.Name,
        // Collect timings on demand without marking otherwise unsampled traces as recorded.
        Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
            s_activityDuration.Enabled ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
        SampleUsingParentId = (ref ActivityCreationOptions<string> _) =>
            s_activityDuration.Enabled ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
        ActivityStopped = activity =>
        {
            if (s_activityDuration.Enabled)
            {
                s_activityDuration.Record(
                    activity.Duration.TotalSeconds,
                    new KeyValuePair<string, object?>("activity.name", activity.OperationName));
            }
        },
    };

    static Activities()
    {
        ActivitySource.AddActivityListener(s_metricsListener);
    }

    /// <summary>
    /// The environment variable used to transfer the chain of parent activity IDs.
    /// This should be used when constructing new sub-processes in order to
    /// track spans across calls.
    /// </summary>
    public const string TRACEPARENT = nameof(TRACEPARENT);
    /// <summary>
    /// The environment variable used to transfer the trace state of the parent activities.
    /// This should be used when constructing new sub-processes in order to
    /// track spans across calls.
    /// </summary>
    public const string TRACESTATE = nameof(TRACESTATE);
}

#endif
