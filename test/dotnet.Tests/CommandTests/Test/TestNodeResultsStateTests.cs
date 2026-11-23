// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test.Terminal;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class TestNodeResultsStateTests
{
    [TestMethod]
    public void AddRunningTestNode_AfterRemove_IsSuppressed()
    {
        // Simulates the stale-add race: the producer may emit an in-progress
        // notification for a test that already completed. The state must not
        // resurrect the test in the "running" list.
        var state = new TestNodeResultsState(id: 1);
        const string instanceId = "instance-A";
        const string uid = "Foo";

        state.AddRunningTestNode(id: 100, instanceId, uid, "Foo", new FakeStopwatch());
        Assert.AreEqual(1, state.Count);

        state.RemoveRunningTestNode(instanceId, uid);
        Assert.AreEqual(0, state.Count);

        // Stale in-progress arriving after completion must be ignored.
        state.AddRunningTestNode(id: 101, instanceId, uid, "Foo", new FakeStopwatch());
        Assert.AreEqual(0, state.Count);
    }

    [TestMethod]
    public void AddRunningTestNode_DifferentInstance_SameUid_NotSuppressed()
    {
        // Retries use a new instanceId. A previous instance completing must
        // not prevent the new instance from showing as running.
        var state = new TestNodeResultsState(id: 1);
        const string uid = "Foo";

        state.AddRunningTestNode(id: 100, "instance-A", uid, "Foo", new FakeStopwatch());
        state.RemoveRunningTestNode("instance-A", uid);
        Assert.AreEqual(0, state.Count);

        state.AddRunningTestNode(id: 200, "instance-B", uid, "Foo", new FakeStopwatch());
        Assert.AreEqual(1, state.Count);
    }

    [TestMethod]
    public void AddRunningTestNode_DistinctTests_AllTracked()
    {
        var state = new TestNodeResultsState(id: 1);

        state.AddRunningTestNode(id: 100, "instance-A", "Test1", "Test1", new FakeStopwatch());
        state.AddRunningTestNode(id: 101, "instance-A", "Test2", "Test2", new FakeStopwatch());
        state.AddRunningTestNode(id: 102, "instance-B", "Test1", "Test1", new FakeStopwatch());

        Assert.AreEqual(3, state.Count);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void GetRunningTasks_WithNonPositiveMaximum_ReturnsNoTasks(int maxCount)
    {
        var state = new TestNodeResultsState(id: 1);
        state.AddRunningTestNode(id: 100, "instance-A", "Test1", "Test1", new FakeStopwatch());

        Assert.IsEmpty(state.GetRunningTasks(maxCount));
    }

    [TestMethod]
    public void AnsiProgressFrame_WithNoDetailLineBudget_RendersOnlyProjectProgress()
    {
        var progress = new TestProgressState(
            id: 1,
            assembly: "MyTests.dll",
            targetFramework: "net11.0",
            architecture: "x64",
            stopwatch: new FakeStopwatch(),
            isDiscovery: false);
        progress.GetOrCreateTestNodeResultsState(() => new TestNodeResultsState(id: 2))
            .AddRunningTestNode(id: 3, "instance-A", "test-1", "Test 1", new FakeStopwatch());

        var previousFrame = new AnsiTerminalTestProgressFrame(width: 120, height: 1);
        var currentFrame = new AnsiTerminalTestProgressFrame(width: 120, height: 1);
        var terminal = new AnsiTerminal(new CapturingConsole(), baseDirectory: null);

        currentFrame.Render(previousFrame, [progress], terminal);

        Assert.IsNotNull(currentFrame.RenderedLines);
        Assert.HasCount(1, currentFrame.RenderedLines);
    }

    private sealed class FakeStopwatch : IStopwatch
    {
        public TimeSpan Elapsed => TimeSpan.Zero;

        public void Start() { }

        public void Stop() { }
    }
}
