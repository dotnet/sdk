// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils;

internal static class ProcessLifecycle
{
    private static readonly CancellationTokenSource s_cancellationTokenSource = new();
    private static int s_cancelKeyPressCancellationSuppressionCount;

    static ProcessLifecycle()
    {
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;

            if (Volatile.Read(ref s_cancelKeyPressCancellationSuppressionCount) == 0 &&
                !s_cancellationTokenSource.IsCancellationRequested)
            {
                s_cancellationTokenSource.Cancel();
            }
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => s_cancellationTokenSource.Cancel();
    }

    public static CancellationToken CancellationToken => s_cancellationTokenSource.Token;

    internal static IDisposable SuppressCancelKeyPressCancellation()
    {
        Interlocked.Increment(ref s_cancelKeyPressCancellationSuppressionCount);
        return new CancelKeyPressCancellationSuppression();
    }

    private sealed class CancelKeyPressCancellationSuppression : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                Interlocked.Decrement(ref s_cancelKeyPressCancellationSuppressionCount);
            }
        }
    }
}
