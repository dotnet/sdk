// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.MsiInstallerTests.Framework
{
    internal class VMStateTree
    {
        public string SnapshotId { get; set; }
        public string SnapshotName { get; set; }

        public Dictionary<SerializedVMAction, (VMActionResult actionResult, VMStateTree resultingState)> Actions { get; set; } = new();

        public Dictionary<SerializedVMAction, VMActionResult> ReadOnlyActions { get; set; } = new();

        public SerializableVMStateTree ToSerializeable()
        {
            return new SerializableVMStateTree()
            {
                SnapshotId = SnapshotId,
                SnapshotName = SnapshotName,
                Actions = Actions.Select(a => new SerializableVMStateTree.Entry()
                {
                    Action = a.Key,
                    ActionResult = a.Value.actionResult,
                    ResultingState = a.Value.resultingState.ToSerializeable()
                }).ToList(),
                ReadOnlyActions = ReadOnlyActions.Select(a => new SerializableVMStateTree.ReadOnlyEntry()
                {
                    Action = a.Key,
                    ActionResult = a.Value
                }).ToList()
                .ToList()
            };
        }
    }

    internal class SerializableVMStateTree
    {
        public string SnapshotId { get; set; }
        public string SnapshotName { get; set; }

        public List<Entry> Actions { get; set; }

        public List<ReadOnlyEntry> ReadOnlyActions { get; set; }


        public class Entry
        {
            public SerializedVMAction Action { get; set; }
            public VMActionResult ActionResult { get; set; }
            public SerializableVMStateTree ResultingState { get; set; }
        }

        public class ReadOnlyEntry
        {
            public SerializedVMAction Action { get; set; }
            public VMActionResult ActionResult { get; set; }
        }

        public VMStateTree ToVMStateTree()
        {
            var tree = new VMStateTree()
            {
                SnapshotId = SnapshotId,
                SnapshotName = SnapshotName,
            };

            foreach (var entry in Actions)
            {
                tree.Actions.Add(entry.Action, (entry.ActionResult, entry.ResultingState.ToVMStateTree()));
            }
            foreach (var entry in ReadOnlyActions)
            {
                tree.ReadOnlyActions.Add(entry.Action, entry.ActionResult);
            }

            return tree;
        }
    }
}
