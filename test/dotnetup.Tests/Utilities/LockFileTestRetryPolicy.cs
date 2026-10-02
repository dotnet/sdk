// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

internal sealed class LockFileTestRetryPolicy(TimeSpan timeout, TimeSpan step) : LockFileRetryPolicy(timeout), IDisposable
{
    private TimeSpan _elapsed;

    public List<TimeSpan> RemainingBudgets { get; } = [];

    public string? VerifyReleasedPath { get; set; }

    public string? TakeLockOnWaitPath { get; set; }

    public ScopedLockFile? TakenLock { get; private set; }

    public ScopedLockFile? ReleaseOnWait { get; set; }

    public LockFileTestRetryPolicy? ReleasePeerOnWait { get; set; }

    public CancellationTokenSource? CancelOnWait { get; set; }

    public bool FailOnWait { get; set; }

    public override long GetTimestamp() => _elapsed.Ticks;

    public override TimeSpan GetElapsedTime(long startingTimestamp) => _elapsed - TimeSpan.FromTicks(startingTimestamp);

    public override void WaitBeforeRetry(int attempt, TimeSpan remaining, CancellationToken cancellationToken)
    {
        RemainingBudgets.Add(remaining);
        if (VerifyReleasedPath is not null)
        {
            using var available = ScopedLockFile.TryAcquireExclusive(VerifyReleasedPath);
            Assert.IsNotNull(available, "The update lock must be released before waiting for activity.");
        }

        ReleaseOnWait?.Dispose();
        ReleaseOnWait = null;
        ReleasePeerOnWait?.TakenLock?.Dispose();
        if (TakeLockOnWaitPath is not null)
        {
            TakenLock = ScopedLockFile.TryAcquireExclusive(TakeLockOnWaitPath);
            Assert.IsNotNull(TakenLock);
        }

        CancelOnWait?.Cancel();
        cancellationToken.ThrowIfCancellationRequested();
        if (FailOnWait)
        {
            throw new InvalidOperationException("Injected wait failure.");
        }

        _elapsed += step < remaining ? step : remaining;
    }

    public void Dispose() => TakenLock?.Dispose();
}