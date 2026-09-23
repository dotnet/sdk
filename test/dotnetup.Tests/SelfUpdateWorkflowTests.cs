// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Runtime.Versioning;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class SelfUpdateWorkflowTests : SdkTest
{
    [TestMethod]
    public void MissingLeaseOwnerIsRejectedBeforeResolvingRelease()
    {
        using var files = new SelfUpdateTestFiles();
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalVersion,
            () => throw new InvalidOperationException("Release resolution must not run without a lease owner."),
            (release, destination) => Assert.Fail("Download must not run without a lease owner."));

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => workflow.Execute(null!));

        Assert.AreEqual("retainUntilExit", exception.ParamName);
        Assert.IsFalse(File.Exists(files.Paths.UpdateLockPath));
        Assert.IsFalse(File.Exists(files.Paths.ActivityLockPath));
    }

    [TestMethod]
    public void FailedLeaseHandoffReleasesBothLocksBeforeDownload()
    {
        using var files = new SelfUpdateTestFiles();
        var failure = new InvalidOperationException("Injected lease handoff failure.");
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalVersion,
            () => CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementVersion),
            (release, destination) => Assert.Fail("Download must not run after a failed lease handoff."),
            CreateImmediateWorkflowCoordinator());

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => workflow.Execute(lease =>
        {
            AssertWorkflowLocksHeld(files.Paths);
            throw failure;
        }));

        Assert.AreSame(failure, exception);
        AssertWorkflowLocksAvailable(files.Paths);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AlreadyCurrentWithActivityOccupiedDoesNotAcquireUpdate(bool occupyUpdate)
    {
        using var files = new SelfUpdateTestFiles();
        using var activeCommand = ScopedLockFile.TryAcquireShared(files.Paths.ActivityLockPath);
        Assert.IsNotNull(activeCommand);
        using var occupiedUpdate = occupyUpdate ? ScopedLockFile.TryAcquireExclusive(files.Paths.UpdateLockPath) : null;
        if (occupyUpdate)
        {
            Assert.IsNotNull(occupiedUpdate);
        }

        var originalBytes = File.ReadAllBytes(files.Paths.InstalledPath);
        var stagedBytes = File.ReadAllBytes(files.Paths.StagedPath);
        var release = CreateWorkflowRelease(SelfUpdateTestFiles.OriginalVersion);
        var resolveCount = 0;
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalVersion,
            () => { resolveCount++; return release; },
            (download, path) => Assert.Fail("An already-current executable must not be downloaded."),
            CreateImmediateWorkflowCoordinator());

        Assert.IsNull(workflow.Execute(lease => Assert.Fail("A no-op must not transfer a lock lease.")));

        Assert.AreEqual(1, resolveCount);
        Assert.AreEqual(0, workflow.VerificationCount);
        Assert.AreEqual(occupyUpdate, File.Exists(files.Paths.UpdateLockPath));
        Assert.AreSequenceEqual(originalBytes, File.ReadAllBytes(files.Paths.InstalledPath));
        Assert.AreSequenceEqual(stagedBytes, File.ReadAllBytes(files.Paths.StagedPath));
        Assert.IsEmpty(Directory.GetFiles(files.Paths.DirectoryPath, "*.old.*"));
    }

    [TestMethod]
    [DataRow("0.2.0-preview.1.26465.7", "0.2.0-preview.1.26465.6")]
    [DataRow("0.3.0", "0.2.0")]
    [DataRow("0.2.0-preview.1.26465.7+commit", "0.2.0-preview.1.26465.7")]
    [DataRow("0.2.0-preview.1.26465.7", "0.2.0-preview.1.26465.7+commit")]
    [DataRow("0.2.0-preview.1.26465.7+build1", "0.2.0-preview.1.26465.7+build2")]
    public void SameChannelReleaseMustBeNewer(string installedVersion, string availableVersion)
    {
        using var files = new SelfUpdateTestFiles();
        SelfUpdateTestFiles.WriteExecutable(files.Paths.InstalledPath, installedVersion);
        var release = CreateWorkflowRelease(availableVersion);
        var workflow = new SelfUpdateTestWorkflow(files.Paths, installedVersion, () => release,
            (download, path) => Assert.Fail("An older release from the same channel must not be downloaded."),
            CreateImmediateWorkflowCoordinator());

        Assert.IsNull(workflow.Execute(lease => Assert.Fail("A no-op must not transfer a lock lease.")));
        Assert.AreEqual(0, workflow.VerificationCount);
        Assert.AreEqual(installedVersion, SelfUpdateVerifier.ReadVersion(files.Paths.InstalledPath));
        AssertWorkflowLocksAvailable(files.Paths);
    }

    [TestMethod]
    [DataRow("0.3.0-preview.1.26465.7", "0.2.0-dev.1")]
    [DataRow("1.0.0", "0.9.0-preview.1")]
    public void CrossChannelReleaseCanChangeSemanticVersionDirection(string installedVersion, string availableVersion)
    {
        using var files = new SelfUpdateTestFiles();
        SelfUpdateTestFiles.WriteExecutable(files.Paths.InstalledPath, installedVersion);
        var release = CreateWorkflowRelease(availableVersion);
        var workflow = new SelfUpdateTestWorkflow(files.Paths, installedVersion, () => release,
            (download, path) => SelfUpdateTestFiles.WriteExecutable(path, ReleaseVersionString(download)),
            CreateImmediateWorkflowCoordinator());

        Assert.AreEqual(availableVersion, workflow.Execute());
        Assert.AreEqual(1, workflow.VerificationCount);
        Assert.AreEqual(ReleaseVersionString(release), SelfUpdateVerifier.ReadVersion(files.Paths.InstalledPath));
        AssertWorkflowLocksAvailable(files.Paths);
    }

    [TestMethod]
    public void DownloadFailureDoesNotReplaceInstalledExecutable()
    {
        using var files = new SelfUpdateTestFiles();
        var originalBytes = File.ReadAllBytes(files.Paths.InstalledPath);
        var release = CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementVersion);
        var failure = new DotnetInstallException(DotnetInstallErrorCode.DownloadFailed, "Injected download failure.");
        var downloadCount = 0;
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalVersion, () => release,
            (download, path) =>
            {
                downloadCount++;
                Assert.AreSame(release, download);
                Assert.AreEqual(files.Paths.StagedPath, path);
                File.WriteAllText(path, "partial download");
                throw failure;
            }, CreateImmediateWorkflowCoordinator());

        var exception = Assert.ThrowsExactly<DotnetInstallException>(() => workflow.Execute());

        Assert.AreSame(failure, exception);
        Assert.AreEqual(1, downloadCount);
        Assert.AreEqual(0, workflow.VerificationCount);
        Assert.AreSequenceEqual(originalBytes, File.ReadAllBytes(files.Paths.InstalledPath));
        Assert.IsEmpty(Directory.GetFiles(files.Paths.DirectoryPath, "*.old.*"));
        AssertWorkflowLocksAvailable(files.Paths);
    }

    [TestMethod]
    public void SuccessfulReplacementVerifiesCanonicalWhileBothLocksAreHeld()
    {
        using var files = new SelfUpdateTestFiles();
        var originalBytes = File.ReadAllBytes(files.Paths.InstalledPath);
        var replacementBytes = File.ReadAllBytes(files.Paths.StagedPath);
        var release = CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementVersion);
        var resolveCount = 0;
        var downloadCount = 0;
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalVersion,
            () => { resolveCount++; return release; },
            (download, path) =>
            {
                downloadCount++;
                Assert.AreSame(release, download);
                Assert.AreEqual(files.Paths.StagedPath, path);
                Assert.IsFalse(File.Exists(path));
                AssertWorkflowLocksHeld(files.Paths);
                File.WriteAllBytes(path, replacementBytes);
            }, CreateImmediateWorkflowCoordinator())
        {
            VerifyAction = path =>
            {
                Assert.AreEqual(files.Paths.InstalledPath, path);
                Assert.AreEqual(ReleaseVersionString(release), SelfUpdateVerifier.ReadVersion(path));
                Assert.AreSequenceEqual(replacementBytes, File.ReadAllBytes(path));
                AssertWorkflowLocksHeld(files.Paths);
                var backups = Directory.GetFiles(files.Paths.DirectoryPath, "*.old.*");
                Assert.HasCount(1, backups);
                Assert.AreSequenceEqual(originalBytes, File.ReadAllBytes(backups[0]));
            },
        };

        Assert.AreEqual(release.Version.ToString(), workflow.Execute());

        Assert.AreEqual(1, resolveCount);
        Assert.AreEqual(1, downloadCount);
        Assert.AreEqual(1, workflow.VerificationCount);
        Assert.AreSequenceEqual(replacementBytes, File.ReadAllBytes(files.Paths.InstalledPath));
        Assert.IsFalse(File.Exists(files.Paths.StagedPath));
        var retainedBackups = Directory.GetFiles(files.Paths.DirectoryPath, "*.old.*");
        Assert.HasCount(1, retainedBackups);
        Assert.AreSequenceEqual(originalBytes, File.ReadAllBytes(retainedBackups[0]));
        AssertWorkflowLocksAvailable(files.Paths);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    [UnsupportedOSPlatform("windows")]
    [DataRow(448, false)]
    [DataRow(448, true)]
    [DataRow(493, false)]
    [DataRow(493, true)]
    [DataRow(509, false)]
    [DataRow(509, true)]
    public void UnixUpdateAndRollbackPreserveInstalledExecutableMode(int mode, bool failVerification)
    {
        using var files = new SelfUpdateTestFiles();
        var originalMode = (UnixFileMode)mode;
        File.SetUnixFileMode(files.Paths.InstalledPath, originalMode);
        var directoryMode = File.GetUnixFileMode(files.Paths.DirectoryPath);
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalVersion,
            () => CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementVersion),
            (release, destination) => SelfUpdateTestFiles.WriteExecutable(destination, ReleaseVersionString(release)), CreateImmediateWorkflowCoordinator())
        {
            VerifyAction = path =>
            {
                Assert.AreEqual(originalMode, File.GetUnixFileMode(path));
                if (failVerification)
                {
                    throw new IOException("Injected verification failure.");
                }
            },
        };

        if (failVerification)
        {
            var exception = Assert.ThrowsExactly<DotnetInstallException>(() => workflow.Execute());
            Assert.AreEqual(DotnetInstallErrorCode.DotnetupVerificationFailed, exception.ErrorCode);
        }
        else
        {
            Assert.IsNotNull(workflow.Execute());
        }

        Assert.AreEqual(originalMode, File.GetUnixFileMode(files.Paths.InstalledPath));
        Assert.AreEqual(directoryMode, File.GetUnixFileMode(files.Paths.DirectoryPath));
        Assert.AreEqual(failVerification ? SelfUpdateTestFiles.OriginalVersion : SelfUpdateTestFiles.ReplacementVersion,
            SelfUpdateVerifier.ReadVersion(files.Paths.InstalledPath));
    }

    [TestMethod]
    public async Task ConcurrentUpdatesRecheckAfterSerializingAndDownloadOnlyOnce()
    {
        using var files = new SelfUpdateTestFiles();
        using var finishDownload = new ManualResetEventSlim();
        using var retrySecondUpdate = new ManualResetEventSlim();
        var firstDownloading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondWaiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementVersion);
        var originalBytes = File.ReadAllBytes(files.Paths.InstalledPath);
        var downloadCount = 0;
        var firstWorkflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalVersion, () => release,
            (download, path) =>
            {
                Interlocked.Increment(ref downloadCount);
                AssertWorkflowLocksHeld(files.Paths);
                firstDownloading.SetResult();
                Assert.IsTrue(finishDownload.Wait(TimeSpan.FromSeconds(30), TestContext.CancellationToken), "First updater was not released.");
                SelfUpdateTestFiles.WriteExecutable(path, ReleaseVersionString(download));
            }, CreateImmediateWorkflowCoordinator())
        {
            VerifyAction = path => AssertWorkflowLocksHeld(files.Paths),
        };
        var retryPolicy = new WorkflowContentionRetryPolicy(() =>
        {
            secondWaiting.SetResult();
            Assert.IsTrue(retrySecondUpdate.Wait(TimeSpan.FromSeconds(30), TestContext.CancellationToken), "Second updater was not released.");
        });
        var secondWorkflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalVersion,
            () =>
            {
                Assert.AreEqual(SelfUpdateTestFiles.OriginalVersion, SelfUpdateVerifier.ReadVersion(files.Paths.InstalledPath));
                return release;
            },
            (download, path) =>
            {
                Interlocked.Increment(ref downloadCount);
                Assert.Fail("The second updater must recheck the installed version without downloading.");
            }, new SelfUpdateCoordinator(retryPolicy, new LockFileRetryPolicy(TimeSpan.Zero)));
        var firstUpdate = Task.Run(() => firstWorkflow.Execute(), TestContext.CancellationToken);
        Task<string?>? secondUpdate = null;
        try
        {
            await firstDownloading.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken).ConfigureAwait(false);
            secondUpdate = Task.Run(() => secondWorkflow.Execute(), TestContext.CancellationToken);
            await secondWaiting.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken).ConfigureAwait(false);

            Assert.AreEqual(1, Volatile.Read(ref downloadCount));
            Assert.IsFalse(firstUpdate.IsCompleted);
            Assert.IsFalse(secondUpdate.IsCompleted);
            Assert.AreSequenceEqual(originalBytes, File.ReadAllBytes(files.Paths.InstalledPath));
            finishDownload.Set();
            Assert.AreEqual(release.Version.ToString(),
                await firstUpdate.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken).ConfigureAwait(false));
            retrySecondUpdate.Set();
            Assert.IsNull(await secondUpdate.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken).ConfigureAwait(false));

            Assert.AreEqual(1, downloadCount);
            Assert.AreEqual(1, firstWorkflow.VerificationCount);
            Assert.AreEqual(0, secondWorkflow.VerificationCount);
            Assert.AreEqual(ReleaseVersionString(release), SelfUpdateVerifier.ReadVersion(files.Paths.InstalledPath));
            var backups = Directory.GetFiles(files.Paths.DirectoryPath, "*.old.*");
            Assert.HasCount(1, backups);
            Assert.AreSequenceEqual(originalBytes, File.ReadAllBytes(backups[0]));
            AssertWorkflowLocksAvailable(files.Paths);
        }
        finally
        {
            finishDownload.Set();
            retrySecondUpdate.Set();
            await Task.WhenAll(firstUpdate, secondUpdate ?? Task.FromResult<string?>(null)).ConfigureAwait(false);
        }
    }

    [TestMethod]
    [DataRow(false, DotnetInstallErrorCode.DotnetupBusyWithAnotherCommand)]
    [DataRow(true, DotnetInstallErrorCode.DotnetupBusyWithUpdateOrCleanup)]
    public void BusyCommandOrUpdaterMapsTimeoutWithoutDownloading(bool occupyUpdate, DotnetInstallErrorCode expectedCode)
    {
        using var files = new SelfUpdateTestFiles();
        var originalBytes = File.ReadAllBytes(files.Paths.InstalledPath);
        var stagedBytes = File.ReadAllBytes(files.Paths.StagedPath);
        using var occupied = occupyUpdate
            ? ScopedLockFile.TryAcquireExclusive(files.Paths.UpdateLockPath)
            : ScopedLockFile.TryAcquireShared(files.Paths.ActivityLockPath);
        Assert.IsNotNull(occupied);
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalVersion,
            () => CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementVersion),
            (download, path) => Assert.Fail("Contention must fail before downloading."), CreateImmediateWorkflowCoordinator());

        var exception = Assert.ThrowsExactly<DotnetInstallException>(() => workflow.Execute());

        Assert.AreEqual(expectedCode, exception.ErrorCode);
        Assert.AreEqual(occupyUpdate
            ? Microsoft.DotNet.Tools.Bootstrapper.Strings.SelfUpdateBusyUpdate
            : Microsoft.DotNet.Tools.Bootstrapper.Strings.SelfUpdateBusyCommand, exception.Message);
        var timeout = Assert.IsInstanceOfType<SelfUpdateLockTimeoutException>(exception.InnerException);
        Assert.AreEqual(occupyUpdate ? SelfUpdateLockKind.Update : SelfUpdateLockKind.Activity, timeout.LockKind);
        Assert.AreEqual(0, workflow.VerificationCount);
        Assert.AreSequenceEqual(originalBytes, File.ReadAllBytes(files.Paths.InstalledPath));
        Assert.AreSequenceEqual(stagedBytes, File.ReadAllBytes(files.Paths.StagedPath));
        Assert.IsEmpty(Directory.GetFiles(files.Paths.DirectoryPath, "*.old.*"));
        occupied.Dispose();
        AssertWorkflowLocksAvailable(files.Paths);
    }

    [TestMethod]
    [DataRow(SelfUpdateTestFiles.ReplacementVersion, "0.2.0-other")]
    [DataRow(SelfUpdateTestFiles.ReplacementVersion + "+expected-build", SelfUpdateTestFiles.ReplacementVersion + "+unexpected-build")]
    [DataRow(SelfUpdateTestFiles.ReplacementVersion + "+expected-build", SelfUpdateTestFiles.ReplacementVersion)]
    public void DownloadedVersionMismatchRestoresOriginal(string expectedVersion, string unexpectedVersion)
    {
        using var files = new SelfUpdateTestFiles();
        var originalBytes = File.ReadAllBytes(files.Paths.InstalledPath);
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalVersion,
            () => CreateWorkflowRelease(expectedVersion),
            (download, path) => SelfUpdateTestFiles.WriteExecutable(path, unexpectedVersion), CreateImmediateWorkflowCoordinator());

        var exception = Assert.ThrowsExactly<DotnetInstallException>(() => workflow.Execute());

        Assert.AreEqual(DotnetInstallErrorCode.DotnetupVerificationFailed, exception.ErrorCode);
        Assert.AreEqual(1, workflow.VerificationCount);
        Assert.AreSequenceEqual(originalBytes, File.ReadAllBytes(files.Paths.InstalledPath));
        Assert.IsFalse(File.Exists(files.Paths.StagedPath));
        if (OperatingSystem.IsWindows())
        {
            var rejected = Directory.GetFiles(files.Paths.DirectoryPath, "*.old.*.rejected");
            Assert.HasCount(1, rejected);
            Assert.AreEqual(unexpectedVersion, SelfUpdateVerifier.ReadVersion(rejected[0]));
        }
        AssertWorkflowLocksAvailable(files.Paths);
    }

    [TestMethod]
    [DataRow(SelfUpdateTestFiles.ReplacementVersion)]
    [DataRow(SelfUpdateTestFiles.ReplacementVersion + "+0123456789abcdef0123456789abcdef01234567")]
    public void ReleaseVerificationAcceptsInformationalSourceRevision(string releaseVersion)
    {
        const string informationalVersion = SelfUpdateTestFiles.ReplacementVersion + "+0123456789abcdef0123456789abcdef01234567";
        using var files = new SelfUpdateTestFiles();
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalVersion,
            () => CreateWorkflowRelease(releaseVersion),
            (download, path) => SelfUpdateTestFiles.WriteExecutable(path, informationalVersion),
            CreateImmediateWorkflowCoordinator());

        Assert.AreEqual(releaseVersion, workflow.Execute());

        Assert.AreEqual(1, workflow.VerificationCount);
        Assert.AreEqual(informationalVersion, SelfUpdateVerifier.ReadVersion(files.Paths.InstalledPath));
        AssertWorkflowLocksAvailable(files.Paths);
    }

    [TestMethod]
    public void VerificationFailureRestoresOriginalAndPreservesRejectedOnWindows()
    {
        using var files = new SelfUpdateTestFiles();
        var originalBytes = File.ReadAllBytes(files.Paths.InstalledPath);
        var replacementBytes = File.ReadAllBytes(files.Paths.StagedPath);
        var verificationFailure = new InvalidDataException("Injected startup smoke-test failure.");
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalVersion,
            () => CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementVersion),
            (download, path) => File.WriteAllBytes(path, replacementBytes), CreateImmediateWorkflowCoordinator())
        {
            VerifyAction = path =>
            {
                AssertWorkflowLocksHeld(files.Paths);
                Assert.AreEqual(SelfUpdateTestFiles.ReplacementVersion, SelfUpdateVerifier.ReadVersion(path));
                throw verificationFailure;
            },
        };

        var exception = Assert.ThrowsExactly<DotnetInstallException>(() => workflow.Execute());

        Assert.AreEqual(DotnetInstallErrorCode.DotnetupVerificationFailed, exception.ErrorCode);
        Assert.AreSame(verificationFailure, exception.InnerException);
        Assert.AreEqual(1, workflow.VerificationCount);
        Assert.AreSequenceEqual(originalBytes, File.ReadAllBytes(files.Paths.InstalledPath));
        Assert.IsFalse(File.Exists(files.Paths.StagedPath));
        if (OperatingSystem.IsWindows())
        {
            var rejected = Directory.GetFiles(files.Paths.DirectoryPath, "*.old.*.rejected");
            Assert.HasCount(1, rejected);
            Assert.AreSequenceEqual(replacementBytes, File.ReadAllBytes(rejected[0]));
            Assert.IsFalse(File.Exists(rejected[0][..^".rejected".Length]));
        }

        AssertWorkflowLocksAvailable(files.Paths);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ReportedVerificationChildTerminationFailureStillRollsBack(bool terminationTimedOut)
    {
        using var files = new SelfUpdateTestFiles();
        var originalBytes = File.ReadAllBytes(files.Paths.InstalledPath);
        var replacementBytes = File.ReadAllBytes(files.Paths.StagedPath);
        var verificationFailure = new IOException("Injected verification timeout.");
        Exception terminationFailure = terminationTimedOut
            ? new OperationCanceledException("Injected termination wait timeout.")
            : new Win32Exception(5, "Injected process termination failure.");
        var failures = new AggregateException(verificationFailure, terminationFailure);
        var reportedFailure = new DotnetInstallException(DotnetInstallErrorCode.InstallFailed,
            "Injected verifier failure.", new IOException("The verification child could not be terminated within five seconds.", failures));
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalVersion,
            () => CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementVersion),
            (download, path) => File.WriteAllBytes(path, replacementBytes), CreateImmediateWorkflowCoordinator())
        {
            VerifyAction = path =>
            {
                AssertWorkflowLocksHeld(files.Paths);
                Assert.AreEqual(SelfUpdateTestFiles.ReplacementVersion, SelfUpdateVerifier.ReadVersion(path));
                throw reportedFailure;
            },
        };

        IDisposable? retained = null;
        try
        {
            var exception = Assert.ThrowsExactly<DotnetInstallException>(() => workflow.Execute(lease => retained = lease));

            Assert.AreEqual(DotnetInstallErrorCode.DotnetupVerificationFailed, exception.ErrorCode);
            Assert.AreSame(reportedFailure, exception.InnerException);
            var preservedFailures = Assert.IsInstanceOfType<AggregateException>(exception.InnerException!.InnerException!.InnerException);
            Assert.AreSame(verificationFailure, preservedFailures.InnerExceptions[0]);
            Assert.AreSame(terminationFailure, preservedFailures.InnerExceptions[1]);
            Assert.AreEqual(1, workflow.VerificationCount);
            Assert.AreSequenceEqual(originalBytes, File.ReadAllBytes(files.Paths.InstalledPath));
            Assert.IsFalse(File.Exists(files.Paths.StagedPath));
            Assert.IsNotNull(retained);
            AssertWorkflowLocksHeld(files.Paths);
        }
        finally
        {
            retained?.Dispose();
        }

        AssertWorkflowLocksAvailable(files.Paths);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RetainedLeaseKeepsBothLocksUntilExplicitDisposal(bool failVerification)
    {
        using var files = new SelfUpdateTestFiles();
        var release = CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementVersion);
        IDisposable? retained = null;
        var callbackCount = 0;
        var failure = new InvalidDataException("Injected verification failure.");
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalVersion, () => release,
            (download, path) =>
            {
                Assert.IsNotNull(retained);
                AssertWorkflowLocksHeld(files.Paths);
                SelfUpdateTestFiles.WriteExecutable(path, ReleaseVersionString(download));
            }, CreateImmediateWorkflowCoordinator())
        {
            VerifyAction = path =>
            {
                Assert.IsNotNull(retained);
                AssertWorkflowLocksHeld(files.Paths);
                if (failVerification)
                {
                    throw failure;
                }
            },
        };
        void RetainWorkflowLease(IDisposable lease)
        {
            retained = lease;
            callbackCount++;
            AssertWorkflowLocksHeld(files.Paths);
        }

        try
        {
            if (failVerification)
            {
                var exception = Assert.ThrowsExactly<DotnetInstallException>(() => workflow.Execute(RetainWorkflowLease));
                Assert.AreEqual(DotnetInstallErrorCode.DotnetupVerificationFailed, exception.ErrorCode);
                Assert.AreSame(failure, exception.InnerException);
            }
            else
            {
                Assert.AreEqual(release.Version.ToString(), workflow.Execute(RetainWorkflowLease));
            }

            Assert.AreEqual(1, callbackCount);
            Assert.AreEqual(1, workflow.VerificationCount);
            Assert.IsNotNull(retained);
            AssertWorkflowLocksHeld(files.Paths);
            retained.Dispose();
            AssertWorkflowLocksAvailable(files.Paths);
        }
        finally
        {
            retained?.Dispose();
        }
    }

    [TestMethod]
    public void RenamedExecutableCannotSelfUpdate()
    {
        using var files = new SelfUpdateTestFiles();
        var renamedPath = Path.Combine(files.Paths.DirectoryPath,
            "dotnetup-renamed" + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));
        SelfUpdateTestFiles.WriteExecutable(renamedPath, SelfUpdateTestFiles.OriginalVersion);
        var renamedBytes = File.ReadAllBytes(renamedPath);
        var workflow = new SelfUpdateTestWorkflow(new SelfUpdatePaths(renamedPath), SelfUpdateTestFiles.OriginalVersion,
            () => { Assert.Fail("A non-canonical executable must be rejected before resolving a release."); return null!; },
            (download, path) => Assert.Fail("A non-canonical executable must not download."), CreateImmediateWorkflowCoordinator());

        var exception = Assert.ThrowsExactly<DotnetInstallException>(() => workflow.Execute());

        Assert.AreEqual(DotnetInstallErrorCode.DotnetupNonCanonicalExecutableName, exception.ErrorCode);
        Assert.Contains(renamedPath, exception.Message);
        Assert.AreSequenceEqual(renamedBytes, File.ReadAllBytes(renamedPath));
        Assert.IsEmpty(Directory.GetFiles(files.Paths.DirectoryPath, "*.old.*"));
    }

    [TestMethod]
    [DataRow("empty")]
    [DataRow("wrong")]
    [DataRow("nonzero")]
    public void UnavailableInstalledVersionFailsControlledWithoutChangingFiles(string mode)
    {
        using var files = new SelfUpdateTestFiles(mode: mode);

        var originalBytes = File.ReadAllBytes(files.Paths.InstalledPath);
        var stagedBytes = File.ReadAllBytes(files.Paths.StagedPath);
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalVersion,
            () => CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementVersion),
            (download, path) => Assert.Fail("Invalid installed version output must fail before downloading."),
            CreateImmediateWorkflowCoordinator());

        var exception = Assert.ThrowsExactly<DotnetInstallException>(() => workflow.Execute());

        Assert.AreEqual(DotnetInstallErrorCode.DotnetupIdentityUnavailable, exception.ErrorCode);
        Assert.AreEqual(0, workflow.VerificationCount);
        Assert.AreSequenceEqual(originalBytes, File.ReadAllBytes(files.Paths.InstalledPath));
        Assert.AreSequenceEqual(stagedBytes, File.ReadAllBytes(files.Paths.StagedPath));
        Assert.IsEmpty(Directory.GetFiles(files.Paths.DirectoryPath, "*.old.*"));
        AssertWorkflowLocksAvailable(files.Paths);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(false)]
    [DataRow(true)]
    public void LockedStagingFileFailsControlledBeforeDownload(bool partialDownload)
    {
        using var files = new SelfUpdateTestFiles();
        var blockedPath = files.Paths.StagedPath + (partialDownload ? ".download" : string.Empty);
        File.WriteAllText(blockedPath, "locked staging bytes");
        var originalBytes = File.ReadAllBytes(files.Paths.InstalledPath);
        var blockedBytes = File.ReadAllBytes(blockedPath);
        using var blocked = new FileStream(blockedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalVersion,
            () => CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementVersion),
            (download, path) => Assert.Fail("Locked staging must fail before downloading."), CreateImmediateWorkflowCoordinator());

        var exception = Assert.ThrowsExactly<DotnetInstallException>(() => workflow.Execute());

        Assert.AreEqual(DotnetInstallErrorCode.InstallFailed, exception.ErrorCode);
        Assert.IsInstanceOfType<IOException>(exception.InnerException);
        Assert.AreEqual(0, workflow.VerificationCount);
        Assert.AreSequenceEqual(originalBytes, File.ReadAllBytes(files.Paths.InstalledPath));
        Assert.AreSequenceEqual(blockedBytes, File.ReadAllBytes(blockedPath));
        Assert.IsEmpty(Directory.GetFiles(files.Paths.DirectoryPath, "*.old.*"));
        AssertWorkflowLocksAvailable(files.Paths);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CleanupDeletesOldBackupsOnlyWhenLoadedVersionMatchesCanonical(bool staleLoadedVersion)
    {
        using var files = new SelfUpdateTestFiles();
        var originalBytes = File.ReadAllBytes(files.Paths.InstalledPath);
        File.WriteAllText(files.BackupPath, "aged backup");
        File.WriteAllText(files.BackupPath + ".rejected", "aged rejected executable");
        File.SetLastWriteTimeUtc(files.BackupPath, DateTime.UtcNow.AddDays(-8));
        File.SetLastWriteTimeUtc(files.BackupPath + ".rejected", DateTime.UtcNow.AddDays(-8));
        var loadedVersion = staleLoadedVersion ? SelfUpdateTestFiles.OriginalVersion + "+other" : SelfUpdateTestFiles.OriginalVersion;
        var workflow = new SelfUpdateTestWorkflow(files.Paths, loadedVersion,
            () => CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementVersion),
            (download, path) =>
            {
                AssertWorkflowLocksHeld(files.Paths);
                Assert.AreEqual(staleLoadedVersion, File.Exists(files.BackupPath));
                Assert.AreEqual(staleLoadedVersion, File.Exists(files.BackupPath + ".rejected"));
                SelfUpdateTestFiles.WriteExecutable(path, ReleaseVersionString(download));
            }, CreateImmediateWorkflowCoordinator());

        Assert.AreEqual(CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementVersion).Version.ToString(), workflow.Execute());

        if (staleLoadedVersion)
        {
            Assert.AreEqual("aged backup", File.ReadAllText(files.BackupPath));
            Assert.AreEqual("aged rejected executable", File.ReadAllText(files.BackupPath + ".rejected"));
        }

        var transactionBackups = Directory.GetFiles(files.Paths.DirectoryPath, "*.old.*")
            .Where(path => path != files.BackupPath && path != files.BackupPath + ".rejected").ToArray();
        Assert.HasCount(1, transactionBackups);
        Assert.AreSequenceEqual(originalBytes, File.ReadAllBytes(transactionBackups[0]));
        AssertWorkflowLocksAvailable(files.Paths);
    }

    private static void AssertWorkflowLocksHeld(SelfUpdatePaths paths)
    {
        using var update = ScopedLockFile.TryAcquireExclusive(paths.UpdateLockPath);
        using var activity = ScopedLockFile.TryAcquireShared(paths.ActivityLockPath);
        Assert.IsNull(update, "The workflow must retain the update lock.");
        Assert.IsNull(activity, "The workflow must hold the activity lock exclusively.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FailedCandidateIsRestoredWithoutAnotherVersionInvocation(bool unstartable)
    {
        using var files = new SelfUpdateTestFiles();
        var original = File.ReadAllBytes(files.Paths.InstalledPath);
        var replacement = File.ReadAllBytes(files.Paths.StagedPath);
        var workflow = new SelfUpdateWorkflow(files.Paths, SelfUpdateTestFiles.OriginalVersion,
            () => CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementVersion),
            (release, destination) =>
            {
                File.WriteAllBytes(destination, unstartable ? [0, 1, 2, 3] : replacement);
                File.WriteAllText(files.Paths.InstalledPath + ".mode", "nonzero");
            }, CreateImmediateWorkflowCoordinator());

        var exception = Assert.ThrowsExactly<DotnetInstallException>(() => SelfUpdateTestWorkflow.ExecuteAndReleaseLocks(workflow));

        Assert.AreEqual(DotnetInstallErrorCode.DotnetupVerificationFailed, exception.ErrorCode);
        Assert.AreSequenceEqual(original, File.ReadAllBytes(files.Paths.InstalledPath));
        Assert.HasCount(unstartable ? 2 : 3, File.ReadAllLines(files.Paths.InstalledPath + ".invocations"),
            "Only discovery, the locked recheck, and (if startable) verification may run --version. Rollback must not run anything.");
        Assert.IsFalse(File.Exists(files.Paths.StagedPath + ".invocations"), "Do not redundantly execute the staged release.");
        AssertWorkflowLocksAvailable(files.Paths);
    }

    private static ResolvedDownload CreateWorkflowRelease(string version)
        => new(new Uri("https://example.invalid/dotnetup.exe"), new string('0', 128), "win-x64",
            ReleaseVersion.Parse(version));

    private static string ReleaseVersionString(ResolvedDownload release) => release.Version.ToString();

    private static SelfUpdateCoordinator CreateImmediateWorkflowCoordinator()
        => new(new LockFileRetryPolicy(TimeSpan.Zero), new LockFileRetryPolicy(TimeSpan.Zero));

    private static void AssertWorkflowLocksAvailable(SelfUpdatePaths paths)
    {
        using var update = ScopedLockFile.TryAcquireExclusive(paths.UpdateLockPath);
        using var activity = ScopedLockFile.TryAcquireExclusive(paths.ActivityLockPath);
        Assert.IsNotNull(update);
        Assert.IsNotNull(activity);
    }

    private sealed class WorkflowContentionRetryPolicy(Action wait) : LockFileRetryPolicy(TimeSpan.FromSeconds(30))
    {
        public override long GetTimestamp() => 0;

        public override TimeSpan GetElapsedTime(long startingTimestamp) => TimeSpan.Zero;

        public override void WaitBeforeRetry(int attempt, TimeSpan remaining, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.AreEqual(0, attempt, "The first updater must release its locks before the second retry.");
            wait();
        }
    }
}