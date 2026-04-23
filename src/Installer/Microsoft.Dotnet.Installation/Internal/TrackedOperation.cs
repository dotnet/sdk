// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Wraps an <see cref="Activity"/> and automatically emits a telemetry event
/// containing all accumulated tags when disposed. Callers use <see cref="Tag"/>
/// to set data on both the span and the eventual event. On dispose, the activity
/// is stopped (capturing duration) and all tags are forwarded to the injected
/// callback.
/// </summary>
internal sealed class TrackedOperation : IDisposable
{
    private readonly Activity? _activity;
    private readonly string _eventName;
    private readonly Action<string, IDictionary<string, string?>>? _onTrackEvent;
    private readonly Dictionary<string, string?> _storedTags = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    internal TrackedOperation(Activity? activity, string eventName, Action<string, IDictionary<string, string?>>? onTrackEvent)
    {
        _activity = activity;
        _eventName = eventName;
        _onTrackEvent = onTrackEvent;
    }

    public Activity? Activity => _activity;

    public void Tag(string key, object? value)
    {
        _activity?.SetTag(key, value);
        _storedTags[key] = value?.ToString();
    }

    public void SetStatus(ActivityStatusCode code, string? description = null)
    {
        _activity?.SetStatus(code, description);
        Tag("command.status", code == ActivityStatusCode.Ok ? "ok" : "error");
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
        foreach (var tag in _activity.TagObjects)
        {
            properties[tag.Key] = tag.Value?.ToString();
        }

        // Merge explicitly stored tags — ensures tags set via Tag() are always
        // present even if the Activity.TagObjects enumeration misses them.
        foreach (var tag in _storedTags)
        {
            properties[tag.Key] = tag.Value;
        }

        properties["command.duration_ms"] = _activity.Duration.TotalMilliseconds
            .ToString(CultureInfo.InvariantCulture);

        _onTrackEvent?.Invoke(_eventName, properties);
    }
}
