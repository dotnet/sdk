// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Microsoft.DotNet.Cli.Utils;

[TestClass]
// Activity listeners affect process-wide sampling, including code that cannot take a resource lock.
[DoNotParallelize]
public sealed class ActivitiesTests : SdkTest
{
    private static readonly DateTime s_startTime = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void StoppingAnActivityRecordsItsDurationInSeconds(bool performanceSource, bool stringParentId)
    {
        using var metrics = new ActivityMeasurements();
        ActivitySource source = performanceSource ? Activities.PerformanceSource : Activities.Source;
        using Activity? activity = stringParentId
            ? source.StartActivity("test-operation", ActivityKind.Internal, parentId: "parent.", startTime: s_startTime)
            : source.StartActivity(
                "test-operation",
                ActivityKind.Internal,
                new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None),
                startTime: s_startTime);

        activity.Should().NotBeNull();
        activity!.IsAllDataRequested.Should().BeTrue();
        activity.Recorded.Should().BeFalse();
        activity.DisplayName = "display name is not the metric dimension";
        activity.SetTag("project", "must not become a metric dimension");
        activity.SetEndTime(s_startTime.AddSeconds(1.25));

        metrics.Measurements.Should().BeEmpty();
        activity.Stop();

        Measurement measurement = metrics.Measurements.Should().ContainSingle().Subject;
        measurement.Instrument.Should().BeOfType<Histogram<double>>();
        measurement.Instrument.Meter.Name.Should().Be("dotnet-cli-perf");
        measurement.Instrument.Name.Should().Be("dotnet.cli.activity.duration");
        measurement.Instrument.Unit.Should().Be("s");
        measurement.Duration.Should().Be(1.25);
        measurement.Tags.Length.Should().Be(1);
        measurement.Tags[0].Key.Should().Be("activity.name");
        measurement.Tags[0].Value.Should().Be("test-operation");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BuiltInTelemetryDoesNotEnablePerformanceActivities(bool stringParentId)
    {
        using var metrics = new ActivityMeasurements(meterName: "dotnet-cli");
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "dotnet-cli",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        using Activity? parent = Activities.Source.StartActivity("normal-telemetry");
        parent.Should().NotBeNull();
        parent!.Recorded.Should().BeTrue();

        using Activity? activity = stringParentId
            ? Activities.PerformanceSource.StartActivity("disabled", ActivityKind.Internal, parentId: parent.Id)
            : Activities.PerformanceSource.StartActivity("disabled", ActivityKind.Internal, parent.Context);

        activity.Should().BeNull();
        Activity.Current.Should().BeSameAs(parent);
        parent.Stop();
        metrics.Measurements.Should().BeEmpty();
    }

    [TestMethod]
    public void ActivitiesFromOtherSourcesAreNotMeasured()
    {
        using var metrics = new ActivityMeasurements();
        using var source = new ActivitySource("not-dotnet-cli");
        using var listener = new ActivityListener
        {
            ShouldListenTo = candidate => candidate == source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);
        using Activity? activity = source.StartActivity("test-operation");

        activity.Should().NotBeNull();
        activity!.Stop();

        metrics.Measurements.Should().BeEmpty();
    }

    private sealed class ActivityMeasurements : IDisposable
    {
        private readonly MeterListener _listener = new();

        public List<Measurement> Measurements { get; } = [];

        public ActivityMeasurements(string meterName = "dotnet-cli-perf")
        {
            // Initialize the production source and its bridge before discovering instruments.
            _ = Activities.Source;
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == meterName && instrument.Name == "dotnet.cli.activity.duration")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<double>((instrument, duration, tags, _) =>
                Measurements.Add(new Measurement(instrument, duration, tags.ToArray())));
            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }

    private sealed record Measurement(
        Instrument Instrument,
        double Duration,
        KeyValuePair<string, object?>[] Tags);
}
