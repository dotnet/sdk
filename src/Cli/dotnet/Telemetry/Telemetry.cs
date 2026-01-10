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
    private static FrozenDictionary<string, object?> s_commonProperties = null!;
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
        s_currentSessionId ??= !string.IsNullOrEmpty(sessionId) ? sessionId : Guid.NewGuid().ToString();

        s_commonProperties = new TelemetryCommonProperties().GetTelemetryCommonProperties(s_currentSessionId);
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

    public void TrackEvent(string? eventName, IDictionary<string, string?>? properties, IDictionary<string, double>? measurements)
    {
        if (!Enabled || eventName is null)
        {
            return;
        }

        // Continue the task in different threads.
        _trackEventTask = _trackEventTask == null
            ? Task.Run(() => TrackEventTask(eventName, properties, measurements))
            : _trackEventTask.ContinueWith(_ => TrackEventTask(eventName, properties, measurements));
    }

    public void Flush()
    {
        if (!Enabled || _trackEventTask == null)
        {
            return;
        }

        _trackEventTask.Wait();
    }

    public void ThreadBlockingTrackEvent(string? eventName, IDictionary<string, string?>? properties, IDictionary<string, double>? measurements)
    {
        if (!Enabled || eventName is null)
        {
            return;
        }

        TrackEventTask(eventName, properties, measurements);
    }

    private static void TrackEventTask(string eventName, IDictionary<string, string?>? properties, IDictionary<string, double>? measurements)
    {
        try
        {
            var eventId = Activity.Current?.Id ?? Guid.NewGuid().ToString();
            properties ??= new Dictionary<string, string?>();
            properties.Add("event id", eventId);
            measurements ??= new Dictionary<string, double>();
            var @event = CreateActivityEvent(PrependProducerNamespace(eventName), properties, measurements);
            Activity.Current?.AddEvent(@event);
        }
        catch (Exception e)
        {
            Debug.Fail(e.ToString());
        }
    }

    private static string PrependProducerNamespace(string eventName) => $"dotnet/cli/{eventName}";

    private static ActivityEvent CreateActivityEvent(string eventName, IDictionary<string, string?> properties, IDictionary<string, double> measurements) =>
        new (eventName, tags: MakeTags(properties, measurements));

    private static ActivityTagsCollection MakeTags(IDictionary<string, string?> eventProperties, IDictionary<string, double> eventMeasurements)
    {
        var tags = new Dictionary<string, object?>(s_commonProperties);
        foreach (var property in eventProperties.Where(p => p.Value is not null))
        {
            tags.TryAdd(property.Key, property.Value);
        }
        foreach (var measurement in eventMeasurements)
        {
            tags.TryAdd(measurement.Key, measurement.Value);
        }
        return [.. tags.OrderBy(p => p.Key)];
    }
}
