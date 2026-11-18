// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils;

internal static class ProcessLifecycle
{
    private const int SigIntExitCode = 130;
    private const int SigTermExitCode = 143;
    private static readonly TimeSpan s_defaultSoftCancellationTimeout = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan s_forcefulExitFallbackTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly object s_lock = new();
    private static readonly CancellationTokenSource s_cancellationTokenSource = new();
    private static readonly CancellationTokenSource s_forcefulCancellationTokenSource = new();
    private static readonly Dictionary<int, TimeSpan> s_softCancellationTimeoutOverrides = [];
#if NET
    private static PosixSignalRegistration? s_sigTermRegistration;
#endif
    private static Timer? s_forcefulCancellationTimer;
    private static Timer? s_forcefulExitTimer;
    private static int s_initializationCount;
    private static int s_nextTimeoutOverrideId;
    private static int s_forcefulCancellationTimerVersion;
    private static int s_cancellationRequested;
    private static int s_forcefulCancellationRequested;
    private static int s_signalExitCode;

    public static CancellationToken CancellationToken => s_cancellationTokenSource.Token;

    internal static CancellationToken ForcefulCancellationToken => s_forcefulCancellationTokenSource.Token;

    internal static bool IsSignalTerminationRequested => Volatile.Read(ref s_signalExitCode) != 0;

    internal static bool IsSigTermRequested => Volatile.Read(ref s_signalExitCode) == SigTermExitCode;

    internal static bool IsForcefulCancellationRequested => Volatile.Read(ref s_forcefulCancellationRequested) != 0;

    internal static bool IsInitialized => Volatile.Read(ref s_initializationCount) != 0;

    internal static IDisposable Initialize()
    {
        lock (s_lock)
        {
            if (s_initializationCount++ == 0)
            {
                Console.CancelKeyPress += HandleCancelKeyPress;
                AppDomain.CurrentDomain.ProcessExit += HandleProcessExit;
#if NET
                s_sigTermRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, HandleSigTerm);
#endif
            }
        }

        return new Initialization();
    }

    internal static IDisposable OverrideSoftCancellationTimeout(TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        lock (s_lock)
        {
            int id = ++s_nextTimeoutOverrideId;
            s_softCancellationTimeoutOverrides.Add(id, timeout);
            ScheduleForcefulCancellation_NoLock();
            return new SoftCancellationTimeoutOverride(id);
        }
    }

    internal static CancellationToken GetProcessTerminationToken(CancellationToken cancellationToken)
        // System.CommandLine links the invocation token before passing it to command
        // actions, so lifecycle-originated tokens do not retain identity here.
        => IsInitialized
            ? s_forcefulCancellationTokenSource.Token
            : cancellationToken;

    internal static bool TryGetSignalExitCode(CancellationToken cancellationToken, out int exitCode)
    {
        exitCode = Volatile.Read(ref s_signalExitCode);
        return cancellationToken == s_cancellationTokenSource.Token && exitCode != 0;
    }

    private static void HandleCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
    {
        eventArgs.Cancel = true;

        if (!RequestSoftCancellation(SigIntExitCode))
        {
            RequestForcefulCancellation(SigIntExitCode, terminateProcess: true);
        }
    }

#if NET
    private static void HandleSigTerm(PosixSignalContext context)
    {
        context.Cancel = true;
        RequestForcefulCancellation(SigTermExitCode, terminateProcess: true);
    }
#endif

    private static void HandleProcessExit(object? sender, EventArgs eventArgs)
    {
        bool cancel;
        lock (s_lock)
        {
            cancel = s_cancellationRequested == 0;
            s_cancellationRequested = 1;
        }

        if (cancel)
        {
            s_cancellationTokenSource.Cancel();
        }
    }

    private static bool RequestSoftCancellation(int exitCode)
    {
        lock (s_lock)
        {
            if (s_cancellationRequested != 0)
            {
                return false;
            }

            s_cancellationRequested = 1;
            s_signalExitCode = exitCode;
            ScheduleForcefulCancellation_NoLock();
        }

        s_cancellationTokenSource.Cancel();
        return true;
    }

    private static void RequestForcefulCancellation(int exitCode, bool terminateProcess)
    {
        bool cancel = false;
        bool exitImmediately = false;
        lock (s_lock)
        {
            if (s_forcefulCancellationRequested != 0)
            {
                if (terminateProcess)
                {
                    s_signalExitCode = exitCode;
                    exitImmediately = true;
                }
            }
            else
            {
                s_cancellationRequested = 1;
                s_forcefulCancellationRequested = 1;
                s_signalExitCode = exitCode;
                CancelForcefulCancellationTimer_NoLock();
                cancel = true;
            }
        }

        if (exitImmediately)
        {
            Environment.Exit(exitCode);
        }

        if (cancel)
        {
            try
            {
                s_forcefulCancellationTokenSource.Cancel();
                if (!terminateProcess)
                {
                    s_cancellationTokenSource.Cancel();
                }
            }
            finally
            {
                if (terminateProcess)
                {
                    Environment.Exit(exitCode);
                }
            }
        }
    }

    private static void ScheduleForcefulCancellation_NoLock()
    {
        if (s_cancellationRequested == 0 || s_forcefulCancellationRequested != 0 || s_signalExitCode == 0)
        {
            return;
        }

        TimeSpan timeout = s_defaultSoftCancellationTimeout;
        foreach (TimeSpan timeoutOverride in s_softCancellationTimeoutOverrides.Values)
        {
            if (timeoutOverride == Timeout.InfiniteTimeSpan)
            {
                timeout = Timeout.InfiniteTimeSpan;
                break;
            }

            if (timeoutOverride > timeout)
            {
                timeout = timeoutOverride;
            }
        }

        CancelForcefulCancellationTimer_NoLock();
        if (timeout != Timeout.InfiniteTimeSpan)
        {
            int version = s_forcefulCancellationTimerVersion;
            s_forcefulCancellationTimer = new Timer(
                static state => ForceAfterSoftCancellationTimeout((int)state!),
                version,
                timeout,
                Timeout.InfiniteTimeSpan);
        }
    }

    private static void ForceAfterSoftCancellationTimeout(int version)
    {
        int exitCode;
        lock (s_lock)
        {
            if (version != s_forcefulCancellationTimerVersion ||
                s_forcefulCancellationRequested != 0 ||
                s_signalExitCode == 0)
            {
                return;
            }

            s_forcefulCancellationRequested = 1;
            exitCode = s_signalExitCode;
            CancelForcefulCancellationTimer_NoLock();
            // Give forceful-token observers a bounded chance to terminate and reap child processes.
            s_forcefulExitTimer = new Timer(
                static state => Environment.Exit((int)state!),
                exitCode,
                s_forcefulExitFallbackTimeout,
                Timeout.InfiniteTimeSpan);
        }

        try
        {
            s_forcefulCancellationTokenSource.Cancel();
        }
        catch (AggregateException)
        {
            Environment.Exit(exitCode);
        }
    }

    private static void CancelForcefulCancellationTimer_NoLock()
    {
        s_forcefulCancellationTimerVersion++;
        s_forcefulCancellationTimer?.Dispose();
        s_forcefulCancellationTimer = null;
    }

    private sealed class Initialization : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            lock (s_lock)
            {
                if (--s_initializationCount == 0)
                {
                    Console.CancelKeyPress -= HandleCancelKeyPress;
                    AppDomain.CurrentDomain.ProcessExit -= HandleProcessExit;
#if NET
                    s_sigTermRegistration?.Dispose();
                    s_sigTermRegistration = null;
#endif
                }
            }
        }
    }

    private sealed class SoftCancellationTimeoutOverride(int id) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            lock (s_lock)
            {
                s_softCancellationTimeoutOverrides.Remove(id);
                ScheduleForcefulCancellation_NoLock();
            }
        }
    }
}
