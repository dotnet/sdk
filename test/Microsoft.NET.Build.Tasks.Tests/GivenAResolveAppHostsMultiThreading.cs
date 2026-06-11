// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    [CollectionDefinition(GivenAResolveAppHostsMultiThreading.CollectionName, DisableParallelization = true)]
    public sealed class ResolveAppHostsCurrentDirectoryCollection
    {
    }

    /// <summary>
    /// Tests for ResolveAppHosts multi-threading support.
    /// Verifies TaskEnvironment-based path resolution against ProjectDirectory (not process CWD)
    /// and that output metadata preserves the original path form.
    /// </summary>
    [Collection(CollectionName)]
    public class GivenAResolveAppHostsMultiThreading : IDisposable
    {
        internal const string CollectionName = "ResolveAppHosts current directory tests";
        private const string PackName = "Microsoft.NETCore.App.Host.win-x64";
        private const string PackVersion = "8.0.0";
        private const string RuntimeGraphJson = "{ \"runtimes\": { \"win-x64\": { \"#import\": [] } } }";

        private readonly string _testDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        private readonly string _decoyDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        private readonly string _originalCurrentDirectory = Directory.GetCurrentDirectory();

        public GivenAResolveAppHostsMultiThreading()
        {
            Directory.CreateDirectory(_testDir);
            Directory.CreateDirectory(_decoyDir);
            Directory.SetCurrentDirectory(_decoyDir);
        }

        public void Dispose()
        {
            Directory.SetCurrentDirectory(_originalCurrentDirectory);
            DeleteDirectory(_testDir);
            DeleteDirectory(_decoyDir);
        }

        [Fact]
        public void RelativeTargetingPackRoot_OutputMetadataRemainsRelative()
        {
            string relativePackRoot = "packs";
            Directory.Exists(Path.Combine(_decoyDir, relativePackRoot)).Should().BeFalse(
                "the process CWD must not contain the relative pack root, or CWD fallback regressions can pass");

            string nativePath = Path.Combine(_testDir, relativePackRoot, PackName, PackVersion, "runtimes", "win-x64", "native");
            CreateHostFiles(nativePath);

            string runtimeGraphPath = Path.Combine(_testDir, "runtime.json");
            File.WriteAllText(runtimeGraphPath, RuntimeGraphJson);

            var task = CreateTask(
                taskEnv: TaskEnvironmentHelper.CreateForTest(_testDir),
                targetingPackRoot: relativePackRoot,
                runtimeGraphPath: runtimeGraphPath);

            task.Execute().Should().BeTrue();

            string expectedPackDir = Path.Combine(relativePackRoot, PackName, PackVersion);
            AssertRelativePackMetadata(task.AppHost, expectedPackDir, "apphost.exe");
            AssertRelativePackMetadata(task.SingleFileHost, expectedPackDir, "singlefilehost.exe");
            AssertRelativePackMetadata(task.ComHost, expectedPackDir, "comhost.dll");
            AssertRelativePackMetadata(task.IjwHost, expectedPackDir, "ijwhost.dll");
        }

        [Fact]
        public void Execute_DoesNotMutateProcessCurrentDirectory()
        {
            string relativePackRoot = "packs";
            string nativePath = Path.Combine(_testDir, relativePackRoot, PackName, PackVersion, "runtimes", "win-x64", "native");
            CreateHostFiles(nativePath);

            string runtimeGraphPath = Path.Combine(_testDir, "runtime.json");
            File.WriteAllText(runtimeGraphPath, RuntimeGraphJson);

            var task = CreateTask(
                taskEnv: TaskEnvironmentHelper.CreateForTest(_testDir),
                targetingPackRoot: relativePackRoot,
                runtimeGraphPath: runtimeGraphPath);

            string currentDirectoryBeforeExecute = Directory.GetCurrentDirectory();

            task.Execute().Should().BeTrue();
            Directory.GetCurrentDirectory().Should().Be(currentDirectoryBeforeExecute,
                "ResolveAppHosts must not mutate process current directory");
        }

        [Fact]
        public void RelativeTargetingPackRoot_ResolvedAgainstProjectDirectory_NotCwd()
        {
            string relativePackRoot = "packs";

            // Place host files under the DECOY (process CWD), NOT under the project directory.
            // If ResolveAppHosts incorrectly resolves the relative pack root against CWD, Directory.Exists
            // would succeed and pack metadata would be set. With correct ProjectDirectory-based resolution,
            // the directory is not found and pack metadata must be absent.
            string decoyNativePath = Path.Combine(_decoyDir, relativePackRoot, PackName, PackVersion, "runtimes", "win-x64", "native");
            CreateHostFiles(decoyNativePath);

            string runtimeGraphPath = Path.Combine(_testDir, "runtime.json");
            File.WriteAllText(runtimeGraphPath, RuntimeGraphJson);

            var task = CreateTask(
                taskEnv: TaskEnvironmentHelper.CreateForTest(_testDir),
                targetingPackRoot: relativePackRoot,
                runtimeGraphPath: runtimeGraphPath);

            task.Execute().Should().BeTrue();

            AssertPackMetadataMissing(task.AppHost);
            AssertPackMetadataMissing(task.SingleFileHost);
            AssertPackMetadataMissing(task.ComHost);
            AssertPackMetadataMissing(task.IjwHost);
        }

        private static void AssertPackMetadataMissing(ITaskItem[] hostItems)
        {
            hostItems.Should().NotBeNull().And.HaveCount(1);
            hostItems[0].GetMetadata(MetadataKeys.PackageDirectory).Should().BeEmpty();
            hostItems[0].GetMetadata(MetadataKeys.Path).Should().BeEmpty();
        }

        private static void AssertRelativePackMetadata(ITaskItem[] hostItems, string expectedPackDir, string hostFileName)
        {
            hostItems.Should().NotBeNull().And.HaveCount(1);
            string packageDirectory = hostItems[0].GetMetadata(MetadataKeys.PackageDirectory);
            string path = hostItems[0].GetMetadata(MetadataKeys.Path);

            packageDirectory.Should().Be(expectedPackDir);
            Path.IsPathRooted(packageDirectory).Should().BeFalse();
            path.Should().Be(Path.Combine(expectedPackDir, "runtimes", "win-x64", "native", hostFileName));
            Path.IsPathRooted(path).Should().BeFalse();
        }

        private static ResolveAppHosts CreateTask(
            TaskEnvironment taskEnv,
            string targetingPackRoot,
            string runtimeGraphPath)
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

        private static void CreateHostFiles(string nativePath)
        {
            Directory.CreateDirectory(nativePath);
            File.WriteAllText(Path.Combine(nativePath, "apphost.exe"), "fake apphost");
            File.WriteAllText(Path.Combine(nativePath, "singlefilehost.exe"), "fake");
            File.WriteAllText(Path.Combine(nativePath, "comhost.dll"), "fake");
            File.WriteAllText(Path.Combine(nativePath, "ijwhost.dll"), "fake");
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
