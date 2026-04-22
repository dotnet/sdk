// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Wraps an <see cref="Activity"/> and automatically emits a telemetry event
/// containing all accumulated tags when disposed. Callers use <see cref="SetTag"/>
/// to set data on both the span and the eventual event. On dispose, the activity
/// is stopped (capturing duration) and all tags are forwarded to the registered
/// <see cref="InstallationActivitySource.OnTrackEvent"/> callback.
/// </summary>
internal sealed class TrackedOperation : IDisposable
{
    private readonly Activity? _activity;
    private readonly string _eventName;
    private bool _disposed;

    internal TrackedOperation(Activity? activity, string eventName)
    {
        _activity = activity;
        _eventName = eventName;
    }

    public Activity? Activity => _activity;

    public void SetTag(string key, object? value)
    {
        _activity?.SetTag(key, value);
    }

    public void SetStatus(ActivityStatusCode code, string? description = null)
    {
        _activity?.SetStatus(code, description);
        _activity?.SetTag("operation.status", code == ActivityStatusCode.Ok ? "ok" : "error");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _activity?.Stop();

        if (_activity is null)
        {
            return;
        }

        var properties = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in _activity.Tags)
        {
            properties[tag.Key] = tag.Value;
        }

        properties["operation.duration_ms"] = _activity.Duration.TotalMilliseconds
            .ToString(CultureInfo.InvariantCulture);

        InstallationActivitySource.OnTrackEvent?.Invoke(_eventName, properties);
    }
}
