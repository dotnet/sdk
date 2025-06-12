// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests;

public class HotReloadDotNetWatcherTests
{
    [Theory]
    [InlineData(new[] { ChangeKind.Update }, new[] { ChangeKind.Update })]
    [InlineData(new[] { ChangeKind.Add }, new[] { ChangeKind.Add })]
    [InlineData(new[] { ChangeKind.Delete }, new[] { ChangeKind.Delete })]

    [InlineData(new[] { ChangeKind.Update, ChangeKind.Update }, new[] { ChangeKind.Update })]
    [InlineData(new[] { ChangeKind.Update, ChangeKind.Delete }, new[] { ChangeKind.Delete })]
    [InlineData(new[] { ChangeKind.Add, ChangeKind.Update }, new[] { ChangeKind.Add })]
    [InlineData(new[] { ChangeKind.Add, ChangeKind.Delete }, new ChangeKind[] { })]
    [InlineData(new[] { ChangeKind.Delete, ChangeKind.Add}, new[] { ChangeKind.Update })]
    [InlineData(new[] { ChangeKind.Add, ChangeKind.Add }, new[] { ChangeKind.Add })]
    [InlineData(new[] { ChangeKind.Delete, ChangeKind.Delete }, new[] { ChangeKind.Delete })]

    [InlineData(new[] { ChangeKind.Add, ChangeKind.Delete, ChangeKind.Add, ChangeKind.Delete }, new ChangeKind[] { })]
    [InlineData(new[] { ChangeKind.Update, ChangeKind.Delete, ChangeKind.Add, ChangeKind.Update }, new[] { ChangeKind.Update })]
    [InlineData(new[] { ChangeKind.Update, ChangeKind.Delete, ChangeKind.Update, ChangeKind.Add, ChangeKind.Update }, new[] { ChangeKind.Update })]
    [InlineData(new[] { ChangeKind.Add, ChangeKind.Delete, ChangeKind.Delete }, new ChangeKind[] { })]
    [InlineData(new[] { ChangeKind.Add, ChangeKind.Add, ChangeKind.Delete }, new ChangeKind[] { })]
    [InlineData(new[] { ChangeKind.Add, ChangeKind.Update, ChangeKind.Delete }, new ChangeKind[] { })]
    [InlineData(new[] { ChangeKind.Update, ChangeKind.Add, ChangeKind.Delete }, new[] { ChangeKind.Update })]

    // File.WriteAllText on macOS may produce Update + Add.
    [InlineData(new[] { ChangeKind.Update, ChangeKind.Add }, new[] { ChangeKind.Add })]

    // The following case should not occur in practice:
    [InlineData(new[] { ChangeKind.Delete, ChangeKind.Update }, new[] { ChangeKind.Delete })]
    internal void NormalizeFileChanges(ChangeKind[] changes, ChangeKind[] expected)
    {
        var normalized = HotReloadDotNetWatcher.NormalizePathChanges(changes.Select(kind => new ChangedPath("a.html", kind)));
        AssertEx.SequenceEqual(expected, normalized.Select(c => c.Kind));
    }
}
