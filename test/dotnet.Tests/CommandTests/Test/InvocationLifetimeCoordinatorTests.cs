// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using ExitCode = Microsoft.DotNet.Cli.Commands.Test.ExitCode;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class InvocationLifetimeCoordinatorTests
{
    public TestContext TestContext { get; set; } = null!;

    private readonly List<string> _warnings = [];
    private readonly List<string> _errors = [];

    [TestMethod]
    [DataRow("1.4.0")]
    [DataRow("")]
    public void HandshakeBelowProtocol15FallsBackWithoutAccepting(string negotiatedVersion)
    {
        using InvocationLifetimeCoordinator coordinator = CreateCoordinator();

        coordinator.OnOwnerHandshake(CreateHandshake("[\"Sql\"]"), negotiatedVersion, controlPipeAvailable: true).Should().BeFalse();

        AssertSiblingLaunch(coordinator, InvocationLifetimeSiblingLaunchKind.Independent);
        coordinator.IsOwnerCoordinated.Should().BeFalse();
    }

    [TestMethod]
    public void HandshakeWithoutControlPipeFallsBack()
    {
        using InvocationLifetimeCoordinator coordinator = CreateCoordinator();

        coordinator.OnOwnerHandshake(CreateHandshake("[\"Sql\"]"), "1.5.0", controlPipeAvailable: false).Should().BeFalse();

        AssertSiblingLaunch(coordinator, InvocationLifetimeSiblingLaunchKind.Independent);
    }

    [TestMethod]
    public void TestHostHandshakeWithoutHandlersFallsBack()
    {
        using InvocationLifetimeCoordinator coordinator = CreateCoordinator();

        coordinator.OnOwnerHandshake(CreateHandshake(handlersJson: null), "1.5.0", controlPipeAvailable: true).Should().BeFalse();

        AssertSiblingLaunch(coordinator, InvocationLifetimeSiblingLaunchKind.Independent);
    }

    [TestMethod]
    public void ControllerHandshakeWithoutHandlersKeepsWaitingForTheTestHost()
    {
        using InvocationLifetimeCoordinator coordinator = CreateCoordinator();

        coordinator.OnOwnerHandshake(CreateHandshake(handlersJson: null, hostType: HandshakeMessageHostTypes.TestHostOrchestrator), "1.5.0", controlPipeAvailable: true).Should().BeFalse();
        coordinator.WaitForSiblingLaunchAsync(CancellationToken.None).IsCompleted.Should().BeFalse();

        coordinator.OnOwnerHandshake(CreateHandshake("[\"Sql\"]"), "1.5.0", controlPipeAvailable: true).Should().BeTrue();
    }

    [TestMethod]
    [DataRow(HandshakeMessageHostTypes.TestHostOrchestrator)]
    [DataRow("TestHostController")]
    public void OutermostControllerHandshakeWithHandlersIsAcceptedAndLaterTestHostIsIgnored(string hostType)
    {
        using InvocationLifetimeCoordinator coordinator = CreateCoordinator();

        coordinator.OnOwnerHandshake(CreateHandshake("[\"Sql\"]", hostType), "1.5.0", controlPipeAvailable: true).Should().BeTrue();
        coordinator.OnOwnerHandshake(CreateHandshake(handlersJson: null), "1.5.0", controlPipeAvailable: true).Should().BeFalse();

        coordinator.WaitForSiblingLaunchAsync(CancellationToken.None).IsCompleted.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("Sql")]
    [DataRow("[\"Sql\",1]")]
    [DataRow("[\"Other\"]")]
    [DataRow("[]")]
    public void InvalidOrNonCoveringHandlersFallBack(string handlersJson)
    {
        using InvocationLifetimeCoordinator coordinator = CreateCoordinator();

        coordinator.OnOwnerHandshake(CreateHandshake(handlersJson), "1.5.0", controlPipeAvailable: true).Should().BeFalse();

        AssertSiblingLaunch(coordinator, InvocationLifetimeSiblingLaunchKind.Independent);
    }

    [TestMethod]
    public void OwnerExitBeforeHandshakeFallsBack()
    {
        using InvocationLifetimeCoordinator coordinator = CreateCoordinator();

        coordinator.OnOwnerExited();

        AssertSiblingLaunch(coordinator, InvocationLifetimeSiblingLaunchKind.Independent);
        _errors.Should().BeEmpty();
    }

    [TestMethod]
    public async Task SuccessfulSetupLaunchesSiblingsWithOutputsAndAdvertisedHandlers()
    {
        using InvocationLifetimeCoordinator coordinator = CreateCoordinator();
        coordinator.OnOwnerHandshake(CreateHandshake("[\"Sql\",\"Blob\"]"), "1.5.0", controlPipeAvailable: true).Should().BeTrue();
        coordinator.IsOwnerCoordinated.Should().BeTrue();

        InvocationLifetimeOutput[] outputs = [new("CONNECTION", true, "secret"), new("REMOVED", false, null)];
        coordinator.OnSetupCompleted(new InvocationSetupCompleted("e", "i", true, null, outputs));

        InvocationLifetimeSiblingLaunch launch = await coordinator.WaitForSiblingLaunchAsync(CancellationToken.None);
        launch.Kind.Should().Be(InvocationLifetimeSiblingLaunchKind.Coordinated);
        launch.Outputs.Should().Equal(outputs);
        launch.CoordinatedHandlers.Should().Be("[\"Blob\",\"Sql\"]");
        _errors.Should().BeEmpty();
    }

    [TestMethod]
    public async Task FailedSetupSkipsSiblingsAndFailsASuccessfulRun()
    {
        using InvocationLifetimeCoordinator coordinator = CreateCoordinator();
        coordinator.OnOwnerHandshake(CreateHandshake("[\"Sql\"]"), "1.5.0", controlPipeAvailable: true);

        coordinator.OnSetupCompleted(new InvocationSetupCompleted("e", "i", false, "boom", []));

        AssertSiblingLaunch(coordinator, InvocationLifetimeSiblingLaunchKind.Skip);
        coordinator.SiblingAbort.IsCompleted.Should().BeTrue();
        _errors.Should().ContainSingle().Which.Should().Contain("boom");
        (await coordinator.CompleteAsync(ExitCode.Success, isCanceled: false)).Should().Be(ExitCode.GenericFailure);
        (await coordinator.CompleteAsync(ExitCode.AtLeastOneTestFailed, isCanceled: false)).Should().Be(ExitCode.AtLeastOneTestFailed);
    }

    [TestMethod]
    public void OwnerExitAfterAcceptingSkipsSiblings()
    {
        using InvocationLifetimeCoordinator coordinator = CreateCoordinator();
        coordinator.OnOwnerHandshake(CreateHandshake("[\"Sql\"]"), "1.5.0", controlPipeAvailable: true);

        coordinator.OnOwnerExited();

        AssertSiblingLaunch(coordinator, InvocationLifetimeSiblingLaunchKind.Skip);
        _errors.Should().ContainSingle();
    }

    [TestMethod]
    public void UnexpectedMessageAfterAcceptingFailsAndAbortsOwner()
    {
        using InvocationLifetimeCoordinator coordinator = CreateCoordinator();
        coordinator.OnOwnerHandshake(CreateHandshake("[\"Sql\"]"), "1.5.0", controlPipeAvailable: true);

        coordinator.OnModuleTestsCompleted(new ModuleTestsCompleted("e", "i", ExitCode.Success));

        coordinator.OwnerAbort.IsCompleted.Should().BeTrue();
        AssertSiblingLaunch(coordinator, InvocationLifetimeSiblingLaunchKind.Skip);
    }

    [TestMethod]
    public void ReadinessTimeoutBeforeHandshakeFallsBackWithWarning()
    {
        var timeProvider = new ManualTimeProvider();
        using InvocationLifetimeCoordinator coordinator = CreateCoordinator(timeProvider);
        coordinator.OnOwnerLaunching();

        timeProvider.FireTimer();

        AssertSiblingLaunch(coordinator, InvocationLifetimeSiblingLaunchKind.Independent);
        _warnings.Should().ContainSingle();
        coordinator.OwnerAbort.IsCompleted.Should().BeFalse();
    }

    [TestMethod]
    public void ReadinessTimeoutAfterAcceptingFailsAndAbortsOwner()
    {
        var timeProvider = new ManualTimeProvider();
        using InvocationLifetimeCoordinator coordinator = CreateCoordinator(timeProvider);
        coordinator.OnOwnerLaunching();
        coordinator.OnOwnerHandshake(CreateHandshake("[\"Sql\"]"), "1.5.0", controlPipeAvailable: true);

        timeProvider.FireTimer();

        AssertSiblingLaunch(coordinator, InvocationLifetimeSiblingLaunchKind.Skip);
        coordinator.OwnerAbort.IsCompleted.Should().BeTrue();
        _errors.Should().ContainSingle();
    }

    [TestMethod]
    public async Task CompleteWithoutCoordinationReleasesWaitingSiblingsUnchanged()
    {
        using InvocationLifetimeCoordinator coordinator = CreateCoordinator();

        (await coordinator.CompleteAsync(ExitCode.ZeroTests, isCanceled: true)).Should().Be(ExitCode.ZeroTests);

        AssertSiblingLaunch(coordinator, InvocationLifetimeSiblingLaunchKind.Independent);
    }

    [TestMethod]
    public async Task ModuleTestsCompletedReplyIsHeldUntilEveryModuleCompleted()
    {
        using InvocationLifetimeCoordinator coordinator = CreateCoordinator();
        coordinator.OnOwnerHandshake(CreateHandshake("[\"Sql\"]"), "1.5.0", controlPipeAvailable: true);
        coordinator.OnSetupCompleted(new InvocationSetupCompleted("e", "i", true, null, []));

        Task reply = coordinator.OnModuleTestsCompleted(new ModuleTestsCompleted("e", "i", ExitCode.Success));

        coordinator.ModuleTestsCompletedTask.IsCompleted.Should().BeTrue();
        reply.IsCompleted.Should().BeFalse();
        await coordinator.CompleteAsync(ExitCode.Success, isCanceled: false);
        reply.IsCompleted.Should().BeTrue();
    }

    [TestMethod]
    public void ModuleTestsCompletedReplyIsReleasedOnDispose()
    {
        InvocationLifetimeCoordinator coordinator = CreateCoordinator();
        coordinator.OnOwnerHandshake(CreateHandshake("[\"Sql\"]"), "1.5.0", controlPipeAvailable: true);
        coordinator.OnSetupCompleted(new InvocationSetupCompleted("e", "i", true, null, []));
        Task reply = coordinator.OnModuleTestsCompleted(new ModuleTestsCompleted("e", "i", ExitCode.Success));

        coordinator.Dispose();

        reply.IsCompleted.Should().BeTrue();
    }

    [TestMethod]
    public async Task ControlLossBeforeSetupSkipsSiblingsAndRepliesImmediately()
    {
        using InvocationLifetimeCoordinator coordinator = CreateCoordinator();
        coordinator.OnOwnerHandshake(CreateHandshake("[\"Sql\"]"), "1.5.0", controlPipeAvailable: true);

        coordinator.OnOwnerControlLost();

        coordinator.HasFailed.Should().BeTrue();
        AssertSiblingLaunch(coordinator, InvocationLifetimeSiblingLaunchKind.Skip);
        coordinator.OwnerAbort.IsCompleted.Should().BeFalse();
        _errors.Should().ContainSingle();

        // The owner continues without coordination: it cleans up locally after its tests.
        coordinator.OnSetupCompleted(new InvocationSetupCompleted("e", "i", true, null, []));
        coordinator.OnModuleTestsCompleted(new ModuleTestsCompleted("e", "i", ExitCode.Success)).IsCompleted.Should().BeTrue();
        coordinator.ModuleTestsCompletedTask.IsCompleted.Should().BeFalse();
        coordinator.OwnerAbort.IsCompleted.Should().BeFalse();
        _errors.Should().ContainSingle();
        (await coordinator.CompleteAsync(ExitCode.Success, isCanceled: false)).Should().Be(ExitCode.GenericFailure);
    }

    [TestMethod]
    public async Task ControlLossAfterSetupStopsLaunchesAndHoldsOwnerUntilRunningModulesComplete()
    {
        using InvocationLifetimeCoordinator coordinator = CreateCoordinator();
        coordinator.OnOwnerHandshake(CreateHandshake("[\"Sql\"]"), "1.5.0", controlPipeAvailable: true);
        coordinator.OnSetupCompleted(new InvocationSetupCompleted("e", "i", true, null, []));
        AssertSiblingLaunch(coordinator, InvocationLifetimeSiblingLaunchKind.Coordinated);
        coordinator.HasFailed.Should().BeFalse();

        coordinator.OnOwnerControlLost();

        coordinator.HasFailed.Should().BeTrue();
        coordinator.SiblingAbort.IsCompleted.Should().BeFalse();
        coordinator.OwnerAbort.IsCompleted.Should().BeFalse();
        _errors.Should().ContainSingle();

        Task reply = coordinator.OnModuleTestsCompleted(new ModuleTestsCompleted("e", "i", ExitCode.Success));
        coordinator.ModuleTestsCompletedTask.IsCompleted.Should().BeTrue();
        reply.IsCompleted.Should().BeFalse();
        coordinator.OwnerAbort.IsCompleted.Should().BeFalse();

        (await coordinator.CompleteAsync(ExitCode.Success, isCanceled: false)).Should().Be(ExitCode.GenericFailure);
        reply.IsCompleted.Should().BeTrue();
        _errors.Should().ContainSingle();
    }

    [TestMethod]
    public void OwnerExitWhileHeldAfterControlLossAbortsRunningModules()
    {
        using InvocationLifetimeCoordinator coordinator = CreateCoordinator();
        coordinator.OnOwnerHandshake(CreateHandshake("[\"Sql\"]"), "1.5.0", controlPipeAvailable: true);
        coordinator.OnSetupCompleted(new InvocationSetupCompleted("e", "i", true, null, []));
        coordinator.OnOwnerControlLost();
        coordinator.OnModuleTestsCompleted(new ModuleTestsCompleted("e", "i", ExitCode.Success));

        coordinator.OnOwnerExited();

        coordinator.SiblingAbort.IsCompleted.Should().BeTrue();
    }

    [TestMethod]
    public async Task ControlLossWithoutAcceptedCoordinationIsIgnored()
    {
        using InvocationLifetimeCoordinator coordinator = CreateCoordinator();

        coordinator.OnOwnerControlLost();

        coordinator.HasFailed.Should().BeFalse();
        _errors.Should().BeEmpty();
        (await coordinator.CompleteAsync(ExitCode.Success, isCanceled: false)).Should().Be(ExitCode.Success);
        coordinator.OnOwnerControlLost();
        _errors.Should().BeEmpty();
    }

    [TestMethod]
    public async Task RejectedSiblingStopsLaunchesWithoutReleasingOwnerBeforeParticipantsDrain()
    {
        using InvocationLifetimeCoordinator coordinator = CreateCoordinator();
        coordinator.OnOwnerHandshake(CreateHandshake("[\"Sql\"]"), "1.5.0", controlPipeAvailable: true);
        coordinator.OnSetupCompleted(new InvocationSetupCompleted("e", "i", true, null, []));

        coordinator.OnSiblingHandshakeRejected(InvocationLifetimeOwnerSelectorTests.CreateModule("Sibling", ["Sql"]));

        coordinator.HasFailed.Should().BeTrue();
        coordinator.SiblingAbort.IsCompleted.Should().BeTrue();
        coordinator.OwnerAbort.IsCompleted.Should().BeFalse();
        Task reply = coordinator.OnModuleTestsCompleted(new ModuleTestsCompleted("e", "i", ExitCode.Success));
        reply.IsCompleted.Should().BeFalse();
        _errors.Should().ContainSingle().Which.Should().Contain("Coordinated execution is stopped");

        (await coordinator.CompleteAsync(ExitCode.Success, isCanceled: false)).Should().Be(ExitCode.GenericFailure);
        reply.IsCompleted.Should().BeTrue();
    }

    [TestMethod]
    public async Task RejectedSiblingStillAllowsOwnerCleanupAfterParticipantsDrain()
    {
        using InvocationLifetimeCoordinator coordinator = CreateCoordinator();
        coordinator.OnOwnerHandshake(CreateHandshake("[\"Sql\"]"), "1.5.0", controlPipeAvailable: true);
        coordinator.OnSetupCompleted(new InvocationSetupCompleted("e", "i", true, null, []));
        coordinator.OnSiblingHandshakeRejected(InvocationLifetimeOwnerSelectorTests.CreateModule("Sibling", ["Sql"]));
        _ = coordinator.OnModuleTestsCompleted(new ModuleTestsCompleted("e", "i", ExitCode.Success));
        using TestApplication application = TestApplicationLaunchTests.CreateApplication(new TestOptions(false, false, TestListFormat.Text));
        var ownerRun = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.AttachDetachedOwner(application, ownerRun.Task);

        Task<int> completion = coordinator.CompleteAsync(ExitCode.GenericFailure, isCanceled: false);
        coordinator.IsOwnerCoordinated.Should().BeTrue();
        coordinator.OnCleanupCompleted(new InvocationCleanupCompleted("e", "i", true, null));
        ownerRun.SetResult(ExitCode.Success);

        (await completion.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken)).Should().Be(ExitCode.GenericFailure);
        coordinator.HasFailed.Should().BeTrue();
        _errors.Should().ContainSingle().Which.Should().Contain("Coordinated execution is stopped");
    }

    [TestMethod]
    [DataRow(0, 0, 0, 0)]
    [DataRow(0, 1, 0, 1)]
    [DataRow(0, 134, 0, 1)]
    [DataRow(2, 2, 2, 2)]
    [DataRow(2, 2, 0, 0)]
    [DataRow(2, 0, 0, 0)]
    [DataRow(8, 8, 0, 0)]
    public async Task FinalOwnerExitPreservesReportedFailuresButRejectsUnexpectedFailures(
        int moduleExitCode, int processExitCode, int aggregateExitCode, int expectedExitCode)
    {
        using InvocationLifetimeCoordinator coordinator = CreateCoordinator();
        coordinator.OnOwnerHandshake(CreateHandshake("[\"Sql\"]"), "1.5.0", controlPipeAvailable: true);
        coordinator.OnSetupCompleted(new InvocationSetupCompleted("e", "i", true, null, []));
        _ = coordinator.OnModuleTestsCompleted(new ModuleTestsCompleted("e", "i", moduleExitCode));
        using TestApplication application = TestApplicationLaunchTests.CreateApplication(new TestOptions(false, false, TestListFormat.Text));
        var ownerRun = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.AttachDetachedOwner(application, ownerRun.Task);

        Task<int> completion = coordinator.CompleteAsync(aggregateExitCode, isCanceled: false);
        coordinator.OnCleanupCompleted(new InvocationCleanupCompleted("e", "i", true, null));
        ownerRun.SetResult(processExitCode);

        (await completion.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken)).Should().Be(expectedExitCode);
        bool unexpectedFailure = processExitCode != ExitCode.Success && processExitCode != moduleExitCode;
        coordinator.HasFailed.Should().Be(unexpectedFailure);
        if (unexpectedFailure)
        {
            _errors.Should().ContainSingle().Which.Should().Contain($"unexpected code {processExitCode}");
        }
        else
        {
            _errors.Should().BeEmpty();
        }
    }

    [TestMethod]
    public async Task OwnerShutdownTimeoutRemainsFailureAfterSuccessfulCleanupAndReaping()
    {
        var timeProvider = new ManualTimeProvider();
        using InvocationLifetimeCoordinator coordinator = CreateCoordinator(timeProvider);
        coordinator.OnOwnerHandshake(CreateHandshake("[\"Sql\"]"), "1.5.0", controlPipeAvailable: true);
        coordinator.OnSetupCompleted(new InvocationSetupCompleted("e", "i", true, null, []));
        _ = coordinator.OnModuleTestsCompleted(new ModuleTestsCompleted("e", "i", ExitCode.Success));
        using TestApplication application = TestApplicationLaunchTests.CreateApplication(new TestOptions(false, false, TestListFormat.Text));
        var ownerRun = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.AttachDetachedOwner(application, ownerRun.Task);

        Task<int> completion = coordinator.CompleteAsync(ExitCode.Success, isCanceled: false);
        coordinator.OnCleanupCompleted(new InvocationCleanupCompleted("e", "i", true, null));
        await timeProvider.SecondTimerCreated.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        timeProvider.FireTimer();
        await coordinator.OwnerAbort.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
        ownerRun.SetResult(ExitCode.Success);

        (await completion.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken)).Should().Be(ExitCode.GenericFailure);
        coordinator.HasFailed.Should().BeTrue();
        _errors.Should().ContainSingle().Which.Should().Contain("did not exit within 10 seconds");
    }

    [TestMethod]
    [DataRow(null, 900)]
    [DataRow("abc", 900)]
    [DataRow("-1", 900)]
    [DataRow("30", 30)]
    public void ParseTimeoutUsesDefaultForInvalidValues(string? value, int expectedSeconds)
        => InvocationLifetimeCoordinator.ParseTimeout(value).Should().Be(TimeSpan.FromSeconds(expectedSeconds));

    [TestMethod]
    public void ParseTimeoutZeroMeansInfinite()
        => InvocationLifetimeCoordinator.ParseTimeout("0").Should().Be(Timeout.InfiniteTimeSpan);

    [TestMethod]
    public void HandlersRoundTripAsCompactJson()
    {
        string json = InvocationLifetimeCoordinator.SerializeHandlers(["A;B", "C\"D"]);

        InvocationLifetimeCoordinator.TryParseHandlers(json, out IReadOnlyList<string>? handlers).Should().BeTrue();
        handlers.Should().Equal("A;B", "C\"D");
        json.Should().NotContain(" ");
    }

    private InvocationLifetimeCoordinator CreateCoordinator(TimeProvider? timeProvider = null)
        => new(
            InvocationLifetimeOwnerSelectorTests.CreateModule("Owner", ["Sql"]),
            TimeSpan.FromSeconds(10),
            timeProvider,
            _warnings.Add,
            _errors.Add);

    private static HandshakeMessage CreateHandshake(string? handlersJson, string hostType = HandshakeMessageHostTypes.TestHost)
    {
        var properties = new Dictionary<byte, string> { [HandshakeMessagePropertyNames.HostType] = hostType };
        if (handlersJson is not null)
        {
            properties[HandshakeMessagePropertyNames.InvocationLifetimeHandlers] = handlersJson;
        }

        return new HandshakeMessage(properties);
    }

    private static void AssertSiblingLaunch(InvocationLifetimeCoordinator coordinator, InvocationLifetimeSiblingLaunchKind expected)
    {
        Task<InvocationLifetimeSiblingLaunch> launch = coordinator.WaitForSiblingLaunchAsync(CancellationToken.None);
        launch.IsCompletedSuccessfully.Should().BeTrue();
        launch.Result.Kind.Should().Be(expected);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private TimerCallback? _callback;
        private object? _state;
        private int _timerCount;
        private readonly TaskCompletionSource _secondTimerCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task SecondTimerCreated => _secondTimerCreated.Task;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _callback = callback;
            _state = state;
            if (++_timerCount == 2)
            {
                _secondTimerCreated.TrySetResult();
            }
            return new NoopTimer();
        }

        public void FireTimer() => _callback!(_state);

        private sealed class NoopTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
