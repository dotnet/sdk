// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Dotnet.Installation;
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
    public void ReplacePreservesOriginalAndRollbackRestoresIt()
    {
        using var files = new SelfUpdateTestFiles();
        SelfUpdateReplacement.Replace(files.Paths, files.BackupPath);
        Assert.AreEqual(SelfUpdateTestFiles.ReplacementIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.BackupPath));
        Assert.IsFalse(File.Exists(files.Paths.StagedPath));
        SelfUpdateReplacement.Rollback(files.Paths, files.BackupPath, SelfUpdateTestFiles.OriginalIdentity);
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
        if (OperatingSystem.IsWindows())
        {
            Assert.AreEqual(SelfUpdateTestFiles.ReplacementIdentity, SelfUpdatePaths.ReadIdentity(files.BackupPath + ".rejected"));
        }
    }

    [TestMethod]
    public void MissingCanonicalCanBeRecoveredFromVerifiedBackup()
    {
        using var files = new SelfUpdateTestFiles();
        File.Move(files.Paths.InstalledPath, files.BackupPath);
        SelfUpdateReplacement.Rollback(files.Paths, files.BackupPath, SelfUpdateTestFiles.OriginalIdentity);
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
        Assert.AreEqual(SelfUpdateTestFiles.ReplacementIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.StagedPath));
    }

    [TestMethod]
    public void OccupiedBackupDoesNotOverwriteEitherExecutable()
    {
        using var files = new SelfUpdateTestFiles();
        File.WriteAllText(files.BackupPath, "recoverable");
        var exception = Assert.ThrowsExactly<DotnetInstallException>(() => SelfUpdateReplacement.Replace(files.Paths, files.BackupPath));
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
        Assert.ThrowsExactly<DotnetInstallException>(() => SelfUpdateReplacement.Replace(files.Paths, files.BackupPath));
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
        Assert.IsFalse(File.Exists(files.BackupPath));
    }

    [TestMethod]
    public void HardLinkedStageCannotMutateAnotherArtifact()
    {
        using var files = new SelfUpdateTestFiles();
        File.Delete(files.Paths.StagedPath);
        File.CreateHardLink(files.Paths.StagedPath, files.Paths.InstalledPath);
        Assert.ThrowsExactly<DotnetInstallException>(() => SelfUpdateReplacement.Replace(files.Paths, files.BackupPath));
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.StagedPath));
        Assert.IsFalse(File.Exists(files.BackupPath));
    }

    [TestMethod]
    public void RollbackRefusesUnexpectedCanonicalAndWrongBackup()
    {
        using var files = new SelfUpdateTestFiles();
        SelfUpdateReplacement.Replace(files.Paths, files.BackupPath);
        SelfUpdateTestFiles.WriteIdentity(files.Paths.InstalledPath, new string('c', 64));
        Assert.ThrowsExactly<DotnetInstallException>(() => SelfUpdateReplacement.Rollback(files.Paths, files.BackupPath, SelfUpdateTestFiles.OriginalIdentity));
        Assert.AreEqual(new string('c', 64), SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.BackupPath));
        SelfUpdateTestFiles.WriteIdentity(files.Paths.InstalledPath, SelfUpdateTestFiles.ReplacementIdentity);
        SelfUpdateTestFiles.WriteIdentity(files.BackupPath, new string('c', 64));
        Assert.ThrowsExactly<DotnetInstallException>(() => SelfUpdateReplacement.Rollback(files.Paths, files.BackupPath, SelfUpdateTestFiles.OriginalIdentity));
        Assert.AreEqual(SelfUpdateTestFiles.ReplacementIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
        Assert.AreEqual(new string('c', 64), SelfUpdatePaths.ReadIdentity(files.BackupPath));
    }

    [TestMethod]
    public void RollbackDoesNotClobberUnrecordedReplacement()
    {
        using var files = new SelfUpdateTestFiles();
        File.Copy(files.Paths.InstalledPath, files.BackupPath);
        File.Move(files.Paths.StagedPath, files.Paths.InstalledPath, overwrite: true);
        Assert.ThrowsExactly<DotnetInstallException>(() => SelfUpdateReplacement.Rollback(files.Paths, files.BackupPath, SelfUpdateTestFiles.OriginalIdentity));
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
        Assert.ThrowsExactly<DotnetInstallException>(() => SelfUpdateReplacement.Replace(files.Paths, files.BackupPath));
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
            Assert.ThrowsExactly<DotnetInstallException>(() => SelfUpdateReplacement.Replace(files.Paths, files.BackupPath));
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
        Assert.ThrowsExactly<DotnetInstallException>(() => SelfUpdateReplacement.Replace(files.Paths, files.Paths.StagedPath));
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
        Assert.AreEqual(SelfUpdateTestFiles.ReplacementIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.StagedPath));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void WindowsDirectoryLeasePreventsParentReplacement()
    {
        using var files = new SelfUpdateTestFiles();
        var movedPath = files.Paths.DirectoryPath + "-moved";
        try
        {
            using (var lease = SelfUpdateFile.PinDirectory(files.Paths.DirectoryPath))
            {
                Assert.ThrowsExactly<IOException>(() => Directory.Move(files.Paths.DirectoryPath, movedPath));
                Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
            }

            Directory.Move(files.Paths.DirectoryPath, movedPath);
        }
        finally
        {
            if (Directory.Exists(movedPath))
            {
                Directory.Move(movedPath, files.Paths.DirectoryPath);
            }
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void WindowsReplacementFailureLeavesOriginalAndStage()
    {
        using var files = new SelfUpdateTestFiles();
        using var locked = new FileStream(files.Paths.InstalledPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        Assert.ThrowsExactly<DotnetInstallException>(() => SelfUpdateReplacement.Replace(files.Paths, files.BackupPath));
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
        Assert.AreEqual(SelfUpdateTestFiles.ReplacementIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.StagedPath));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void WindowsRollbackFirstMoveFailureLeavesCanonicalAndBackup()
    {
        using var files = new SelfUpdateTestFiles();
        SelfUpdateReplacement.Replace(files.Paths, files.BackupPath);
        using (var locked = new FileStream(files.Paths.InstalledPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.ThrowsExactly<DotnetInstallException>(() => SelfUpdateReplacement.Rollback(files.Paths, files.BackupPath, SelfUpdateTestFiles.OriginalIdentity));
            Assert.AreEqual(SelfUpdateTestFiles.ReplacementIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
            Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.BackupPath));
            Assert.IsFalse(File.Exists(files.BackupPath + ".rejected"));
        }

        SelfUpdateReplacement.Rollback(files.Paths, files.BackupPath, SelfUpdateTestFiles.OriginalIdentity);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void WindowsRollbackSecondMoveFailurePreservesBothRecoveryFiles()
    {
        using var files = new SelfUpdateTestFiles();
        SelfUpdateReplacement.Replace(files.Paths, files.BackupPath);
        using (var locked = new FileStream(files.BackupPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.ThrowsExactly<DotnetInstallException>(() => SelfUpdateReplacement.Rollback(files.Paths, files.BackupPath, SelfUpdateTestFiles.OriginalIdentity));
            Assert.IsFalse(File.Exists(files.Paths.InstalledPath));
            Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.BackupPath));
            Assert.AreEqual(SelfUpdateTestFiles.ReplacementIdentity, SelfUpdatePaths.ReadIdentity(files.BackupPath + ".rejected"));
        }

        SelfUpdateReplacement.Rollback(files.Paths, files.BackupPath, SelfUpdateTestFiles.OriginalIdentity);
        Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity, SelfUpdatePaths.ReadIdentity(files.Paths.InstalledPath));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void WindowsRollbackOccupiedRejectedPathPreservesEverything()
    {
        using var files = new SelfUpdateTestFiles();
        SelfUpdateReplacement.Replace(files.Paths, files.BackupPath);
        File.WriteAllText(files.BackupPath + ".rejected", "recoverable");
        Assert.ThrowsExactly<DotnetInstallException>(() => SelfUpdateReplacement.Rollback(files.Paths, files.BackupPath, SelfUpdateTestFiles.OriginalIdentity));
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
            SelfUpdateReplacement.Replace(files.Paths, files.BackupPath);
            SelfUpdateVerifier.Verify(files.Paths.InstalledPath, SelfUpdateTestFiles.ReplacementIdentity, TimeSpan.FromSeconds(20));
            SelfUpdateReplacement.Rollback(files.Paths, files.BackupPath, SelfUpdateTestFiles.OriginalIdentity);
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