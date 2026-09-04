// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

using System.Diagnostics.Metrics;

namespace Microsoft.DotNet.Cli.Utils;

internal static class CliMetrics
{
    internal const string MeterName = "dotnet-cli";
    internal const string ManagedEntryToMSBuildSubmissionDurationName =
        "dotnet.cli.managed_entry_to_msbuild_submission.duration";
    internal const string CommandNameTag = "command.name";

    private static long s_managedEntryTimeUtcTicks;

    internal static void SetManagedEntryTimeUtc(DateTime managedEntryTimeUtc)
    {
        s_managedEntryTimeUtcTicks = managedEntryTimeUtc.Ticks;
    }

    internal static void RecordManagedEntryToMSBuildSubmission(string commandName)
    {
        RecordManagedEntryToMSBuildSubmission(commandName, DateTime.UtcNow);
    }

    internal static void RecordManagedEntryToMSBuildSubmission(string commandName, DateTime endTimeUtc)
    {
        try
        {
            RecordCore(commandName, endTimeUtc);
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException or AccessViolationException))
        {
            Debug.WriteLine($"Couldn't collect dotnet CLI metrics for command '{commandName}': {ex}");
        }
    }

    private static void RecordCore(string commandName, DateTime endTimeUtc)
    {
        if (!Instruments.ManagedEntryToMSBuildSubmissionDuration.Enabled)
        {
            return;
        }

        long startTimeUtcTicks = s_managedEntryTimeUtcTicks;
        if (startTimeUtcTicks != 0 && endTimeUtc.Ticks >= startTimeUtcTicks)
        {
            RecordCore(commandName, TimeSpan.FromTicks(endTimeUtc.Ticks - startTimeUtcTicks));
        }
    }

    private static void RecordCore(string commandName, TimeSpan duration)
    {
        TagList tags = default;
        tags.Add(CommandNameTag, commandName);
        Instruments.ManagedEntryToMSBuildSubmissionDuration.Record(duration.TotalSeconds, in tags);
    }

    private static class Instruments
    {
        private static readonly Meter s_meter = new(MeterName, Product.Version);

        internal static readonly Histogram<double> ManagedEntryToMSBuildSubmissionDuration =
            s_meter.CreateHistogram<double>(
                ManagedEntryToMSBuildSubmissionDurationName,
                unit: "s",
                description: "Elapsed time from managed dotnet CLI entry to the first Pack or Publish MSBuild invocation, excluding native host and CLR startup.");
    }
}

#endif
