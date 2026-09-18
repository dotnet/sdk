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
        var directory = Directory.CreateTempSubdirectory("dotnetup-gate-");
        try
        {
            var paths = CreateExecutable(directory.FullName);
            using var first = NonSafeCommandGate.Enter(paths, SelfUpdateTestFiles.OriginalIdentity);
            using var second = NonSafeCommandGate.Enter(paths, SelfUpdateTestFiles.OriginalIdentity);
            using var update = ScopedLockFile.TryAcquireExclusive(paths.ActivityLockPath);
            Assert.IsNull(update);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [TestMethod]
    public void BusyGateFailsImmediately()
    {
        var directory = Directory.CreateTempSubdirectory("dotnetup-gate-");
        try
        {
            var paths = CreateExecutable(directory.FullName);
            using var updater = ScopedLockFile.TryAcquireExclusive(paths.ActivityLockPath);
            var exception = Assert.ThrowsExactly<DotnetInstallException>(() => NonSafeCommandGate.Enter(paths, SelfUpdateTestFiles.OriginalIdentity));
            Assert.AreEqual(DotnetInstallErrorCode.DotnetupUpdateInProgress, exception.ErrorCode);
            Assert.AreEqual(Microsoft.DotNet.Tools.Bootstrapper.Strings.SelfUpdateInProgress, exception.Message);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [TestMethod]
    public void StaleImageIsRejectedEvenWhenTheLockWasFree()
    {
        var directory = Directory.CreateTempSubdirectory("dotnetup-gate-");
        try
        {
            var paths = CreateExecutable(directory.FullName);
            var exception = Assert.ThrowsExactly<DotnetInstallException>(() => NonSafeCommandGate.Enter(paths, SelfUpdateTestFiles.ReplacementIdentity));
            Assert.AreEqual(DotnetInstallErrorCode.DotnetupExecutableChanged, exception.ErrorCode);
            using var update = ScopedLockFile.TryAcquireExclusive(paths.ActivityLockPath);
            Assert.IsNotNull(update);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    private static SelfUpdatePaths CreateExecutable(string directory)
    {
        var paths = new SelfUpdatePaths(Path.Combine(directory, OperatingSystem.IsWindows() ? "dotnetup.exe" : "dotnetup"));
        SelfUpdateTestFiles.WriteIdentity(paths.InstalledPath, SelfUpdateTestFiles.OriginalIdentity);
        return paths;
    }
}