// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
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
    public class GivenAResolveAppHostsMultiThreading
    {
        private const string PackName = "Microsoft.NETCore.App.Host.win-x64";
        private const string PackVersion = "8.0.0";

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
            try
            {
                // Sanity: the decoy. testDir must differ from process CWD so that a relative
                // "packs" input can only resolve correctly via TaskEnvironment.ProjectDirectory.
                Directory.CreateDirectory(testDir);
                testDir.Should().NotBe(Directory.GetCurrentDirectory(),
                    "decoy CWD pattern requires ProjectDirectory != process CWD");

                string relativePackRoot = "packs";
                string packPath = Path.Combine(testDir, relativePackRoot, PackName, PackVersion);
                string nativePath = Path.Combine(packPath, "runtimes", "win-x64", "native");
                Directory.CreateDirectory(nativePath);
                File.WriteAllText(Path.Combine(nativePath, "apphost.exe"), "fake apphost");
                File.WriteAllText(Path.Combine(nativePath, "singlefilehost.exe"), "fake");
                File.WriteAllText(Path.Combine(nativePath, "comhost.dll"), "fake");
                File.WriteAllText(Path.Combine(nativePath, "ijwhost.dll"), "fake");

                string runtimeGraphPath = Path.Combine(testDir, "runtime.json");
                File.WriteAllText(runtimeGraphPath,
                    "{ \"runtimes\": { \"win-x64\": { \"#import\": [] } } }");

                var task = CreateTask(
                    taskEnv: TaskEnvironmentHelper.CreateForTest(testDir),
                    targetingPackRoot: relativePackRoot,   // RELATIVE path
                    runtimeGraphPath: runtimeGraphPath);

                bool result = task.Execute();

                result.Should().BeTrue();
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
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, recursive: true);
                }
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
            try
            {
                Directory.CreateDirectory(testDir);
                Directory.CreateDirectory(decoyDir);

                string absolutePackRoot = Path.Combine(testDir, "packs");
                string nativePath = Path.Combine(absolutePackRoot, PackName, PackVersion, "runtimes", "win-x64", "native");
                Directory.CreateDirectory(nativePath);
                File.WriteAllText(Path.Combine(nativePath, "apphost.exe"), "fake apphost");

                string runtimeGraphPath = Path.Combine(testDir, "runtime.json");
                File.WriteAllText(runtimeGraphPath,
                    "{ \"runtimes\": { \"win-x64\": { \"#import\": [] } } }");

                // decoyDir as ProjectDirectory proves absolute-path handling does not depend on it.
                var task = CreateTask(
                    taskEnv: TaskEnvironmentHelper.CreateForTest(decoyDir),
                    targetingPackRoot: absolutePackRoot,   // ABSOLUTE path
                    runtimeGraphPath: runtimeGraphPath);

                bool result = task.Execute();

                result.Should().BeTrue();
                task.AppHost.Should().NotBeNull().And.HaveCount(1);

                string packageDirMetadata = task.AppHost[0].GetMetadata(MetadataKeys.PackageDirectory);
                Path.IsPathRooted(packageDirMetadata).Should().BeTrue(
                    "absolute input must produce absolute output");
                packageDirMetadata.Should().Be(Path.Combine(absolutePackRoot, PackName, PackVersion));

                string pathMetadata = task.AppHost[0].GetMetadata(MetadataKeys.Path);
                Path.IsPathRooted(pathMetadata).Should().BeTrue();
                pathMetadata.Should().Be(Path.Combine(absolutePackRoot, PackName, PackVersion,
                    "runtimes", "win-x64", "native", "apphost.exe"));
            }
            finally
            {
                if (Directory.Exists(testDir)) Directory.Delete(testDir, recursive: true);
                if (Directory.Exists(decoyDir)) Directory.Delete(decoyDir, recursive: true);
            }
        }

        private static ResolveAppHosts CreateTask(TaskEnvironment taskEnv, string targetingPackRoot, string runtimeGraphPath)
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
                BuildEngine = new MockBuildEngine(),
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
    }
}
