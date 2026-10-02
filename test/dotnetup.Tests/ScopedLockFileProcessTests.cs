// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class ScopedLockFileProcessTests : SdkTest
{
    private static string s_dotnetPath = null!;
    private static string s_assemblyPath = null!;
    private static string s_buildDirectory = null!;
    private DirectoryInfo _directory = null!;
    private string _lockPath = null!;

    [ClassInitialize]
    public static void BuildAsset(TestContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var repoRoot = Path.GetFullPath(typeof(ScopedLockFileProcessTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == "RepoRoot").Value!);
        s_dotnetPath = Path.Combine(repoRoot, ".dotnet", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        var artifactsRoot = Environment.GetEnvironmentVariable("ArtifactsDir") ?? Path.GetTempPath();
        s_buildDirectory = Path.Combine(artifactsRoot, "lock-process-" + Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(s_buildDirectory, "out");
        var framework = new FrameworkName(typeof(ScopedLockFileProcessTests).Assembly
            .GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        var startInfo = new ProcessStartInfo(s_dotnetPath) { UseShellExecute = false, WorkingDirectory = repoRoot };
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(Path.Combine(repoRoot, "test", "TestAssets", "ScopedLockFileProcess", "ScopedLockFileProcess.csproj"));
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(outputDirectory);
        startInfo.ArgumentList.Add("/p:CurrentTargetFramework=net" + framework.Version.ToString(2));
        startInfo.ArgumentList.Add("/p:ArtifactsDir=" + s_buildDirectory + Path.DirectorySeparatorChar);
        startInfo.ArgumentList.Add("/p:BaseIntermediateOutputPath=" + Path.Combine(s_buildDirectory, "obj") + Path.DirectorySeparatorChar);
        startInfo.ArgumentList.Add("/nodeReuse:false");
        using var process = Process.Start(startInfo);
        Assert.IsNotNull(process);
        try
        {
            Assert.IsTrue(process.WaitForExit(120_000), "Lock process asset build timed out.");
            Assert.AreEqual(0, process.ExitCode, "Lock process asset build failed.");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(10_000);
            }
        }

        s_assemblyPath = Path.Combine(outputDirectory, "ScopedLockFileProcess.dll");
    }

    [ClassCleanup]
    public static void CleanupAsset()
    {
        if (Directory.Exists(s_buildDirectory))
        {
            Directory.Delete(s_buildDirectory, recursive: true);
        }
    }

    [TestInitialize]
    public void Initialize()
    {
        _directory = Directory.CreateTempSubdirectory("lock-process-test-");
        _lockPath = Path.Combine(_directory.FullName, "activity.lock");
    }

    [TestCleanup]
    public void Cleanup() => _directory.Delete(recursive: true);

    [TestMethod]
    public void SharedLeasesCoexistAcrossProcessesAndDenyExclusive()
    {
        using (var first = LockFileTestProcess.Start(s_dotnetPath, s_assemblyPath, "shared", _lockPath))
        using (var second = LockFileTestProcess.Start(s_dotnetPath, s_assemblyPath, "shared", _lockPath))
        using (var local = ScopedLockFile.TryAcquireShared(_lockPath))
        {
            Assert.IsNotNull(local);
            Assert.AreEqual(2, LockFileTestProcess.Probe(s_dotnetPath, s_assemblyPath, "exclusive", _lockPath));
            using var exclusive = ScopedLockFile.TryAcquireExclusive(_lockPath);
            Assert.IsNull(exclusive);
        }

        Assert.AreEqual(0, LockFileTestProcess.Probe(s_dotnetPath, s_assemblyPath, "exclusive", _lockPath));
        Assert.AreEqual(0L, new FileInfo(_lockPath).Length);
    }

    [TestMethod]
    public void ExclusiveLeaseDeniesBothModesAcrossProcesses()
    {
        using (var holder = LockFileTestProcess.Start(s_dotnetPath, s_assemblyPath, "exclusive", _lockPath))
        {
            Assert.AreEqual(2, LockFileTestProcess.Probe(s_dotnetPath, s_assemblyPath, "shared", _lockPath));
            Assert.AreEqual(2, LockFileTestProcess.Probe(s_dotnetPath, s_assemblyPath, "exclusive", _lockPath));
            using var shared = ScopedLockFile.TryAcquireShared(_lockPath);
            Assert.IsNull(shared);
        }

        Assert.AreEqual(0, LockFileTestProcess.Probe(s_dotnetPath, s_assemblyPath, "shared", _lockPath));
    }

    [TestMethod]
    [DataRow("shared")]
    [DataRow("exclusive")]
    public void ProcessTerminationReleasesLockWithoutDeletingFile(string mode)
    {
        using var holder = LockFileTestProcess.Start(s_dotnetPath, s_assemblyPath, mode, _lockPath);
        Assert.AreEqual(2, LockFileTestProcess.Probe(s_dotnetPath, s_assemblyPath, "exclusive", _lockPath));
        holder.Kill();
        Assert.AreEqual(0, LockFileTestProcess.Probe(s_dotnetPath, s_assemblyPath, "exclusive", _lockPath));
        Assert.AreEqual(0L, new FileInfo(_lockPath).Length);
    }
}