// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Wraps an <see cref="Activity"/> and automatically emits a telemetry event
/// containing all accumulated tags when disposed. Callers use <see cref="Tag"/>
/// to set data on both the span and the eventual event. On dispose, the activity
/// reference and stored tags are forwarded to the injected callback, which is
/// responsible for stopping the activity and emitting the event.
/// </summary>
internal sealed class TrackedOperation : IDisposable
{
    private readonly string _eventName;
    private readonly Action<string, Activity?, IDictionary<string, string?>>? _onTrackEvent;
    private readonly Dictionary<string, string?> _storedTags = [];
    private bool _disposed;

    internal TrackedOperation(Activity? activity, string eventName, Action<string, Activity?, IDictionary<string, string?>>? onTrackEvent)
    {
        Activity = activity;
        _eventName = eventName;
        _onTrackEvent = onTrackEvent;
    }

    public Activity? Activity { get; }

    public void Tag(string key, object? value)
    {
        Activity?.SetTag(key, value);
        _storedTags[key] = value?.ToString();
    }

    public void SetStatus(ActivityStatusCode code, string? description = null)
    {
        Activity?.SetStatus(code, description);
        Tag("command.status", code == ActivityStatusCode.Ok ? "ok" : "error");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_onTrackEvent is not null)
        {
            // Callback is responsible for stopping the activity and emitting the event.
            _onTrackEvent.Invoke(_eventName, Activity, _storedTags);
        }
        else
        {
            // No host callback registered — still stop the activity so the span completes.
            Activity?.Stop();
        }
    }
}
