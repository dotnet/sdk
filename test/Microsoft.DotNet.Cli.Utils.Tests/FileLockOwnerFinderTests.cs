// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Utils.Tests;

[TestClass]
public class FileLockOwnerFinderTests : SdkTest
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
    public void GivenAnUnknownFileItDoesNotReportAnOwner()
    {
        string filePath = Path.Combine(TestAssetsManager.CreateTestDirectory().Path, "missing");

        FileLockOwnerFinder.TryGetFileLockOwner(filePath).Should().BeNull();
    }
}
