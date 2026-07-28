// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.Metrics;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tests.TelemetryTests;

[TestClass]
public class CliMetricsTests
{
    [TestMethod]
    public void ItRecordsProcessStartToMSBuildSubmissionDuration()
    {
        double? recordedDuration = null;
        string? recordedCommand = null;
        string? recordedUnit = null;

        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == CliMetrics.MeterName &&
                instrument.Name == CliMetrics.ProcessStartToMSBuildSubmissionDurationName)
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

        CliMetrics.RecordProcessStartToMSBuildSubmission("pack", TimeSpan.FromMilliseconds(125));

        recordedDuration.Should().Be(0.125);
        recordedCommand.Should().Be("pack");
        recordedUnit.Should().Be("s");
    }
}
