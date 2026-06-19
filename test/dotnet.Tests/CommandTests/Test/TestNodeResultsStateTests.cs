// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test.Terminal;

namespace dotnet.Tests.CommandTests.Test;

public class TestNodeResultsStateTests
{
    [Fact]
    public void AddRunningTestNode_AfterRemove_IsSuppressed()
    {
        // Simulates the stale-add race: the producer may emit an in-progress
        // notification for a test that already completed. The state must not
        // resurrect the test in the "running" list.
        var state = new TestNodeResultsState(id: 1);
        const string instanceId = "instance-A";
        const string uid = "Foo";

        state.AddRunningTestNode(id: 100, instanceId, uid, "Foo", new FakeStopwatch());
        Assert.Equal(1, state.Count);

        state.RemoveRunningTestNode(instanceId, uid);
        Assert.Equal(0, state.Count);

        // Stale in-progress arriving after completion must be ignored.
        state.AddRunningTestNode(id: 101, instanceId, uid, "Foo", new FakeStopwatch());
        Assert.Equal(0, state.Count);
    }

    [Fact]
    public void AddRunningTestNode_DifferentInstance_SameUid_NotSuppressed()
    {
        // Retries use a new instanceId. A previous instance completing must
        // not prevent the new instance from showing as running.
        var state = new TestNodeResultsState(id: 1);
        const string uid = "Foo";

        state.AddRunningTestNode(id: 100, "instance-A", uid, "Foo", new FakeStopwatch());
        state.RemoveRunningTestNode("instance-A", uid);
        Assert.Equal(0, state.Count);

        state.AddRunningTestNode(id: 200, "instance-B", uid, "Foo", new FakeStopwatch());
        Assert.Equal(1, state.Count);
    }

    [Fact]
    public void AddRunningTestNode_DistinctTests_AllTracked()
    {
        var state = new TestNodeResultsState(id: 1);

        state.AddRunningTestNode(id: 100, "instance-A", "Test1", "Test1", new FakeStopwatch());
        state.AddRunningTestNode(id: 101, "instance-A", "Test2", "Test2", new FakeStopwatch());
        state.AddRunningTestNode(id: 102, "instance-B", "Test1", "Test1", new FakeStopwatch());

        Assert.Equal(3, state.Count);
    }

    private sealed class FakeStopwatch : IStopwatch
    {
        public TimeSpan Elapsed => TimeSpan.Zero;

        public void Start() { }

        public void Stop() { }
    }
}
