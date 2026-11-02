// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Copyright (c) Microsoft Corporation. All rights reserved.
// Adapted from https://github.com/microsoft/testfx/blob/f4d8d35bf45ce0ece75a66e1b197c96684767e48/src/Platform/Microsoft.Testing.Platform/OutputDevice/Terminal/SilenceDrivenHeartbeatRenderer.cs

using System.Globalization;

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

internal sealed class SilenceDrivenHeartbeatRenderer(
    TimeSpan silenceThreshold,
    TimeSpan slowTestThreshold,
    Func<IStopwatch> createStopwatch,
    Func<IEnumerable<TestProgressState>>? getTestRuns = null) : IProgressRenderer
{
    private readonly Dictionary<long, SlowTestThresholdState> _slowTestThresholdStates = [];
    private StringBuilder? _slowTestDescriptionBuilder;
    private IStopwatch? _clock;
    private long _lastVisibleOutputTicks;
    private long _lastCompletionTicks = -1;

    public TimeSpan TickInterval => TimeSpan.FromSeconds(1);

    private long NowTicks => Volatile.Read(ref _clock)?.Elapsed.Ticks ?? 0;

    public void OnStart()
    {
        _slowTestThresholdStates.Clear();
        IStopwatch clock = createStopwatch();
        Volatile.Write(ref _clock, clock);
        _lastVisibleOutputTicks = clock.Elapsed.Ticks;
        Interlocked.Exchange(ref _lastCompletionTicks, -1);
    }

    public void OnTestCompleted()
        => Interlocked.Exchange(ref _lastCompletionTicks, NowTicks);

    public void OnWrite(ITerminal terminal, TestProgressState?[] progressItems, Action<ITerminal> write)
    {
        write(terminal);
        _lastVisibleOutputTicks = NowTicks;
    }

    public void OnTick(ITerminal terminal, TestProgressState?[] progressItems)
    {
        EmitSilenceHeartbeat(terminal, progressItems);
        EmitSlowTests(terminal, progressItems);
    }

    private void EmitSilenceHeartbeat(ITerminal terminal, TestProgressState?[] progressItems)
    {
        if (silenceThreshold <= TimeSpan.Zero)
        {
            return;
        }

        long now = NowTicks;
        if (now - _lastVisibleOutputTicks < silenceThreshold.Ticks)
        {
            return;
        }

        _lastVisibleOutputTicks = now;

        int completed = 0;
        int failed = 0;
        // Finished modules remain in the reporter after their worker slots have been reused.
        IEnumerable<TestProgressState?> testRuns = progressItems;
        if (getTestRuns is not null)
        {
            testRuns = getTestRuns();
        }

        foreach (TestProgressState? item in testRuns)
        {
            if (item is not null)
            {
                completed += item.TotalTests;
                failed += item.FailedTests;
            }
        }

        int activeModules = 0;
        string? activeTest = null;
        foreach (TestProgressState? item in progressItems)
        {
            if (item is null)
            {
                continue;
            }

            activeModules++;
            if (activeTest is null)
            {
                TestDetailState? active = item.TestNodeResultsState?.GetRunningTasks(1).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(active?.Text))
                {
                    activeTest = active.Text;
                }
            }
        }

        string line = string.Format(CultureInfo.CurrentCulture, CliCommandStrings.TerminalProgressHeartbeat, completed, failed, activeModules);
        long lastCompletion = Interlocked.Read(ref _lastCompletionTicks);
        line += lastCompletion < 0
            ? CliCommandStrings.TerminalProgressHeartbeatNoCompletionSuffix
            : string.Format(
                CultureInfo.CurrentCulture, CliCommandStrings.TerminalProgressHeartbeatLastCompletionSuffix,
                HumanReadableDurationFormatter.Render(TimeSpan.FromTicks(Math.Max(0, now - lastCompletion)), wrapInParentheses: false));
        if (activeTest is not null)
        {
            line += string.Format(CultureInfo.CurrentCulture, CliCommandStrings.TerminalProgressHeartbeatActiveSuffix, activeTest);
        }

        terminal.AppendLine(line);
    }

    private void EmitSlowTests(ITerminal terminal, TestProgressState?[] progressItems)
    {
        if (slowTestThreshold <= TimeSpan.Zero)
        {
            return;
        }

        foreach (TestProgressState? item in progressItems)
        {
            TestNodeResultsState? results = item?.TestNodeResultsState;
            if (item is null || results is null)
            {
                continue;
            }

            foreach (TestDetailState detail in results.GetRunningTasks(int.MaxValue))
            {
                if (detail.Stopwatch is null)
                {
                    continue;
                }

                TimeSpan elapsed = detail.Stopwatch.Elapsed;
                if (!_slowTestThresholdStates.TryGetValue(detail.Id, out SlowTestThresholdState? thresholdState))
                {
                    if (elapsed < slowTestThreshold)
                    {
                        continue;
                    }

                    thresholdState = new(slowTestThreshold);
                }

                if (!thresholdState.IsDue(elapsed))
                {
                    continue;
                }

                _slowTestThresholdStates[detail.Id] = thresholdState;
                string duration = HumanReadableDurationFormatter.Render(elapsed, wrapInParentheses: false);
                terminal.AppendLine(string.Format(
                    CultureInfo.CurrentCulture, CliCommandStrings.TerminalProgressSlowTest,
                    duration, BuildSlowTestDescription(item, detail)));
                _lastVisibleOutputTicks = NowTicks;
            }
        }
    }

    private string BuildSlowTestDescription(TestProgressState item, TestDetailState detail)
    {
        StringBuilder builder = _slowTestDescriptionBuilder ??= new StringBuilder();
        builder.Clear();
        builder.Append(detail.Text);
        builder.Append(" (");
        builder.Append(item.AssemblyName);
        if (item.TargetFramework is not null)
        {
            builder.Append('|');
            builder.Append(item.TargetFramework);
        }

        if (item.Architecture is not null)
        {
            builder.Append('|');
            builder.Append(item.Architecture);
        }

        builder.Append(')');
        return builder.ToString();
    }
}
