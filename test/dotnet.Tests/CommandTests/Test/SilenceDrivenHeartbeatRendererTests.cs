// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using Microsoft.Testing.Platform.OutputDevice.Terminal;
using Moq;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public sealed class SilenceDrivenHeartbeatRendererTests
{
    [TestMethod]
    public void ContextualHeartbeat_RendersActiveModuleIdentityElapsedTimeAndTestAttribution()
    {
        var clock = new FakeStopwatch();
        var alpha = CreateActiveModule("Alpha.dll", "net10.0", "Alpha running test", TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(15));
        var beta = CreateActiveModule("Beta.dll", "net9.0", "Beta running test", TimeSpan.FromMinutes(3), TimeSpan.FromSeconds(20));
        var renderer = new SilenceDrivenHeartbeatRenderer(
            TimeSpan.FromSeconds(60),
            TimeSpan.Zero,
            () => clock,
            () => [alpha, beta],
            contextualModuleOutput: true);
        var console = new CapturingConsole();
        var terminal = new NonAnsiTerminal(console);
        renderer.OnStart();

        clock.Elapsed = TimeSpan.FromSeconds(60);
        terminal.StartUpdate();
        renderer.OnTick(terminal, [alpha, beta]);
        terminal.StopUpdate();

        string[] lines = console.GetOutput().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().Contain("running... 0 completed, 0 failed, active modules: 2 | no tests completed yet");
        lines.Should().Contain(line => line.Contains("[Alpha.dll net10.0|x64] 2m 00s - Alpha running test", StringComparison.Ordinal));
        lines.Should().Contain(line => line.Contains("[Beta.dll net9.0|x64] 3m 00s - Beta running test", StringComparison.Ordinal));

        TestProgressState CreateActiveModule(string assembly, string targetFramework, string testName, TimeSpan moduleElapsed, TimeSpan testElapsed)
        {
            var moduleClock = new FakeStopwatch { Elapsed = moduleElapsed };
            var testClock = new FakeStopwatch { Elapsed = testElapsed };
            var module = new TestProgressState(1, assembly, targetFramework, "x64", moduleClock, false);
            module.GetOrCreateTestNodeResultsState(() => new TestNodeResultsState(1))
                .AddRunningTestNode(1, assembly, testName, testName, testClock);
            return module;
        }
    }

    [TestMethod]
    public void ContextualSlowTests_UseModuleIdentityAndExponentialCadence()
    {
        var clock = new FakeStopwatch();
        var alphaClock = new FakeStopwatch();
        var betaClock = new FakeStopwatch();
        var alpha = CreateActiveModule(1, "Alpha.dll", "net10.0", "Alpha slow test", alphaClock);
        var beta = CreateActiveModule(2, "Beta.dll", "net9.0", "Beta slow test", betaClock);
        var renderer = new SilenceDrivenHeartbeatRenderer(
            TimeSpan.FromMinutes(10),
            TimeSpan.FromSeconds(30),
            () => clock,
            contextualModuleOutput: true);
        var console = new CapturingConsole();
        var terminal = new NonAnsiTerminal(console);
        renderer.OnStart();

        Tick(TimeSpan.FromSeconds(30));
        console.GetOutput().Should().Contain("[Alpha.dll net10.0|x64] [slow] still running after 30s: Alpha slow test")
            .And.Contain("[Beta.dll net9.0|x64] [slow] still running after 30s: Beta slow test");
        CountSlowTestLines().Should().Be(2);

        Tick(TimeSpan.FromSeconds(59));
        CountSlowTestLines().Should().Be(2);

        Tick(TimeSpan.FromSeconds(60));
        console.GetOutput().Should().Contain("[Alpha.dll net10.0|x64] [slow] still running after 1m 00s: Alpha slow test")
            .And.Contain("[Beta.dll net9.0|x64] [slow] still running after 1m 00s: Beta slow test");
        CountSlowTestLines().Should().Be(4);

        void Tick(TimeSpan elapsed)
        {
            clock.Elapsed = elapsed;
            alphaClock.Elapsed = elapsed;
            betaClock.Elapsed = elapsed;
            terminal.StartUpdate();
            renderer.OnTick(terminal, [alpha, beta]);
            terminal.StopUpdate();
        }

        int CountSlowTestLines()
            => console.GetOutput().Split("[slow]", StringSplitOptions.None).Length - 1;

        static TestProgressState CreateActiveModule(int id, string assembly, string targetFramework, string testName, FakeStopwatch testClock)
        {
            var module = new TestProgressState(id, assembly, targetFramework, "x64", new FakeStopwatch(), false);
            module.GetOrCreateTestNodeResultsState(() => new TestNodeResultsState(id))
                .AddRunningTestNode(id, assembly, testName, testName, testClock);
            return module;
        }
    }

    [TestMethod]
    public void ContextualHeartbeat_WithoutTestProgressStillShowsActiveModules()
    {
        var clock = new FakeStopwatch();
        var alpha = new TestProgressState(1, "Alpha.dll", "net10.0", "x64", new FakeStopwatch { Elapsed = TimeSpan.FromMinutes(2) }, false);
        var beta = new TestProgressState(2, "Beta.dll", "net9.0", "x64", new FakeStopwatch { Elapsed = TimeSpan.FromMinutes(3) }, false);
        var renderer = new SilenceDrivenHeartbeatRenderer(
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30),
            () => clock,
            () => [alpha, beta],
            contextualModuleOutput: true);
        var console = new CapturingConsole();
        var terminal = new NonAnsiTerminal(console);
        renderer.OnStart();
        clock.Elapsed = TimeSpan.FromSeconds(30);

        terminal.StartUpdate();
        renderer.OnTick(terminal, [alpha, beta]);
        terminal.StopUpdate();

        string[] lines = console.GetOutput().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().Contain("running... 0 completed, 0 failed, active modules: 2 | no tests completed yet");
        lines.Should().Contain(line => line.Contains("[Alpha.dll net10.0|x64] 2m", StringComparison.Ordinal));
        lines.Should().Contain(line => line.Contains("[Beta.dll net9.0|x64] 3m", StringComparison.Ordinal));
        lines.Should().NotContain(line => line.Contains("[slow]", StringComparison.Ordinal));
    }

    [TestMethod]
    public void HiddenCompletions_DoNotPostponeHeartbeatAndFinishedModulesRemainCounted()
    {
        TimeSpan elapsed = TimeSpan.Zero;
        var clock = new Mock<IStopwatch>();
        clock.SetupGet(stopwatch => stopwatch.Elapsed).Returns(() => elapsed);
        var finished = new TestProgressState(1, "Finished.dll", "net9.0", "x64", clock.Object, false);
        var active = new TestProgressState(2, "Active.dll", "net9.0", "x64", clock.Object, false);
        finished.NotifyHandshake("finished");
        active.NotifyHandshake("active");
        finished.ReportPassingTest("finished-test", "Finished test", "finished");
        var renderer = new SilenceDrivenHeartbeatRenderer(
            TimeSpan.FromSeconds(60), TimeSpan.Zero, () => clock.Object, () => [finished, active]);
        var console = new CapturingConsole();
        var terminal = new NonAnsiTerminal(console);
        renderer.OnStart();

        elapsed = TimeSpan.FromSeconds(50);
        active.ReportFailedTest("failed-test", "Failed test", "active");
        renderer.OnTestCompleted();
        Tick(TimeSpan.FromSeconds(59));
        console.GetOutput().Should().BeEmpty();

        Tick(TimeSpan.FromSeconds(60));
        console.GetOutput().Should().Contain("running... 2 completed, 1 failed, active modules: 1")
            .And.Contain("last completion: 10s ago");

        void Tick(TimeSpan timestamp)
        {
            elapsed = timestamp;
            terminal.StartUpdate();
            renderer.OnTick(terminal, [active]);
            terminal.StopUpdate();
        }
    }

    [TestMethod]
    public void VisibleOutput_PostponesHeartbeatButNoCompletionIsExplicit()
    {
        TimeSpan elapsed = TimeSpan.Zero;
        var clock = new Mock<IStopwatch>();
        clock.SetupGet(stopwatch => stopwatch.Elapsed).Returns(() => elapsed);
        var renderer = new SilenceDrivenHeartbeatRenderer(TimeSpan.FromSeconds(60), TimeSpan.Zero, () => clock.Object);
        var console = new CapturingConsole();
        var terminal = new NonAnsiTerminal(console);
        renderer.OnStart();
        elapsed = TimeSpan.FromSeconds(50);
        terminal.StartUpdate();
        renderer.OnWrite(terminal, [], terminal => terminal.AppendLine("visible diagnostic"));
        terminal.StopUpdate();

        elapsed = TimeSpan.FromSeconds(109);
        terminal.StartUpdate();
        renderer.OnTick(terminal, []);
        terminal.StopUpdate();
        console.GetOutput().Should().NotContain("running...");

        elapsed = TimeSpan.FromSeconds(110);
        terminal.StartUpdate();
        renderer.OnTick(terminal, []);
        terminal.StopUpdate();
        console.GetOutput().Should().Contain("running... 0 completed, 0 failed, active modules: 0 | no tests completed yet");
    }

    [TestMethod]
    public void ZeroSilenceThreshold_DisablesHeartbeat()
    {
        var clock = new Mock<IStopwatch>();
        clock.SetupGet(stopwatch => stopwatch.Elapsed).Returns(TimeSpan.FromHours(2));
        var renderer = new SilenceDrivenHeartbeatRenderer(TimeSpan.Zero, TimeSpan.Zero, () => clock.Object);
        var console = new CapturingConsole();
        var terminal = new NonAnsiTerminal(console);
        renderer.OnStart();

        terminal.StartUpdate();
        renderer.OnTick(terminal, []);
        terminal.StopUpdate();

        console.GetOutput().Should().BeEmpty();
    }

    [TestMethod]
    public void SlowTestThreshold_UsesExponentialBackoff()
    {
        var state = new SlowTestThresholdState(TimeSpan.FromSeconds(60));

        state.IsDue(TimeSpan.FromSeconds(59)).Should().BeFalse();
        state.IsDue(TimeSpan.FromSeconds(60)).Should().BeTrue();
        state.IsDue(TimeSpan.FromSeconds(119)).Should().BeFalse();
        state.IsDue(TimeSpan.FromSeconds(120)).Should().BeTrue();
        state.IsDue(TimeSpan.FromSeconds(239)).Should().BeFalse();
        state.IsDue(TimeSpan.FromSeconds(240)).Should().BeTrue();
    }

    private sealed class FakeStopwatch : IStopwatch
    {
        public TimeSpan Elapsed { get; set; }

        public void Start()
        {
        }

        public void Stop()
        {
        }
    }
}
