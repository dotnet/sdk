// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Workloads.Workload.History;

namespace Microsoft.DotNet.Cli.Commands.DotNetWorkloads
{
    internal static class WorkloadHistoryDisplay
    {
        public class DisplayRecord
        {
            public int? ID { get; set; }
            public DateTimeOffset? TimeStarted { get; set; }
            public string Command { get; set; }
            public List<string> Workloads { get; set; }

            public WorkloadHistoryRecord HistoryRecord { get; set; }
        }

        public static List<DisplayRecord> ProcessWorkloadHistoryRecords(IEnumerable<WorkloadHistoryRecord> historyRecords)
        {
            List<DisplayRecord> displayRecords = new();

            int currentId = 1;

            foreach (var historyRecord in historyRecords.OrderBy(r => r.TimeStarted))
            {
                if (displayRecords.Any() && displayRecords.Last().HistoryRecord != null &&
                    !displayRecords.Last().HistoryRecord.StateAfterCommand.Equals(historyRecord.StateBeforeCommand))
                {
                    //  Workload state changed without history record being written
                    var unknownDisplayRecord = new DisplayRecord();

                    unknownDisplayRecord.Command = "<unknown>";
                    displayRecords.Add(unknownDisplayRecord);
                }

                displayRecords.Add(new DisplayRecord()
                {
                    ID = currentId,
                    TimeStarted = historyRecord.TimeStarted,
                    Command = historyRecord.CommandName,
                    Workloads = historyRecord.WorkloadArguments
                });

                currentId++;
            }

            return displayRecords;
        }
    }
}
