// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Test;

/// <summary>
/// Mirrors the two-stage Ctrl+C cancellation UX implemented in Microsoft.Testing.Platform
/// (testfx PR #8581 / SDK issue https://github.com/dotnet/sdk/issues/50732) at the
/// <c>dotnet test</c> orchestrator level:
/// <list type="bullet">
///   <item>First Ctrl+C: cooperative cancellation. The <see cref="Token"/> is signaled so the
///     queue stops scheduling new test apps; running test apps are left alone so they can
///     gracefully report their final state via IPC (their own testfx-side Ctrl+C handler will
///     cancel the session inside the child process).</item>
///   <item>Second Ctrl+C: force exit. Every registered child process is killed
///     (<c>entireProcessTree: true</c>) and the host process exits with
///     <see cref="ExitCode.TestSessionAborted"/> (=3).</item>
///   <item>Any further presses are no-ops (the state machine is idempotent).</item>
/// </list>
/// </summary>
internal sealed class CtrlCCancellationManager : IDisposable
{
    private const int StateIdle = 0;
    private const int StateCancelling = 1;
    private const int StateForcing = 2;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Action _onFirstCtrlC;
    private readonly Action<int> _exitAction;
    private readonly ConcurrentDictionary<Process, byte> _processes = new();
    private readonly bool _subscribedToConsole;
    private int _state = StateIdle;
    private int _disposed;

    public CtrlCCancellationManager(Action onFirstCtrlC, Action<int>? exitAction = null, bool subscribeToConsole = true)
    {
        _onFirstCtrlC = onFirstCtrlC ?? throw new ArgumentNullException(nameof(onFirstCtrlC));
        _exitAction = exitAction ?? Environment.Exit;

        if (subscribeToConsole)
        {
            Console.CancelKeyPress += OnConsoleCancelKeyPress;
            _subscribedToConsole = true;
        }
    }

    /// <summary>
    /// A token that is canceled when the user first presses Ctrl+C. Consumers should use it to
    /// stop scheduling new work but should not use it to tear down in-flight IPC for already
    /// running test apps (their cooperative cancellation is driven by their own Ctrl+C handler).
    /// </summary>
    public CancellationToken Token => _cancellationTokenSource.Token;

    /// <summary>
    /// Registers a child test-app process so it will be killed if the user requests a force exit
    /// (second Ctrl+C). If the manager is already in the forcing state when this is called, the
    /// process is killed immediately to avoid orphaning a child that started during the force
    /// exit race window.
    /// </summary>
    public void Register(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);

        _processes.TryAdd(process, 0);

        if (Volatile.Read(ref _state) == StateForcing)
        {
            TryKill(process);
        }
    }

    public void Unregister(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);

        _processes.TryRemove(process, out _);
    }

    /// <summary>
    /// Test-only hook to drive the state machine without depending on a real Console signal.
    /// </summary>
    internal void SimulateCtrlC() => HandleCtrlC();

    private void OnConsoleCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        // Suppress the runtime's default Ctrl+C handling on every press, including the second one,
        // so that we (not the runtime) get to decide the exit code and ordering. This must happen
        // before any other work in this handler.
        e.Cancel = true;

        HandleCtrlC();
    }

    private void HandleCtrlC()
    {
        // First press: Idle -> Cancelling. Signal the token, invoke the UI callback.
        if (Interlocked.CompareExchange(ref _state, StateCancelling, StateIdle) == StateIdle)
        {
            // CancellationTokenSource.Cancel() can throw an AggregateException if any registered
            // callback throws, or an ObjectDisposedException if Dispose races with a Ctrl+C press
            // (e.g. user presses Ctrl+C while we're tearing down). Swallow both — we still want the
            // state machine to advance and the UI callback to run.
            try
            {
                _cancellationTokenSource.Cancel();
            }
            catch (Exception ex) when (ex is AggregateException or ObjectDisposedException)
            {
                Logger.LogTrace($"Exception during CtrlCCancellationManager cancel:\n{ex}");
            }

            // The UI callback (typically TerminalTestReporter.StartCancelling) must not be able to
            // affect cancellation state — wrap it best-effort.
            try
            {
                _onFirstCtrlC();
            }
            catch (Exception ex)
            {
                Logger.LogTrace($"Exception during CtrlCCancellationManager first-press UI callback:\n{ex}");
            }

            return;
        }

        // Second press: Cancelling -> Forcing. Kill every registered child and exit.
        // We intentionally do not print any additional message here because the user already saw
        // the "Press Ctrl+C again to force exit." hint and the exit itself is the confirmation.
        if (Interlocked.CompareExchange(ref _state, StateForcing, StateCancelling) == StateCancelling)
        {
            foreach (var process in _processes.Keys)
            {
                TryKill(process);
            }

            _exitAction(ExitCode.TestSessionAborted);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            // The process may have exited between the HasExited check and Kill, or we may lack
            // permissions to read its state. Either way, nothing useful we can do from here.
            Logger.LogTrace($"Exception killing child process during force exit:\n{ex}");
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_subscribedToConsole)
        {
            Console.CancelKeyPress -= OnConsoleCancelKeyPress;
        }

        _cancellationTokenSource.Dispose();
    }
}
