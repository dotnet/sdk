// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.Metrics;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tests.TelemetryTests;

[TestClass]
public class CliMetricsTests : SdkTest
{
    [TestMethod]
    public void ItRecordsManagedEntryToMSBuildSubmissionDuration()
    {
        double? recordedDuration = null;
        string? recordedCommand = null;
        string? recordedUnit = null;

        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == CliMetrics.MeterName &&
                instrument.Name == CliMetrics.ManagedEntryToMSBuildSubmissionDurationName)
            {
                recordedUnit = instrument.Unit;
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((_, measurement, tags, _) =>
        {
            recordedDuration = measurement;
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                if (tag.Key == CliMetrics.CommandNameTag)
                {
                    recordedCommand = tag.Value as string;
                }
            }
        });
        listener.Start();

        DateTime startTimeUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        CliMetrics.SetManagedEntryTimeUtc(startTimeUtc);
        CliMetrics.RecordManagedEntryToMSBuildSubmission(
            "pack",
            startTimeUtc + TimeSpan.FromMilliseconds(125));

        recordedDuration.Should().Be(0.125);
        recordedCommand.Should().Be("pack");
        recordedUnit.Should().Be("s");
    }
}
