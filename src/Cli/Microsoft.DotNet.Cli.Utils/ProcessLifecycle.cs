// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils;

internal static class ProcessLifecycle
{
    private const int SigIntExitCode = 130;
    private const int SigTermExitCode = 143;
    private static readonly TimeSpan s_forcedTerminationTimeout = TimeSpan.FromSeconds(2);
    private static readonly CancellationTokenSource s_cancellationTokenSource = new();
#if NET
    private static readonly PosixSignalRegistration s_sigTermRegistration;
#endif
    private static int s_terminationCancellationSuppressionCount;
    private static int s_cancellationRequested;
    private static int s_signalTerminationRequested;
    private static Timer? s_forcedTerminationTimer;

    static ProcessLifecycle()
    {
#if NET
        s_sigTermRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
            if (Volatile.Read(ref s_terminationCancellationSuppressionCount) != 0)
            {
                // The active process reaper forwards SIGTERM during ProcessExit. Exit without
                // cancelling so command cancellation cannot kill the child before it handles
                // the forwarded signal.
                if (Interlocked.Exchange(ref s_cancellationRequested, 1) == 0)
                {
                    Volatile.Write(ref s_signalTerminationRequested, 1);
                    context.Cancel = true;
                    Environment.Exit(SigTermExitCode);
                }

                return;
            }

            if (RequestCancellation(SigTermExitCode))
            {
                context.Cancel = true;
            }
        });
#endif

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            if (Volatile.Read(ref s_terminationCancellationSuppressionCount) != 0)
            {
                eventArgs.Cancel = true;
                return;
            }

            if (RequestCancellation(SigIntExitCode))
            {
                eventArgs.Cancel = true;
            }
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            if (Volatile.Read(ref s_terminationCancellationSuppressionCount) != 0)
            {
                // ProcessReaper owns child shutdown while its suppression lease is active.
                Interlocked.Exchange(ref s_cancellationRequested, 1);
                return;
            }

            RequestCancellation(forcedTerminationExitCode: null);
        };
    }

    public static CancellationToken CancellationToken => s_cancellationTokenSource.Token;

    internal static bool IsSignalTerminationRequested => Volatile.Read(ref s_signalTerminationRequested) != 0;

    internal static TimeSpan SignalTerminationTimeout => s_forcedTerminationTimeout;

    internal static IDisposable SuppressTerminationCancellation()
    {
        Interlocked.Increment(ref s_terminationCancellationSuppressionCount);
        return new TerminationCancellationSuppression();
    }

    private static bool RequestCancellation(int? forcedTerminationExitCode)
    {
        if (Interlocked.Exchange(ref s_cancellationRequested, 1) != 0)
        {
            return false;
        }

        if (forcedTerminationExitCode is int exitCode)
        {
            s_forcedTerminationTimer = new Timer(
                static state => Environment.Exit((int)state!),
                exitCode,
                s_forcedTerminationTimeout,
                Timeout.InfiniteTimeSpan);
        }

        s_cancellationTokenSource.Cancel();
        return true;
    }

    private sealed class TerminationCancellationSuppression : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                Interlocked.Decrement(ref s_terminationCancellationSuppressionCount);
            }
        }
    }
}
