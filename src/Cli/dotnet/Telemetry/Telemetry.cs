// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Frozen;
using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;

namespace Microsoft.DotNet.Cli.Telemetry;

public class Telemetry : ITelemetry
{
    private static bool s_disabledForTests = false;
    private static FrozenDictionary<string, string?> s_commonProperties = [];
    private Task? _trackEventTask;

    public static string ConnectionString { get; } = "InstrumentationKey=74cc1c9e-3e6e-4d05-b3fc-dde9101d0254";
    public static string DefaultStorageDirectory { get; } = Path.Combine(CliFolderPathCalculator.DotnetUserProfileFolderPath, "TelemetryStorageService");
    public static string? CurrentSessionId { get; private set; } = null;
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
        CurrentSessionId ??= !string.IsNullOrEmpty(sessionId) ? sessionId : Guid.NewGuid().ToString();

        s_commonProperties = new TelemetryCommonProperties().GetTelemetryCommonProperties(CurrentSessionId);
    }

    internal static void DisableForTests()
    {
        s_disabledForTests = true;
        CurrentSessionId = null;
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
            properties ??= new Dictionary<string, string?>();
            properties.Add("event id", Guid.NewGuid().ToString());
            measurements ??= new Dictionary<string, double>();
            var @event = new ActivityEvent($"dotnet/cli/{eventName}", tags: MakeTags(properties, measurements));
            Activity.Current?.AddEvent(@event);
        }
        catch (Exception e)
        {
            Debug.Fail(e.ToString());
        }
    }

    private static ActivityTagsCollection MakeTags(IDictionary<string, string?> eventProperties, IDictionary<string, double> eventMeasurements)
    {
        var common = s_commonProperties
            .Select(p => new KeyValuePair<string, object?>(p.Key, p.Value));
        var properties = eventProperties
            .Where(p => p.Value is not null)
            .Select(p => new KeyValuePair<string, object?>(p.Key, p.Value))
            .OrderBy(p => p.Key);
        var measurements = eventMeasurements
            .Select(p => new KeyValuePair<string, object?>(p.Key, p.Value))
            .OrderBy(p => p.Key);
        return [.. common, .. properties, .. measurements];
    }
}
