// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Commands.Test;

// Disambiguate from Microsoft.NET.TestFramework.ExitCode which is brought in by the test
// framework's global usings (FluentAssertions/Xunit/Microsoft.NET.TestFramework).
using ExitCode = Microsoft.DotNet.Cli.Commands.Test.ExitCode;

namespace dotnet.Tests.CommandTests.Test;

public class CtrlCCancellationManagerTests
{
    [Fact]
    public void FirstCtrlC_CancelsTokenAndInvokesUiCallbackExactlyOnce()
    {
        int callbackCount = 0;
        int exitCount = 0;
        using var manager = new CtrlCCancellationManager(
            onFirstCtrlC: () => Interlocked.Increment(ref callbackCount),
            exitAction: _ => Interlocked.Increment(ref exitCount),
            subscribeToConsole: false);

        manager.Token.IsCancellationRequested.Should().BeFalse("the token should be alive before any Ctrl+C");

        manager.SimulateCtrlC();

        manager.Token.IsCancellationRequested.Should().BeTrue("the first Ctrl+C should cancel the cooperative token");
        callbackCount.Should().Be(1);
        exitCount.Should().Be(0, "the first Ctrl+C must not force-exit");
    }

    [Fact]
    public void SecondCtrlC_InvokesExitActionWithTestSessionAborted()
    {
        int callbackCount = 0;
        int? receivedExitCode = null;
        int exitCount = 0;
        using var manager = new CtrlCCancellationManager(
            onFirstCtrlC: () => Interlocked.Increment(ref callbackCount),
            exitAction: code => { Interlocked.Increment(ref exitCount); receivedExitCode = code; },
            subscribeToConsole: false);

        manager.SimulateCtrlC();
        manager.SimulateCtrlC();

        callbackCount.Should().Be(1, "the UI callback fires only on the first press");
        exitCount.Should().Be(1, "the exit action fires on the second press");
        receivedExitCode.Should().Be(ExitCode.TestSessionAborted);
    }

    [Fact]
    public void ThirdAndSubsequentCtrlC_AreNoOps()
    {
        int callbackCount = 0;
        int exitCount = 0;
        using var manager = new CtrlCCancellationManager(
            onFirstCtrlC: () => Interlocked.Increment(ref callbackCount),
            exitAction: _ => Interlocked.Increment(ref exitCount),
            subscribeToConsole: false);

        manager.SimulateCtrlC();
        manager.SimulateCtrlC();
        manager.SimulateCtrlC();
        manager.SimulateCtrlC();

        callbackCount.Should().Be(1);
        exitCount.Should().Be(1, "presses after force-exit must not re-trigger the exit action");
    }

    [Fact]
    public void Register_AfterForcing_KillsTheProcessImmediately()
    {
        int exitCount = 0;
        using var manager = new CtrlCCancellationManager(
            onFirstCtrlC: () => { },
            exitAction: _ => Interlocked.Increment(ref exitCount),
            subscribeToConsole: false);

        manager.SimulateCtrlC();
        manager.SimulateCtrlC();
        exitCount.Should().Be(1);

        using var process = StartLongRunningProcess();
        try
        {
            manager.Register(process);

            // The manager should have killed the process immediately because we are in the Forcing state.
            process.WaitForExit(TimeSpan.FromSeconds(10)).Should().BeTrue("Register after Forcing must kill the registered process");
            process.HasExited.Should().BeTrue();
        }
        finally
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
            }
        }
    }

    [Fact]
    public void Register_BeforeForcing_KillsTheProcessOnSecondCtrlC()
    {
        int exitCount = 0;
        using var manager = new CtrlCCancellationManager(
            onFirstCtrlC: () => { },
            exitAction: _ => Interlocked.Increment(ref exitCount),
            subscribeToConsole: false);

        using var process = StartLongRunningProcess();
        try
        {
            manager.Register(process);

            manager.SimulateCtrlC();
            process.HasExited.Should().BeFalse("first Ctrl+C must not kill registered processes");

            manager.SimulateCtrlC();
            process.WaitForExit(TimeSpan.FromSeconds(10)).Should().BeTrue("second Ctrl+C must kill registered processes");
            exitCount.Should().Be(1);
        }
        finally
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
            }
        }
    }

    [Fact]
    public void Unregister_RemovesProcessFromForceKillSet()
    {
        int exitCount = 0;
        using var manager = new CtrlCCancellationManager(
            onFirstCtrlC: () => { },
            exitAction: _ => Interlocked.Increment(ref exitCount),
            subscribeToConsole: false);

        using var process = StartLongRunningProcess();
        try
        {
            manager.Register(process);
            manager.Unregister(process);

            manager.SimulateCtrlC();
            manager.SimulateCtrlC();

            // After unregister, the manager should not have killed the process.
            // Give it a moment in case the kill is asynchronous, then check.
            Thread.Sleep(200);
            process.HasExited.Should().BeFalse("unregistered processes must not be killed by force-exit");
            exitCount.Should().Be(1);
        }
        finally
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
            }
        }
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var manager = new CtrlCCancellationManager(
            onFirstCtrlC: () => { },
            exitAction: _ => { },
            subscribeToConsole: false);

        Action act = () => { manager.Dispose(); manager.Dispose(); };
        act.Should().NotThrow();
    }

    [Fact]
    public void Token_DoesNotCancel_WithoutAnyPress()
    {
        using var manager = new CtrlCCancellationManager(
            onFirstCtrlC: () => { },
            exitAction: _ => { },
            subscribeToConsole: false);

        manager.Token.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public void FirstCtrlC_CallbackThrowing_DoesNotAffectStateTransition()
    {
        int exitCount = 0;
        using var manager = new CtrlCCancellationManager(
            onFirstCtrlC: () => throw new InvalidOperationException("ui boom"),
            exitAction: _ => Interlocked.Increment(ref exitCount),
            subscribeToConsole: false);

        Action firstPress = () => manager.SimulateCtrlC();
        firstPress.Should().NotThrow("an exception from the UI callback must not propagate out of the handler");

        manager.Token.IsCancellationRequested.Should().BeTrue("token cancellation must happen before the UI callback");

        manager.SimulateCtrlC();
        exitCount.Should().Be(1, "the state machine must still advance to Forcing on the second press even if the first-press callback threw");
    }

    [Fact]
    public void FirstCtrlC_AfterDispose_DoesNotThrowFromDisposedTokenSource()
    {
        // Race window: the user presses Ctrl+C between the manager being disposed (which unsubscribes
        // from Console.CancelKeyPress but cannot remove an already-in-flight handler invocation) and
        // the handler reaching Cancel() on the disposed CancellationTokenSource. SimulateCtrlC drives
        // that path directly.
        var manager = new CtrlCCancellationManager(
            onFirstCtrlC: () => { },
            exitAction: _ => { },
            subscribeToConsole: false);

        manager.Dispose();

        Action act = () => manager.SimulateCtrlC();
        act.Should().NotThrow("a Ctrl+C arriving after Dispose must not surface as an unhandled exception");
    }

    private static Process StartLongRunningProcess()
    {
        // Spawn a platform-appropriate long-running process so we have a real OS process to
        // register/kill in tests. The actual program is irrelevant — we only need a process tree
        // the manager can target with Process.Kill.
        //
        // On Windows we deliberately avoid `cmd /c "timeout /t N"` because `timeout` requires an
        // interactive console (it exits immediately with "ERROR: Input redirection is not supported"
        // when stdin is not a console, which is the case on Helix and other headless CI agents).
        // `ping -n N 127.0.0.1` waits ~1 second between echoes and does not depend on a console,
        // so it stays alive reliably across local dev and CI.
        var psi = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "ping.exe" : "/bin/sh",
            Arguments = OperatingSystem.IsWindows()
                ? "-n 600 -w 1000 127.0.0.1"
                : "-c \"sleep 600\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start long-running helper process");

        // Sanity-check that the helper is actually alive before handing it back. If it exited
        // immediately the assertions on HasExited downstream would silently produce false failures
        // (as happened on Helix with `timeout` — see comment above).
        Thread.Sleep(100);
        if (process.HasExited)
        {
            var stderr = process.StandardError.ReadToEnd();
            throw new InvalidOperationException(
                $"Long-running helper process exited immediately with code {process.ExitCode}. Stderr: {stderr}");
        }

        return process;
    }
}
