// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.Text;
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
    [DataRow("cr")]
    [DataRow("space")]
    [DataRow("stderr-flood")]
    public void ParseableVersionOutputIsAccepted(string mode)
    {
        using var files = new SelfUpdateTestFiles(executable: true, mode);
        SelfUpdateVerifier.Verify(files.Paths.InstalledPath, TimeSpan.FromSeconds(20));
    }

    [TestMethod]
    public async Task PublishedFixtureUsesRelativeRuntimeInsteadOfEnvironment()
    {
        using var files = new SelfUpdateTestFiles(executable: true);
        var startInfo = new ProcessStartInfo(files.Paths.InstalledPath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("--version");
        foreach (var variable in new[] { "DOTNET_ROOT", "DOTNET_ROOT_X64", "DOTNET_ROOT_X86", "DOTNET_ROOT_ARM64", "DOTNET_ROOT(x86)" })
        {
            startInfo.Environment[variable] = Path.Combine(files.Paths.DirectoryPath, "missing-runtime");
        }

        using var process = Process.Start(startInfo);
        Assert.IsNotNull(process);
        var stdout = process.StandardOutput.ReadToEndAsync(TestContext.CancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(TestContext.CancellationToken);
        try
        {
            await process.WaitForExitAsync(TestContext.CancellationToken).WaitAsync(TimeSpan.FromSeconds(20), TestContext.CancellationToken);
            Assert.AreEqual(0, process.ExitCode, await stderr);
            Assert.AreEqual(SelfUpdateTestFiles.OriginalIdentity.Split('|')[0] + Environment.NewLine, await stdout);
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

    [TestMethod]
    [DataRow("empty", 0)]
    [DataRow("wrong", 0)]
    [DataRow("extra", 0)]
    [DataRow("bom", 0)]
    [DataRow("nonzero", 17)]
    [DataRow("flood", 0)]
    public void BadVersionExitStatusAndExcessiveOutputAreRejected(string mode, int expectedExitCode)
    {
        using var files = new SelfUpdateTestFiles(executable: true, mode);
        var exception = Assert.ThrowsExactly<DotnetInstallException>(() =>
            SelfUpdateVerifier.Verify(files.Paths.InstalledPath, TimeSpan.FromSeconds(20)));
        Assert.AreEqual(DotnetInstallErrorCode.InstallFailed, exception.ErrorCode);
        Assert.IsLessThan(5000, exception.Message.Length);
        Assert.IsInstanceOfType<IOException>(exception.InnerException);
        Assert.Contains($"Verification exited with code {expectedExitCode} or", exception.InnerException.Message);
        Assert.Contains($"SelfUpdateProcess mode: {mode}", exception.InnerException.Message);

        var version = SelfUpdateTestFiles.OriginalIdentity.Split('|')[0];
        var expectedOutput = mode switch
        {
            "empty" => [],
            "wrong" => Encoding.ASCII.GetBytes(new string('f', 64) + Environment.NewLine),
            "extra" => Encoding.ASCII.GetBytes(version + Environment.NewLine + version + Environment.NewLine),
            "bom" => new byte[] { 0xef, 0xbb, 0xbf }.Concat(Encoding.ASCII.GetBytes(version)).ToArray(),
            "nonzero" => Encoding.ASCII.GetBytes(version + Environment.NewLine),
            "flood" => Encoding.ASCII.GetBytes(new string('x', 524288) + version + Environment.NewLine),
            _ => throw new InvalidOperationException($"Unknown test mode: {mode}"),
        };
        Assert.IsTrue(File.Exists(files.Paths.InstalledPath + ".stdout"), "The fixture must have finished emitting the selected output.");
        Assert.AreSequenceEqual(expectedOutput, File.ReadAllBytes(files.Paths.InstalledPath + ".stdout"));
    }

    [TestMethod]
    public void TimeoutTerminatesTheChildAndDoesNotHoldFilesOpen()
    {
        using var files = new SelfUpdateTestFiles(executable: true, "timeout");
        var watch = Stopwatch.StartNew();
        var exception = Assert.ThrowsExactly<DotnetInstallException>(() =>
            SelfUpdateVerifier.Verify(files.Paths.InstalledPath, TimeSpan.FromSeconds(3)));
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
        Assert.ThrowsExactly<DotnetInstallException>(() => SelfUpdateVerifier.Verify(files.Paths.InstalledPath, TimeSpan.FromSeconds(3)));
        Assert.ThrowsExactly<DotnetInstallException>(() => SelfUpdateVerifier.Verify(files.Paths.InstalledPath, TimeSpan.Zero));
    }
}