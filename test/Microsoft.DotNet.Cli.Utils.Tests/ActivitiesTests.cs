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
    public void StoppingAnActivityRecordsItsDurationInSecondsExactlyOnce()
    {
        using var metrics = new ActivityMeasurements();
        using Activity? activity = Activities.Source.StartActivity(
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
        activity.Stop();
        activity.Dispose();

        Measurement measurement = metrics.Measurements.Should().ContainSingle().Subject;
        measurement.Instrument.Should().BeOfType<Histogram<double>>();
        measurement.Instrument.Meter.Name.Should().Be("dotnet-cli");
        measurement.Instrument.Name.Should().Be("dotnet.cli.activity.duration");
        measurement.Instrument.Unit.Should().Be("s");
        measurement.Duration.Should().Be(1.25);
        measurement.Tags.Should().Equal(new KeyValuePair<string, object?>("activity.name", "test-operation"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DisabledMeasurementsDoNotForceActivities(bool stringParentId)
    {
        using Activity? activity = stringParentId
            ? Activities.Source.StartActivity("disabled", ActivityKind.Internal, parentId: "parent.")
            : Activities.Source.StartActivity("disabled", ActivityKind.Internal, default(ActivityContext));

        activity.Should().BeNull();
    }

    [TestMethod]
    public void StringParentIdsAreSampledWhenMeasurementsAreEnabled()
    {
        using var metrics = new ActivityMeasurements();
        using Activity? activity = Activities.Source.StartActivity(
            "string-parent",
            ActivityKind.Internal,
            parentId: "parent.",
            startTime: s_startTime);

        activity.Should().NotBeNull();
        activity!.IsAllDataRequested.Should().BeTrue();
        activity.Recorded.Should().BeFalse();
        activity.SetEndTime(s_startTime.AddSeconds(2));
        activity.Stop();

        metrics.Measurements.Should().ContainSingle().Which.Duration.Should().Be(2);
    }

    [TestMethod]
    public void DisposingTheMetricListenerStopsForcingActivities()
    {
        using (var metrics = new ActivityMeasurements())
        {
            using Activity? activity = Activities.Source.StartActivity("enabled");
            activity.Should().NotBeNull();
        }

        using Activity? disabledActivity = Activities.Source.StartActivity("disabled-again");
        disabledActivity.Should().BeNull();
    }

    [TestMethod]
    public void AnIndependentTraceListenerCanStillSampleWithoutMetrics()
    {
        ActivitySource source = Activities.Source;
        using var listener = new ActivityListener
        {
            ShouldListenTo = candidate => candidate == source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        using Activity? activity = Activities.Source.StartActivity("trace-only");

        activity.Should().NotBeNull();
        activity!.Recorded.Should().BeTrue();
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

    [TestMethod]
    public void NestedActivitiesRecordDistinctInclusiveDurations()
    {
        using var metrics = new ActivityMeasurements();
        using Activity? parent = Activities.Source.StartActivity(
            "outer",
            ActivityKind.Internal,
            new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None),
            startTime: s_startTime);
        parent.Should().NotBeNull();

        using Activity? child = Activities.Source.StartActivity(
            "inner",
            ActivityKind.Internal,
            parent!.Context,
            startTime: s_startTime.AddSeconds(1));
        child.Should().NotBeNull();
        child!.ParentSpanId.Should().Be(parent.SpanId);
        child.SetEndTime(s_startTime.AddSeconds(3));
        child.Stop();
        parent.SetEndTime(s_startTime.AddSeconds(5));
        parent.Stop();

        metrics.Measurements.Select(m => m.Duration).Should().Equal(2, 5);
        metrics.Measurements.Select(m => m.Tags.Single().Value).Should().Equal("inner", "outer");
    }

    [TestMethod]
    public void DisposingAFailedOperationRecordsItsDurationAndRestoresTheParent()
    {
        using var metrics = new ActivityMeasurements();
        using Activity? parent = Activities.Source.StartActivity("parent");
        parent.Should().NotBeNull();

        Action fail = () =>
        {
            using Activity? activity = Activities.Source.StartActivity(
                "failed-operation",
                ActivityKind.Internal,
                parent!.Context,
                startTime: s_startTime);
            activity.Should().NotBeNull();
            activity!.SetEndTime(s_startTime.AddSeconds(3));
            throw new InvalidOperationException("Expected failure.");
        };

        fail.Should().Throw<InvalidOperationException>().WithMessage("Expected failure.");
        Activity.Current.Should().BeSameAs(parent);
        Measurement measurement = metrics.Measurements.Should().ContainSingle().Subject;
        measurement.Duration.Should().Be(3);
        measurement.Tags.Should().Equal(new KeyValuePair<string, object?>("activity.name", "failed-operation"));
    }

    private sealed class ActivityMeasurements : IDisposable
    {
        private readonly MeterListener _listener = new();

        public List<Measurement> Measurements { get; } = [];

        public ActivityMeasurements()
        {
            // Initialize the production source and its bridge before discovering instruments.
            _ = Activities.Source;
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "dotnet-cli" && instrument.Name == "dotnet.cli.activity.duration")
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
