// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Frozen;
using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Telemetry;

public class Telemetry : ITelemetry
{
    internal static string? s_currentSessionId = null;
    internal static bool s_disabledForTests = false;
    private static readonly FrozenDictionary<string, object?> s_commonProperties = new TelemetryCommonProperties().GetTelemetryCommonProperties();
    private Task? _trackEventTask;

    public static string ConnectionString { get; } = "InstrumentationKey=74cc1c9e-3e6e-4d05-b3fc-dde9101d0254";
    public static string DefaultStorageFolderName { get; } = "TelemetryStorageService";
    public bool Enabled { get; }

    public Telemetry() : this(null) { }

    public Telemetry(string? sessionId, IEnvironmentProvider? environmentProvider = null)
    {
        if (s_disabledForTests)
        {
            return;
        }

        environmentProvider ??= new EnvironmentProvider();
        Enabled = !environmentProvider.GetEnvironmentVariableAsBool(EnvironmentVariableNames.TELEMETRY_OPTOUT, defaultValue: CompileOptions.TelemetryOptOutDefault);
        if (!Enabled)
        {
            return;
        }

        // Store the session ID in a static field so that it can be reused
        if (!string.IsNullOrEmpty(sessionId))
        {
            s_currentSessionId = sessionId;
        }
        else
        {
            // Generate a new session ID if not provided
            s_currentSessionId ??= Guid.NewGuid().ToString();
        }
    }

    internal static void DisableForTests()
    {
        s_disabledForTests = true;
        s_currentSessionId = null;
    }

    internal static void EnableForTests()
    {
        s_disabledForTests = false;
    }

    public void TrackEvent(string eventName, IDictionary<string, string?>? properties, IDictionary<string, double>? measurements)
    {
        if (!Enabled)
        {
            return;
        }

        // Continue the task in different threads.
        if (_trackEventTask == null)
        {
            _trackEventTask = Task.Run(() => TrackEventTask(eventName, properties, measurements));
        }
        else
        {
            _trackEventTask = _trackEventTask.ContinueWith(_ => TrackEventTask(eventName, properties, measurements));
        }
    }

    public void Flush()
    {
        if (!Enabled || _trackEventTask == null)
        {
            return;
        }

        _trackEventTask.Wait();
    }

    public void ThreadBlockingTrackEvent(string eventName, IDictionary<string, string?>? properties, IDictionary<string, double>? measurements)
    {
        if (!Enabled)
        {
            return;
        }
        TrackEventTask(eventName, properties, measurements);
    }

    private static void TrackEventTask(string eventName, IDictionary<string, string?>? properties, IDictionary<string, double>? measurements)
    {
        try
        {
            var activity = new ActivityEvent($"dotnet/cli/{eventName}", tags: MakeTags(properties, measurements));
            Activity.Current?.AddEvent(activity);
        }
        catch (Exception e)
        {
            Debug.Fail(e.ToString());
        }
    }

    private static ActivityTagsCollection MakeTags(IDictionary<string, string?>? eventProperties, IDictionary<string, double>? eventMeasurements)
    {
        var tags = new ActivityTagsCollection(s_commonProperties);
        if (s_currentSessionId is not null)
        {
            tags.Add("sessionId", s_currentSessionId);
        }

        foreach (var property in s_commonProperties)
        {
            if (property.Value is null)
            {
                continue; // Skip null properties
            }
            tags.TryAdd(property.Key, property.Value);
        }

        if (eventProperties is not null)
        {
            foreach (var property in eventProperties)
            {
                if (property.Value is null)
                {
                    continue; // Skip null properties
                }
                tags.TryAdd(property.Key, property.Value);
            }
        }

        if (eventMeasurements is not null)
        {
            foreach (var measurement in eventMeasurements)
            {
                tags.TryAdd(measurement.Key, measurement.Value);
            }
        }

        return tags;
    }
}
