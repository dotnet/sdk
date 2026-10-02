// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class SelfUpdateCoordinatorTests : SdkTest
{
    private DirectoryInfo _directory = null!;
    private string _updatePath = null!;
    private string _activityPath = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Directory.CreateTempSubdirectory("self-update-locks-");
        _updatePath = Path.Combine(_directory.FullName, "update.lock");
        _activityPath = Path.Combine(_directory.FullName, "activity.lock");
    }

    [TestCleanup]
    public void Cleanup() => _directory.Delete(recursive: true);

    [TestMethod]
    public void LeaseHoldsBothLocksUntilIdempotentDispose()
    {
        using var lease = new SelfUpdateCoordinator().Acquire(_updatePath, _activityPath, TestContext.CancellationToken);
        using var update = ScopedLockFile.TryAcquireExclusive(_updatePath);
        using var activity = ScopedLockFile.TryAcquireShared(_activityPath);
        Assert.IsNull(update);
        Assert.IsNull(activity);
        lease.Dispose();
        lease.Dispose();
        AssertBothAvailable();
        Assert.AreEqual(0L, new FileInfo(_updatePath).Length);
        Assert.AreEqual(0L, new FileInfo(_activityPath).Length);
    }

    [TestMethod]
    public void ActivityContentionReleasesUpdateBeforeWaitingThenRetriesPair()
    {
        using var activeCommand = ScopedLockFile.TryAcquireShared(_activityPath);
        Assert.IsNotNull(activeCommand);
        using var activityPolicy = new LockFileTestRetryPolicy(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1))
        {
            VerifyReleasedPath = _updatePath,
            ReleaseOnWait = activeCommand,
        };

        using var lease = new SelfUpdateCoordinator(activityRetryPolicy: activityPolicy).Acquire(_updatePath, _activityPath, TestContext.CancellationToken);
        Assert.HasCount(1, activityPolicy.RemainingBudgets);
    }

    [TestMethod]
    public void ActivityBudgetIsCumulativeAcrossUpdateContention()
    {
        using var activeCommand = ScopedLockFile.TryAcquireShared(_activityPath);
        using var activityPolicy = new LockFileTestRetryPolicy(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1))
        {
            VerifyReleasedPath = _updatePath,
            TakeLockOnWaitPath = _updatePath,
        };
        using var updatePolicy = new LockFileTestRetryPolicy(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5))
        {
            ReleasePeerOnWait = activityPolicy,
        };

        var exception = Assert.ThrowsExactly<SelfUpdateLockTimeoutException>(
            () => new SelfUpdateCoordinator(updatePolicy, activityPolicy).Acquire(_updatePath, _activityPath, TestContext.CancellationToken));

        Assert.AreEqual(SelfUpdateLockKind.Activity, exception.LockKind);
        Assert.AreSequenceEqual(new[] { TimeSpan.FromSeconds(10) }, updatePolicy.RemainingBudgets);
        Assert.AreSequenceEqual(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1) }, activityPolicy.RemainingBudgets);
    }

    [TestMethod]
    public void UpdateBudgetIsCumulativeAcrossActivityRetries()
    {
        using var activeCommand = ScopedLockFile.TryAcquireShared(_activityPath);
        using var activityPolicy = new LockFileTestRetryPolicy(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1))
        {
            VerifyReleasedPath = _updatePath,
            TakeLockOnWaitPath = _updatePath,
        };
        using var updatePolicy = new LockFileTestRetryPolicy(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5))
        {
            ReleasePeerOnWait = activityPolicy,
        };
        var exception = Assert.ThrowsExactly<SelfUpdateLockTimeoutException>(
            () => new SelfUpdateCoordinator(updatePolicy, activityPolicy).Acquire(_updatePath, _activityPath, TestContext.CancellationToken));

        Assert.AreEqual(SelfUpdateLockKind.Update, exception.LockKind);
        Assert.AreSequenceEqual(new[] { TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5) }, updatePolicy.RemainingBudgets);
        Assert.AreSequenceEqual(new[] { TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2) }, activityPolicy.RemainingBudgets);
    }

    [TestMethod]
    public void ActivityTimeoutReleasesUpdateAndReportsActivity()
    {
        using var activeCommand = ScopedLockFile.TryAcquireShared(_activityPath);
        using var activityPolicy = new LockFileTestRetryPolicy(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1))
        {
            VerifyReleasedPath = _updatePath,
        };
        var exception = Assert.ThrowsExactly<SelfUpdateLockTimeoutException>(
            () => new SelfUpdateCoordinator(activityRetryPolicy: activityPolicy).Acquire(_updatePath, _activityPath, TestContext.CancellationToken));

        Assert.AreEqual(SelfUpdateLockKind.Activity, exception.LockKind);
        Assert.AreEqual(_activityPath, exception.LockPath);
        Assert.AreSequenceEqual(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1) }, activityPolicy.RemainingBudgets);
        using var update = ScopedLockFile.TryAcquireExclusive(_updatePath);
        Assert.IsNotNull(update);
    }

    [TestMethod]
    public void UpdateContentionNeverAcquiresActivityAndUsesOnlyUpdateBudget()
    {
        using var peerUpdate = ScopedLockFile.TryAcquireExclusive(_updatePath);
        using var updatePolicy = new LockFileTestRetryPolicy(TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(60));
        using var activityPolicy = new LockFileTestRetryPolicy(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));
        var exception = Assert.ThrowsExactly<SelfUpdateLockTimeoutException>(
            () => new SelfUpdateCoordinator(updatePolicy, activityPolicy).Acquire(_updatePath, _activityPath, TestContext.CancellationToken));

        Assert.AreEqual(SelfUpdateLockKind.Update, exception.LockKind);
        Assert.AreEqual(_updatePath, exception.LockPath);
        Assert.IsFalse(File.Exists(_activityPath));
        Assert.IsEmpty(activityPolicy.RemainingBudgets);
        Assert.AreSequenceEqual(new[] { TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(60) }, updatePolicy.RemainingBudgets);
    }

    [TestMethod]
    public void UpdateContentionRetriesSuccessfullyAfterPeerReleases()
    {
        using var peerUpdate = ScopedLockFile.TryAcquireExclusive(_updatePath);
        using var updatePolicy = new LockFileTestRetryPolicy(TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(60))
        {
            ReleaseOnWait = peerUpdate,
        };
        using var lease = new SelfUpdateCoordinator(updatePolicy).Acquire(_updatePath, _activityPath, TestContext.CancellationToken);
        Assert.HasCount(1, updatePolicy.RemainingBudgets);
    }

    [TestMethod]
    public void ActivityOpenFailureReleasesUpdateAndIsNotRetried()
    {
        using var policy = new LockFileTestRetryPolicy(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));
        Assert.ThrowsExactly<DirectoryNotFoundException>(() => new SelfUpdateCoordinator(activityRetryPolicy: policy)
            .Acquire(_updatePath, Path.Combine(_directory.FullName, "missing", "activity.lock"), TestContext.CancellationToken));
        Assert.IsEmpty(policy.RemainingBudgets);
        using var update = ScopedLockFile.TryAcquireExclusive(_updatePath);
        Assert.IsNotNull(update);
    }

    [TestMethod]
    public void CancellationBeforeAcquisitionCreatesNoFiles()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => new SelfUpdateCoordinator()
            .Acquire(_updatePath, _activityPath, cancellation.Token));
        Assert.IsFalse(File.Exists(_updatePath));
        Assert.IsFalse(File.Exists(_activityPath));
    }

    [TestMethod]
    public void CancellationDuringActivityWaitLeavesNoUpdateLock()
    {
        using var cancellation = new CancellationTokenSource();
        using var activeCommand = ScopedLockFile.TryAcquireShared(_activityPath);
        using var policy = new LockFileTestRetryPolicy(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1))
        {
            VerifyReleasedPath = _updatePath,
            CancelOnWait = cancellation,
        };
        Assert.ThrowsExactly<OperationCanceledException>(() => new SelfUpdateCoordinator(activityRetryPolicy: policy)
            .Acquire(_updatePath, _activityPath, cancellation.Token));
        using var update = ScopedLockFile.TryAcquireExclusive(_updatePath);
        Assert.IsNotNull(update);
    }

    [TestMethod]
    public void WaitFailureLeavesNoUpdateLock()
    {
        using var activeCommand = ScopedLockFile.TryAcquireShared(_activityPath);
        using var policy = new LockFileTestRetryPolicy(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1))
        {
            VerifyReleasedPath = _updatePath,
            FailOnWait = true,
        };
        Assert.ThrowsExactly<InvalidOperationException>(() => new SelfUpdateCoordinator(activityRetryPolicy: policy)
            .Acquire(_updatePath, _activityPath, TestContext.CancellationToken));
        using var update = ScopedLockFile.TryAcquireExclusive(_updatePath);
        Assert.IsNotNull(update);
    }

    private void AssertBothAvailable()
    {
        using var update = ScopedLockFile.TryAcquireExclusive(_updatePath);
        using var activity = ScopedLockFile.TryAcquireExclusive(_activityPath);
        Assert.IsNotNull(update);
        Assert.IsNotNull(activity);
    }

    [TestMethod]
    public void ZeroBudgetAllowsImmediateSuccessButNeverWaitsForContention()
    {
        using var policy = new LockFileTestRetryPolicy(TimeSpan.Zero, TimeSpan.Zero);
        var coordinator = new SelfUpdateCoordinator(policy, policy);
        using (var lease = coordinator.Acquire(_updatePath, _activityPath, TestContext.CancellationToken))
        {
            var exception = Assert.ThrowsExactly<SelfUpdateLockTimeoutException>(
                () => coordinator.Acquire(_updatePath, _activityPath, TestContext.CancellationToken));
            Assert.AreEqual(SelfUpdateLockKind.Update, exception.LockKind);
        }

        using var activeCommand = ScopedLockFile.TryAcquireShared(_activityPath);
        var activityException = Assert.ThrowsExactly<SelfUpdateLockTimeoutException>(
            () => coordinator.Acquire(_updatePath, _activityPath, TestContext.CancellationToken));
        Assert.AreEqual(SelfUpdateLockKind.Activity, activityException.LockKind);
        Assert.IsEmpty(policy.RemainingBudgets);
    }

    [TestMethod]
    public void CancellationDuringUpdateWaitNeverTouchesActivity()
    {
        using var cancellation = new CancellationTokenSource();
        using var peer = ScopedLockFile.TryAcquireExclusive(_updatePath);
        using var policy = new LockFileTestRetryPolicy(TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(1))
        {
            CancelOnWait = cancellation,
        };
        Assert.ThrowsExactly<OperationCanceledException>(() => new SelfUpdateCoordinator(policy)
            .Acquire(_updatePath, _activityPath, cancellation.Token));
        Assert.IsFalse(File.Exists(_activityPath));
    }

    [TestMethod]
    public void RetryPolicyRejectsNegativeTimeout()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LockFileRetryPolicy(TimeSpan.FromTicks(-1)));
    }
}