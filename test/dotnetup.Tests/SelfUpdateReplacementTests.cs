// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.IO.Compression;
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

[TestClass]
public class SelfUpdateReplacementTests : SdkTest
{
    [TestMethod]
    public void PathsAreCanonicalSiblingsWithUniqueBackups()
    {
        var directory = Directory.CreateTempSubdirectory("selfupdate-paths-");
        directory = new DirectoryInfo(ExecutablePathResolver.ResolveRealPath(directory.FullName)!);
        try
        {
            var installed = Path.Combine(directory.FullName, OperatingSystem.IsWindows() ? "dotnetup.exe" : "dotnetup");
            File.WriteAllText(installed, "original");
            var paths = new SelfUpdatePaths(installed);
            paths.Validate();
            Assert.AreEqual(installed, paths.InstalledPath);
            Assert.AreEqual(directory.FullName, paths.DirectoryPath);
            Assert.AreEqual(installed + ".new", paths.StagedPath);
            Assert.AreEqual(Path.Combine(directory.FullName, "dotnetup.update.lock"), paths.UpdateLockPath);
            Assert.AreEqual(Path.Combine(directory.FullName, "dotnetup.activity.lock"), paths.ActivityLockPath);
            var backup = paths.CreateBackupPath();
            Assert.IsTrue(backup.StartsWith(installed + ".old.", StringComparison.Ordinal));
            Assert.AreNotEqual(backup, paths.CreateBackupPath());
            Assert.IsTrue(Guid.TryParseExact(backup[(installed.Length + 5)..], "N", out _));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    [DataRow("other.exe")]
    [DataRow("dotnetup.exe.new")]
    [DataRow("dotnetup ")]
    public void UnsafeCanonicalNameIsRejected(string name)
    {
        var paths = new SelfUpdatePaths(Path.Combine(Path.GetTempPath(), name));
        Assert.ThrowsExactly<IOException>(paths.Validate);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    public void ExecutableSymlinkIsRejectedWithoutChangingTarget()
    {
        var directory = Directory.CreateTempSubdirectory("selfupdate-paths-");
        directory = new DirectoryInfo(ExecutablePathResolver.ResolveRealPath(directory.FullName)!);
        try
        {
            var target = Path.Combine(directory.FullName, "target");
            File.WriteAllText(target, "unchanged");
            var paths = new SelfUpdatePaths(Path.Combine(directory.FullName, OperatingSystem.IsWindows() ? "dotnetup.exe" : "dotnetup"));
            _ = File.CreateSymbolicLink(paths.InstalledPath, target);
            Assert.ThrowsExactly<IOException>(paths.Validate);
            Assert.AreEqual("unchanged", File.ReadAllText(target));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    public void ResolvedDirectoryAllowsAccessWithoutAcceptingSymlinkedPaths()
    {
        using var files = new SelfUpdateTestFiles();
        var linkedDirectory = files.Paths.DirectoryPath + "-link";
        try
        {
            Directory.CreateSymbolicLink(linkedDirectory, files.Paths.DirectoryPath);
            var linkedExecutable = Path.Combine(linkedDirectory, Path.GetFileName(files.Paths.InstalledPath));
            var linkedPaths = new SelfUpdatePaths(linkedExecutable);
            Assert.ThrowsExactly<IOException>(linkedPaths.Validate);

            var resolvedDirectory = ExecutablePathResolver.ResolveRealDirectory(linkedExecutable);
            Assert.AreEqual(files.Paths.DirectoryPath, resolvedDirectory);
            var resolvedPaths = new SelfUpdatePaths(Path.Combine(resolvedDirectory, Path.GetFileName(linkedExecutable)));
            resolvedPaths.Validate();
            Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(resolvedPaths.InstalledPath));
        }
        finally
        {
            Directory.Delete(linkedDirectory);
        }
    }

    [TestMethod]
    public void ReplacePreservesOriginalAndRollbackRestoresIt()
    {
        using var files = new SelfUpdateTestFiles();
        files.Replacement.Replace();
        Assert.AreEqual(SelfUpdateTestFiles.ReplacementIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.BackupPath));
        Assert.IsFalse(File.Exists(files.Paths.StagedPath));
        files.Replacement.Rollback();
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
        if (OperatingSystem.IsWindows())
        {
            Assert.AreEqual(SelfUpdateTestFiles.ReplacementIdentity, SelfUpdatePaths.ReadIdentity(files.BackupPath + ".rejected"));
        }
    }

    [TestMethod]
    public void ReplacementOfOldExecutableRetainsBackupUntilRetentionExpires()
    {
        using var files = new SelfUpdateTestFiles();
        using var locks = new SelfUpdateCoordinator().Acquire(files.Paths.UpdateLockPath, files.Paths.ActivityLockPath, TestContext.CancellationToken);
        var originalBytes = File.ReadAllBytes(files.Paths.InstalledPath);
        File.SetLastWriteTimeUtc(files.Paths.InstalledPath, DateTime.UtcNow.AddDays(-30));
        var updateStarted = DateTime.UtcNow;

        files.Replacement.Replace();
        SelfUpdateCleanup.RunWithUpdateLock(files.Paths.InstalledPath, SelfUpdateTestFiles.ReplacementIdentity);

        Assert.IsTrue(File.Exists(files.BackupPath), "A new backup must not expire based on the executable's old timestamp.");
        Assert.AreSequenceEqual(originalBytes, File.ReadAllBytes(files.BackupPath));
        var backupTime = File.GetLastWriteTimeUtc(files.BackupPath);
        // Allow for filesystem timestamp precision without sleeping.
        Assert.IsTrue(backupTime >= updateStarted.AddSeconds(-2) && backupTime <= DateTime.UtcNow.AddSeconds(2));

        File.SetLastWriteTimeUtc(files.BackupPath, DateTime.UtcNow.AddDays(-8));
        SelfUpdateCleanup.RunWithUpdateLock(files.Paths.InstalledPath, SelfUpdateTestFiles.ReplacementIdentity);
        Assert.IsFalse(File.Exists(files.BackupPath));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(false)]
    [DataRow(true)]
    public void RollbackRetainsOldRejectedExecutableUntilRetentionExpires(bool replacementThrows)
    {
        using var files = new SelfUpdateTestFiles();
        using var locks = new SelfUpdateCoordinator().Acquire(files.Paths.UpdateLockPath, files.Paths.ActivityLockPath, TestContext.CancellationToken);
        var stagedBytes = File.ReadAllBytes(files.Paths.StagedPath);
        File.SetLastWriteTimeUtc(files.Paths.StagedPath, DateTime.UtcNow.AddDays(-30));
        var updateStarted = DateTime.UtcNow;

        if (replacementThrows)
        {
            Assert.ThrowsExactly<DotnetInstallException>(() => files.Replacement.Replace(
                (source, destination, backup) =>
                {
                    File.Replace(source, destination, backup, ignoreMetadataErrors: false);
                    throw new IOException("Injected failure after the canonical name switched.");
                }));
        }
        else
        {
            files.Replacement.Replace();
            files.Replacement.Rollback();
        }

        SelfUpdateCleanup.RunWithUpdateLock(files.Paths.InstalledPath, SelfUpdateTestFiles.OriginalIdentity);

        var rejectedPath = files.BackupPath + ".rejected";
        Assert.IsTrue(File.Exists(rejectedPath), "A newly rejected executable must not expire based on its old timestamp.");
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
        Assert.AreSequenceEqual(stagedBytes, File.ReadAllBytes(rejectedPath));
        var rejectedTime = File.GetLastWriteTimeUtc(rejectedPath);
        Assert.IsTrue(rejectedTime >= updateStarted.AddSeconds(-2) && rejectedTime <= DateTime.UtcNow.AddSeconds(2));

        File.SetLastWriteTimeUtc(rejectedPath, DateTime.UtcNow.AddDays(-8));
        SelfUpdateCleanup.RunWithUpdateLock(files.Paths.InstalledPath, SelfUpdateTestFiles.OriginalIdentity);
        Assert.IsFalse(File.Exists(rejectedPath));
    }

    [TestMethod]
    public void TransactionsSharingPathsKeepIndependentRecoveryState()
    {
        using var files = new SelfUpdateTestFiles();
        files.Replacement.Replace();
        var nextIdentity = new string('c', 64);
        SelfUpdateTestFiles.WriteIdentity(files.Paths.StagedPath, nextIdentity);
        var secondBackup = files.Paths.CreateBackupPath();
        var second = new SelfUpdateReplacement(files.Paths, secondBackup, SelfUpdateTestFiles.ReplacementIdentity);

        second.Replace();
        Assert.AreEqual(nextIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
        second.Rollback();
        Assert.AreEqual(SelfUpdateTestFiles.ReplacementIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
        files.Replacement.Rollback();
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
    }

    [TestMethod]
    public void DifferentTransactionCannotUseAnotherInstancesRecordedCandidate()
    {
        using var files = new SelfUpdateTestFiles();
        files.Replacement.Replace();
        var unrecorded = new SelfUpdateReplacement(files.Paths, files.BackupPath, SelfUpdateTestFiles.OriginalIdentity);

        Assert.ThrowsExactly<DotnetInstallException>(() => unrecorded.Rollback());
        Assert.AreEqual(SelfUpdateTestFiles.ReplacementIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.BackupPath));
        files.Replacement.Rollback();
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
    }

    [TestMethod]
    public void ReplacementRejectsChangedOriginalBeforeMutation()
    {
        using var files = new SelfUpdateTestFiles();
        var unexpectedIdentity = new string('c', 64);
        SelfUpdateTestFiles.WriteIdentity(files.Paths.InstalledPath, unexpectedIdentity);

        Assert.ThrowsExactly<DotnetInstallException>(() => files.Replacement.Replace());
        Assert.AreEqual(unexpectedIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
        Assert.AreEqual(SelfUpdateTestFiles.ReplacementIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.StagedPath));
        Assert.IsFalse(File.Exists(files.BackupPath));
    }

    [TestMethod]
    public void MissingCanonicalCanBeRecoveredFromVerifiedBackup()
    {
        using var files = new SelfUpdateTestFiles();
        File.Move(files.Paths.InstalledPath, files.BackupPath);
        files.Replacement.Rollback();
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
        Assert.AreEqual(SelfUpdateTestFiles.ReplacementIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.StagedPath));
    }

    [TestMethod]
    public void OccupiedBackupDoesNotOverwriteEitherExecutable()
    {
        using var files = new SelfUpdateTestFiles();
        File.WriteAllText(files.BackupPath, "recoverable");
        var exception = Assert.ThrowsExactly<DotnetInstallException>(() => files.Replacement.Replace());
        Assert.AreEqual(DotnetInstallErrorCode.InstallFailed, exception.ErrorCode);
        Assert.AreEqual("recoverable", File.ReadAllText(files.BackupPath));
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
        Assert.AreEqual(SelfUpdateTestFiles.ReplacementIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.StagedPath));
    }

    [TestMethod]
    public void InvalidStageDoesNotMutateInstallation()
    {
        using var files = new SelfUpdateTestFiles();
        File.WriteAllText(files.Paths.StagedPath, "not an identity");
        Assert.ThrowsExactly<DotnetInstallException>(() => files.Replacement.Replace());
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
        Assert.IsFalse(File.Exists(files.BackupPath));
    }

    [TestMethod]
    public void HardLinkedStagePreservesLinkedFileContents()
    {
        using var files = new SelfUpdateTestFiles();
        var linkedPath = Path.Combine(files.Paths.DirectoryPath, "linked-executable");
        var replacementBytes = File.ReadAllBytes(files.Paths.StagedPath);
        File.CreateHardLink(linkedPath, files.Paths.StagedPath);
        files.Replacement.Replace();
        Assert.AreSequenceEqual(replacementBytes, File.ReadAllBytes(linkedPath));
        Assert.AreEqual(SelfUpdateTestFiles.ReplacementIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.BackupPath));
    }

    [TestMethod]
    public void RollbackRefusesUnexpectedCanonicalAndWrongBackup()
    {
        using var files = new SelfUpdateTestFiles();
        files.Replacement.Replace();
        SelfUpdateTestFiles.WriteIdentity(files.Paths.InstalledPath, new string('c', 64));
        Assert.ThrowsExactly<DotnetInstallException>(() => files.Replacement.Rollback());
        Assert.AreEqual(new string('c', 64), SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.BackupPath));
        SelfUpdateTestFiles.WriteIdentity(files.Paths.InstalledPath, SelfUpdateTestFiles.ReplacementIdentity);
        SelfUpdateTestFiles.WriteIdentity(files.BackupPath, new string('c', 64));
        Assert.ThrowsExactly<DotnetInstallException>(() => files.Replacement.Rollback());
        Assert.AreEqual(SelfUpdateTestFiles.ReplacementIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
        Assert.AreEqual(new string('c', 64), SelfUpdatePaths.ReadIdentity(files.BackupPath));
    }

    [TestMethod]
    public void RollbackDoesNotClobberUnrecordedReplacement()
    {
        using var files = new SelfUpdateTestFiles();
        File.Copy(files.Paths.InstalledPath, files.BackupPath);
        File.Move(files.Paths.StagedPath, files.Paths.InstalledPath, overwrite: true);
        Assert.ThrowsExactly<DotnetInstallException>(() => files.Replacement.Rollback());
        Assert.AreEqual(SelfUpdateTestFiles.ReplacementIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.BackupPath));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    public void StagedSymlinkAndDirectorySymlinkAreRejected()
    {
        using var files = new SelfUpdateTestFiles();
        File.Delete(files.Paths.StagedPath);
        _ = File.CreateSymbolicLink(files.Paths.StagedPath, files.Paths.InstalledPath);
        Assert.ThrowsExactly<DotnetInstallException>(() => files.Replacement.Replace());
        Assert.ThrowsExactly<IOException>(() => SelfUpdatePaths.ReadIdentity(files.Paths.StagedPath));
        var linkedDirectory = files.Paths.DirectoryPath + "-link";
        try
        {
            _ = Directory.CreateSymbolicLink(linkedDirectory, files.Paths.DirectoryPath);
            var linkedPaths = new SelfUpdatePaths(Path.Combine(linkedDirectory, Path.GetFileName(files.Paths.InstalledPath)));
            Assert.ThrowsExactly<IOException>(linkedPaths.Validate);
        }
        finally
        {
            Directory.Delete(linkedDirectory);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("canonical")]
    [DataRow("staged")]
    [DataRow("backup")]
    [DataRow("parent")]
    public void WindowsJunctionsAreRejectedWithoutTouchingTargets(string location)
    {
        using var files = new SelfUpdateTestFiles();
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
        if (location == "parent")
        {
            var paths = new SelfUpdatePaths(Path.Combine(junction, "dotnetup.exe"));
            Assert.ThrowsExactly<IOException>(paths.Validate);
        }
        else
        {
            Assert.ThrowsExactly<IOException>(() => SelfUpdatePaths.ReadIdentity(junction));
            Assert.ThrowsExactly<DotnetInstallException>(() => files.Replacement.Replace());
        }

        Assert.AreEqual("untouched", File.ReadAllText(marker));
        Directory.Delete(junction);
    }

    [TestMethod]
    public void ReadIdentityRejectsMissingAndNonRegularFiles()
    {
        using var files = new SelfUpdateTestFiles();
        Assert.ThrowsExactly<FileNotFoundException>(() => SelfUpdatePaths.ReadIdentity(files.BackupPath));
        File.Delete(files.Paths.StagedPath);
        _ = Directory.CreateDirectory(files.Paths.StagedPath);
        Assert.ThrowsExactly<IOException>(() => SelfUpdatePaths.ReadIdentity(files.Paths.StagedPath));
    }

    [TestMethod]
    public void UnsafeBackupNameIsRejected()
    {
        using var files = new SelfUpdateTestFiles();
        var replacement = new SelfUpdateReplacement(files.Paths, files.Paths.StagedPath, SelfUpdateTestFiles.OriginalIdentity);
        Assert.ThrowsExactly<DotnetInstallException>(() => replacement.Replace());
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
        Assert.AreEqual(SelfUpdateTestFiles.ReplacementIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.StagedPath));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [SupportedOSPlatform("windows")]
    public void WindowsReplacementPreservesNormalInheritedPermissions()
    {
        using var files = new SelfUpdateTestFiles();
        var directory = new DirectoryInfo(files.Paths.DirectoryPath);
        var permissions = directory.GetAccessControl();
        permissions.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            FileSystemRights.Modify, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Allow));
        directory.SetAccessControl(permissions);
        var originalPermissions = new FileInfo(files.Paths.InstalledPath).GetAccessControl()
            .GetSecurityDescriptorSddlForm(AccessControlSections.Access);
        var directoryPermissions = directory.GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access);
        using var archiveBytes = new MemoryStream();
        using (var archive = new ZipArchive(archiveBytes, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var content = archive.CreateEntry("sdk-file").Open();
            content.WriteByte(1);
        }

        archiveBytes.Position = 0;
        var extractedPath = Path.Combine(files.Paths.DirectoryPath, "sdk-file");
        using (var archive = new ZipArchive(archiveBytes, ZipArchiveMode.Read))
        {
            archive.GetEntry("sdk-file")!.ExtractToFile(extractedPath);
        }

        Assert.AreEqual(new FileInfo(extractedPath).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access),
            new FileInfo(files.Paths.StagedPath).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access));

        files.Replacement.Replace();

        Assert.AreEqual(originalPermissions, new FileInfo(files.Paths.InstalledPath).GetAccessControl()
            .GetSecurityDescriptorSddlForm(AccessControlSections.Access));
        Assert.AreEqual(directoryPermissions, directory.GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access));
        files.Replacement.Rollback();
        Assert.AreEqual(originalPermissions, new FileInfo(files.Paths.InstalledPath).GetAccessControl()
            .GetSecurityDescriptorSddlForm(AccessControlSections.Access));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void WindowsReplacementFailureLeavesOriginalAndStage()
    {
        using var files = new SelfUpdateTestFiles();
        using var locked = new FileStream(files.Paths.InstalledPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        Assert.ThrowsExactly<DotnetInstallException>(() => files.Replacement.Replace());
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
        Assert.AreEqual(SelfUpdateTestFiles.ReplacementIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.StagedPath));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(87)]
    [DataRow(1175)]
    [DataRow(1176)]
    [DataRow(1177)]
    public void WindowsReplacementRecoversDocumentedFailureStates(int nativeError)
    {
        using var files = new SelfUpdateTestFiles();
        using var locks = new SelfUpdateCoordinator().Acquire(files.Paths.UpdateLockPath, files.Paths.ActivityLockPath, TestContext.CancellationToken);
        var originalBytes = File.ReadAllBytes(files.Paths.InstalledPath);
        var stagedBytes = File.ReadAllBytes(files.Paths.StagedPath);
        var failure = new IOException("Injected ReplaceFileW failure.", unchecked((int)0x80070000) | nativeError);
        var replacementCalls = 0;

        var exception = Assert.ThrowsExactly<DotnetInstallException>(() => files.Replacement.Replace(
            (source, destination, backup) =>
            {
                replacementCalls++;
                Assert.AreEqual(files.Paths.StagedPath, source);
                Assert.AreEqual(files.Paths.InstalledPath, destination);
                Assert.AreEqual(files.BackupPath, backup);
                AssertReplacementLocksHeld(files.Paths);
                if (nativeError == 1177)
                {
                    File.Move(destination, backup);
                    File.SetAttributes(source, File.GetAttributes(backup));
                    Assert.IsFalse(File.Exists(destination));
                }

                throw failure;
            }));

        Assert.AreEqual(1, replacementCalls);
        Assert.AreEqual(DotnetInstallErrorCode.InstallFailed, exception.ErrorCode);
        Assert.AreSame(failure, exception.InnerException);
        Assert.AreEqual(unchecked((int)0x80070000) | nativeError, exception.InnerException!.HResult);
        Assert.AreSequenceEqual(originalBytes, File.ReadAllBytes(files.Paths.InstalledPath));
        Assert.AreSequenceEqual(stagedBytes, File.ReadAllBytes(files.Paths.StagedPath));
        Assert.IsFalse(File.Exists(files.BackupPath));
        Assert.IsFalse(File.Exists(files.BackupPath + ".rejected"));
        AssertReplacementLocksHeld(files.Paths);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void WindowsReplacementExceptionAfterSwitchRollsBackKnownCandidate()
    {
        using var files = new SelfUpdateTestFiles();
        using var locks = new SelfUpdateCoordinator().Acquire(files.Paths.UpdateLockPath, files.Paths.ActivityLockPath, TestContext.CancellationToken);
        var originalBytes = File.ReadAllBytes(files.Paths.InstalledPath);
        var stagedBytes = File.ReadAllBytes(files.Paths.StagedPath);
        var failure = new IOException("Injected failure after the canonical name switched.");

        var exception = Assert.ThrowsExactly<DotnetInstallException>(() => files.Replacement.Replace(
            (source, destination, backup) =>
            {
                File.Replace(source, destination, backup, ignoreMetadataErrors: false);
                throw failure;
            }));

        Assert.AreEqual(DotnetInstallErrorCode.InstallFailed, exception.ErrorCode);
        Assert.AreSame(failure, exception.InnerException);
        Assert.AreSequenceEqual(originalBytes, File.ReadAllBytes(files.Paths.InstalledPath));
        Assert.AreSequenceEqual(stagedBytes, File.ReadAllBytes(files.BackupPath + ".rejected"));
        Assert.IsFalse(File.Exists(files.BackupPath));
        Assert.IsFalse(File.Exists(files.Paths.StagedPath));
        AssertReplacementLocksHeld(files.Paths);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("backup-locked")]
    [DataRow("unknown-canonical")]
    [DataRow("wrong-backup")]
    public void WindowsPartialReplacementRecoveryFailurePreservesArtifacts(string obstruction)
    {
        using var files = new SelfUpdateTestFiles();
        using var locks = new SelfUpdateCoordinator().Acquire(files.Paths.UpdateLockPath, files.Paths.ActivityLockPath, TestContext.CancellationToken);
        var originalBytes = File.ReadAllBytes(files.Paths.InstalledPath);
        var stagedBytes = File.ReadAllBytes(files.Paths.StagedPath);
        var failure = new IOException("Injected partial replacement failure.", unchecked((int)0x80070499));
        var unexpectedIdentity = new string('c', 64);
        FileStream? blockedBackup = null;
        try
        {
            var exception = Assert.ThrowsExactly<DotnetInstallException>(() => files.Replacement.Replace(
                (source, destination, backup) =>
                {
                    File.Move(destination, backup);
                    if (obstruction == "backup-locked")
                    {
                        blockedBackup = new FileStream(backup, FileMode.Open, FileAccess.Read, FileShare.Read);
                    }
                    else
                    {
                        SelfUpdateTestFiles.WriteIdentity(obstruction == "unknown-canonical" ? destination : backup, unexpectedIdentity);
                    }

                    throw failure;
                }));

            Assert.AreEqual(DotnetInstallErrorCode.InstallFailed, exception.ErrorCode);
            Assert.Contains("reinstall", exception.Message);
            Assert.Contains(files.BackupPath, exception.Message);
            var failures = Assert.IsInstanceOfType<AggregateException>(exception.InnerException);
            Assert.HasCount(2, failures.InnerExceptions);
            Assert.AreSame(failure, failures.InnerExceptions[0]);
            Assert.IsInstanceOfType<DotnetInstallException>(failures.InnerExceptions[1]);
            Assert.AreSequenceEqual(stagedBytes, File.ReadAllBytes(files.Paths.StagedPath));
            Assert.AreEqual(obstruction == "unknown-canonical", File.Exists(files.Paths.InstalledPath));
            if (obstruction == "unknown-canonical")
            {
                Assert.AreEqual(unexpectedIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
            }

            if (obstruction == "wrong-backup")
            {
                Assert.AreEqual(unexpectedIdentity, SelfUpdatePaths.ReadIdentity(files.BackupPath));
            }
            else
            {
                Assert.AreSequenceEqual(originalBytes, File.ReadAllBytes(files.BackupPath));
            }

            Assert.IsFalse(File.Exists(files.BackupPath + ".rejected"));
            AssertReplacementLocksHeld(files.Paths);
        }
        finally
        {
            blockedBackup?.Dispose();
        }
    }

    private static void AssertReplacementLocksHeld(SelfUpdatePaths paths)
    {
        using var update = ScopedLockFile.TryAcquireExclusive(paths.UpdateLockPath);
        using var activity = ScopedLockFile.TryAcquireShared(paths.ActivityLockPath);
        Assert.IsNull(update);
        Assert.IsNull(activity);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void WindowsRollbackFirstMoveFailureLeavesCanonicalAndBackup()
    {
        using var files = new SelfUpdateTestFiles();
        files.Replacement.Replace();
        using (var locked = new FileStream(files.Paths.InstalledPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.ThrowsExactly<DotnetInstallException>(() => files.Replacement.Rollback());
            Assert.AreEqual(SelfUpdateTestFiles.ReplacementIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
            Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.BackupPath));
            Assert.IsFalse(File.Exists(files.BackupPath + ".rejected"));
        }

        files.Replacement.Rollback();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void WindowsRollbackSecondMoveFailurePreservesBothRecoveryFiles()
    {
        using var files = new SelfUpdateTestFiles();
        files.Replacement.Replace();
        using (var locked = new FileStream(files.BackupPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.ThrowsExactly<DotnetInstallException>(() => files.Replacement.Rollback());
            Assert.IsFalse(File.Exists(files.Paths.InstalledPath));
            Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.BackupPath));
            Assert.AreEqual(SelfUpdateTestFiles.ReplacementIdentity, SelfUpdatePaths.ReadIdentity(files.BackupPath + ".rejected"));
        }

        files.Replacement.Rollback();
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void WindowsRollbackOccupiedRejectedPathPreservesEverything()
    {
        using var files = new SelfUpdateTestFiles();
        files.Replacement.Replace();
        File.WriteAllText(files.BackupPath + ".rejected", "recoverable");
        Assert.ThrowsExactly<DotnetInstallException>(() => files.Replacement.Rollback());
        Assert.AreEqual("recoverable", File.ReadAllText(files.BackupPath + ".rejected"));
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.BackupPath));
        Assert.AreEqual(SelfUpdateTestFiles.ReplacementIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
    }

    [TestMethod]
    public async Task LoadedExecutableCanBeReplacedAndRolledBack()
    {
        using var files = new SelfUpdateTestFiles(executable: true);
        var startInfo = new ProcessStartInfo(files.Paths.InstalledPath)
        {
            UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--hold");
        using var process = Process.Start(startInfo);
        Assert.IsNotNull(process);
        try
        {
            Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, await process.StandardOutput.ReadLineAsync(TestContext.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
            files.Replacement.Replace();
            SelfUpdateVerifier.Verify(files.Paths.InstalledPath, SelfUpdateTestFiles.ReplacementIdentity, TimeSpan.FromSeconds(20));
            files.Replacement.Rollback();
            SelfUpdateVerifier.Verify(files.Paths.InstalledPath, SelfUpdateTestFiles.OriginalIdentity, TimeSpan.FromSeconds(20));
            process.StandardInput.Close();
            Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, await process.StandardOutput.ReadLineAsync(TestContext.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
            Assert.IsTrue(process.WaitForExit(10_000));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                Assert.IsTrue(process.WaitForExit(10_000));
            }
        }
    }
}