// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class NonSafeCommandGateTests
{
    [TestMethod]
    public void MatchingCommandsShareTheGateAndExcludeUpdates()
    {
        using var files = new SelfUpdateTestFiles();
        using var first = NonSafeCommandGate.Enter(files.Paths, SelfUpdateTestFiles.OriginalVersion);
        using var second = NonSafeCommandGate.Enter(files.Paths, SelfUpdateTestFiles.OriginalVersion);
        using var update = ScopedLockFile.TryAcquireExclusive(files.Paths.ActivityLockPath);
        Assert.IsNull(update);
    }

    [TestMethod]
    public void BusyGateFailsBeforeStartingVersionProbe()
    {
        using var files = new SelfUpdateTestFiles();
        using var updater = ScopedLockFile.TryAcquireExclusive(files.Paths.ActivityLockPath);
        var exception = Assert.ThrowsExactly<DotnetInstallException>(() =>
            NonSafeCommandGate.Enter(files.Paths, SelfUpdateTestFiles.OriginalVersion));
        Assert.AreEqual(DotnetInstallErrorCode.DotnetupUpdateInProgress, exception.ErrorCode);
        Assert.AreEqual(Microsoft.DotNet.Tools.Bootstrapper.Strings.SelfUpdateInProgress, exception.Message);
        Assert.IsFalse(File.Exists(files.Paths.InstalledPath + ".invocations"));
    }

    [TestMethod]
    [DataRow(SelfUpdateTestFiles.ReplacementVersion)]
    [DataRow(SelfUpdateTestFiles.OriginalVersion + "+different-build")]
    public void StaleImageIsRejectedEvenWhenTheLockWasFree(string loadedVersion)
    {
        using var files = new SelfUpdateTestFiles();
        var exception = Assert.ThrowsExactly<DotnetInstallException>(() => NonSafeCommandGate.Enter(files.Paths, loadedVersion));
        Assert.AreEqual(DotnetInstallErrorCode.DotnetupExecutableChanged, exception.ErrorCode);
        using var update = ScopedLockFile.TryAcquireExclusive(files.Paths.ActivityLockPath);
        Assert.IsNotNull(update);
    }

    [TestMethod]
    public void InvalidVersionOutputReleasesActivityLock()
    {
        using var files = new SelfUpdateTestFiles(mode: "empty");
        var exception = Assert.ThrowsExactly<DotnetInstallException>(() =>
            NonSafeCommandGate.Enter(files.Paths, SelfUpdateTestFiles.OriginalVersion));
        Assert.AreEqual(DotnetInstallErrorCode.DotnetupIdentityUnavailable, exception.ErrorCode);
        using var update = ScopedLockFile.TryAcquireExclusive(files.Paths.ActivityLockPath);
        Assert.IsNotNull(update);
    }
}
