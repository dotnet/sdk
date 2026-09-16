// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

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
            using var first = NonSafeCommandGate.Enter(paths, new string('a', 64));
            using var second = NonSafeCommandGate.Enter(paths, new string('a', 64));
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
            var exception = Assert.ThrowsExactly<DotnetInstallException>(() => NonSafeCommandGate.Enter(paths, new string('a', 64)));
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
            var exception = Assert.ThrowsExactly<DotnetInstallException>(() => NonSafeCommandGate.Enter(paths, new string('b', 64)));
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
        File.WriteAllBytes(paths.InstalledPath, Encoding.ASCII.GetBytes("DOTNETUP-ID-REC\0\u0001\0\0\0\u0040\0\0\0" + new string('a', 64) + "END-ID\0\0"));
        return paths;
    }
}