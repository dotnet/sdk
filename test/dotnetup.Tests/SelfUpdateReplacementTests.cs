// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>Transaction tests deliberately use non-executable bytes: recovery must never run either file.</summary>
[TestClass]
public class SelfUpdateReplacementTests : SdkTest
{
    [TestMethod]
    public void PathsAreCanonicalSiblingsWithUniqueBackups()
    {
        using var files = new SelfUpdateTestFiles(executable: false);
        var paths = files.Paths;
        paths.Validate();
        var realDirectory = ExecutablePathResolver.ResolveRealDirectory(paths.InstalledPath)!;
        Assert.AreEqual(realDirectory, paths.DirectoryPath);
        Assert.AreEqual(paths.InstalledPath + ".new", paths.StagedPath);
        Assert.AreEqual(Path.Combine(realDirectory, "dotnetup.update.lock"), paths.UpdateLockPath);
        Assert.AreEqual(Path.Combine(realDirectory, "dotnetup.activity.lock"), paths.ActivityLockPath);
        var backup = paths.CreateBackupPath();
        Assert.AreNotEqual(backup, paths.CreateBackupPath());
        Assert.IsTrue(backup.StartsWith(paths.InstalledPath + ".old.", StringComparison.Ordinal));
        Assert.IsTrue(SelfUpdatePaths.IsBackupIdentifier(backup.AsSpan(paths.InstalledPath.Length + 5)));
    }

    [TestMethod]
    public void RelativeExecutablePathsResolveToTheSameInstallation()
    {
        using var files = new SelfUpdateTestFiles(executable: false);
        var paths = new SelfUpdatePaths(Path.GetRelativePath(Environment.CurrentDirectory, files.Paths.InstalledPath));
        paths.Validate();
        Assert.AreEqual(files.Paths.InstalledPath, paths.InstalledPath);
        Assert.AreEqual(files.Paths.UpdateLockPath, paths.UpdateLockPath);
    }

    [TestMethod]
    [DataRow("other.exe")]
    [DataRow("dotnetup.exe.new")]
    [DataRow("dotnetup ")]
    public void UnsafeCanonicalNameIsRejected(string name)
    {
        using var files = new SelfUpdateTestFiles(executable: false);
        var paths = new SelfUpdatePaths(Path.Combine(files.Paths.DirectoryPath, name));
        Assert.ThrowsExactly<IOException>(paths.Validate);
    }

    [TestMethod]
    [DataRow("1234567")]
    [DataRow("123456789")]
    [DataRow("1234567g")]
    [DataRow("12345678.extra")]
    [DataRow("0123456789abcdef0123456789abcdef")]
    public void InvalidBackupIdentifiersAreRejected(string identifier)
    {
        using var files = new SelfUpdateTestFiles(executable: false);
        var backup = files.Paths.InstalledPath + ".old." + identifier;
        var replacement = new SelfUpdateReplacement(files.Paths, backup);
        Assert.ThrowsExactly<DotnetInstallException>(replacement.Replace);
        Assert.IsFalse(File.Exists(backup));
        Assert.AreEqual("original", File.ReadAllText(files.Paths.InstalledPath));
        Assert.AreEqual("replacement", File.ReadAllText(files.Paths.StagedPath));
    }

    [TestMethod]
    public void ReplaceAndRollbackWorkWithoutExecutableVersionOrReadableCandidate()
    {
        using var files = new SelfUpdateTestFiles(executable: false);
        using var locks = AcquireLocks(files);
        files.Replacement.Replace();
        Assert.AreEqual("replacement", File.ReadAllText(files.Paths.InstalledPath));
        Assert.AreEqual("original", File.ReadAllText(files.BackupPath));
        Assert.IsFalse(File.Exists(files.Paths.StagedPath));
        files.Replacement.Rollback();
        files.Replacement.Rollback();
        Assert.AreEqual("original", File.ReadAllText(files.Paths.InstalledPath));
        if (OperatingSystem.IsWindows())
        {
            Assert.AreEqual("replacement", File.ReadAllText(files.BackupPath + ".rejected"));
        }
        AssertReplacementLocksHeld(files.Paths);
    }

    [TestMethod]
    public void TransactionsSharingPathsKeepIndependentRecoveryState()
    {
        using var files = new SelfUpdateTestFiles(executable: false);
        using var locks = AcquireLocks(files);
        files.Replacement.Replace();
        File.WriteAllText(files.Paths.StagedPath, "next");
        var second = new SelfUpdateReplacement(files.Paths, files.Paths.CreateBackupPath());
        second.Replace();
        Assert.AreEqual("next", File.ReadAllText(files.Paths.InstalledPath));
        second.Rollback();
        Assert.AreEqual("replacement", File.ReadAllText(files.Paths.InstalledPath));
        files.Replacement.Rollback();
        Assert.AreEqual("original", File.ReadAllText(files.Paths.InstalledPath));
    }

    [TestMethod]
    public void DifferentTransactionCannotUseAnotherInstancesRecoveryState()
    {
        using var files = new SelfUpdateTestFiles(executable: false);
        using var locks = AcquireLocks(files);
        files.Replacement.Replace();
        var unrecorded = new SelfUpdateReplacement(files.Paths, files.BackupPath);
        Assert.ThrowsExactly<DotnetInstallException>(unrecorded.Rollback);
        Assert.AreEqual("replacement", File.ReadAllText(files.Paths.InstalledPath));
        Assert.AreEqual("original", File.ReadAllText(files.BackupPath));
        files.Replacement.Rollback();
        Assert.AreEqual("original", File.ReadAllText(files.Paths.InstalledPath));
    }

    [TestMethod]
    public void RollbackWithoutStartedTransactionDoesNotConsumeBackup()
    {
        using var files = new SelfUpdateTestFiles(executable: false);
        File.Move(files.Paths.InstalledPath, files.BackupPath);
        Assert.ThrowsExactly<DotnetInstallException>(files.Replacement.Rollback);
        Assert.IsFalse(File.Exists(files.Paths.InstalledPath));
        Assert.AreEqual("original", File.ReadAllText(files.BackupPath));
    }

    [TestMethod]
    public void OccupiedBackupDoesNotOverwriteEitherExecutable()
    {
        using var files = new SelfUpdateTestFiles(executable: false);
        File.WriteAllText(files.BackupPath, "recoverable");
        Assert.ThrowsExactly<DotnetInstallException>(files.Replacement.Replace);
        Assert.AreEqual("recoverable", File.ReadAllText(files.BackupPath));
        Assert.AreEqual("original", File.ReadAllText(files.Paths.InstalledPath));
        Assert.AreEqual("replacement", File.ReadAllText(files.Paths.StagedPath));
        Assert.ThrowsExactly<DotnetInstallException>(files.Replacement.Rollback);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("canonical")]
    [DataRow("staged")]
    [DataRow("backup")]
    [DataRow("parent")]
    public void WindowsJunctionsAreRejectedWithoutTouchingTargets(string location)
    {
        using var files = new SelfUpdateTestFiles(executable: false);
        var target = Directory.CreateDirectory(Path.Combine(files.Paths.DirectoryPath, "target"));
        var marker = Path.Combine(target.FullName, "untouched");
        File.WriteAllText(marker, "untouched");
        var junction = location switch
        {
            "canonical" => files.Paths.InstalledPath,
            "staged" => files.Paths.StagedPath,
            "backup" => files.BackupPath,
            _ => Path.Combine(files.Paths.DirectoryPath, "parent"),
        };
        File.Delete(junction);
        var startInfo = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            Arguments = $"/d /c mklink /J \"{junction}\" \"{target.FullName}\"",
        };
        using var process = Process.Start(startInfo);
        Assert.IsNotNull(process);
        Assert.IsTrue(process.WaitForExit(10_000));
        Assert.AreEqual(0, process.ExitCode);
        try
        {
            if (location == "parent")
            {
                var paths = new SelfUpdatePaths(Path.Combine(junction, "dotnetup.exe"));
                Assert.ThrowsExactly<IOException>(paths.Validate);
            }
            else
            {
                Assert.ThrowsExactly<IOException>(() => { using var file = SelfUpdatePaths.OpenFile(junction); });
                Assert.ThrowsExactly<DotnetInstallException>(files.Replacement.Replace);
            }

            Assert.AreEqual("untouched", File.ReadAllText(marker));
        }
        finally
        {
            Directory.Delete(junction);
        }
    }

    [TestMethod]
    public void MissingStageDoesNotMutateInstallation()
    {
        using var files = new SelfUpdateTestFiles(executable: false);
        File.Delete(files.Paths.StagedPath);
        Assert.ThrowsExactly<DotnetInstallException>(files.Replacement.Replace);
        Assert.AreEqual("original", File.ReadAllText(files.Paths.InstalledPath));
        Assert.IsFalse(File.Exists(files.BackupPath));
    }

    [TestMethod]
    public void HardLinkedStagePreservesLinkedFileContents()
    {
        using var files = new SelfUpdateTestFiles(executable: false);
        var linkedPath = Path.Combine(files.Paths.DirectoryPath, "linked-executable");
        File.CreateHardLink(linkedPath, files.Paths.StagedPath);
        files.Replacement.Replace();
        Assert.AreEqual("replacement", File.ReadAllText(linkedPath));
        Assert.AreEqual("replacement", File.ReadAllText(files.Paths.InstalledPath));
        Assert.AreEqual("original", File.ReadAllText(files.BackupPath));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(87)]
    [DataRow(1175)]
    [DataRow(1176)]
    [DataRow(1177)]
    public void WindowsReplacementRecoversDocumentedFailureStates(int nativeError)
    {
        using var files = new SelfUpdateTestFiles(executable: false);
        using var locks = AcquireLocks(files);
        var failure = new IOException("Injected ReplaceFileW failure.", unchecked((int)0x80070000) | nativeError);
        var calls = 0;
        var exception = Assert.ThrowsExactly<DotnetInstallException>(() => files.Replacement.Replace((source, destination, backup) =>
        {
            calls++;
            AssertReplacementLocksHeld(files.Paths);
            if (nativeError == 1177)
            {
                File.Move(destination, backup);
                File.SetAttributes(source, File.GetAttributes(backup));
            }
            throw failure;
        }));
        Assert.AreEqual(1, calls);
        Assert.AreSame(failure, exception.InnerException);
        Assert.AreEqual("original", File.ReadAllText(files.Paths.InstalledPath));
        Assert.AreEqual("replacement", File.ReadAllText(files.Paths.StagedPath));
        Assert.IsFalse(File.Exists(files.BackupPath));
        Assert.IsFalse(File.Exists(files.BackupPath + ".rejected"));
        AssertReplacementLocksHeld(files.Paths);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void WindowsReplacementExceptionAfterSwitchRollsBackWithoutExecutingCandidate()
    {
        using var files = new SelfUpdateTestFiles(executable: false);
        using var locks = AcquireLocks(files);
        var failure = new IOException("Injected failure after switching the canonical path.");
        var exception = Assert.ThrowsExactly<DotnetInstallException>(() => files.Replacement.Replace((source, destination, backup) =>
        {
            File.Replace(source, destination, backup, ignoreMetadataErrors: false);
            throw failure;
        }));
        Assert.AreSame(failure, exception.InnerException);
        Assert.AreEqual("original", File.ReadAllText(files.Paths.InstalledPath));
        Assert.AreEqual("replacement", File.ReadAllText(files.BackupPath + ".rejected"));
        Assert.IsFalse(File.Exists(files.BackupPath));
        AssertReplacementLocksHeld(files.Paths);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void WindowsPartialReplacementRecoveryFailureRetainsBackupForRetry()
    {
        using var files = new SelfUpdateTestFiles(executable: false);
        using var locks = AcquireLocks(files);
        FileStream? blockedBackup = null;
        var failure = new IOException("Injected partial replacement failure.");
        try
        {
            var exception = Assert.ThrowsExactly<DotnetInstallException>(() => files.Replacement.Replace((source, destination, backup) =>
            {
                File.Move(destination, backup);
                blockedBackup = new FileStream(backup, FileMode.Open, FileAccess.Read, FileShare.Read);
                throw failure;
            }));
            var failures = Assert.IsInstanceOfType<AggregateException>(exception.InnerException);
            Assert.AreSame(failure, failures.InnerExceptions[0]);
            Assert.IsFalse(File.Exists(files.Paths.InstalledPath));
            Assert.AreEqual("original", File.ReadAllText(files.BackupPath));
            Assert.AreEqual("replacement", File.ReadAllText(files.Paths.StagedPath));
            AssertReplacementLocksHeld(files.Paths);
        }
        finally
        {
            blockedBackup?.Dispose();
        }
        files.Replacement.Rollback();
        Assert.AreEqual("original", File.ReadAllText(files.Paths.InstalledPath));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(false)]
    [DataRow(true)]
    public void WindowsRollbackMoveFailureRetainsRecoveryFilesForRetry(bool secondMove)
    {
        using var files = new SelfUpdateTestFiles(executable: false);
        files.Replacement.Replace();
        using (var locked = new FileStream(secondMove ? files.BackupPath : files.Paths.InstalledPath,
            FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.ThrowsExactly<DotnetInstallException>(files.Replacement.Rollback);
            Assert.AreEqual(!secondMove, File.Exists(files.Paths.InstalledPath));
            Assert.AreEqual("original", File.ReadAllText(files.BackupPath));
            Assert.AreEqual("replacement", File.ReadAllText(secondMove ? files.BackupPath + ".rejected" : files.Paths.InstalledPath));
        }
        files.Replacement.Rollback();
        Assert.AreEqual("original", File.ReadAllText(files.Paths.InstalledPath));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void WindowsRollbackOccupiedRejectedPathPreservesEverything()
    {
        using var files = new SelfUpdateTestFiles(executable: false);
        files.Replacement.Replace();
        File.WriteAllText(files.BackupPath + ".rejected", "recoverable");
        Assert.ThrowsExactly<DotnetInstallException>(files.Replacement.Rollback);
        Assert.AreEqual("recoverable", File.ReadAllText(files.BackupPath + ".rejected"));
        Assert.AreEqual("original", File.ReadAllText(files.BackupPath));
        Assert.AreEqual("replacement", File.ReadAllText(files.Paths.InstalledPath));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void WindowsReplacementFailureLeavesOriginalAndStage()
    {
        using var files = new SelfUpdateTestFiles(executable: false);
        using var locked = new FileStream(files.Paths.InstalledPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        Assert.ThrowsExactly<DotnetInstallException>(files.Replacement.Replace);
        Assert.AreEqual("original", File.ReadAllText(files.Paths.InstalledPath));
        Assert.AreEqual("replacement", File.ReadAllText(files.Paths.StagedPath));
    }

    [TestMethod]
    public void ReplacementStampsBackupAndRejectedRetentionTime()
    {
        using var files = new SelfUpdateTestFiles(executable: false);
        File.SetLastWriteTimeUtc(files.Paths.InstalledPath, DateTime.UtcNow.AddDays(-30));
        File.SetLastWriteTimeUtc(files.Paths.StagedPath, DateTime.UtcNow.AddDays(-30));
        var started = DateTime.UtcNow.AddSeconds(-2);
        files.Replacement.Replace();
        Assert.IsTrue(File.GetLastWriteTimeUtc(files.BackupPath) >= started);
        files.Replacement.Rollback();
        if (OperatingSystem.IsWindows())
        {
            Assert.IsTrue(File.GetLastWriteTimeUtc(files.BackupPath + ".rejected") >= started);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    [DataRow(false)]
    [DataRow(true)]
    public void ExecutableAndStagedSymlinksAreRejected(bool staged)
    {
        using var files = new SelfUpdateTestFiles(executable: false);
        var target = Path.Combine(files.Paths.DirectoryPath, "target");
        File.WriteAllText(target, "untouched");
        var path = staged ? files.Paths.StagedPath : files.Paths.InstalledPath;
        File.Delete(path);
        File.CreateSymbolicLink(path, target);
        Assert.ThrowsExactly<DotnetInstallException>(files.Replacement.Replace);
        Assert.ThrowsExactly<IOException>(() => SelfUpdatePaths.OpenFile(path));
        Assert.AreEqual("untouched", File.ReadAllText(target));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    public void DirectorySymlinksResolveToTheSameInstallationAndLocks()
    {
        using var files = new SelfUpdateTestFiles(executable: false);
        var link = files.Paths.DirectoryPath + "-link";
        try
        {
            Directory.CreateSymbolicLink(link, files.Paths.DirectoryPath);
            var paths = new SelfUpdatePaths(Path.Combine(link, Path.GetFileName(files.Paths.InstalledPath)));
            paths.Validate();
            Assert.AreEqual(files.Paths.InstalledPath, paths.InstalledPath);
            using var update = ScopedLockFile.TryAcquireExclusive(paths.UpdateLockPath);
            Assert.IsNotNull(update);
            using var competing = ScopedLockFile.TryAcquireExclusive(files.Paths.UpdateLockPath);
            Assert.IsNull(competing);
            File.Delete(files.Paths.StagedPath);
            File.CreateSymbolicLink(files.Paths.StagedPath, files.Paths.InstalledPath);
            Assert.ThrowsExactly<IOException>(() => SelfUpdatePaths.OpenFile(paths.StagedPath));
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [TestMethod]
    public void OpenFileRejectsMissingAndNonRegularFiles()
    {
        using var files = new SelfUpdateTestFiles(executable: false);
        Assert.ThrowsExactly<FileNotFoundException>(() => SelfUpdatePaths.OpenFile(files.BackupPath));
        File.Delete(files.Paths.StagedPath);
        Directory.CreateDirectory(files.Paths.StagedPath);
        Assert.ThrowsExactly<IOException>(() => SelfUpdatePaths.OpenFile(files.Paths.StagedPath));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [SupportedOSPlatform("windows")]
    public void WindowsReplacementPreservesNormalInheritedPermissions()
    {
        using var files = new SelfUpdateTestFiles(executable: false);
        var directory = new DirectoryInfo(files.Paths.DirectoryPath);
        var permissions = directory.GetAccessControl();
        permissions.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            FileSystemRights.Modify, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        directory.SetAccessControl(permissions);
        var original = new FileInfo(files.Paths.InstalledPath).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access);
        files.Replacement.Replace();
        Assert.AreEqual(original, new FileInfo(files.Paths.InstalledPath).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access));
        files.Replacement.Rollback();
        Assert.AreEqual(original, new FileInfo(files.Paths.InstalledPath).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access));
    }

    [TestMethod]
    public async Task LoadedExecutableCanBeReplacedAndRolledBack()
    {
        using var files = new SelfUpdateTestFiles();
        using var locks = AcquireLocks(files);
        var start = new ProcessStartInfo(files.Paths.InstalledPath)
        {
            UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, CreateNoWindow = true,
        };
        start.ArgumentList.Add("--hold");
        using var process = Process.Start(start);
        Assert.IsNotNull(process);
        try
        {
            Assert.AreEqual(SelfUpdateTestFiles.OriginalVersion, await process.StandardOutput.ReadLineAsync(TestContext.CancellationToken)
                .AsTask().WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
            files.Replacement.Replace();
            Assert.AreEqual(SelfUpdateTestFiles.ReplacementVersion, SelfUpdateVerifier.ReadVersion(files.Paths.InstalledPath));
            files.Replacement.Rollback();
            Assert.AreEqual(SelfUpdateTestFiles.OriginalVersion, SelfUpdateVerifier.ReadVersion(files.Paths.InstalledPath));
            process.StandardInput.Close();
            Assert.AreEqual(SelfUpdateTestFiles.OriginalVersion, await process.StandardOutput.ReadLineAsync(TestContext.CancellationToken)
                .AsTask().WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
            Assert.IsTrue(process.WaitForExit(10_000));
        }
        finally
        {
            NativeSelfUpdateFiles.Stop(process);
        }
    }

    private SelfUpdateLockLease AcquireLocks(SelfUpdateTestFiles files)
        => new SelfUpdateCoordinator().Acquire(files.Paths.UpdateLockPath, files.Paths.ActivityLockPath, TestContext.CancellationToken);

    private static void AssertReplacementLocksHeld(SelfUpdatePaths paths)
    {
        using var update = ScopedLockFile.TryAcquireExclusive(paths.UpdateLockPath);
        using var activity = ScopedLockFile.TryAcquireShared(paths.ActivityLockPath);
        Assert.IsNull(update);
        Assert.IsNull(activity);
    }
}
