// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.MsiInstallerTests
{
    internal class VMStateTree
    {
        public string SnapshotId {  get; set; }
        public string SnapshotName { get; set; }

        public Dictionary<SerializedVMAction, (VMActionResult actionResult, VMStateTree resultingState)> Actions { get; set; } = new();

        public SerializeableVMStateTree ToSerializeable()
        {
            return new SerializeableVMStateTree()
            {
                SnapshotId = SnapshotId,
                SnapshotName = SnapshotName,
                Actions = Actions.Select(a => new SerializeableVMStateTree.Entry() {
                    Action = a.Key,
                    ActionResult = a.Value.actionResult,
                    ResultingState = a.Value.resultingState.ToSerializeable()
                })
                .ToList()
            };
        }
    }

    internal class  SerializeableVMStateTree
    {
        public string SnapshotId { get; set; }
        public string SnapshotName { get; set; }

        public List<Entry> Actions { get; set; }


        public class Entry
        {
            public SerializedVMAction Action { get; set; }
            public VMActionResult ActionResult { get; set; }
            public SerializeableVMStateTree ResultingState { get; set; }
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

            return tree;
        }
    }
}
