// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class SelfUpdateVerifierTests : SdkTest
{
    [TestMethod]
    [DataRow("valid")]
    [DataRow("none")]
    [DataRow("lf")]
    [DataRow("crlf")]
    [DataRow("stderr-flood")]
    public void ExactIdentityAndNormalNewlinesAreAccepted(string mode)
    {
        using var files = new SelfUpdateTestFiles(executable: true, mode);
        SelfUpdateVerifier.Verify(files.Paths.InstalledPath, SelfUpdateTestFiles.OriginalIdentity, TimeSpan.FromSeconds(20));
    }

    [TestMethod]
    [DataRow("empty")]
    [DataRow("wrong")]
    [DataRow("upper")]
    [DataRow("extra")]
    [DataRow("space")]
    [DataRow("bom")]
    [DataRow("cr")]
    [DataRow("nonzero")]
    [DataRow("flood")]
    public void BadIdentityExitStatusAndExcessiveOutputAreRejected(string mode)
    {
        using var files = new SelfUpdateTestFiles(executable: true, mode);
        var exception = Assert.ThrowsExactly<DotnetInstallException>(() =>
            SelfUpdateVerifier.Verify(files.Paths.InstalledPath, SelfUpdateTestFiles.OriginalIdentity, TimeSpan.FromSeconds(20)));
        Assert.AreEqual(DotnetInstallErrorCode.InstallFailed, exception.ErrorCode);
        Assert.IsLessThan(5000, exception.Message.Length);
        Assert.IsInstanceOfType<IOException>(exception.InnerException);
        Assert.Contains("Verification exited", exception.Message);
    }

    [TestMethod]
    public void TimeoutTerminatesTheChildAndDoesNotHoldFilesOpen()
    {
        using var files = new SelfUpdateTestFiles(executable: true, "timeout");
        var watch = Stopwatch.StartNew();
        var exception = Assert.ThrowsExactly<DotnetInstallException>(() =>
            SelfUpdateVerifier.Verify(files.Paths.InstalledPath, SelfUpdateTestFiles.OriginalIdentity, TimeSpan.FromSeconds(3)));
        Assert.AreEqual(DotnetInstallErrorCode.InstallFailed, exception.ErrorCode);
        Assert.IsLessThan(TimeSpan.FromSeconds(15), watch.Elapsed);
        Assert.IsTrue(File.Exists(files.Paths.InstalledPath + ".pid"), "The fixture must have started before timing out.");
        var processId = int.Parse(File.ReadAllText(files.Paths.InstalledPath + ".pid"), CultureInfo.InvariantCulture);
        try
        {
            using var process = Process.GetProcessById(processId);
            Assert.IsTrue(process.HasExited);
        }
        catch (ArgumentException)
        {
        }

        File.Move(files.Paths.InstalledPath, files.BackupPath);
        Assert.IsTrue(File.Exists(files.BackupPath));
    }

    [TestMethod]
    public void UnstartableExecutableAndInvalidTimeoutAreReported()
    {
        using var files = new SelfUpdateTestFiles();
        Assert.ThrowsExactly<DotnetInstallException>(() => SelfUpdateVerifier.Verify(files.Paths.InstalledPath, SelfUpdateTestFiles.OriginalIdentity, TimeSpan.FromSeconds(3)));
        Assert.ThrowsExactly<DotnetInstallException>(() => SelfUpdateVerifier.Verify(files.Paths.InstalledPath, SelfUpdateTestFiles.OriginalIdentity, TimeSpan.Zero));
        Assert.ThrowsExactly<DotnetInstallException>(() => SelfUpdateVerifier.Verify(files.Paths.InstalledPath, "invalid", TimeSpan.FromSeconds(3)));
    }
}