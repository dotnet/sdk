// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class HotReloadDotNetWatcherTests
{
    [TestMethod]
    [DataRow(new[] { ChangeKind.Update }, new[] { ChangeKind.Update })]
    [DataRow(new[] { ChangeKind.Add }, new[] { ChangeKind.Add })]
    [DataRow(new[] { ChangeKind.Delete }, new[] { ChangeKind.Delete })]

    [DataRow(new[] { ChangeKind.Update, ChangeKind.Update }, new[] { ChangeKind.Update })]
    [DataRow(new[] { ChangeKind.Update, ChangeKind.Delete }, new[] { ChangeKind.Delete })]
    [DataRow(new[] { ChangeKind.Add, ChangeKind.Update }, new[] { ChangeKind.Add })]
    [DataRow(new[] { ChangeKind.Add, ChangeKind.Delete }, new ChangeKind[] { })]
    [DataRow(new[] { ChangeKind.Delete, ChangeKind.Add}, new[] { ChangeKind.Update })]
    [DataRow(new[] { ChangeKind.Add, ChangeKind.Add }, new[] { ChangeKind.Add })]
    [DataRow(new[] { ChangeKind.Delete, ChangeKind.Delete }, new[] { ChangeKind.Delete })]

    [DataRow(new[] { ChangeKind.Add, ChangeKind.Delete, ChangeKind.Add, ChangeKind.Delete }, new ChangeKind[] { })]
    [DataRow(new[] { ChangeKind.Update, ChangeKind.Delete, ChangeKind.Add, ChangeKind.Update }, new[] { ChangeKind.Update })]
    [DataRow(new[] { ChangeKind.Update, ChangeKind.Delete, ChangeKind.Update, ChangeKind.Add, ChangeKind.Update }, new[] { ChangeKind.Update })]
    [DataRow(new[] { ChangeKind.Add, ChangeKind.Delete, ChangeKind.Delete }, new ChangeKind[] { })]
    [DataRow(new[] { ChangeKind.Add, ChangeKind.Add, ChangeKind.Delete }, new ChangeKind[] { })]
    [DataRow(new[] { ChangeKind.Add, ChangeKind.Update, ChangeKind.Delete }, new ChangeKind[] { })]
    [DataRow(new[] { ChangeKind.Update, ChangeKind.Add, ChangeKind.Delete }, new[] { ChangeKind.Update })]

    // File.WriteAllText on macOS may produce Update + Add.
    [DataRow(new[] { ChangeKind.Update, ChangeKind.Add }, new[] { ChangeKind.Add })]

    // The following case should not occur in practice:
    [DataRow(new[] { ChangeKind.Delete, ChangeKind.Update }, new[] { ChangeKind.Delete })]
    public void NormalizeFileChanges(Array changes, Array expected)
    {
        var normalized = HotReloadDotNetWatcher.NormalizePathChanges(changes.Cast<ChangeKind>().Select(kind => new ChangedPath("a.html", kind)));
        AssertEx.SequenceEqual(expected.Cast<ChangeKind>(), normalized.Select(c => c.Kind));
    }
}

