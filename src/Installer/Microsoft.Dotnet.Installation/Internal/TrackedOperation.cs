// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Wraps an <see cref="Activity"/> and, on dispose, hands the activity
/// to the host callback (set via <see cref="Metrics.OnTrackEvent"/>)
/// which emits the completion log record and stops the activity. If no
/// callback is registered the activity is simply stopped.
/// </summary>
internal sealed class TrackedOperation : IDisposable
{
    private readonly string _eventName;
    private readonly Action<string, Activity?>? _onTrackEvent;
    private bool _disposed;

    internal TrackedOperation(Activity? activity, string eventName, Action<string, Activity?>? onTrackEvent)
    {
        Activity = activity;
        _eventName = eventName;
        _onTrackEvent = onTrackEvent;
    }

    public Activity? Activity { get; }

    public void Tag(string key, object? value)
    {
        Activity?.SetTag(key, value);
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
            _onTrackEvent.Invoke(_eventName, Activity);
        }
        else
        {
            // No host callback registered — still stop the activity so the span completes.
            Activity?.Stop();
        }
    }
}
