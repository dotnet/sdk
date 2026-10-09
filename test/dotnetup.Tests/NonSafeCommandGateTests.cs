// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Microsoft.NET.TestFramework;

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
    [OSCondition(OperatingSystems.Windows)]
    public void BusyGateDuringReplacementGapReportsUpdateInProgress()
    {
        using var files = new SelfUpdateTestFiles();
        var retainedPath = files.Paths.InstalledPath + ".retained";
        using var updater = ScopedLockFile.TryAcquireExclusive(files.Paths.ActivityLockPath);
        Assert.IsNotNull(updater);
        File.Move(files.Paths.InstalledPath, retainedPath);
        try
        {
            var exception = Assert.ThrowsExactly<DotnetInstallException>(() =>
                NonSafeCommandGate.Enter(files.Paths, SelfUpdateTestFiles.OriginalVersion));

            Assert.AreEqual(DotnetInstallErrorCode.DotnetupUpdateInProgress, exception.ErrorCode);
        }
        finally
        {
            File.Move(retainedPath, files.Paths.InstalledPath);
        }
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
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    public void InvalidVersionOutputReleasesActivityLock()
    {
        using var files = new SelfUpdateTestFiles(mode: "empty");
        var exception = Assert.ThrowsExactly<DotnetInstallException>(() =>
            NonSafeCommandGate.Enter(files.Paths, SelfUpdateTestFiles.OriginalVersion));
        Assert.AreEqual(DotnetInstallErrorCode.DotnetupIdentityUnavailable, exception.ErrorCode);
        using var update = ScopedLockFile.TryAcquireExclusive(files.Paths.ActivityLockPath);
        Assert.IsNotNull(update);
    }

    [TestMethod]
    public void UnreadableInstalledVersionReleasesActivityLock()
    {
        using var files = new SelfUpdateTestFiles(executable: false);
        var exception = Assert.ThrowsExactly<DotnetInstallException>(() =>
            NonSafeCommandGate.Enter(files.Paths, SelfUpdateTestFiles.OriginalVersion));
        Assert.AreEqual(DotnetInstallErrorCode.DotnetupIdentityUnavailable, exception.ErrorCode);
        Assert.AreEqual(Microsoft.DotNet.Tools.Bootstrapper.Strings.SelfUpdateIdentityUnavailable, exception.Message);
        using var update = ScopedLockFile.TryAcquireExclusive(files.Paths.ActivityLockPath);
        Assert.IsNotNull(update);
    }

    [TestMethod]
    public void MissingInstallationDirectoryReportsDirectoryUnavailable()
    {
        using var files = new SelfUpdateTestFiles();
        var directory = Path.Combine(files.Paths.DirectoryPath, Guid.NewGuid().ToString("N"), "missing");
        var paths = new SelfUpdatePaths(Path.Combine(directory, OperatingSystem.IsWindows() ? "dotnetup.exe" : "dotnetup"));

        var exception = Assert.ThrowsExactly<DotnetInstallException>(() =>
            NonSafeCommandGate.Enter(paths, SelfUpdateTestFiles.OriginalVersion));

        Assert.AreEqual(DotnetInstallErrorCode.InstallFailed, exception.ErrorCode);
        Assert.AreEqual(
            string.Format(CultureInfo.CurrentCulture,
                Microsoft.DotNet.Tools.Bootstrapper.Strings.SelfUpdateDirectoryUnavailable, paths.DirectoryPath),
            exception.Message);
        Assert.IsInstanceOfType<IOException>(exception.InnerException);
    }

    [TestMethod]
    public void InvalidActivityLockPathReportsCoordinationFileFailure()
    {
        using var files = new SelfUpdateTestFiles();
        Directory.CreateDirectory(files.Paths.ActivityLockPath);

        var exception = Assert.ThrowsExactly<DotnetInstallException>(() =>
            NonSafeCommandGate.Enter(files.Paths, SelfUpdateTestFiles.OriginalVersion));

        Assert.AreEqual(DotnetInstallErrorCode.PermissionDenied, exception.ErrorCode);
        Assert.AreEqual(
            string.Format(CultureInfo.CurrentCulture,
                Microsoft.DotNet.Tools.Bootstrapper.Strings.SelfUpdateActivityLockAccessDenied, files.Paths.ActivityLockPath),
            exception.Message);
        Assert.IsInstanceOfType<UnauthorizedAccessException>(exception.InnerException);
    }

    [TestMethod]
    public void MissingExecutableReportsExecutableUnavailable()
    {
        using var files = new SelfUpdateTestFiles();
        File.Delete(files.Paths.InstalledPath);

        var exception = Assert.ThrowsExactly<DotnetInstallException>(() =>
            NonSafeCommandGate.Enter(files.Paths, SelfUpdateTestFiles.OriginalVersion));

        Assert.AreEqual(DotnetInstallErrorCode.DotnetupIdentityUnavailable, exception.ErrorCode);
        Assert.AreEqual(
            string.Format(CultureInfo.CurrentCulture,
                Microsoft.DotNet.Tools.Bootstrapper.Strings.SelfUpdateExecutableUnavailable, files.Paths.InstalledPath),
            exception.Message);
        Assert.IsInstanceOfType<FileNotFoundException>(exception.InnerException);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void WindowsGateReadsVersionResourceWithoutStartingProcess()
    {
        // The version child would fail in this mode, so passing proves the gate did not start it.
        using var files = new SelfUpdateTestFiles(mode: "nonzero");

        using var lease = NonSafeCommandGate.Enter(files.Paths, SelfUpdateTestFiles.OriginalVersion);

        Assert.IsFalse(File.Exists(files.Paths.InstalledPath + ".invocations"));
        var exception = Assert.ThrowsExactly<DotnetInstallException>(() =>
            NonSafeCommandGate.Enter(files.Paths, SelfUpdateTestFiles.ReplacementVersion));
        Assert.AreEqual(DotnetInstallErrorCode.DotnetupExecutableChanged, exception.ErrorCode);
    }

    [TestMethod]
    public void RenamedExecutablePassesGateAndExcludesUpdates()
    {
        using var files = new SelfUpdateTestFiles();
        var renamed = CreateRenamedExecutable(files, SelfUpdateTestFiles.OriginalVersion);

        using var lease = NonSafeCommandGate.Enter(renamed, SelfUpdateTestFiles.OriginalVersion);

        Assert.AreEqual(files.Paths.ActivityLockPath, renamed.ActivityLockPath);
        using var update = ScopedLockFile.TryAcquireExclusive(files.Paths.ActivityLockPath);
        Assert.IsNull(update);
        // Windows reads the version resource in-process; other platforms start one --version child.
        Assert.AreEqual(!OperatingSystem.IsWindows(), File.Exists(renamed.InstalledPath + ".invocations"));
    }

    [TestMethod]
    public void RenamedExecutableStillRejectsStaleImage()
    {
        using var files = new SelfUpdateTestFiles();
        var renamed = CreateRenamedExecutable(files, SelfUpdateTestFiles.OriginalVersion);

        var exception = Assert.ThrowsExactly<DotnetInstallException>(() =>
            NonSafeCommandGate.Enter(renamed, SelfUpdateTestFiles.ReplacementVersion));

        Assert.AreEqual(DotnetInstallErrorCode.DotnetupExecutableChanged, exception.ErrorCode);
    }

    [TestMethod]
    public void RenamedExecutableStillFailsWhileUpdateHoldsActivityLock()
    {
        using var files = new SelfUpdateTestFiles();
        var renamed = CreateRenamedExecutable(files, SelfUpdateTestFiles.OriginalVersion);
        using var updater = ScopedLockFile.TryAcquireExclusive(files.Paths.ActivityLockPath);
        Assert.IsNotNull(updater);

        var exception = Assert.ThrowsExactly<DotnetInstallException>(() =>
            NonSafeCommandGate.Enter(renamed, SelfUpdateTestFiles.OriginalVersion));

        Assert.AreEqual(DotnetInstallErrorCode.DotnetupUpdateInProgress, exception.ErrorCode);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [SupportedOSPlatform("windows")]
    public void UnwritableDirectoryWithoutLockFileReportsPermissionDenied()
    {
        using var files = new SelfUpdateTestFiles();
        Assert.IsFalse(File.Exists(files.Paths.ActivityLockPath));
        var directory = new DirectoryInfo(files.Paths.DirectoryPath);
        var denyCreate = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.CreateFiles, AccessControlType.Deny);
        var security = directory.GetAccessControl();
        security.AddAccessRule(denyCreate);
        directory.SetAccessControl(security);
        try
        {
            var exception = Assert.ThrowsExactly<DotnetInstallException>(() =>
                NonSafeCommandGate.Enter(files.Paths, SelfUpdateTestFiles.OriginalVersion));

            Assert.AreEqual(DotnetInstallErrorCode.PermissionDenied, exception.ErrorCode);
            Assert.AreEqual(
                string.Format(CultureInfo.CurrentCulture,
                    Microsoft.DotNet.Tools.Bootstrapper.Strings.SelfUpdateActivityLockAccessDenied, files.Paths.ActivityLockPath),
                exception.Message);
            Assert.IsInstanceOfType<UnauthorizedAccessException>(exception.InnerException);
        }
        finally
        {
            security = directory.GetAccessControl();
            security.RemoveAccessRule(denyCreate);
            directory.SetAccessControl(security);
        }
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [UnsupportedOSPlatform("windows")]
    public void UnwritableUnixDirectoryWithoutLockFileReportsPermissionDenied()
    {
        using var files = new SelfUpdateTestFiles();
        if (Environment.UserName == "root")
        {
            Assert.Inconclusive("Directory permissions do not restrict root.");
        }

        var original = File.GetUnixFileMode(files.Paths.DirectoryPath);
        File.SetUnixFileMode(files.Paths.DirectoryPath, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var exception = Assert.ThrowsExactly<DotnetInstallException>(() =>
                NonSafeCommandGate.Enter(files.Paths, SelfUpdateTestFiles.OriginalVersion));

            Assert.AreEqual(DotnetInstallErrorCode.PermissionDenied, exception.ErrorCode);
            Assert.AreEqual(
                string.Format(CultureInfo.CurrentCulture,
                    Microsoft.DotNet.Tools.Bootstrapper.Strings.SelfUpdateActivityLockAccessDenied, files.Paths.ActivityLockPath),
                exception.Message);
        }
        finally
        {
            File.SetUnixFileMode(files.Paths.DirectoryPath, original);
        }
    }

    [TestMethod]
    public void LinkedExecutableReportsUnsupportedLocation()
    {
        using var files = new SelfUpdateTestFiles();
        var link = Path.Combine(files.Paths.DirectoryPath, "linked-" + Path.GetFileName(files.Paths.InstalledPath));
        try
        {
            File.CreateSymbolicLink(link, files.Paths.InstalledPath);
        }
        catch (Exception linkFailure) when (linkFailure is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Inconclusive($"Symbolic links are not supported or permitted: {linkFailure.Message}");
        }

        var exception = Assert.ThrowsExactly<DotnetInstallException>(() =>
            NonSafeCommandGate.Enter(new SelfUpdatePaths(link), SelfUpdateTestFiles.OriginalVersion));

        Assert.AreEqual(DotnetInstallErrorCode.DotnetupUnsupportedInstallLocation, exception.ErrorCode);
        Assert.Contains(link, exception.Message);
    }

    private static SelfUpdatePaths CreateRenamedExecutable(SelfUpdateTestFiles files, string version)
    {
        // Matches the RID-suffixed file name served by the aka.ms download links.
        var rid = DotnetupUtilities.GetRuntimeIdentifier(InstallerUtilities.GetDefaultInstallArchitecture());
        var path = Path.Combine(files.Paths.DirectoryPath, BlobFeedUrlBuilder.GetDotnetupFileName(rid));
        SelfUpdateTestFiles.WriteExecutable(path, version);
        return new SelfUpdatePaths(path);
    }
}
