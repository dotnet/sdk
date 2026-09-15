// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>
/// Acquires update then activity in caller-owned directories, releasing update before any retry.
/// Update and activity contention have independent cumulative budgets for each acquisition.
/// </summary>
internal sealed class SelfUpdateCoordinator
{
    private readonly LockFileRetryPolicy _updateRetryPolicy;
    private readonly LockFileRetryPolicy _activityRetryPolicy;

    public SelfUpdateCoordinator(
        LockFileRetryPolicy? updateRetryPolicy = null,
        LockFileRetryPolicy? activityRetryPolicy = null)
    {
        _updateRetryPolicy = updateRetryPolicy ?? new LockFileRetryPolicy(TimeSpan.FromMinutes(2));
        _activityRetryPolicy = activityRetryPolicy ?? new LockFileRetryPolicy(TimeSpan.FromSeconds(2));
    }

    public SelfUpdateLockLease Acquire(string updateLockPath, string activityLockPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(updateLockPath);
        ArgumentException.ThrowIfNullOrEmpty(activityLockPath);

        var updateContention = TimeSpan.Zero;
        var activityContention = TimeSpan.Zero;
        var updateAttempt = 0;
        var activityAttempt = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var updateStart = _updateRetryPolicy.GetTimestamp();
            var updateLock = ScopedLockFile.TryAcquireExclusive(updateLockPath);
            if (updateLock is null)
            {
                WaitAfterContention(_updateRetryPolicy, updateStart, ref updateContention, updateAttempt++,
                    SelfUpdateLockKind.Update, updateLockPath, cancellationToken);
                continue;
            }

            ScopedLockFile? activityLock = null;
            long activityStart;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                activityStart = _activityRetryPolicy.GetTimestamp();
                activityLock = ScopedLockFile.TryAcquireExclusive(activityLockPath);
                if (activityLock is not null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var lease = new SelfUpdateLockLease(updateLock, activityLock);
                    updateLock = null;
                    activityLock = null;
                    return lease;
                }
            }
            finally
            {
                try
                {
                    activityLock?.Dispose();
                }
                finally
                {
                    updateLock?.Dispose();
                }
            }

            WaitAfterContention(_activityRetryPolicy, activityStart, ref activityContention, activityAttempt++,
                SelfUpdateLockKind.Activity, activityLockPath, cancellationToken);
        }
    }

    private static void WaitAfterContention(
        LockFileRetryPolicy policy,
        long attemptStart,
        ref TimeSpan contention,
        int attempt,
        SelfUpdateLockKind lockKind,
        string lockPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        contention += policy.GetElapsedTime(attemptStart);
        if (contention >= policy.Timeout)
        {
            throw new SelfUpdateLockTimeoutException(lockKind, lockPath);
        }

        var waitStart = policy.GetTimestamp();
        policy.WaitBeforeRetry(attempt, policy.Timeout - contention, cancellationToken);
        contention += policy.GetElapsedTime(waitStart);
        cancellationToken.ThrowIfCancellationRequested();
        if (contention >= policy.Timeout)
        {
            throw new SelfUpdateLockTimeoutException(lockKind, lockPath);
        }
    }
}