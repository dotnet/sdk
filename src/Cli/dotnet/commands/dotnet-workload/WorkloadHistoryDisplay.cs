// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
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
            public string GlobalJsonVersion { get; set; }

            public WorkloadHistoryState HistoryState { get; set; }
        }

        public static List<DisplayRecord> ProcessWorkloadHistoryRecords(IEnumerable<WorkloadHistoryRecord> historyRecords, out bool unknownRecordsPresent)
        {
            List<DisplayRecord> displayRecords = new();
            unknownRecordsPresent = false;

            int currentId = 2;

            foreach (var historyRecord in historyRecords.OrderBy(r => r.TimeStarted))
            {
                if (displayRecords.Any() && !historyRecord.StateBeforeCommand.Equals(displayRecords.Last()?.HistoryState))
                {
                    //  Workload state changed without history record being written
                    var unknownDisplayRecord = new DisplayRecord()
                    {
                        Command = "Unlogged Changes",
                        ID = currentId,
                        TimeStarted = null,
                        HistoryState = historyRecord.StateBeforeCommand
                    };

                    currentId++;
                    displayRecords.Add(unknownDisplayRecord);
                    unknownRecordsPresent = true;
                }

                displayRecords.Add(new DisplayRecord()
                {
                    ID = currentId,
                    TimeStarted = historyRecord.TimeStarted,
                    Command = historyRecord.CommandName,
                    GlobalJsonVersion = historyRecord.GlobalJsonVersion,
                    HistoryState = historyRecord.StateAfterCommand
                });

                currentId++;
            }

            if (displayRecords.Count > 0)
            {
                displayRecords.Insert(0, new DisplayRecord()
                {
                    TimeStarted = DateTimeOffset.MinValue,
                    ID = 1,
                    Command = "InitialState",
                    HistoryState = historyRecords.First().StateBeforeCommand
                });
            }

            return displayRecords;
        }
    }
}