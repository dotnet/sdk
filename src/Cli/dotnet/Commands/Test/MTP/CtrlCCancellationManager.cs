// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Test;

/// <summary>
/// Mirrors the two-stage cancellation UX implemented in Microsoft.Testing.Platform
/// (testfx PR #8581 / SDK issue https://github.com/dotnet/sdk/issues/50732) at the
/// <c>dotnet test</c> orchestrator level. It can observe either console Ctrl+C directly
/// or the shared CLI lifecycle's cooperative and forceful cancellation tokens:
/// <list type="bullet">
///   <item>First Ctrl+C: cooperative cancellation. The <see cref="Token"/> is signaled so the
///     queue stops scheduling new test apps; running test apps are left alone so they can
///     gracefully report their final state via IPC (their own testfx-side Ctrl+C handler will
///     cancel the session inside the child process).</item>
///   <item>Forceful cancellation: SIGTERM is forwarded to every registered child with a
///     bounded grace period, while a repeated Ctrl+C kills every registered process tree
///     immediately. When this manager owns the console handler, a second Ctrl+C also exits
///     with <see cref="ExitCode.TestSessionAborted"/> (=3); otherwise the shared process
///     lifecycle owns the host exit.</item>
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
    private readonly Lock _processesLock = new();
    private readonly HashSet<Process> _processes = [];
    private readonly bool _subscribedToConsole;
    private readonly CancellationTokenRegistration _cooperativeCancellationRegistration;
    private readonly CancellationTokenRegistration _forcefulCancellationRegistration;
    private int _state = StateIdle;
    private int _forwardSigTerm;
    private int _disposed;

    public CtrlCCancellationManager(
        Action onFirstCtrlC,
        Action<int>? exitAction = null,
        bool subscribeToConsole = true,
        CancellationToken cooperativeCancellationToken = default,
        CancellationToken forcefulCancellationToken = default)
    {
        _onFirstCtrlC = onFirstCtrlC ?? throw new ArgumentNullException(nameof(onFirstCtrlC));
        _exitAction = exitAction ?? Environment.Exit;

        if (subscribeToConsole)
        {
            Console.CancelKeyPress += OnConsoleCancelKeyPress;
            _subscribedToConsole = true;
        }

        _cooperativeCancellationRegistration =
            cooperativeCancellationToken.Register(() => HandleCooperativeCancellation());
        _forcefulCancellationRegistration =
            forcefulCancellationToken.Register(
                () => HandleForcefulCancellation(ProcessLifecycle.IsSigTermRequested));
    }

    /// <summary>
    /// A token that is canceled when the user first presses Ctrl+C. Consumers should use it to
    /// stop scheduling new work but should not use it to tear down in-flight IPC for already
    /// running test apps (their cooperative cancellation is driven by their own Ctrl+C handler).
    /// </summary>
    public CancellationToken Token => _cancellationTokenSource.Token;

    /// <summary>
    /// Starts and registers a child test-app process as one operation with respect to forceful
    /// cancellation. Returns <see langword="null"/> without starting a process when forceful
    /// cancellation has already begun.
    /// </summary>
    public Process? StartAndRegister(Func<Process?> startProcess)
    {
        ArgumentNullException.ThrowIfNull(startProcess);

        lock (_processesLock)
        {
            if (Volatile.Read(ref _state) == StateForcing)
            {
                return null;
            }

            Process? process = startProcess();
            if (process is not null)
            {
                _processes.Add(process);
            }

            return process;
        }
    }

    public void Unregister(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);

        lock (_processesLock)
        {
            _processes.Remove(process);
        }
    }

    /// <summary>
    /// Test-only hook to drive the state machine without depending on a real Console signal.
    /// </summary>
    internal void SimulateCtrlC() => HandleCtrlC();

    internal bool IsForcing => Volatile.Read(ref _state) == StateForcing;

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
        if (HandleCooperativeCancellation())
        {
            return;
        }

        if (HandleForcefulCancellation(forwardSigTerm: false))
        {
            _exitAction(ExitCode.TestSessionAborted);
        }
    }

    private bool HandleCooperativeCancellation()
    {
        // First press: Idle -> Cancelling. Signal the token, invoke the UI callback.
        if (Interlocked.CompareExchange(ref _state, StateCancelling, StateIdle) != StateIdle)
        {
            return false;
        }

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

        return true;
    }

    private bool HandleForcefulCancellation(bool forwardSigTerm)
    {
        // A second Ctrl+C transitions Cancelling -> Forcing. A forceful lifecycle signal such as
        // SIGTERM can transition directly from Idle -> Forcing.
        if (forwardSigTerm)
        {
            Volatile.Write(ref _forwardSigTerm, 1);
        }

        int previousState = Interlocked.Exchange(ref _state, StateForcing);
        if (previousState == StateForcing)
        {
            return false;
        }

        lock (_processesLock)
        {
            TerminateProcesses([.. _processes], forwardSigTerm);
        }

        return true;
    }

    private static void TerminateProcesses(IReadOnlyCollection<Process> processes, bool forwardSigTerm)
    {
        if (forwardSigTerm)
        {
            ProcessReaper.ForwardSigTermAndTerminate(processes);
            return;
        }

        foreach (Process process in processes)
        {
            TryKill(process);
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

        _cooperativeCancellationRegistration.Dispose();
        _forcefulCancellationRegistration.Dispose();
        _cancellationTokenSource.Dispose();
    }
}
