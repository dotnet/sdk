// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Supplies monotonic timing and bounded synchronous backoff for lock contention.
/// Only time spent contending for this policy's lock is charged to its timeout.
/// </summary>
internal class LockFileRetryPolicy
{
    private const int InitialDelayMilliseconds = 25;
    private const int MaximumDelayMilliseconds = 250;
    private const int MaximumBackoffExponent = 4;
    private const double MinimumJitterFactor = 0.5;

    public LockFileRetryPolicy(TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);
        Timeout = timeout;
    }

    public TimeSpan Timeout { get; }

    public virtual long GetTimestamp() => Stopwatch.GetTimestamp();

    public virtual TimeSpan GetElapsedTime(long startingTimestamp) => Stopwatch.GetElapsedTime(startingTimestamp);

    public void WaitAfterContention(
        long attemptStart,
        ref TimeSpan contention,
        int attempt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        contention += GetElapsedTime(attemptStart);
        if (contention >= Timeout)
        {
            throw new TimeoutException();
        }

        var waitStart = GetTimestamp();
        WaitBeforeRetry(attempt, Timeout - contention, cancellationToken);
        contention += GetElapsedTime(waitStart);
        cancellationToken.ThrowIfCancellationRequested();
        if (contention >= Timeout)
        {
            throw new TimeoutException();
        }
    }

    public virtual void WaitBeforeRetry(int attempt, TimeSpan remaining, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var maximumMilliseconds = Math.Min(MaximumDelayMilliseconds,
            InitialDelayMilliseconds * (1 << Math.Clamp(attempt, 0, MaximumBackoffExponent)));
        var milliseconds = maximumMilliseconds *
            (MinimumJitterFactor + (Random.Shared.NextDouble() * (1 - MinimumJitterFactor)));
        var delay = TimeSpan.FromMilliseconds(Math.Min(milliseconds, remaining.TotalMilliseconds));
        if (cancellationToken.WaitHandle.WaitOne(delay))
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}