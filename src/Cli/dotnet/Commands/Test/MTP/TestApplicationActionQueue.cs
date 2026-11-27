// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Channels;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal class TestApplicationActionQueue
{
    private readonly Channel<ParallelizableTestModuleGroupWithSequentialInnerModules> _channel;
    private readonly Task[] _readers;
    private readonly CancellationToken _cancellationToken;
    private readonly TestApplicationSettings _fallbackTestApplicationSettings;
    private readonly BuildOptions _buildOptions;
    private readonly OutputOptions _outputOptions;
    private readonly bool _outputOptionSpecified;
    private readonly TestOptions _testOptions;
    private readonly TestRunPolicy _testRunPolicy;
    private readonly Func<TestApplicationActionQueue, int, int>? _applyRunPolicies;
    private volatile InvocationLifetimeCoordinator? _invocationLifetimeCoordinator;

    private int? _aggregateExitCode;
    private readonly List<TestApplicationPolicy> _effectivePolicies = [];
    private readonly Dictionary<TestModule, TestApplicationSettings> _effectiveSettings = [];

    private readonly Lock _lock = new();

    public TestApplicationActionQueue(
        int degreeOfParallelism,
        BuildOptions buildOptions,
        TestOptions testOptions,
        TestResultsDirectoryResolver resultsDirectoryResolver,
        TerminalTestReporter output,
        Action<CommandLineOptionMessages> onHelpRequested,
        CtrlCCancellationManager ctrlC,
        ArtifactPostProcessingManager artifactPostProcessingManager,
        TestRunPolicy testRunPolicy,
        CancellationToken cancellationToken,
        OutputOptions outputOptions,
        bool outputOptionSpecified,
        TestApplicationSettings fallbackTestApplicationSettings,
        Func<TestApplicationActionQueue, int, int>? applyRunPolicies = null)
    {
        _channel = Channel.CreateUnbounded<ParallelizableTestModuleGroupWithSequentialInnerModules>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
        _readers = new Task[degreeOfParallelism];
        _cancellationToken = cancellationToken;
        _fallbackTestApplicationSettings = fallbackTestApplicationSettings;
        _buildOptions = buildOptions;
        _outputOptions = outputOptions;
        _outputOptionSpecified = outputOptionSpecified;
        _testOptions = testOptions;
        _testRunPolicy = testRunPolicy;
        _applyRunPolicies = applyRunPolicies;

        for (int i = 0; i < degreeOfParallelism; i++)
        {
            _readers[i] = Task.Run(async () => await Read(
                buildOptions,
                testOptions,
                resultsDirectoryResolver,
                output,
                onHelpRequested,
                ctrlC,
                artifactPostProcessingManager,
                testRunPolicy));
        }
    }

    /// <summary>
    /// Selects the invocation lifetime owner among every module of the command. Must be called before any module is
    /// enqueued; the returned owner, if any, must be enqueued first.
    /// </summary>
    public TestModule? ConfigureInvocationLifetime(IEnumerable<TestModule> modules)
    {
        // Microsoft.Testing.Platform never runs invocation lifetime handlers for help or discovery.
        if (_testOptions.IsHelp || _testOptions.IsDiscovery || _testOptions.IsArtifactPostProcessing)
        {
            return null;
        }

        IReadOnlyList<TestModule> moduleList = [.. modules];
        InvocationLifetimeOwnerSelection selection = InvocationLifetimeOwnerSelector.Select(moduleList);
        if (_buildOptions.HasNoBuild && selection.Status != InvocationLifetimeOwnerSelectionStatus.NotRequested)
        {
            // Evaluated project capabilities do not establish which MTP version the existing binaries contain.
            Reporter.Output.WriteLine(CliCommandStrings.InvocationLifetimeNoBuild.Yellow());
            return null;
        }

        if (selection.Status != InvocationLifetimeOwnerSelectionStatus.NotRequested &&
            moduleList.Any(static module => module.IsTestingPlatformApplication && !module.BuildsProjectReferences))
        {
            // Unbuilt project references (for example a source-referenced Microsoft.Testing.Platform or handler
            // extension) can be copied into the runnable output from stale binaries.
            Reporter.Output.WriteLine(CliCommandStrings.InvocationLifetimeNoDependencies.Yellow());
            return null;
        }

        switch (selection.Status)
        {
            case InvocationLifetimeOwnerSelectionStatus.Selected:
                _invocationLifetimeCoordinator = new InvocationLifetimeCoordinator(selection.Owner!, InvocationLifetimeCoordinator.GetTimeout());
                return selection.Owner;

            case InvocationLifetimeOwnerSelectionStatus.Ambiguous:
                Reporter.Output.WriteLine(CliCommandStrings.InvocationLifetimeOwnerAmbiguous.Yellow());
                return null;

            case InvocationLifetimeOwnerSelectionStatus.UnsupportedTransport:
                Reporter.Output.WriteLine(string.Format(
                    CliCommandStrings.InvocationLifetimeOwnerUnsupportedTransport,
                    selection.Owner!.ProjectFullPath).Yellow());
                return null;

            case InvocationLifetimeOwnerSelectionStatus.UnsupportedParticipant:
                Reporter.Output.WriteLine(string.Format(
                    CliCommandStrings.InvocationLifetimeUnsupportedParticipant,
                    selection.UnsupportedParticipant!.ProjectFullPath,
                    selection.UnsupportedParticipant.TargetFramework).Yellow());
                return null;

            default:
                return null;
        }
    }

    public void Enqueue(ParallelizableTestModuleGroupWithSequentialInnerModules testApplication)
    {
        lock (_lock)
        {
            foreach (TestModule module in testApplication)
            {
                TestApplicationSettings settings =
                    MicrosoftTestingPlatformTestCommand.GetEffectiveTestApplicationSettings(
                        module,
                        _buildOptions,
                        _outputOptions,
                        _outputOptionSpecified);
                _effectiveSettings[module] = settings;
                _effectivePolicies.Add(settings.Policy);
            }
        }

        if (_cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (!_channel.Writer.TryWrite(testApplication))
        {
            throw new InvalidOperationException($"Failed to write to channel for test application: {testApplication}");
        }
    }

    public int CompleteEnqueueAndWait()
    {
        // Notify readers that no more data will be written
        _channel.Writer.Complete();

        int aggregateExitCode = ExitCode.GenericFailure;
        try
        {
            Task.WaitAll(_readers);

            // If _aggregateExitCode is null, that means we didn't get any results.
            // So, we exit with "zero tests".
            int moduleExitCode = _aggregateExitCode ?? ExitCode.ZeroTests;
            aggregateExitCode = _applyRunPolicies?.Invoke(this, moduleExitCode) ?? moduleExitCode;
        }
        finally
        {
            // WaitAll drains every reader even when one faults; a failed policy must not strand the held owner either.
            if (_invocationLifetimeCoordinator is { } coordinator)
            {
                using (coordinator)
                {
                    aggregateExitCode = coordinator.CompleteAsync(
                        aggregateExitCode,
                        _cancellationToken.IsCancellationRequested || _testRunPolicy.Cancellation.IsCompleted).GetAwaiter().GetResult();
                }
            }
        }

        return aggregateExitCode;
    }

    private async Task Read(
        BuildOptions buildOptions,
        TestOptions testOptions,
        TestResultsDirectoryResolver resultsDirectoryResolver,
        TerminalTestReporter output,
        Action<CommandLineOptionMessages> onHelpRequested,
        CtrlCCancellationManager ctrlC,
        ArtifactPostProcessingManager artifactPostProcessingManager,
        TestRunPolicy testRunPolicy)
    {
        try
        {
            await foreach (var nonParallelizedGroup in _channel.Reader.ReadAllAsync(_cancellationToken))
            {
                InvocationLifetimeCoordinator? coordinator = _invocationLifetimeCoordinator;
                IEnumerable<TestModule> modules = coordinator is null
                    ? nonParallelizedGroup
                    : nonParallelizedGroup.OrderBy(module => ReferenceEquals(module, coordinator.Owner) ? 0 : 1);
                foreach (var module in modules)
                {
                    _cancellationToken.ThrowIfCancellationRequested();

                    bool isOwner = coordinator is not null && ReferenceEquals(module, coordinator.Owner);
                    InvocationLifetimeSiblingLaunch? siblingLaunch = null;
                    if (coordinator is not null && !isOwner)
                    {
                        siblingLaunch = await coordinator.WaitForSiblingLaunchAsync(_cancellationToken);
                        // Coordination can fail after earlier modules were launched with the setup outputs, for example
                        // when the owner loses its control connection: the remaining modules are not launched.
                        if (siblingLaunch.Kind == InvocationLifetimeSiblingLaunchKind.Skip
                            || (siblingLaunch.Kind == InvocationLifetimeSiblingLaunchKind.Coordinated && coordinator.HasFailed))
                        {
                            // Invocation setup might have started and failed: launching would run tests without it.
                            output.MarkModuleExecutionIncomplete();
                            AggregateResult(ExitCode.GenericFailure);
                            continue;
                        }
                    }

                    int result = ExitCode.GenericFailure;
                    bool ownerDetached = false;
                    var testApp = new TestApplication(
                        module,
                        buildOptions,
                        testOptions,
                        resultsDirectoryResolver,
                        output,
                        onHelpRequested,
                        artifactPostProcessingManager,
                        testRunPolicy: testRunPolicy,
                        testApplicationSettings: GetEffectiveSettings(module),
                        invocationLifetimeOwnerCoordinator: isOwner ? coordinator : null,
                        invocationLifetimeSiblingLaunch: siblingLaunch,
                        abortSignal: coordinator is null ? null : isOwner ? coordinator.OwnerAbort : coordinator.SiblingAbort,
                        invocationLifetimeSiblingCoordinator: isOwner ? null : coordinator);
                    try
                    {
                        output.TestMapCollectionModuleStarted(module.TargetPath);
                        if (isOwner)
                        {
                            (result, ownerDetached) = await RunInvocationLifetimeOwnerAsync(coordinator!, testApp, ctrlC);
                        }
                        else
                        {
                            result = await testApp.RunAsync(ctrlC);
                        }
                    }
                    catch (Exception ex)
                    {
                        output.MarkModuleExecutionIncomplete();
                        output.MarkCoverageIncomplete();
                        var exAsString = ex.ToString();
                        Logger.LogTrace($"Exception running test module {module.RunProperties?.Command} {module.RunProperties?.Arguments}: {exAsString}");
                        Reporter.Error.WriteLine(string.Format(CliCommandStrings.ErrorRunningTestModule, module.RunProperties?.Command, module.RunProperties?.Arguments, exAsString));
                        result = ExitCode.GenericFailure;
                    }
                    finally
                    {
                        if (!ownerDetached)
                        {
                            testApp.Dispose();
                        }

                        output.TestMapCollectionModuleCompleted();
                    }

                    // A module that ran zero tests (exit code 8) is not, by itself, a whole-run failure.
                    // With --test-modules or a global --filter, some modules may legitimately match no tests.
                    // Normalize it to success here; the aggregate "zero tests ran" verdict is decided once at
                    // the whole-run level in MicrosoftTestingPlatformTestCommand from the total test count. A
                    // stricter per-module minimum requested via -- --minimum-expected-tests N still fails that
                    // module with ExitCode.MinimumExpectedTestsPolicyViolation (9) and is preserved.
                    // See https://github.com/microsoft/testfx/issues/7457.
                    result = NormalizeExitCode(result, testApp.HasFailureDuringDispose, output);
                    AggregateResult(result);
                }
            }

        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
            // Stop scheduling new test apps once cancellation is requested.
            // Already-running test apps are left alone so they can gracefully cancel themselves
            // and report final session state via IPC.
        }
    }

    private static async Task<(int Result, bool Detached)> RunInvocationLifetimeOwnerAsync(
        InvocationLifetimeCoordinator coordinator,
        TestApplication testApp,
        CtrlCCancellationManager ctrlC)
    {
        coordinator.OnOwnerLaunching();
        Task<int> run = testApp.RunAsync(ctrlC);
        _ = run.ContinueWith(
            static (_, state) => ((InvocationLifetimeCoordinator)state!).OnOwnerExited(),
            coordinator,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        if (await Task.WhenAny(run, coordinator.ModuleTestsCompletedTask) == run)
        {
            return (await run, false);
        }

        // The owner keeps running until invocation cleanup, which runs after every other module completed.
        coordinator.AttachDetachedOwner(testApp, run);
        return (await coordinator.ModuleTestsCompletedTask, true);
    }

    private void AggregateResult(int result)
    {
        lock (_lock)
        {
            if (_aggregateExitCode is null)
            {
                // This is the first result we are getting.
                // So we assign the exit code, regardless of whether it's failure or success.
                _aggregateExitCode = result;
            }
            else if (_aggregateExitCode.Value != result)
            {
                if (_aggregateExitCode == ExitCode.Success)
                {
                    // The current result we are dealing with is the first failure after previous Success.
                    // So we assign the current failure.
                    _aggregateExitCode = result;
                }
                else if (result != ExitCode.Success)
                {
                    // If we get a new failure result, which is different from a previous failure, we use GenericFailure.
                    _aggregateExitCode = ExitCode.GenericFailure;
                }
                else
                {
                    // The current result is a success, but we already have a failure.
                    // So, we keep the failure exit code.
                }
            }
        }
    }

    internal bool FailOnAllSkippedTests
    {
        get
        {
            lock (_lock)
            {
                IReadOnlyList<TestApplicationPolicy> effectivePolicies = GetEffectivePolicies();
                return ShouldFailOnAllSkippedTests(effectivePolicies);
            }
        }
    }

    internal int ApplyExitCodeIgnorePolicy(int exitCode)
    {
        // Invocation infrastructure failures must never be converted into success by a test policy.
        if (_invocationLifetimeCoordinator?.HasFailed == true)
        {
            return exitCode == ExitCode.Success ? ExitCode.GenericFailure : exitCode;
        }

        lock (_lock)
        {
            IReadOnlyList<TestApplicationPolicy> effectivePolicies = GetEffectivePolicies();
            return ApplyExitCodeIgnorePolicy(exitCode, effectivePolicies);
        }
    }

    internal static bool ShouldFailOnAllSkippedTests(IReadOnlyList<TestApplicationPolicy> effectivePolicies)
        => effectivePolicies.Any(policy =>
            policy.FailOnAllSkippedTests &&
            MicrosoftTestingPlatformTestCommand.ApplyExitCodeIgnorePolicy(
                ExitCode.ZeroTests,
                policy.IgnoredExitCodes) != ExitCode.Success);

    internal static int ApplyExitCodeIgnorePolicy(
        int exitCode,
        IReadOnlyList<TestApplicationPolicy> effectivePolicies)
        => effectivePolicies.All(policy =>
            MicrosoftTestingPlatformTestCommand.ApplyExitCodeIgnorePolicy(
                exitCode,
                policy.IgnoredExitCodes) == ExitCode.Success)
                ? ExitCode.Success
                : exitCode;

    private IReadOnlyList<TestApplicationPolicy> GetEffectivePolicies()
        => _effectivePolicies.Count == 0
            ? [_fallbackTestApplicationSettings.Policy]
            : _effectivePolicies;

    private TestApplicationSettings GetEffectiveSettings(TestModule module)
    {
        lock (_lock)
        {
            return _effectiveSettings.TryGetValue(module, out TestApplicationSettings settings)
                ? settings
                : _fallbackTestApplicationSettings;
        }
    }

    internal static int NormalizeExitCode(int result, bool hasFailureDuringDispose, TerminalTestReporter output)
    {
        if (hasFailureDuringDispose)
        {
            output.MarkModuleExecutionIncomplete();
        }

        if (result == ExitCode.ZeroTests)
        {
            result = ExitCode.Success;
        }

        return result == ExitCode.Success && hasFailureDuringDispose
            ? ExitCode.GenericFailure
            : result;
    }
}
