// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>Exercises deferred cleanup against isolated installation directories and real file locks.</summary>
[TestClass]
public class SelfUpdateCleanupTests : SdkTest
{
    private const int ExpiredBackupAgeDays = 8;
    private const string Identity = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const int RetainedBackupAgeDays = 6;
    private DirectoryInfo _directory = null!;
    private string _installedPath = null!;
    private string _lockPath = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Directory.CreateTempSubdirectory("self-update-cleanup-");
        _installedPath = Path.Combine(_directory.FullName, "dotnetup.exe");
        _lockPath = Path.Combine(_directory.FullName, "dotnetup.update.lock");
        File.WriteAllBytes(_installedPath, CreateRecord());
    }

    [TestCleanup]
    public void Cleanup() => _directory.Delete(recursive: true);

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
            SelfUpdateCleanup.TryRun(_installedPath, Identity);
            Assert.IsTrue(File.Exists(backup));
            using var competingLock = ScopedLockFile.TryAcquireExclusive(_lockPath);
            Assert.IsNull(competingLock);
        }

        SelfUpdateCleanup.TryRun(_installedPath, Identity);
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
    [DataRow("missing", false)]
    [DataRow("missing", true)]
    [DataRow("truncated", false)]
    [DataRow("truncated", true)]
    [DataRow("malformed", false)]
    [DataRow("malformed", true)]
    [DataRow("duplicate", false)]
    [DataRow("duplicate", true)]
    public void InvalidCanonicalPreservesBackups(string kind, bool ownsUpdateLock)
    {
        var backup = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));
        var record = CreateRecord();
        var bytes = kind switch
        {
            "missing" => Array.Empty<byte>(),
            "truncated" => record[..^1],
            "malformed" => CreateRecord(new string('g', 64)),
            "duplicate" => record.Concat(record).ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        File.WriteAllBytes(_installedPath, bytes);

        RunCleanup(ownsUpdateLock);

        Assert.IsTrue(File.Exists(backup));
        AssertUpdateLockAvailable();
    }

    [TestMethod]
    [DataRow("ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff", false)]
    [DataRow("ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff", true)]
    [DataRow("", false)]
    [DataRow("", true)]
    [DataRow("invalid", false)]
    [DataRow("invalid", true)]
    [DataRow("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", false)]
    [DataRow("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true)]
    public void DifferentOrMalformedLoadedIdentityPreservesBackups(string loadedIdentity, bool ownsUpdateLock)
    {
        var backup = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));

        RunCleanup(ownsUpdateLock, loadedIdentity: loadedIdentity);

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
        var transaction = Guid.NewGuid().ToString("N");
        string[] names =
        [
            "dotnetup.activity.lock",
            "dotnetup.exe.new",
            "dotnetup.exe.old.",
            "dotnetup.exe.old.invalid",
            "dotnetup.exe.old." + Guid.NewGuid().ToString("D"),
            "dotnetup.exe.old." + new string('g', 32),
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

        Assert.AreSequenceEqual(CreateRecord(), File.ReadAllBytes(_installedPath));
        Assert.AreEqual(0L, new FileInfo(_lockPath).Length);
    }

    [TestMethod]
    public void UsesTheExactInstalledFileNameIncludingUnixNames()
    {
        var installedPath = Path.Combine(_directory.FullName, "dotnetup");
        File.WriteAllBytes(installedPath, CreateRecord());
        var backup = installedPath + ".old." + Guid.NewGuid().ToString("N");
        File.WriteAllText(backup, "old");
        File.SetLastWriteTimeUtc(backup, DateTime.UtcNow.AddDays(-ExpiredBackupAgeDays));
        var otherBackup = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));

        SelfUpdateCleanup.TryRun(installedPath, Identity);

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

        SelfUpdateCleanup.TryRun(_installedPath, Identity);

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

        SelfUpdateCleanup.TryRun(_installedPath, Identity);

        Assert.IsFalse(File.Exists(backup));
    }

    [TestMethod]
    public void SkipsDirectoriesAndDoesNotRecurse()
    {
        var backupDirectory = Directory.CreateDirectory(_installedPath + ".old." + Guid.NewGuid().ToString("N"));
        var nestedBackup = Path.Combine(backupDirectory.FullName, "dotnetup.exe.old." + Guid.NewGuid().ToString("N"));
        File.WriteAllText(nestedBackup, "preserve");
        File.SetLastWriteTimeUtc(nestedBackup, DateTime.UtcNow.AddDays(-ExpiredBackupAgeDays));
        backupDirectory.LastWriteTimeUtc = DateTime.UtcNow.AddDays(-ExpiredBackupAgeDays);
        var backup = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));

        SelfUpdateCleanup.TryRun(_installedPath, Identity);

        Assert.AreEqual("preserve", File.ReadAllText(nestedBackup));
        Assert.IsFalse(File.Exists(backup));
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void SkipsLockedBackupsAndContinuesOnWindows()
    {
        var locked = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));
        var other = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));
        using var handle = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.Read);

        SelfUpdateCleanup.TryRun(_installedPath, Identity);

        Assert.IsTrue(File.Exists(locked));
        Assert.IsFalse(File.Exists(other));
        AssertUpdateLockAvailable();
    }

    [TestMethod]
    public void LockOpenFailureIsSwallowed()
    {
        var backup = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));
        Directory.CreateDirectory(_lockPath);

        SelfUpdateCleanup.TryRun(_installedPath, Identity);

        Assert.IsTrue(File.Exists(backup));
    }

    [TestMethod]
    public void InvalidOrMissingPathsAreSwallowedWithoutCreatingDirectories()
    {
        var missing = Path.Combine(_directory.FullName, "missing", "dotnetup.exe");
        SelfUpdateCleanup.TryRun(missing, Identity);
        SelfUpdateCleanup.RunWithUpdateLock(missing, Identity);
        SelfUpdateCleanup.TryRun("\0", Identity);
        SelfUpdateCleanup.RunWithUpdateLock("\0", Identity);
        Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(missing)));
    }

    [TestMethod]
    public void SkipsBackupFileAndDirectorySymlinks()
    {
        var outside = Directory.CreateTempSubdirectory("self-update-cleanup-target-");
        var fileLink = _installedPath + ".old." + Guid.NewGuid().ToString("N");
        var directoryLink = _installedPath + ".old." + Guid.NewGuid().ToString("N") + ".rejected";
        try
        {
            var target = Path.Combine(outside.FullName, "target");
            File.WriteAllText(target, "preserve");
            File.SetLastWriteTimeUtc(target, DateTime.UtcNow.AddDays(-ExpiredBackupAgeDays));
            CreateSymbolicLink(fileLink, target, isDirectory: false);
            CreateSymbolicLink(directoryLink, outside.FullName, isDirectory: true);
            var backup = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));

            SelfUpdateCleanup.TryRun(_installedPath, Identity);

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
    public void SkipsUnexpectedSymlinksBeforeOpeningFiles(string kind)
    {
        var outside = Directory.CreateTempSubdirectory("self-update-cleanup-target-");
        var link = Path.Combine(_directory.FullName, "link");
        try
        {
            var backup = CreateBackup(TimeSpan.FromDays(ExpiredBackupAgeDays));
            var installedPath = _installedPath;
            if (kind is "canonical" or "lock")
            {
                var target = Path.Combine(outside.FullName, "target");
                File.WriteAllBytes(target, CreateRecord());
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
                File.WriteAllBytes(target, CreateRecord());
                backup = target + ".old." + Guid.NewGuid().ToString("N");
                File.WriteAllText(backup, "preserve");
                File.SetLastWriteTimeUtc(backup, DateTime.UtcNow.AddDays(-ExpiredBackupAgeDays));
                CreateSymbolicLink(link, outside.FullName, isDirectory: true);
                installedPath = kind == "ancestor" ? Path.Combine(link, "nested", "dotnetup.exe") : Path.Combine(link, "dotnetup.exe");
            }

            SelfUpdateCleanup.TryRun(installedPath, Identity);

            Assert.IsTrue(File.Exists(backup));
            Assert.IsFalse(File.Exists(Path.Combine(outside.FullName, "dotnetup.update.lock")));
            Assert.IsFalse(File.Exists(Path.Combine(outside.FullName, "nested", "dotnetup.update.lock")));
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

    private void RunCleanup(bool ownsUpdateLock, string loadedIdentity = Identity)
    {
        if (ownsUpdateLock)
        {
            using var updateLock = ScopedLockFile.TryAcquireExclusive(_lockPath);
            Assert.IsNotNull(updateLock);
            SelfUpdateCleanup.RunWithUpdateLock(_installedPath, loadedIdentity);
            using var competingLock = ScopedLockFile.TryAcquireExclusive(_lockPath);
            Assert.IsNull(competingLock);
        }
        else
        {
            SelfUpdateCleanup.TryRun(_installedPath, loadedIdentity);
        }
    }

    private string CreateBackup(TimeSpan age, string suffix = "")
    {
        var path = _installedPath + ".old." + Guid.NewGuid().ToString("N") + suffix;
        File.WriteAllText(path, "backup");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow - age);
        return path;
    }

    private void AssertUpdateLockAvailable()
    {
        using var updateLock = ScopedLockFile.TryAcquireExclusive(_lockPath);
        Assert.IsNotNull(updateLock);
    }

    private static byte[] CreateRecord(string identity = Identity)
        => Encoding.ASCII.GetBytes("DOTNETUP-ID-REC\0\u0001\0\0\0\u0040\0\0\0" + identity + "END-ID\0\0");

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