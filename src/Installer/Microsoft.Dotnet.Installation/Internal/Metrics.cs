// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Provides the ActivitySource for installation telemetry.
/// This source is listened to by dotnetup's DotnetupTelemetry when running via CLI,
/// and can be subscribed to by other consumers via ActivityListener.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Important:</strong> If you use this library and collect telemetry, you are responsible
/// for complying with the .NET SDK telemetry policy. This includes:
/// </para>
/// <list type="bullet">
///   <item><description>Displaying a first-run notice to users explaining what data is collected</description></item>
///   <item><description>Honoring the <c>DOTNET_CLI_TELEMETRY_OPTOUT</c> environment variable</description></item>
///   <item><description>Providing documentation about your telemetry practices</description></item>
/// </list>
/// <para>
/// See the .NET SDK telemetry documentation for guidance:
/// https://learn.microsoft.com/dotnet/core/tools/telemetry
/// </para>
/// <para>
/// Library consumers can hook into installation telemetry by subscribing to this ActivitySource.
/// The following activities are emitted:
/// </para>
/// <list type="bullet">
///   <item><term>download</term><description>SDK/runtime archive download. Tags: download.version, download.url, download.bytes, download.from_cache</description></item>
///   <item><term>extract</term><description>Archive extraction. Tags: download.version</description></item>
/// </list>
/// <para>
/// When activities originate from dotnetup CLI, they include the tag <c>caller=dotnetup</c>.
/// Library consumers can use this to distinguish CLI-originated vs direct library calls.
/// </para>
/// <para>
/// For a working example of how to integrate with this ActivitySource, see the
/// TelemetryIntegrationDemo project in test/dotnetup.Tests/TestAssets/.
/// </para>
/// </remarks>
internal static class Metrics
{
    /// <summary>
    /// The name of the ActivitySource. Must match what consumers listen for.
    /// </summary>
    public const string SourceName = "Microsoft.Dotnet.Installation";

    public static ActivitySource ActivitySource { get; } = new(
        SourceName,
        typeof(Metrics).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0");

    /// <summary>
    /// Optional callback registered by the host (e.g., dotnetup) to receive
    /// completion notifications. When set, every <see cref="TrackedOperation"/>
    /// disposes through this callback so the host can emit a structured log
    /// record (the AppInsights <c>traces</c> row). When null, library
    /// activities still complete via <c>Activity.Stop</c> but no log record
    /// is produced.
    /// </summary>
    public static Action<string, Activity?>? OnTrackEvent { get; set; }

    /// <summary>
    /// Starts a tracked operation that emits a completion log record on
    /// dispose (via <see cref="OnTrackEvent"/>). Tags set on the returned
    /// <see cref="TrackedOperation"/> land on the underlying span
    /// (<see cref="Activity.TagObjects"/>) and are folded into the LogRecord
    /// state.
    /// </summary>
    /// <param name="eventName">
    /// The stable event discriminator stamped onto the completion
    /// LogRecord Message as <c>dotnetup/{eventName}</c> (the GDPR-classified
    /// row in the AppInsights <c>traces</c> table). Conventionally
    /// hierarchical with <c>'/'</c> separators (e.g., <c>"download/complete"</c>,
    /// <c>"extract/complete"</c>, <c>"gc/delete-orphaned-subcomponents"</c>).
    /// The portion before the final <c>'/'</c> is also used by default as
    /// the <see cref="Activity.OperationName"/>, so <c>'/'</c> must not
    /// appear inside the leaf name segment.
    /// </param>
    /// <param name="activityName">
    /// Optional override for the <see cref="Activity.OperationName"/> used
    /// for in-process correlation and ancestor walks. When null (the default),
    /// derived from <paramref name="eventName"/> by taking everything before
    /// the final <c>'/'</c> (or the full string if there is no <c>'/'</c>).
    /// Pass an explicit value only when the desired Activity name diverges
    /// from the event hierarchy and must remain stable for existing dashboard
    /// queries that filter on <c>error.first_failure_stage</c> or
    /// <c>operation.parent_name</c>.
    /// </param>
    public static TrackedOperation Track(string eventName, string? activityName = null)
    {
        if (activityName is null)
        {
            int lastSlash = eventName.AsSpan().LastIndexOf('/');
            activityName = lastSlash > 0 ? eventName[..lastSlash] : eventName;
        }

        var activity = ActivitySource.StartActivity(activityName, ActivityKind.Internal);
        return new TrackedOperation(activity, eventName, OnTrackEvent);
    }

    /// <summary>
    /// Sets a tag on the current activity. Equivalent to
    /// <c>Activity.Current?.SetTag(key, value)</c> — the host's completion
    /// log builder reads <see cref="Activity.TagObjects"/> when the enclosing
    /// <see cref="TrackedOperation"/> disposes, so any tag set here lands on
    /// the resulting <c>traces</c> row.
    /// </summary>
    public static void Tag(string key, object? value)
    {
        Activity.Current?.SetTag(key, value);
    }
}
