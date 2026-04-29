// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using ThreadingTask = System.Threading.Tasks.Task;
using TaskCreationOptions = System.Threading.Tasks.TaskCreationOptions;
using TaskScheduler = System.Threading.Tasks.TaskScheduler;
using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    internal static class ResolveAppHostsTestCollections
    {
        public const string UsesCurrentDirectory = "ResolveAppHosts current directory tests";
    }

    [CollectionDefinition(ResolveAppHostsTestCollections.UsesCurrentDirectory, DisableParallelization = true)]
    public sealed class ResolveAppHostsCurrentDirectoryCollection
    {
    }

    /// <summary>
    /// Tests for ResolveAppHosts multi-threading support.
    /// These tests verify:
    /// 1. Output path relativity preservation (AR-May finding #2)
    /// 2. TaskEnvironment-based path resolution against ProjectDirectory (not process CWD)
    ///
    /// Both tests run on .NET Core (the test project's only TFM) using
    /// TaskEnvironmentHelper.CreateForTest to construct a TaskEnvironment whose
    /// ProjectDirectory differs from the process's current working directory (decoy CWD pattern).
    /// </summary>
    [Collection(ResolveAppHostsTestCollections.UsesCurrentDirectory)]
    public class GivenAResolveAppHostsMultiThreading
    {
        private const string PackName = "Microsoft.NETCore.App.Host.win-x64";
        private const string PackVersion = "8.0.0";
        private const string RuntimeGraphJson = "{ \"runtimes\": { \"win-x64\": { \"#import\": [] } } }";
        private const int ParallelResolveCount = 64;

        /// <summary>
        /// AR-May Finding #2: Output metadata (PackageDirectory, Path) must preserve the
        /// ORIGINAL (as-input) path form. When TargetingPackRoot is relative, output metadata
        /// must remain relative even though the task absolutizes the path internally for
        /// Directory.Exists. The absolutized form must NEVER leak into SetMetadata.
        ///
        /// Exercises the fix with a decoy CWD: TaskEnvironment.ProjectDirectory = testDir,
        /// while the process's Directory.GetCurrentDirectory() is something else. If the
        /// task accidentally used the process CWD to resolve the relative pack root, or
        /// leaked the absolutized path into metadata, this test fails.
        /// </summary>
        [Fact]
        public void OutputPathRelativity_PreservesOriginalPaths()
        {
            string testDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string decoyDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string originalCurrentDirectory = Directory.GetCurrentDirectory();
            try
            {
                // Sanity: the decoy. testDir must differ from process CWD so that a relative
                // "packs" input can only resolve correctly via TaskEnvironment.ProjectDirectory.
                Directory.CreateDirectory(testDir);
                Directory.CreateDirectory(decoyDir);
                Directory.SetCurrentDirectory(decoyDir);
                Directory.GetCurrentDirectory().Should().Be(decoyDir);
                testDir.Should().NotBe(decoyDir,
                    "decoy CWD pattern requires ProjectDirectory != process CWD");

                string relativePackRoot = "packs";
                Directory.Exists(Path.Combine(decoyDir, relativePackRoot)).Should().BeFalse(
                    "the process CWD must not contain the relative pack root, or CWD fallback regressions can pass");
                string packPath = Path.Combine(testDir, relativePackRoot, PackName, PackVersion);
                string nativePath = Path.Combine(packPath, "runtimes", "win-x64", "native");
                CreateHostFiles(nativePath);

                string runtimeGraphPath = Path.Combine(testDir, "runtime.json");
                File.WriteAllText(runtimeGraphPath, RuntimeGraphJson);

                var task = CreateTask(
                    taskEnv: TaskEnvironmentHelper.CreateForTest(testDir),
                    targetingPackRoot: relativePackRoot,   // RELATIVE path
                    runtimeGraphPath: runtimeGraphPath);

                bool result = task.Execute();

                result.Should().BeTrue();
                Directory.GetCurrentDirectory().Should().Be(decoyDir,
                    "ResolveAppHosts must not mutate process current directory when resolving TaskEnvironment paths");
                task.AppHost.Should().NotBeNull().And.HaveCount(1);

                var appHostItem = task.AppHost[0];

                string packageDirMetadata = appHostItem.GetMetadata(MetadataKeys.PackageDirectory);
                string pathMetadata = appHostItem.GetMetadata(MetadataKeys.Path);

                // CRITICAL: metadata must preserve relativity — the absolutized form used for
                // Directory.Exists must NOT leak into SetMetadata.
                packageDirMetadata.Should().NotBeNullOrEmpty();
                Path.IsPathRooted(packageDirMetadata).Should().BeFalse(
                    "PackageDirectory must remain relative when input TargetingPackRoot was relative");
                packageDirMetadata.Should().Be(Path.Combine(relativePackRoot, PackName, PackVersion));

                pathMetadata.Should().NotBeNullOrEmpty();
                Path.IsPathRooted(pathMetadata).Should().BeFalse(
                    "Path must remain relative when input TargetingPackRoot was relative");
                pathMetadata.Should().Be(
                    Path.Combine(relativePackRoot, PackName, PackVersion, "runtimes", "win-x64", "native", "apphost.exe"));

                // Same guarantee must hold for the other three host outputs.
                foreach (var item in new[] { task.SingleFileHost?[0], task.ComHost?[0], task.IjwHost?[0] })
                {
                    item.Should().NotBeNull();
                    Path.IsPathRooted(item.GetMetadata(MetadataKeys.PackageDirectory)).Should().BeFalse();
                    Path.IsPathRooted(item.GetMetadata(MetadataKeys.Path)).Should().BeFalse();
                }
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCurrentDirectory);
                DeleteDirectory(testDir);
                DeleteDirectory(decoyDir);
            }
        }

        /// <summary>
        /// Absolute TargetingPackRoot input: outputs must still be absolute (OriginalValue == absolute input).
        /// Also exercises the decoy CWD pattern to confirm absolute-path inputs are unaffected
        /// by TaskEnvironment.ProjectDirectory.
        /// </summary>
        [Fact]
        public void AbsolutePathInput_WorksCorrectly()
        {
            string testDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string decoyDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string originalCurrentDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.CreateDirectory(testDir);
                Directory.CreateDirectory(decoyDir);
                Directory.SetCurrentDirectory(decoyDir);
                Directory.GetCurrentDirectory().Should().Be(decoyDir);

                string absolutePackRoot = Path.Combine(testDir, "packs");
                string nativePath = Path.Combine(absolutePackRoot, PackName, PackVersion, "runtimes", "win-x64", "native");
                CreateHostFiles(nativePath);

                string runtimeGraphPath = Path.Combine(testDir, "runtime.json");
                File.WriteAllText(runtimeGraphPath, RuntimeGraphJson);

                // decoyDir as ProjectDirectory proves absolute-path handling does not depend on it.
                var task = CreateTask(
                    taskEnv: TaskEnvironmentHelper.CreateForTest(decoyDir),
                    targetingPackRoot: absolutePackRoot,   // ABSOLUTE path
                    runtimeGraphPath: runtimeGraphPath);

                bool result = task.Execute();

                result.Should().BeTrue();
                Directory.GetCurrentDirectory().Should().Be(decoyDir,
                    "ResolveAppHosts must not mutate process current directory for absolute pack roots");
                task.AppHost.Should().NotBeNull().And.HaveCount(1);

                string expectedPackageDirectory = Path.Combine(absolutePackRoot, PackName, PackVersion);
                string packageDirMetadata = task.AppHost[0].GetMetadata(MetadataKeys.PackageDirectory);
                Path.IsPathRooted(packageDirMetadata).Should().BeTrue(
                    "absolute input must produce absolute output");
                packageDirMetadata.Should().Be(expectedPackageDirectory);

                string pathMetadata = task.AppHost[0].GetMetadata(MetadataKeys.Path);
                Path.IsPathRooted(pathMetadata).Should().BeTrue();
                pathMetadata.Should().Be(Path.Combine(expectedPackageDirectory,
                    "runtimes", "win-x64", "native", "apphost.exe"));

                foreach (var item in new[] { task.SingleFileHost?[0], task.ComHost?[0], task.IjwHost?[0] })
                {
                    item.Should().NotBeNull();
                    item.GetMetadata(MetadataKeys.PackageDirectory).Should().Be(expectedPackageDirectory);
                    Path.IsPathRooted(item.GetMetadata(MetadataKeys.Path)).Should().BeTrue();
                }
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCurrentDirectory);
                DeleteDirectory(testDir);
                DeleteDirectory(decoyDir);
            }
        }

        [Fact]
        public async ThreadingTask ParallelExecution_ResolvesRelativePacksWithSharedRuntimeGraphCache()
        {
            string rootDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(rootDir);
                string runtimeGraphPath = Path.Combine(rootDir, "runtime.json");
                File.WriteAllText(runtimeGraphPath, RuntimeGraphJson);

                string[] projectDirs = Enumerable.Range(0, ParallelResolveCount)
                    .Select(index =>
                    {
                        string projectDir = Path.Combine(rootDir, "project" + index);
                        CreateHostFiles(Path.Combine(projectDir, "packs", PackName, PackVersion, "runtimes", "win-x64", "native"));
                        return projectDir;
                    })
                    .ToArray();

                var sharedBuildEngine = new MockBuildEngine();
                var warmupTask = CreateTask(
                    TaskEnvironmentHelper.CreateForTest(projectDirs[0]),
                    "packs",
                    runtimeGraphPath,
                    sharedBuildEngine);
                warmupTask.Execute().Should().BeTrue();
                sharedBuildEngine.RegisteredTaskObjects.Should().ContainSingle(
                    "all parallel ResolveAppHosts instances should read the same registered runtime graph");

                using var ready = new CountdownEvent(ParallelResolveCount);
                using var start = new ManualResetEventSlim(initialState: false);
                CancellationToken cancellationToken = TestContext.Current.CancellationToken;

                var tasks = Enumerable.Range(0, ParallelResolveCount)
                    .Select(index => ThreadingTask.Factory.StartNew(
                        () =>
                        {
                            ready.Signal();
                            if (!start.Wait(TimeSpan.FromSeconds(30), cancellationToken))
                            {
                                throw new TimeoutException("Timed out waiting to start parallel ResolveAppHosts stress test.");
                            }

                            var task = CreateTask(
                                TaskEnvironmentHelper.CreateForTest(projectDirs[index]),
                                "packs",
                                runtimeGraphPath,
                                sharedBuildEngine);

                            return (Index: index, Succeeded: task.Execute(), Task: task);
                        },
                        cancellationToken,
                        TaskCreationOptions.LongRunning,
                        TaskScheduler.Default))
                    .ToArray();

                bool allReady = ready.Wait(TimeSpan.FromSeconds(30), cancellationToken);
                start.Set();

                var results = await ThreadingTask.WhenAll(tasks);

                allReady.Should().BeTrue("all ResolveAppHosts instances should overlap before the start gate opens");
                results.Should().HaveCount(ParallelResolveCount);
                sharedBuildEngine.Errors.Should().BeEmpty();

                foreach (var result in results)
                {
                    result.Succeeded.Should().BeTrue($"parallel ResolveAppHosts instance {result.Index} should succeed");
                    AssertRelativeHostOutput(result.Task.AppHost, "apphost.exe");
                    AssertRelativeHostOutput(result.Task.SingleFileHost, "singlefilehost.exe");
                    AssertRelativeHostOutput(result.Task.ComHost, "comhost.dll");
                    AssertRelativeHostOutput(result.Task.IjwHost, "ijwhost.dll");
                }
            }
            finally
            {
                DeleteDirectory(rootDir);
            }
        }

        [Fact]
        public void MissingTaskEnvironment_FailsWithGuardError()
        {
            string testDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(testDir);
                string runtimeGraphPath = Path.Combine(testDir, "runtime.json");
                File.WriteAllText(runtimeGraphPath, RuntimeGraphJson);

                var buildEngine = new MockBuildEngine();
                var task = CreateTask(
                    taskEnv: null,
                    targetingPackRoot: "packs",
                    runtimeGraphPath: runtimeGraphPath,
                    buildEngine: buildEngine);

                task.Execute().Should().BeFalse();
                buildEngine.Errors.Should().ContainSingle();
                buildEngine.Errors[0].Code.Should().Be("NETSDK1236");
                buildEngine.Errors[0].Message.Should().Contain(nameof(ResolveAppHosts.TaskEnvironment))
                    .And.Contain(nameof(IMultiThreadableTask));
            }
            finally
            {
                DeleteDirectory(testDir);
            }
        }

        [Fact]
        public void ResolveAppHosts_IsRecognizedAsMSBuildMultiThreadableTask()
        {
            typeof(IMultiThreadableTask).IsAssignableFrom(typeof(ResolveAppHosts)).Should().BeTrue();
            typeof(ResolveAppHosts).GetInterfaces().Select(i => i.FullName).Should()
                .Contain("Microsoft.Build.Framework.IMultiThreadableTask");

            typeof(ResolveAppHosts).GetCustomAttributes(inherit: false)
                .Select(attribute => attribute.GetType().FullName)
                .Should()
                .Contain("Microsoft.Build.Framework.MSBuildMultiThreadableTaskAttribute",
                    "MSBuild recognizes multi-threadable tasks by attribute namespace and name");

            var taskEnvironmentProperty = typeof(ResolveAppHosts).GetProperty(nameof(ResolveAppHosts.TaskEnvironment));
            taskEnvironmentProperty.Should().NotBeNull();
            taskEnvironmentProperty.PropertyType.FullName.Should().Be("Microsoft.Build.Framework.TaskEnvironment");
        }

        private static ResolveAppHosts CreateTask(
            TaskEnvironment taskEnv,
            string targetingPackRoot,
            string runtimeGraphPath,
            IBuildEngine buildEngine = null)
        {
            var knownAppHostPacks = new ITaskItem[]
            {
                new TaskItem("Microsoft.NETCore.App.Host", new Dictionary<string, string>
                {
                    { "TargetFramework", "net8.0" },
                    { "AppHostRuntimeIdentifiers", "win-x64" },
                    { "AppHostPackNamePattern", "Microsoft.NETCore.App.Host.**RID**" },
                    { "AppHostPackVersion", PackVersion },
                    { MetadataKeys.ExcludedRuntimeIdentifiers, "" }
                })
            };

            return new ResolveAppHosts
            {
                BuildEngine = buildEngine ?? new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                TargetFrameworkIdentifier = ".NETCoreApp",
                TargetFrameworkVersion = "8.0",
                TargetingPackRoot = targetingPackRoot,
                AppHostRuntimeIdentifier = "win-x64",
                DotNetAppHostExecutableNameWithoutExtension = "apphost",
                DotNetSingleFileHostExecutableNameWithoutExtension = "singlefilehost",
                DotNetComHostLibraryNameWithoutExtension = "comhost",
                DotNetIjwHostLibraryNameWithoutExtension = "ijwhost",
                RuntimeGraphPath = runtimeGraphPath,
                KnownAppHostPacks = knownAppHostPacks,
                NuGetRestoreSupported = true,
                EnableAppHostPackDownload = false,
            };
        }

        private static void CreateHostFiles(string nativePath)
        {
            Directory.CreateDirectory(nativePath);
            File.WriteAllText(Path.Combine(nativePath, "apphost.exe"), "fake apphost");
            File.WriteAllText(Path.Combine(nativePath, "singlefilehost.exe"), "fake");
            File.WriteAllText(Path.Combine(nativePath, "comhost.dll"), "fake");
            File.WriteAllText(Path.Combine(nativePath, "ijwhost.dll"), "fake");
        }

        private static void AssertRelativeHostOutput(ITaskItem[] hostItems, string hostFileName)
        {
            hostItems.Should().NotBeNull().And.HaveCount(1);
            string expectedPackageDirectory = Path.Combine("packs", PackName, PackVersion);
            string packageDirectory = hostItems[0].GetMetadata(MetadataKeys.PackageDirectory);
            string path = hostItems[0].GetMetadata(MetadataKeys.Path);

            packageDirectory.Should().Be(expectedPackageDirectory);
            Path.IsPathRooted(packageDirectory).Should().BeFalse();
            path.Should().Be(Path.Combine(expectedPackageDirectory, "runtimes", "win-x64", "native", hostFileName));
            Path.IsPathRooted(path).Should().BeFalse();
        }

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }
}
