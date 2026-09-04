// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test;

internal enum TestRunCancellationReason
{
    None,
    MaximumFailedTests,
    Timeout,
}

internal sealed class TestRunPolicy : IDisposable
{
    private const int CompletedState = -1;

    internal static readonly TimeSpan DefaultCancellationGracePeriod = TimeSpan.FromSeconds(30);

    private readonly int? _maximumFailedTests;
    private readonly TimeSpan? _timeout;
    private readonly TimeProvider _timeProvider;
    private readonly Action<TestRunCancellationReason>? _onCancellation;
    private readonly TaskCompletionSource<TestRunCancellationReason> _cancellation =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Lock _timerLock = new();

    private ITimer? _timer;
    private TimeSpan? _remainingTimeout;
    private long _timerStartedTimestamp;
    private int _activeTestApplications;
    private int _failedTests;
    private int _reason;
    private bool _disposed;

    public TestRunPolicy(
        int? maximumFailedTests,
        TimeSpan? timeout,
        TimeSpan? cancellationGracePeriod = null,
        Action<TestRunCancellationReason>? onCancellation = null,
        TimeProvider? timeProvider = null)
    {
        if (maximumFailedTests is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumFailedTests));
        }

        if (timeout.HasValue && timeout.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        _maximumFailedTests = maximumFailedTests;
        _timeout = timeout;
        _remainingTimeout = timeout;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _onCancellation = onCancellation;
        CancellationGracePeriod = cancellationGracePeriod ?? DefaultCancellationGracePeriod;
    }

    public CancellationToken Token => _cancellationTokenSource.Token;

    public Task<TestRunCancellationReason> Cancellation => _cancellation.Task;

    public TestRunCancellationReason Reason
    {
        get
        {
            int reason = Volatile.Read(ref _reason);
            return reason == CompletedState
                ? TestRunCancellationReason.None
                : (TestRunCancellationReason)reason;
        }
    }

    public int FailedTests => Volatile.Read(ref _failedTests);

    public TimeSpan CancellationGracePeriod { get; }

    public void OnTestApplicationStarted()
    {
        if (_timeout is null)
        {
            return;
        }

        lock (_timerLock)
        {
            _activeTestApplications++;
            if (_activeTestApplications == 1 &&
                !_disposed &&
                Reason == TestRunCancellationReason.None &&
                _remainingTimeout is { } remainingTimeout)
            {
                _timerStartedTimestamp = _timeProvider.GetTimestamp();

                // A concurrent OnTestApplicationExited can drive _remainingTimeout non-positive a
                // moment before it flips Reason to Timeout, so clamp to avoid passing a negative due
                // time to Timer (which throws ArgumentOutOfRangeException). A zero due time fires
                // immediately and OnTimeout performs the cancellation.
                TimeSpan dueTime = remainingTimeout > TimeSpan.Zero ? remainingTimeout : TimeSpan.Zero;
                _timer = _timeProvider.CreateTimer(
                    static state => ((TestRunPolicy)state!).OnTimeout(),
                    this,
                    dueTime,
                    Timeout.InfiniteTimeSpan);
            }
        }
    }

    public void OnTestApplicationExited()
    {
        bool timeoutReached = false;
        lock (_timerLock)
        {
            if (_timeout is null)
            {
                return;
            }

            if (_activeTestApplications <= 0)
            {
                throw new InvalidOperationException("A test application exited without a matching start.");
            }

            _activeTestApplications--;
            if (_activeTestApplications == 0 && _timer is not null)
            {
                _timer.Dispose();
                _timer = null;

                TimeSpan elapsed = _timeProvider.GetElapsedTime(_timerStartedTimestamp);
                _remainingTimeout -= elapsed;
                timeoutReached = _remainingTimeout <= TimeSpan.Zero;
            }
        }

        if (timeoutReached)
        {
            TryCancel(TestRunCancellationReason.Timeout);
        }
    }

    public void ReportFailedTests(int count)
    {
        if (count <= 0)
        {
            return;
        }

        int failedTests = Interlocked.Add(ref _failedTests, count);
        if (_maximumFailedTests is { } maximumFailedTests && failedTests >= maximumFailedTests)
        {
            TryCancel(TestRunCancellationReason.MaximumFailedTests);
        }
    }

    public TestRunCancellationReason Complete()
    {
        int state = Interlocked.CompareExchange(
            ref _reason,
            CompletedState,
            (int)TestRunCancellationReason.None);

        lock (_timerLock)
        {
            _timer?.Dispose();
            _timer = null;
        }

        return state is (int)TestRunCancellationReason.None or CompletedState
            ? TestRunCancellationReason.None
            : (TestRunCancellationReason)state;
    }

    private void TryCancel(TestRunCancellationReason reason)
    {
        if (Interlocked.CompareExchange(ref _reason, (int)reason, (int)TestRunCancellationReason.None) !=
            (int)TestRunCancellationReason.None)
        {
            return;
        }

        _onCancellation?.Invoke(reason);
        _cancellation.TrySetResult(reason);
        _cancellationTokenSource.Cancel();
    }

    private void OnTimeout()
    {
        lock (_timerLock)
        {
            if (_activeTestApplications > 0 && !_disposed)
            {
                TryCancel(TestRunCancellationReason.Timeout);
            }
        }
    }

    public void Dispose()
    {
        Complete();
        lock (_timerLock)
        {
            _disposed = true;
        }

        _cancellationTokenSource.Dispose();
    }
}
