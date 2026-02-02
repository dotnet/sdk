// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Frozen;
using System.Diagnostics;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using CLIRuntimeEnvironment = Microsoft.DotNet.Cli.Utils.RuntimeEnvironment;

namespace Microsoft.DotNet.Cli.Telemetry;

public class Telemetry : ITelemetry
{
    internal static string? CurrentSessionId = null;
    internal static bool DisabledForTests = false;
    private readonly int _senderCount;
    private TelemetryClient? _client = null;
    private FrozenDictionary<string, string>? _commonProperties = null;
    private FrozenDictionary<string, double>? _commonMeasurements = null;
    private Task? _trackEventTask = null;

    private const string ConnectionString = "InstrumentationKey=74cc1c9e-3e6e-4d05-b3fc-dde9101d0254";

    public bool Enabled { get; }

    public Telemetry() : this(null) { }

    public Telemetry(IFirstTimeUseNoticeSentinel? sentinel) : this(sentinel, null) { }

    public Telemetry(
        IFirstTimeUseNoticeSentinel? sentinel,
        string? sessionId,
        bool blockThreadInitialization = false,
        IEnvironmentProvider? environmentProvider = null,
        int senderCount = 3)
    {

        if (DisabledForTests)
        {
            return;
        }

        environmentProvider ??= new EnvironmentProvider();

        Enabled = !environmentProvider.GetEnvironmentVariableAsBool(EnvironmentVariableNames.TELEMETRY_OPTOUT, defaultValue: CompileOptions.TelemetryOptOutDefault)
                    && PermissionExists(sentinel);

        if (!Enabled)
        {
            return;
        }

        // Store the session ID in a static field so that it can be reused
        CurrentSessionId = sessionId ?? Guid.NewGuid().ToString();
        _senderCount = senderCount;
        if (blockThreadInitialization)
        {
            InitializeTelemetry();
        }
        else
        {
            //initialize in task to offload to parallel thread
            _trackEventTask = Task.Run(() => InitializeTelemetry());
        }
    }

    internal static void DisableForTests()
    {
        DisabledForTests = true;
        CurrentSessionId = null;
    }

    internal static void EnableForTests()
    {
        DisabledForTests = false;
    }

    private static bool PermissionExists(IFirstTimeUseNoticeSentinel? sentinel)
    {
        if (sentinel == null)
        {
            return false;
        }

        return sentinel.Exists();
    }

    public void TrackEvent(string eventName, IDictionary<string, string> properties,
        IDictionary<string, double> measurements)
    {
        if (!Enabled)
        {
            return;
        }

        //continue the task in different threads
        if (_trackEventTask == null)
        {
            _trackEventTask = Task.Run(() => TrackEventTask(eventName, properties, measurements));
            return;
        }
        else
        {
            _trackEventTask = _trackEventTask.ContinueWith(
                x => TrackEventTask(eventName, properties, measurements)
            );
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

    // Adding dispose on graceful shutdown per https://github.com/microsoft/ApplicationInsights-dotnet/issues/1152#issuecomment-518742922
    public void Dispose()
    {
        if (_client != null)
        {
            _client.TelemetryConfiguration.Dispose();
            _client = null;
        }
    }

    public void ThreadBlockingTrackEvent(string eventName, IDictionary<string, string> properties, IDictionary<string, double> measurements)
    {
        if (!Enabled)
        {
            return;
        }
        TrackEventTask(eventName, properties, measurements);
    }

    private void InitializeTelemetry()
    {
        try
        {
            var persistenceChannel = new PersistenceChannel.PersistenceChannel(sendersCount: _senderCount)
            {
                SendingInterval = TimeSpan.FromMilliseconds(1)
            };

            var config = TelemetryConfiguration.CreateDefault();
            config.TelemetryChannel = persistenceChannel;
            config.ConnectionString = ConnectionString;
            _client = new TelemetryClient(config);
            _client.Context.Session.Id = CurrentSessionId;
            _client.Context.Device.OperatingSystem = CLIRuntimeEnvironment.OperatingSystem;

            _commonProperties = new TelemetryCommonProperties().GetTelemetryCommonProperties(CurrentSessionId);
            _commonMeasurements = FrozenDictionary<string, double>.Empty;
        }
        catch (Exception e)
        {
            _client = null;
            // we dont want to fail the tool if telemetry fails.
            Debug.Fail(e.ToString());
        }
    }

    private void TrackEventTask(
        string eventName,
        IDictionary<string, string> properties,
        IDictionary<string, double> measurements)
    {
        if (_client == null)
        {
            return;
        }

        try
        {
            var eventProperties = GetEventProperties(properties);
            var eventMeasurements = GetEventMeasures(measurements);

            eventProperties ??= new Dictionary<string, string>();
            eventProperties.Add("event id", Guid.NewGuid().ToString());

            _client.TrackEvent(PrependProducerNamespace(eventName), eventProperties, eventMeasurements);
            Activity.Current?.AddEvent(CreateActivityEvent(eventName, eventProperties, eventMeasurements));
        }
        catch (Exception e)
        {
            Debug.Fail(e.ToString());
        }
    }

    private static ActivityEvent CreateActivityEvent(
        string eventName,
        IDictionary<string, string>? properties,
        IDictionary<string, double>? measurements)
    {
        var tags = MakeTags(properties, measurements);
        return new ActivityEvent(
            PrependProducerNamespace(eventName),
            tags: tags);
    }

    private static ActivityTagsCollection? MakeTags(
        IDictionary<string, string>? properties,
        IDictionary<string, double>? measurements)
    {
        if (properties == null && measurements == null)
        {
            return null;
        }
        else if (properties != null && measurements == null)
        {
            return [.. properties.Select(p => new KeyValuePair<string, object?>(p.Key, p.Value))];
        }
        else if (properties == null && measurements != null)
        {
            return [.. measurements.Select(m => new KeyValuePair<string, object?>(m.Key, m.Value.ToString()))];
        }
        else return [ .. properties!.Select(p => new KeyValuePair<string, object?>(p.Key, p.Value)),
                 .. measurements!.Select(m => new KeyValuePair<string, object?>(m.Key, m.Value.ToString())) ];
    }

    private static string PrependProducerNamespace(string eventName) => $"dotnet/cli/{eventName}";

    private IDictionary<string, double>? GetEventMeasures(IDictionary<string, double>? measurements)
    {
        return (measurements, _commonMeasurements) switch
        {
            (null, null) => null,
            (null, not null) => _commonMeasurements == FrozenDictionary<string, double>.Empty ? null : new Dictionary<string, double>(_commonMeasurements),
            (not null, null) => measurements,
            (not null, not null) => Combine(_commonMeasurements, measurements),
        };
    }

    private IDictionary<string, string>? GetEventProperties(IDictionary<string, string>? properties)
    {
        return (properties, _commonProperties) switch
        {
            (null, null) => null,
            (null, not null) => _commonProperties == FrozenDictionary<string, string>.Empty ? null : new Dictionary<string, string>(_commonProperties),
            (not null, null) => properties,
            (not null, not null) => Combine(_commonProperties, properties),
        };
    }

    static IDictionary<TKey, TValue> Combine<TKey, TValue>(IDictionary<TKey, TValue> common, IDictionary<TKey, TValue> specific) where TKey : notnull
    {
        IDictionary<TKey, TValue> eventMeasurements = new Dictionary<TKey, TValue>(capacity: common.Count + specific.Count);
        foreach (KeyValuePair<TKey, TValue> measurement in common)
        {
            eventMeasurements[measurement.Key] = measurement.Value;
        }
        foreach (KeyValuePair<TKey, TValue> measurement in specific)
        {
            eventMeasurements[measurement.Key] = measurement.Value;
        }
        return eventMeasurements;
    }
}
