// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.InternalMicrosoft;
using OpenTelemetry;

namespace Microsoft.DotNet.Cli.Telemetry;

/// <summary>
/// Starts detection once and supplies the resolved classification to CLI telemetry.
/// <see cref="TelemetryClient"/> owns one instance for the process lifetime.
/// </summary>
internal sealed class InternalMicrosoftTelemetry
{
    private static readonly TimeSpan s_completionTimeout = TimeSpan.FromSeconds(20);

    private readonly object _lock = new();
    private Task? _completionTask;
    private KeyValuePair<string, object?>[] _resolvedTags = [];

    /// <summary>
    /// Starts detection once. Later calls keep the first detector task.
    /// </summary>
    public void Start(
        IInternalMicrosoftDetector detector,
        CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _completionTask ??= RunAsync(detector, cancellationToken);
        }
    }

    /// <summary>
    /// Waits for a bounded time so shutdown can enrich the final activities.
    /// </summary>
    public void WaitForCompletion()
    {
        Task? completionTask;
        lock (_lock)
        {
            completionTask = _completionTask;
        }

        if (completionTask is null)
        {
            return;
        }

        try
        {
            completionTask.Wait(s_completionTimeout);
        }
        catch
        {
            // Telemetry must never affect command completion.
        }
    }

    /// <summary>
    /// Gets the cached, privacy-filtered tags from the completed detector run.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, object?>> GetResolvedTags() =>
        Volatile.Read(ref _resolvedTags);

    private static KeyValuePair<string, object?>[] CreateResolvedTags(InternalMicrosoftDetectionResult result)
    {
        var tags = new List<KeyValuePair<string, object?>>
        {
            new(InternalMicrosoftTelemetryConstants.IsInternal, result.IsInternalMicrosoft)
        };

        if (result.IsInternalMicrosoft && !string.IsNullOrEmpty(result.Source))
        {
            tags.Add(new(InternalMicrosoftTelemetryConstants.Source, result.Source));
        }

        if (!result.IsCIEnvironment && result.IsInternalMicrosoft && !string.IsNullOrEmpty(result.Alias))
        {
            tags.Add(new(InternalMicrosoftTelemetryConstants.Alias, result.Alias));
        }

        if (!result.IsCIEnvironment && result.IsInternalMicrosoft && !string.IsNullOrEmpty(result.Domain))
        {
            tags.Add(new(InternalMicrosoftTelemetryConstants.Domain, result.Domain));
        }

        return [.. tags];
    }

    private async Task RunAsync(
        IInternalMicrosoftDetector detector,
        CancellationToken cancellationToken)
    {
        InternalMicrosoftDetectionResult result;
        try
        {
            result = await detector.IsInternalMicrosoftMachineAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            result = new InternalMicrosoftDetectionResult(
                IsInternalMicrosoft: false,
                Source: null,
                Alias: null,
                Domain: null,
                IsCIEnvironment: false,
                Outcome: InternalMicrosoftDetectorOutcome.Failed,
                CacheStatus: InternalMicrosoftDetectorCacheStatus.Miss,
                Duration: TimeSpan.Zero,
                ProbeDiagnostics: []);
        }

        Volatile.Write(ref _resolvedTags, CreateResolvedTags(result));
    }
}

/// <summary>
/// Adds the resolved classification to activities when OpenTelemetry ends them.
/// A tracer provider owns this processor for the provider lifetime.
/// </summary>
internal sealed class InternalMicrosoftTelemetryProcessor(InternalMicrosoftTelemetry telemetry) : BaseProcessor<Activity>
{
    public override void OnEnd(Activity activity)
    {
        foreach (var tag in telemetry.GetResolvedTags())
        {
            activity.SetTag(tag.Key, tag.Value);
        }
    }
}

/// <summary>
/// Defines activity names and tag keys shared by the telemetry adapter and its tests.
/// </summary>
internal static class InternalMicrosoftTelemetryConstants
{
    public const string IsInternal = "dotnet.cli.is_microsoft_internal";
    public const string Source = "dotnet.cli.microsoft_internal_source";
    public const string Alias = "dotnet.cli.microsoft_internal_alias";
    public const string Domain = "dotnet.cli.microsoft_internal_domain";
}
