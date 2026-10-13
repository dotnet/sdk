// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Win32.SafeHandles;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Utils.Tests;

[TestClass]
public partial class FileLockOwnerFinderTests : SdkTest
{
    [TestMethod]
    public void GivenALockedFileItFindsTheOwningProcessWhenSupported()
    {
        string filePath = Path.Combine(TestAssetsManager.CreateTestDirectory().Path, "lock");
        using var fileLock = new FileStream(
            filePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);

        FileLockOwner? owner = FileLockOwnerFinder.TryGetFileLockOwner(filePath);

        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            owner.Should().NotBeNull();
            owner.ProcessId.Should().Be(Environment.ProcessId);
            owner.ProcessName.Should().NotBeNullOrWhiteSpace();
        }
        else
        {
            owner.Should().BeNull();
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public void GivenAProcessWithAnUnrelatedLockAndTheTargetOpenItDoesNotReportThatProcess()
    {
        string directory = TestAssetsManager.CreateTestDirectory().Path;
        string targetPath = Path.Combine(directory, "target");
        string unrelatedPath = Path.Combine(directory, "unrelated");
        File.WriteAllText(targetPath, string.Empty);
        using var target = new SafeFileHandle((IntPtr)OpenWithoutLock(targetPath, flags: 0), ownsHandle: true);
        target.IsInvalid.Should().BeFalse();
        using var unrelatedLock = new FileStream(
            unrelatedPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);

        FileLockOwnerFinder.TryGetFileLockOwner(targetPath).Should().BeNull();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.OSX)]
    public void GivenAMacOSProcessWithAnUnrelatedLockAndTheTargetOpenItDoesNotReportThatProcess()
    {
        string directory = TestAssetsManager.CreateTestDirectory().Path;
        string targetPath = Path.Combine(directory, "target");
        string unrelatedPath = Path.Combine(directory, "unrelated");
        using var target = new FileStream(
            targetPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.ReadWrite);
        using var unrelatedLock = new FileStream(
            unrelatedPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);

        FileLockOwnerFinder.TryGetFileLockOwner(targetPath).Should().BeNull();
    }

    [TestMethod]
    public void GivenAnUnknownFileItDoesNotReportAnOwner()
    {
        string filePath = Path.Combine(TestAssetsManager.CreateTestDirectory().Path, "missing");

        FileLockOwnerFinder.TryGetFileLockOwner(filePath).Should().BeNull();
    }

    /// <summary>
    /// Opens a descriptor without the advisory lock that <see cref="FileStream"/> normally acquires on Unix.
    /// </summary>
    [LibraryImport("libc", EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int OpenWithoutLock(string path, int flags);
}
