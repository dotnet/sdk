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
}
