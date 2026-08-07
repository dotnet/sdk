// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

using System.Diagnostics.Metrics;

namespace Microsoft.DotNet.Cli.Utils;

internal static class CliMetrics
{
    internal const string MeterName = "dotnet-cli";
    internal const string ProcessStartToMSBuildSubmissionDurationName =
        "dotnet.cli.process_start_to_msbuild_submission.duration";
    internal const string CommandNameTag = "command.name";

    internal static void RecordProcessStartToMSBuildSubmission(string commandName)
    {
        try
        {
            if (!Instruments.ProcessStartToMSBuildSubmissionDuration.Enabled)
            {
                return;
            }

            DateTime endTimeUtc = DateTime.UtcNow;
            DateTime startTimeUtc = Process.GetCurrentProcess().StartTime.ToUniversalTime();
            TimeSpan duration = endTimeUtc - startTimeUtc;
            if (duration >= TimeSpan.Zero)
            {
                RecordCore(commandName, duration);
            }
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException or AccessViolationException))
        {
            Debug.WriteLine($"dotnet CLI metrics disabled for this measurement: {ex}");
        }
    }

    internal static void RecordProcessStartToMSBuildSubmission(string commandName, TimeSpan duration)
    {
        try
        {
            RecordCore(commandName, duration);
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException or AccessViolationException))
        {
            Debug.WriteLine($"dotnet CLI metrics disabled for this measurement: {ex}");
        }
    }

    private static void RecordCore(string commandName, TimeSpan duration)
    {
        TagList tags = default;
        tags.Add(CommandNameTag, commandName);
        Instruments.ProcessStartToMSBuildSubmissionDuration.Record(duration.TotalSeconds, in tags);
    }

    private static class Instruments
    {
        private static readonly Meter s_meter = new(MeterName, Product.Version);

        internal static readonly Histogram<double> ProcessStartToMSBuildSubmissionDuration =
            s_meter.CreateHistogram<double>(
                ProcessStartToMSBuildSubmissionDurationName,
                unit: "s",
                description: "Elapsed time from dotnet process start until Pack or Publish is ready to make its first MSBuild invocation.");
    }
}

#endif
