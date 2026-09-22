// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>Exercises deferred cleanup against isolated installation directories and real file locks.</summary>
[TestClass]
public class SelfUpdateCleanupTests : SdkTest
{
    private const int ExpiredBackupAgeDays = 8;
    private const string Version = SelfUpdateTestFiles.OriginalVersion;
    private const int RetainedBackupAgeDays = 6;
    private DirectoryInfo _directory = null!;
    private string _installedPath = null!;
    private string _lockPath = null!;
    private SelfUpdateTestFiles _files = null!;

    [TestInitialize]
    public void Initialize()
    {
        _files = new SelfUpdateTestFiles();
        _directory = new DirectoryInfo(_files.Paths.DirectoryPath);
        _installedPath = Path.Combine(_directory.FullName, "dotnetup.exe");
        _lockPath = Path.Combine(_directory.FullName, "dotnetup.update.lock");
        if (_installedPath != _files.Paths.InstalledPath)
        {
            File.Move(_files.Paths.InstalledPath, _installedPath);
        }
    }

    [TestCleanup]
    public void Cleanup() => _files.Dispose();

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DeletesOldBackupsButKeepsFreshAndFutureBackups(bool ownsUpdateLock)
    {
        var old = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));
        var fresh = CreateBackup(TimeSpan.FromDays(RetainedBackupAgeDays));
        var future = CreateBackup(TimeSpan.FromDays(-2));

        RunCleanup(ownsUpdateLock);

        Assert.IsFalse(File.Exists(old));
        Assert.IsTrue(File.Exists(fresh));
        Assert.IsTrue(File.Exists(future));
        AssertUpdateLockAvailable();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DeletesOldRejectedBackupsButKeepsFreshRejectedBackups(bool ownsUpdateLock)
    {
        var old = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays), ".rejected");
        var fresh = CreateBackup(TimeSpan.FromDays(RetainedBackupAgeDays), ".rejected");

        RunCleanup(ownsUpdateLock);

        Assert.IsFalse(File.Exists(old));
        Assert.IsTrue(File.Exists(fresh));
    }

    [TestMethod]
    public void BusyUpdateLockSkipsCleanup()
    {
        var backup = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));
        using (var updateLock = ScopedLockFile.TryAcquireExclusive(_lockPath))
        {
            Assert.IsNotNull(updateLock);
            SelfUpdateCleanup.TryRun(_installedPath, Version);
            Assert.IsTrue(File.Exists(backup));
            using var competingLock = ScopedLockFile.TryAcquireExclusive(_lockPath);
            Assert.IsNull(competingLock);
        }

        SelfUpdateCleanup.TryRun(_installedPath, Version);
        Assert.IsFalse(File.Exists(backup));
        AssertUpdateLockAvailable();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MissingCanonicalPreservesBackups(bool ownsUpdateLock)
    {
        var backup = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));
        File.Delete(_installedPath);

        RunCleanup(ownsUpdateLock);

        Assert.IsTrue(File.Exists(backup));
        Assert.IsFalse(File.Exists(_installedPath));
        AssertUpdateLockAvailable();
    }

    [TestMethod]
    [DataRow("empty", false)]
    [DataRow("empty", true)]
    [DataRow("wrong", false)]
    [DataRow("wrong", true)]
    [DataRow("nonzero", false)]
    [DataRow("nonzero", true)]
    public void InvalidVersionOutputPreservesBackups(string mode, bool ownsUpdateLock)
    {
        var backup = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));
        File.WriteAllText(_installedPath + ".mode", mode);

        RunCleanup(ownsUpdateLock);

        Assert.IsTrue(File.Exists(backup));
        Assert.HasCount(1, File.ReadAllLines(_installedPath + ".invocations"));
        AssertUpdateLockAvailable();
    }

    [TestMethod]
    [DataRow(Version + "+different-build", false)]
    [DataRow(Version + "+different-build", true)]
    [DataRow("", false)]
    [DataRow("", true)]
    [DataRow("invalid", false)]
    [DataRow("invalid", true)]
    [DataRow(SelfUpdateTestFiles.ReplacementVersion, false)]
    [DataRow(SelfUpdateTestFiles.ReplacementVersion, true)]
    public void DifferentOrMalformedLoadedVersionPreservesBackups(string loadedVersion, bool ownsUpdateLock)
    {
        var backup = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));

        RunCleanup(ownsUpdateLock, loadedVersion: loadedVersion);

        Assert.IsTrue(File.Exists(backup));
        AssertUpdateLockAvailable();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void UnreadableCanonicalPreservesBackups(bool ownsUpdateLock)
    {
        var backup = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));
        using var canonical = new FileStream(_installedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        RunCleanup(ownsUpdateLock);

        Assert.IsTrue(File.Exists(backup));
        AssertUpdateLockAvailable();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OnlyExactBackupNamesAreDeleted(bool ownsUpdateLock)
    {
        var originalBytes = File.ReadAllBytes(_installedPath);
        var transaction = Guid.NewGuid().ToString("N")[..8];
        string[] names =
        [
            "dotnetup.activity.lock",
            "dotnetup.exe.new",
            "dotnetup.exe.old.",
            "dotnetup.exe.old.invalid",
            "dotnetup.exe.old." + Guid.NewGuid().ToString("D"),
            "dotnetup.exe.old." + Guid.NewGuid().ToString("N"),
            "dotnetup.exe.old." + new string('g', 32),
            "dotnetup.exe.old." + new string('g', 8),
            "dotnetup.exe.old." + transaction[..^1],
            "dotnetup.exe.old." + transaction + "x",
            "dotnetup.exe.old." + transaction + ".rejected.extra",
            "dotnetup.exe.old." + transaction + ".REJECTED",
            "other.exe.old." + transaction,
            "prefix-dotnetup.exe.old." + transaction,
            "dotnetup.old." + transaction,
        ];
        foreach (var name in names)
        {
            var path = Path.Combine(_directory.FullName, name);
            File.WriteAllText(path, "preserve");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-ExpiredBackupAgeDays));
        }

        var backup = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));
        RunCleanup(ownsUpdateLock);

        Assert.IsFalse(File.Exists(backup));
        foreach (var name in names)
        {
            Assert.AreEqual("preserve", File.ReadAllText(Path.Combine(_directory.FullName, name)));
        }

        Assert.AreSequenceEqual(originalBytes, File.ReadAllBytes(_installedPath));
        Assert.AreEqual(0L, new FileInfo(_lockPath).Length);
    }

    [TestMethod]
    public void UsesTheExactInstalledFileNameIncludingUnixNames()
    {
        var installedPath = Path.Combine(_directory.FullName, "dotnetup");
        SelfUpdateTestFiles.WriteExecutable(installedPath, Version);
        var backup = installedPath + ".old." + Guid.NewGuid().ToString("N")[..8];
        File.WriteAllText(backup, "old");
        File.SetLastWriteTimeUtc(backup, DateTime.UtcNow.AddDays(-ExpiredBackupAgeDays));
        var otherBackup = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));

        SelfUpdateCleanup.TryRun(installedPath, Version);

        Assert.IsFalse(File.Exists(backup));
        Assert.IsTrue(File.Exists(otherBackup));
        Assert.IsTrue(File.Exists(installedPath));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BoundsDeletionsPerLaunch(bool ownsUpdateLock)
    {
        for (var count = 0; count < 80; count++)
        {
            CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));
        }

        RunCleanup(ownsUpdateLock);

        var remaining = Directory.GetFiles(_directory.FullName, "dotnetup.exe.old.*").Length;
        Assert.IsTrue(remaining >= 48 && remaining < 80, $"Unexpected remaining backups: {remaining}");
        AssertUpdateLockAvailable();
    }

    [TestMethod]
    public void FreshMatchingEntriesAlsoConsumeEnumerationBudget()
    {
        for (var count = 0; count < 80; count++)
        {
            CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));
        }

        File.WriteAllBytes(_lockPath, []);
        var firstEntries = _directory.EnumerateFileSystemInfos("dotnetup.exe.old.*").Take(32).ToArray();
        foreach (var entry in firstEntries)
        {
            File.SetLastWriteTimeUtc(entry.FullName, DateTime.UtcNow);
        }

        SelfUpdateCleanup.TryRun(_installedPath, Version);

        Assert.HasCount(80, Directory.GetFiles(_directory.FullName, "dotnetup.exe.old.*"));
    }

    [TestMethod]
    public void UnrelatedEntriesDoNotConsumeEnumerationBudget()
    {
        for (var count = 0; count < 80; count++)
        {
            File.WriteAllText(Path.Combine(_directory.FullName, $"unrelated-{count}"), "preserve");
        }

        var backup = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));

        SelfUpdateCleanup.TryRun(_installedPath, Version);

        Assert.IsFalse(File.Exists(backup));
    }

    [TestMethod]
    public void SkipsDirectoriesAndDoesNotRecurse()
    {
        var backupDirectory = Directory.CreateDirectory(_installedPath + ".old." + Guid.NewGuid().ToString("N")[..8]);
        var nestedBackup = Path.Combine(backupDirectory.FullName, "dotnetup.exe.old." + Guid.NewGuid().ToString("N")[..8]);
        File.WriteAllText(nestedBackup, "preserve");
        File.SetLastWriteTimeUtc(nestedBackup, DateTime.UtcNow.AddDays(-ExpiredBackupAgeDays));
        backupDirectory.LastWriteTimeUtc = DateTime.UtcNow.AddDays(-ExpiredBackupAgeDays);
        var backup = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));

        SelfUpdateCleanup.TryRun(_installedPath, Version);

        Assert.AreEqual("preserve", File.ReadAllText(nestedBackup));
        Assert.IsFalse(File.Exists(backup));
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void SkipsLockedBackupsAndContinuesOnWindows()
    {
        var locked = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));
        var other = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));
        using var handle = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.Read);

        SelfUpdateCleanup.TryRun(_installedPath, Version);

        Assert.IsTrue(File.Exists(locked));
        Assert.IsFalse(File.Exists(other));
        AssertUpdateLockAvailable();
    }

    [TestMethod]
    public void LockOpenFailureIsSwallowed()
    {
        var backup = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));
        Directory.CreateDirectory(_lockPath);

        SelfUpdateCleanup.TryRun(_installedPath, Version);

        Assert.IsTrue(File.Exists(backup));
    }

    [TestMethod]
    public void InvalidOrMissingPathsAreSwallowedWithoutCreatingDirectories()
    {
        var missing = Path.Combine(_directory.FullName, "missing", "dotnetup.exe");
        SelfUpdateCleanup.TryRun(missing, Version);
        SelfUpdateCleanup.RunWithUpdateLock(missing, Version);
        SelfUpdateCleanup.TryRun("\0", Version);
        SelfUpdateCleanup.RunWithUpdateLock("\0", Version);
        Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(missing)));
    }

    [TestMethod]
    public void SkipsBackupFileAndDirectorySymlinks()
    {
        var outside = Directory.CreateDirectory(_directory.FullName + "-target");
        var fileLink = _installedPath + ".old." + Guid.NewGuid().ToString("N")[..8];
        var directoryLink = _installedPath + ".old." + Guid.NewGuid().ToString("N")[..8] + ".rejected";
        try
        {
            var target = Path.Combine(outside.FullName, "target");
            File.WriteAllText(target, "preserve");
            File.SetLastWriteTimeUtc(target, DateTime.UtcNow.AddDays(-ExpiredBackupAgeDays));
            CreateSymbolicLink(fileLink, target, isDirectory: false);
            CreateSymbolicLink(directoryLink, outside.FullName, isDirectory: true);
            var backup = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));

            SelfUpdateCleanup.TryRun(_installedPath, Version);

            Assert.IsFalse(File.Exists(backup));
            Assert.AreEqual("preserve", File.ReadAllText(target));
            Assert.IsNotNull(new FileInfo(fileLink).LinkTarget);
            Assert.IsNotNull(new DirectoryInfo(directoryLink).LinkTarget);
        }
        finally
        {
            File.Delete(fileLink);
            if (Directory.Exists(directoryLink))
            {
                Directory.Delete(directoryLink);
            }

            outside.Delete(recursive: true);
        }
    }

    [TestMethod]
    [DataRow("canonical")]
    [DataRow("lock")]
    [DataRow("directory")]
    [DataRow("ancestor")]
    public void ResolvesDirectoryLinksButRejectsFileLinks(string kind)
    {
        var outside = Directory.CreateDirectory(_directory.FullName + "-target");
        var link = Path.Combine(_directory.FullName, "link");
        try
        {
            var backup = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));
            var installedPath = _installedPath;
            if (kind is "canonical" or "lock")
            {
                var target = Path.Combine(outside.FullName, "target");
                SelfUpdateTestFiles.WriteExecutable(target, Version);
                link = kind == "canonical" ? _installedPath : _lockPath;
                File.Delete(link);
                CreateSymbolicLink(link, target, isDirectory: false);
            }
            else
            {
                var targetDirectory = kind == "ancestor"
                    ? Directory.CreateDirectory(Path.Combine(outside.FullName, "nested")).FullName
                    : outside.FullName;
                var target = Path.Combine(targetDirectory, "dotnetup.exe");
                SelfUpdateTestFiles.WriteExecutable(target, Version);
                backup = target + ".old." + Guid.NewGuid().ToString("N")[..8];
                File.WriteAllText(backup, "preserve");
                File.SetLastWriteTimeUtc(backup, DateTime.UtcNow.AddDays(-ExpiredBackupAgeDays));
                CreateSymbolicLink(link, outside.FullName, isDirectory: true);
                installedPath = kind == "ancestor" ? Path.Combine(link, "nested", "dotnetup.exe") : Path.Combine(link, "dotnetup.exe");
            }

            SelfUpdateCleanup.TryRun(installedPath, Version);

            if (!OperatingSystem.IsWindows() && kind is "directory" or "ancestor")
            {
                Assert.IsFalse(File.Exists(backup));
                Assert.IsTrue(File.Exists(Path.Combine(Path.GetDirectoryName(installedPath)!, "dotnetup.update.lock")));
            }
            else
            {
                Assert.IsTrue(File.Exists(backup));
                Assert.IsFalse(File.Exists(Path.Combine(outside.FullName, "dotnetup.update.lock")));
                Assert.IsFalse(File.Exists(Path.Combine(outside.FullName, "nested", "dotnetup.update.lock")));
            }
        }
        finally
        {
            if (kind is "canonical" or "lock")
            {
                File.Delete(link);
            }
            else if (Directory.Exists(link))
            {
                Directory.Delete(link);
            }

            outside.Delete(recursive: true);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    public void CleanupWithOwnedLockResolvesDirectoryLinks()
    {
        var link = _directory.FullName + "-link";
        try
        {
            Directory.CreateSymbolicLink(link, _directory.FullName);
            var backup = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));
            using var updateLock = ScopedLockFile.TryAcquireExclusive(_lockPath);
            Assert.IsNotNull(updateLock);

            SelfUpdateCleanup.RunWithUpdateLock(Path.Combine(link, Path.GetFileName(_installedPath)), Version);

            Assert.IsFalse(File.Exists(backup));
            using var competingLock = ScopedLockFile.TryAcquireExclusive(Path.Combine(link, "dotnetup.update.lock"));
            Assert.IsNull(competingLock);
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    private void RunCleanup(bool ownsUpdateLock, string loadedVersion = Version)
    {
        if (ownsUpdateLock)
        {
            using var updateLock = ScopedLockFile.TryAcquireExclusive(_lockPath);
            Assert.IsNotNull(updateLock);
            SelfUpdateCleanup.RunWithUpdateLock(_installedPath, loadedVersion);
            using var competingLock = ScopedLockFile.TryAcquireExclusive(_lockPath);
            Assert.IsNull(competingLock);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void NoEligibleBackupsDoesNotStartVersionProbe(bool ownsUpdateLock)
        {
            CreateBackup(TimeSpan.FromDays(RetainedBackupAgeDays));
            File.WriteAllText(_installedPath + ".mode", "timeout");

            RunCleanup(ownsUpdateLock);

            Assert.IsFalse(File.Exists(_installedPath + ".invocations"));
        }

        [TestMethod]
        public void ManyEligibleBackupsUseOnlyOneVersionProbeWithParentUpdateLock()
        {
            CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));
            CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays), ".rejected");

            RunCleanup(ownsUpdateLock: true);

            Assert.HasCount(1, File.ReadAllLines(_installedPath + ".invocations"));
            Assert.IsEmpty(Directory.GetFiles(_directory.FullName, "dotnetup.exe.old.*"));
        }
        else
        {
            SelfUpdateCleanup.TryRun(_installedPath, loadedVersion);
        }
    }

    private string CreateBackup(TimeSpan age, string suffix = "")
    {
        var path = _installedPath + ".old." + Guid.NewGuid().ToString("N")[..8] + suffix;
        File.WriteAllText(path, "backup");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow - age);
        return path;
    }

    private void AssertUpdateLockAvailable()
    {
        using var updateLock = ScopedLockFile.TryAcquireExclusive(_lockPath);
        Assert.IsNotNull(updateLock);
    }

    private static void CreateSymbolicLink(string path, string target, bool isDirectory)
    {
        try
        {
            if (isDirectory)
            {
                Directory.CreateSymbolicLink(path, target);
            }
            else
            {
                File.CreateSymbolicLink(path, target);
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or PlatformNotSupportedException ||
            exception is IOException && (exception.HResult & 0xffff) == 1314)
        {
            Assert.Inconclusive($"Symbolic links are not supported or permitted: {exception.Message}");
        }
    }
}