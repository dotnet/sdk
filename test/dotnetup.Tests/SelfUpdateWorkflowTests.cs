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
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalIdentity,
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
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalIdentity,
            () => CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementIdentity),
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
        var release = CreateWorkflowRelease(SelfUpdateTestFiles.OriginalIdentity);
        var resolveCount = 0;
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalIdentity,
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
    public void SameChannelReleaseMustBeNewer(string installedVersion, string availableVersion)
    {
        using var files = new SelfUpdateTestFiles();
        var installedMetadata = DotnetupVersionMetadataReader.Format(installedVersion, "win-x64");
        SelfUpdateTestFiles.WriteIdentity(files.Paths.InstalledPath, installedMetadata);
        var release = CreateWorkflowRelease(DotnetupVersionMetadataReader.Format(availableVersion, "win-x64"));
        var workflow = new SelfUpdateTestWorkflow(files.Paths, installedMetadata, () => release,
            (download, path) => Assert.Fail("An older release from the same channel must not be downloaded."),
            CreateImmediateWorkflowCoordinator());

        Assert.IsNull(workflow.Execute(lease => Assert.Fail("A no-op must not transfer a lock lease.")));
        Assert.AreEqual(0, workflow.VerificationCount);
        Assert.AreEqual(installedMetadata, SelfUpdatePaths.ReadVersionMetadata(files.Paths.InstalledPath));
        AssertWorkflowLocksAvailable(files.Paths);
    }

    [TestMethod]
    [DataRow("0.3.0-preview.1.26465.7", "0.2.0-dev.1")]
    [DataRow("1.0.0", "0.9.0-preview.1")]
    public void CrossChannelReleaseCanChangeSemanticVersionDirection(string installedVersion, string availableVersion)
    {
        using var files = new SelfUpdateTestFiles();
        var installedMetadata = DotnetupVersionMetadataReader.Format(installedVersion, "win-x64");
        SelfUpdateTestFiles.WriteIdentity(files.Paths.InstalledPath, installedMetadata);
        var release = CreateWorkflowRelease(DotnetupVersionMetadataReader.Format(availableVersion, "win-x64"));
        var workflow = new SelfUpdateTestWorkflow(files.Paths, installedMetadata, () => release,
            (download, path) => SelfUpdateTestFiles.WriteIdentity(path, ReleaseMetadata(download)),
            CreateImmediateWorkflowCoordinator());

        Assert.AreEqual(availableVersion, workflow.Execute());
        Assert.AreEqual(1, workflow.VerificationCount);
        Assert.AreEqual(ReleaseMetadata(release), SelfUpdatePaths.ReadVersionMetadata(files.Paths.InstalledPath));
        AssertWorkflowLocksAvailable(files.Paths);
    }

    [TestMethod]
    public void DownloadFailureDoesNotReplaceInstalledExecutable()
    {
        using var files = new SelfUpdateTestFiles();
        var originalBytes = File.ReadAllBytes(files.Paths.InstalledPath);
        var release = CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementIdentity);
        var failure = new DotnetInstallException(DotnetInstallErrorCode.DownloadFailed, "Injected download failure.");
        var downloadCount = 0;
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalIdentity, () => release,
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
        var release = CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementIdentity);
        var resolveCount = 0;
        var downloadCount = 0;
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalIdentity,
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
                Assert.AreEqual(ReleaseMetadata(release), SelfUpdatePaths.ReadVersionMetadata(path));
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
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalIdentity,
            () => CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementIdentity),
            (release, destination) => SelfUpdateTestFiles.WriteIdentity(destination, ReleaseMetadata(release)), CreateImmediateWorkflowCoordinator())
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
        Assert.AreEqual(failVerification ? SelfUpdateTestFiles.OriginalIdentity : SelfUpdateTestFiles.ReplacementIdentity,
            SelfUpdatePaths.ReadVersionMetadata(files.Paths.InstalledPath));
    }

    [TestMethod]
    public async Task ConcurrentUpdatesRecheckAfterSerializingAndDownloadOnlyOnce()
    {
        using var files = new SelfUpdateTestFiles();
        using var finishDownload = new ManualResetEventSlim();
        using var retrySecondUpdate = new ManualResetEventSlim();
        var firstDownloading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondWaiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementIdentity);
        var originalBytes = File.ReadAllBytes(files.Paths.InstalledPath);
        var downloadCount = 0;
        var firstWorkflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalIdentity, () => release,
            (download, path) =>
            {
                Interlocked.Increment(ref downloadCount);
                AssertWorkflowLocksHeld(files.Paths);
                firstDownloading.SetResult();
                Assert.IsTrue(finishDownload.Wait(TimeSpan.FromSeconds(30), TestContext.CancellationToken), "First updater was not released.");
                SelfUpdateTestFiles.WriteIdentity(path, ReleaseMetadata(download));
            }, CreateImmediateWorkflowCoordinator())
        {
            VerifyAction = path => AssertWorkflowLocksHeld(files.Paths),
        };
        var retryPolicy = new WorkflowContentionRetryPolicy(() =>
        {
            secondWaiting.SetResult();
            Assert.IsTrue(retrySecondUpdate.Wait(TimeSpan.FromSeconds(30), TestContext.CancellationToken), "Second updater was not released.");
        });
        var secondWorkflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalIdentity,
            () =>
            {
                Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadVersionMetadata(files.Paths.InstalledPath));
                return release;
            },
            (download, path) =>
            {
                Interlocked.Increment(ref downloadCount);
                Assert.Fail("The second updater must recheck canonical identity without downloading.");
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
            Assert.AreEqual(ReleaseMetadata(release), SelfUpdatePaths.ReadVersionMetadata(files.Paths.InstalledPath));
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
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalIdentity,
            () => CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementIdentity),
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
    public void DownloadedIdentityMismatchLeavesCanonicalUntouched()
    {
        using var files = new SelfUpdateTestFiles();
        var originalBytes = File.ReadAllBytes(files.Paths.InstalledPath);
        var unexpectedIdentity = "0.2.0-other|win-x64";
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalIdentity,
            () => CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementIdentity),
            (download, path) => SelfUpdateTestFiles.WriteIdentity(path, unexpectedIdentity), CreateImmediateWorkflowCoordinator());

        var exception = Assert.ThrowsExactly<DotnetInstallException>(() => workflow.Execute());

        Assert.AreEqual(DotnetInstallErrorCode.DotnetupIdentityUnavailable, exception.ErrorCode);
        Assert.AreEqual(0, workflow.VerificationCount);
        Assert.AreSequenceEqual(originalBytes, File.ReadAllBytes(files.Paths.InstalledPath));
        Assert.AreEqual(unexpectedIdentity, SelfUpdatePaths.ReadVersionMetadata(files.Paths.StagedPath));
        Assert.IsEmpty(Directory.GetFiles(files.Paths.DirectoryPath, "*.old.*"));
        AssertWorkflowLocksAvailable(files.Paths);
    }

    [TestMethod]
    public void VerificationFailureRestoresOriginalAndPreservesRejectedOnWindows()
    {
        using var files = new SelfUpdateTestFiles();
        var originalBytes = File.ReadAllBytes(files.Paths.InstalledPath);
        var replacementBytes = File.ReadAllBytes(files.Paths.StagedPath);
        var verificationFailure = new InvalidDataException("Injected startup smoke-test failure.");
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalIdentity,
            () => CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementIdentity),
            (download, path) => File.WriteAllBytes(path, replacementBytes), CreateImmediateWorkflowCoordinator())
        {
            VerifyAction = path =>
            {
                AssertWorkflowLocksHeld(files.Paths);
                Assert.AreEqual(SelfUpdateTestFiles.ReplacementIdentity, SelfUpdatePaths.ReadVersionMetadata(path));
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
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalIdentity,
            () => CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementIdentity),
            (download, path) => File.WriteAllBytes(path, replacementBytes), CreateImmediateWorkflowCoordinator())
        {
            VerifyAction = path =>
            {
                AssertWorkflowLocksHeld(files.Paths);
                Assert.AreEqual(SelfUpdateTestFiles.ReplacementIdentity, SelfUpdatePaths.ReadVersionMetadata(path));
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
        var release = CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementIdentity);
        IDisposable? retained = null;
        var callbackCount = 0;
        var failure = new InvalidDataException("Injected verification failure.");
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalIdentity, () => release,
            (download, path) =>
            {
                Assert.IsNotNull(retained);
                AssertWorkflowLocksHeld(files.Paths);
                SelfUpdateTestFiles.WriteIdentity(path, ReleaseMetadata(download));
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
    [DataRow("missing")]
    [DataRow("truncated")]
    [DataRow("duplicate")]
    public void MalformedInstalledIdentityFailsControlledWithoutChangingFiles(string recordKind)
    {
        using var files = new SelfUpdateTestFiles();
        switch (recordKind)
        {
            case "missing":
                File.WriteAllText(files.Paths.InstalledPath, "no identity record");
                break;
            case "truncated":
                var bytes = File.ReadAllBytes(files.Paths.InstalledPath);
                File.WriteAllBytes(files.Paths.InstalledPath, bytes[..^1]);
                break;
            case "duplicate":
                SelfUpdateTestFiles.WriteIdentity(files.Paths.InstalledPath, SelfUpdateTestFiles.OriginalIdentity, append: true);
                break;
        }

        var originalBytes = File.ReadAllBytes(files.Paths.InstalledPath);
        var stagedBytes = File.ReadAllBytes(files.Paths.StagedPath);
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalIdentity,
            () => CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementIdentity),
            (download, path) => Assert.Fail("Malformed canonical identity must fail before downloading."),
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
        var workflow = new SelfUpdateTestWorkflow(files.Paths, SelfUpdateTestFiles.OriginalIdentity,
            () => CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementIdentity),
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
    public void CleanupDeletesOldBackupsOnlyWhenLoadedIdentityMatchesCanonical(bool staleLoadedIdentity)
    {
        using var files = new SelfUpdateTestFiles();
        var originalBytes = File.ReadAllBytes(files.Paths.InstalledPath);
        File.WriteAllText(files.BackupPath, "aged backup");
        File.WriteAllText(files.BackupPath + ".rejected", "aged rejected executable");
        File.SetLastWriteTimeUtc(files.BackupPath, DateTime.UtcNow.AddDays(-8));
        File.SetLastWriteTimeUtc(files.BackupPath + ".rejected", DateTime.UtcNow.AddDays(-8));
        var loadedIdentity = staleLoadedIdentity ? "0.2.0-other|win-x64" : SelfUpdateTestFiles.OriginalIdentity;
        var workflow = new SelfUpdateTestWorkflow(files.Paths, loadedIdentity,
            () => CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementIdentity),
            (download, path) =>
            {
                AssertWorkflowLocksHeld(files.Paths);
                Assert.AreEqual(staleLoadedIdentity, File.Exists(files.BackupPath));
                Assert.AreEqual(staleLoadedIdentity, File.Exists(files.BackupPath + ".rejected"));
                SelfUpdateTestFiles.WriteIdentity(path, ReleaseMetadata(download));
            }, CreateImmediateWorkflowCoordinator());

        Assert.AreEqual(CreateWorkflowRelease(SelfUpdateTestFiles.ReplacementIdentity).Version.ToString(), workflow.Execute());

        if (staleLoadedIdentity)
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

    private static ResolvedDownload CreateWorkflowRelease(string identity)
        => new(new Uri("https://example.invalid/dotnetup.exe"), new string('0', 128), identity.Split('|')[1],
            ReleaseVersion.Parse(identity.Split('|')[0]));

    private static string ReleaseMetadata(ResolvedDownload release)
        => DotnetupVersionMetadataReader.Format(release.Version.ToString(), release.Rid);

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