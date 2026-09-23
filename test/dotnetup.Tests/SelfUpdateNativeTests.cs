// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class SelfUpdateNativeTests : SdkTest
{
    [TestMethod]
    [OSCondition(OperatingSystems.Windows | OperatingSystems.Linux)]
    public void NativeHelpRegistersSelfUpdate()
    {
        using var files = new NativeSelfUpdateFiles();
        Assert.Contains("self update", files.Run(["self", "update", "--help"]));
        Assert.Contains("--version", files.Run(["--help"]));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows | OperatingSystems.Linux)]
    public void NativeVersionBypassesBusyLocks()
    {
        using var files = new NativeSelfUpdateFiles();
        using (var locks = new SelfUpdateCoordinator().Acquire(files.Paths.UpdateLockPath, files.Paths.ActivityLockPath, TestContext.CancellationToken))
        {
            Assert.AreEqual(files.OriginalVersion, files.Run(["--version"]).Trim());
            Assert.Contains("update", files.Run(["--info"], succeeds: false));
        }

        Assert.Contains(files.OriginalVersion, files.Run(["--info"]));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows | OperatingSystems.Linux)]
    public void NativeWorkflowVerifiesCanonicalAndRetainsBothLocksUntilCallerExit()
    {
        using var files = new NativeSelfUpdateFiles();
        IDisposable? retained = null;
        try
        {
            var workflow = files.CreateWorkflow((release, destination) =>
            {
                Assert.IsNotNull(retained);
                AssertNativeLocksHeld(files.Paths);
                files.CopyReplacement(release, destination);
            });
            Assert.AreEqual(files.Release.Version.ToString(), workflow.Execute(lease => retained = lease));
            Assert.IsNotNull(retained);
            AssertNativeLocksHeld(files.Paths);
            Assert.AreEqual(files.ReplacementVersion, SelfUpdateVerifier.ReadVersion(files.Paths.InstalledPath));
            Assert.StartsWith(files.Release.Version.ToString(), files.Run(["--version"]));
            Assert.Contains("update", files.Run(["--info"], succeeds: false));
            Assert.IsFalse(File.Exists(files.Paths.StagedPath));
            var backups = Directory.GetFiles(files.Paths.DirectoryPath, Path.GetFileName(files.Paths.InstalledPath) + ".old.*");
            Assert.HasCount(1, backups);
            Assert.AreEqual(files.OriginalVersion, SelfUpdateVerifier.ReadVersion(backups[0]));
        }
        finally
        {
            retained?.Dispose();
        }

        AssertNativeLocksAvailable(files.Paths);
        Assert.Contains(files.Release.Version.ToString(), files.Run(["--info"]));
        Assert.IsNull(files.CreateWorkflow((release, destination) => Assert.Fail("Current release must not download again."))
            .Execute(lease => Assert.Fail("A no-op must not transfer a lock lease.")));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void NativeOriginalIsRestoredWhenCandidateExitsNonzero()
    {
        using var native = new NativeSelfUpdateFiles();
        using var candidate = new SelfUpdateTestFiles(executable: true, "nonzero");
        File.Copy(native.Paths.InstalledPath, candidate.Paths.InstalledPath, overwrite: true);
        var originalBytes = File.ReadAllBytes(candidate.Paths.InstalledPath);
        var candidateBytes = File.ReadAllBytes(candidate.Paths.StagedPath);
        var release = new ResolvedDownload(new Uri("https://example.invalid/rejected-dotnetup.exe"), new string('0', 128), "win-x64",
            Microsoft.Deployment.DotNet.Releases.ReleaseVersion.Parse(SelfUpdateTestFiles.ReplacementVersion));
        var workflow = new SelfUpdateWorkflow(candidate.Paths, native.OriginalVersion, () => release,
            (download, destination) => File.WriteAllBytes(destination, candidateBytes));

        var exception = Assert.ThrowsExactly<DotnetInstallException>(() => SelfUpdateTestWorkflow.ExecuteAndReleaseLocks(workflow));

        Assert.AreEqual(DotnetInstallErrorCode.DotnetupVerificationFailed, exception.ErrorCode);
        Assert.IsNotNull(exception.InnerException);
        Assert.Contains("fixture failure", exception.InnerException.ToString());
        Assert.Contains("17", exception.InnerException.ToString());
        Assert.AreSequenceEqual(originalBytes, File.ReadAllBytes(candidate.Paths.InstalledPath));
        Assert.AreEqual(native.OriginalVersion, SelfUpdateVerifier.ReadVersion(candidate.Paths.InstalledPath, TimeSpan.FromSeconds(20)));
        AssertNativeLocksAvailable(candidate.Paths);
        var rejected = Directory.GetFiles(candidate.Paths.DirectoryPath, "*.old.*.rejected");
        Assert.HasCount(1, rejected);
        Assert.AreSequenceEqual(candidateBytes, File.ReadAllBytes(rejected[0]));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows | OperatingSystems.Linux)]
    public async Task NativeReplacementAllowsOldSafeDotnetChildToRemainRunning()
    {
        using var files = new NativeSelfUpdateFiles();
        var dotnetRoot = Path.GetDirectoryName(SelfUpdateTestFiles.DotnetHostPath)!;
        using var oldProcess = files.Start(
            ["dotnet", SelfUpdateTestFiles.ProcessAssemblyPath, "--wait"], dotnetRoot: dotnetRoot);
        var stderr = oldProcess.StandardError.ReadToEndAsync(TestContext.CancellationToken);
        try
        {
            var ready = await oldProcess.StandardOutput.ReadLineAsync(TestContext.CancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(20), TestContext.CancellationToken);
            Assert.AreEqual("ready", ready);
            Assert.IsFalse(oldProcess.HasExited);
            Assert.AreEqual(files.Release.Version.ToString(), SelfUpdateTestWorkflow.ExecuteAndReleaseLocks(files.CreateWorkflow()));
            Assert.IsFalse(oldProcess.HasExited, "Replacement must not terminate an existing safe dotnet invocation.");
            Assert.StartsWith(files.Release.Version.ToString(), files.Run(["--version"]));
            Assert.Contains(files.Release.Version.ToString(), files.Run(["--info"]));
            await oldProcess.StandardInput.WriteLineAsync("done".AsMemory(), TestContext.CancellationToken);
            oldProcess.StandardInput.Close();
            await oldProcess.WaitForExitAsync(TestContext.CancellationToken).WaitAsync(TimeSpan.FromSeconds(20), TestContext.CancellationToken);
            Assert.AreEqual(0, oldProcess.ExitCode, await stderr);
        }
        finally
        {
            NativeSelfUpdateFiles.Stop(oldProcess);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows | OperatingSystems.Linux)]
    public async Task NativeConcurrentWorkflowsSerializeIntoSuccessAndNoOp()
    {
        using var files = new NativeSelfUpdateFiles();
        using var downloading = new ManualResetEventSlim();
        using var finishDownload = new ManualResetEventSlim();
        using var contended = new ManualResetEventSlim();
        var downloadCount = 0;
        var first = Task.Run(() => SelfUpdateTestWorkflow.ExecuteAndReleaseLocks(files.CreateWorkflow((release, destination) =>
        {
            Interlocked.Increment(ref downloadCount);
            downloading.Set();
            Assert.IsTrue(finishDownload.Wait(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
            files.CopyReplacement(release, destination);
        })), TestContext.CancellationToken);
        Task<string?>? second = null;
        try
        {
            Assert.IsTrue(downloading.Wait(TimeSpan.FromSeconds(20), TestContext.CancellationToken));
            var coordinator = new SelfUpdateCoordinator(new NativeSelfUpdateContentionPolicy(contended));
            second = Task.Run(() => SelfUpdateTestWorkflow.ExecuteAndReleaseLocks(
                files.CreateWorkflow((release, destination) => Assert.Fail("The serialized second workflow must not download."), coordinator)), TestContext.CancellationToken);
            Assert.IsTrue(contended.Wait(TimeSpan.FromSeconds(20), TestContext.CancellationToken), "The second workflow must contend before replacement completes.");
            finishDownload.Set();
            Assert.AreEqual(files.Release.Version.ToString(), await first.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
            Assert.IsNull(await second.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
            Assert.AreEqual(1, downloadCount);
            AssertNativeLocksAvailable(files.Paths);
            Assert.StartsWith(files.Release.Version.ToString(), files.Run(["--version"]));
            Assert.Contains(files.Release.Version.ToString(), files.Run(["--info"]));
        }
        finally
        {
            finishDownload.Set();
            await Task.WhenAll(second is null ? [first] : new[] { first, second }).WaitAsync(TimeSpan.FromSeconds(60), CancellationToken.None);
        }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ExplicitExecutableOverrideMustExist(bool exists)
    {
        var directory = Directory.CreateTempSubdirectory("dotnetup-override-");
        var previous = Environment.GetEnvironmentVariable("DOTNETUP_TEST_EXECUTABLE");
        try
        {
            var path = Path.Combine(directory.FullName, "dotnetup.exe");
            if (exists)
            {
                File.WriteAllText(path, "test executable placeholder");
            }

            Environment.SetEnvironmentVariable("DOTNETUP_TEST_EXECUTABLE", path);
            if (exists)
            {
                Assert.AreEqual(path, DotnetupTestUtilities.GetDotnetupExecutablePath());
            }
            else
            {
                var exception = Assert.ThrowsExactly<FileNotFoundException>(() => DotnetupTestUtilities.GetDotnetupExecutablePath());
                Assert.AreEqual(path, exception.FileName);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNETUP_TEST_EXECUTABLE", previous);
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void LatestNativeAotExecutableIsSelected()
    {
        var directory = Directory.CreateTempSubdirectory("dotnetup-native-selection-");
        try
        {
            const string rid = "test-x64";
            const string executableName = "dotnetup";
            string older = Path.Combine(directory.FullName, "Debug", "net11.0", rid, "publish", executableName);
            string latest = Path.Combine(directory.FullName, "Release", "net11.0", rid, "publish", executableName);
            string managed = Path.Combine(directory.FullName, "Debug", "net11.0", executableName);
            string otherRid = Path.Combine(directory.FullName, "Release", "net11.0", "other-x64", "publish", executableName);
            foreach (string path in new[] { older, latest, managed, otherRid })
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, path);
            }

            DateTime latestWriteTime = DateTime.UtcNow;
            File.SetLastWriteTimeUtc(older, latestWriteTime.AddMinutes(-1));
            File.SetLastWriteTimeUtc(latest, latestWriteTime);
            File.SetLastWriteTimeUtc(managed, latestWriteTime.AddMinutes(1));
            File.SetLastWriteTimeUtc(otherRid, latestWriteTime.AddMinutes(2));

            Assert.AreEqual(latest, DotnetupTestUtilities.GetLatestNativeAotExecutablePath(directory.FullName, rid, executableName));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void NativeConfigurationIsOptInLocally()
    {
        Assert.ThrowsExactly<AssertInconclusiveException>(() => NativeSelfUpdateFiles.GetExecutablePaths(_ => null));
    }

    [TestMethod]
    [DataRow(null, null, "true")]
    [DataRow(null, "replacement", "TRUE")]
    [DataRow("original", null, "true")]
    [DataRow(" ", "replacement", "true")]
    [DataRow("original", null, null)]
    [DataRow(null, "replacement", null)]
    public void NativeConfigurationRejectsMissingRequiredPaths(string? original, string? replacement, string? required)
    {
        Assert.ThrowsExactly<AssertFailedException>(() => NativeSelfUpdateFiles.GetExecutablePaths(name => name switch
        {
            "DOTNETUP_TEST_EXECUTABLE" => original,
            "DOTNETUP_TEST_REPLACEMENT" => replacement,
            "DOTNETUP_TEST_REQUIRE_NATIVE" => required,
            _ => null,
        }));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("true")]
    public void NativeConfigurationUsesBothExplicitPaths(string? required)
    {
        var paths = NativeSelfUpdateFiles.GetExecutablePaths(name => name switch
        {
            "DOTNETUP_TEST_EXECUTABLE" => "original",
            "DOTNETUP_TEST_REPLACEMENT" => "replacement",
            "DOTNETUP_TEST_REQUIRE_NATIVE" => required,
            _ => null,
        });

        Assert.AreEqual(("original", "replacement"), paths);
    }

    private static void AssertNativeLocksHeld(SelfUpdatePaths paths)
    {
        using var update = ScopedLockFile.TryAcquireExclusive(paths.UpdateLockPath);
        using var activity = ScopedLockFile.TryAcquireExclusive(paths.ActivityLockPath);
        Assert.IsNull(update);
        Assert.IsNull(activity);
    }

    private static void AssertNativeLocksAvailable(SelfUpdatePaths paths)
    {
        using var update = ScopedLockFile.TryAcquireExclusive(paths.UpdateLockPath);
        using var activity = ScopedLockFile.TryAcquireExclusive(paths.ActivityLockPath);
        Assert.IsNotNull(update);
        Assert.IsNotNull(activity);
    }
}
