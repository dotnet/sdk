// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAResolveAppHostsMultiThreading
    {
        [Fact]
        public void ItResolvesTargetingPackRootViaTaskEnvironment()
        {
            // Create a temp directory to act as a fake project dir (different from CWD).
            // Set up TargetingPackRoot as a relative path that only resolves under projectDir.
            // If the task absolutizes via TaskEnvironment, Directory.Exists will find the pack.
            // If not, it'll look at a CWD-relative path and won't find it.
            var projectDir = Path.Combine(Path.GetTempPath(), "apphost-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            try
            {
                // Create a RuntimeGraph file under projectDir
                var runtimeGraphPath = Path.Combine(projectDir, "runtime.json");
                File.WriteAllText(runtimeGraphPath, "{\"runtimes\":{\"win-x64\":{\"#import\":[\"win\",\"any\"]},\"win\":{\"#import\":[\"any\"]},\"any\":{}}}");

                // Create a targeting pack root with an app host pack structure under projectDir
                var targetingPackRelative = "packs";
                var hostPackName = "Microsoft.NETCore.App.Host.win-x64";
                var hostPackVersion = "8.0.0";
                var packPath = Path.Combine(projectDir, targetingPackRelative, hostPackName, hostPackVersion);
                var hostBinaryDir = Path.Combine(packPath, "runtimes", "win-x64", "native");
                Directory.CreateDirectory(hostBinaryDir);
                File.WriteAllText(Path.Combine(hostBinaryDir, "apphost.exe"), "fake");
                File.WriteAllText(Path.Combine(hostBinaryDir, "singlefilehost.exe"), "fake");
                File.WriteAllText(Path.Combine(hostBinaryDir, "comhost.dll"), "fake");
                File.WriteAllText(Path.Combine(hostBinaryDir, "ijwhost.dll"), "fake");

                var knownAppHostPack = new Microsoft.Build.Utilities.TaskItem("Microsoft.NETCore.App.Host");
                knownAppHostPack.SetMetadata("TargetFramework", "net8.0");
                knownAppHostPack.SetMetadata("AppHostRuntimeIdentifiers", "win-x64;linux-x64;osx-x64");
                knownAppHostPack.SetMetadata("AppHostPackNamePattern", "Microsoft.NETCore.App.Host.**RID**");
                knownAppHostPack.SetMetadata("AppHostPackVersion", hostPackVersion);
                knownAppHostPack.SetMetadata(MetadataKeys.ExcludedRuntimeIdentifiers, "");

                var task = new ResolveAppHosts
                {
                    TargetFrameworkIdentifier = ".NETCoreApp",
                    TargetFrameworkVersion = "8.0",
                    TargetingPackRoot = targetingPackRelative,
                    AppHostRuntimeIdentifier = "win-x64",
                    RuntimeFrameworkVersion = null,
                    DotNetAppHostExecutableNameWithoutExtension = "apphost",
                    DotNetSingleFileHostExecutableNameWithoutExtension = "singlefilehost",
                    DotNetComHostLibraryNameWithoutExtension = "comhost",
                    DotNetIjwHostLibraryNameWithoutExtension = "ijwhost",
                    RuntimeGraphPath = runtimeGraphPath,
                    KnownAppHostPacks = new ITaskItem[] { knownAppHostPack },
                    NuGetRestoreSupported = true,
                    EnableAppHostPackDownload = false,
                };
                var mockEngine = new MockBuildEngine();
                task.BuildEngine = mockEngine;

                // Set TaskEnvironment pointing to projectDir
                task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

                task.Execute().Should().BeTrue(
                    string.Join("; ", mockEngine.Errors.Select(e => e.Message)));

                // If TargetingPackRoot was resolved via TaskEnvironment, the task should have
                // found the pack directory and set the Path metadata on the AppHost item.
                task.AppHost.Should().NotBeNull().And.HaveCount(1);
                var appHostPath = task.AppHost[0].GetMetadata(MetadataKeys.Path);
                appHostPath.Should().Contain(packPath,
                    "the AppHost path should be resolved relative to the project dir via TaskEnvironment");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        [Fact]
        public void ItHandlesEmptyRuntimeGraphPath()
        {
            var projectDir = Path.Combine(Path.GetTempPath(), "apphost-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            try
            {
                var knownAppHostPack = new Microsoft.Build.Utilities.TaskItem("Microsoft.NETCore.App.Host");
                knownAppHostPack.SetMetadata("TargetFramework", "net8.0");
                knownAppHostPack.SetMetadata("AppHostRuntimeIdentifiers", "win-x64;linux-x64;osx-x64");
                knownAppHostPack.SetMetadata("AppHostPackNamePattern", "Microsoft.NETCore.App.Host.**RID**");
                knownAppHostPack.SetMetadata("AppHostPackVersion", "8.0.0");
                knownAppHostPack.SetMetadata(MetadataKeys.ExcludedRuntimeIdentifiers, "");

                var task = new ResolveAppHosts
                {
                    TargetFrameworkIdentifier = ".NETCoreApp",
                    TargetFrameworkVersion = "8.0",
                    TargetingPackRoot = "packs",
                    AppHostRuntimeIdentifier = "win-x64",
                    RuntimeFrameworkVersion = null,
                    DotNetAppHostExecutableNameWithoutExtension = "apphost",
                    DotNetSingleFileHostExecutableNameWithoutExtension = "singlefilehost",
                    DotNetComHostLibraryNameWithoutExtension = "comhost",
                    DotNetIjwHostLibraryNameWithoutExtension = "ijwhost",
                    RuntimeGraphPath = "",
                    KnownAppHostPacks = new ITaskItem[] { knownAppHostPack },
                    NuGetRestoreSupported = true,
                    EnableAppHostPackDownload = false,
                };
                var mockEngine = new MockBuildEngine();
                task.BuildEngine = mockEngine;
                task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

                var result = false;
                Exception? caught = null;
                try { result = task.Execute(); } catch (Exception ex) { caught = ex; }

                caught.Should().NotBeOfType<NullReferenceException>(
                    "empty RuntimeGraphPath should not cause NullReferenceException");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        [Fact]
        public void ItPreservesRelativePathInPackageMetadata()
        {
            // When TargetingPackRoot is relative, the task absolutizes it via TaskEnvironment.
            // PathInPackage metadata should remain a relative path (it's the path *within* the pack),
            // while Path metadata should be the fully resolved absolute path.
            var projectDir = Path.Combine(Path.GetTempPath(), "apphost-pathtest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            try
            {
                var runtimeGraphPath = Path.Combine(projectDir, "runtime.json");
                File.WriteAllText(runtimeGraphPath, "{\"runtimes\":{\"win-x64\":{\"#import\":[\"win\",\"any\"]},\"win\":{\"#import\":[\"any\"]},\"any\":{}}}");

                var targetingPackRelative = "packs";
                var hostPackName = "Microsoft.NETCore.App.Host.win-x64";
                var hostPackVersion = "8.0.0";
                var packPath = Path.Combine(projectDir, targetingPackRelative, hostPackName, hostPackVersion);
                var hostBinaryDir = Path.Combine(packPath, "runtimes", "win-x64", "native");
                Directory.CreateDirectory(hostBinaryDir);
                File.WriteAllText(Path.Combine(hostBinaryDir, "apphost.exe"), "fake");
                File.WriteAllText(Path.Combine(hostBinaryDir, "singlefilehost.exe"), "fake");
                File.WriteAllText(Path.Combine(hostBinaryDir, "comhost.dll"), "fake");
                File.WriteAllText(Path.Combine(hostBinaryDir, "ijwhost.dll"), "fake");

                var knownAppHostPack = new Microsoft.Build.Utilities.TaskItem("Microsoft.NETCore.App.Host");
                knownAppHostPack.SetMetadata("TargetFramework", "net8.0");
                knownAppHostPack.SetMetadata("AppHostRuntimeIdentifiers", "win-x64;linux-x64;osx-x64");
                knownAppHostPack.SetMetadata("AppHostPackNamePattern", "Microsoft.NETCore.App.Host.**RID**");
                knownAppHostPack.SetMetadata("AppHostPackVersion", hostPackVersion);
                knownAppHostPack.SetMetadata(MetadataKeys.ExcludedRuntimeIdentifiers, "");

                var task = new ResolveAppHosts
                {
                    TargetFrameworkIdentifier = ".NETCoreApp",
                    TargetFrameworkVersion = "8.0",
                    TargetingPackRoot = targetingPackRelative,
                    AppHostRuntimeIdentifier = "win-x64",
                    RuntimeFrameworkVersion = null,
                    DotNetAppHostExecutableNameWithoutExtension = "apphost",
                    DotNetSingleFileHostExecutableNameWithoutExtension = "singlefilehost",
                    DotNetComHostLibraryNameWithoutExtension = "comhost",
                    DotNetIjwHostLibraryNameWithoutExtension = "ijwhost",
                    RuntimeGraphPath = runtimeGraphPath,
                    KnownAppHostPacks = new ITaskItem[] { knownAppHostPack },
                    NuGetRestoreSupported = true,
                    EnableAppHostPackDownload = false,
                };
                var mockEngine = new MockBuildEngine();
                task.BuildEngine = mockEngine;
                task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

                task.Execute().Should().BeTrue(
                    string.Join("; ", mockEngine.Errors.Select(e => e.Message)));

                task.AppHost.Should().NotBeNull().And.HaveCount(1);

                // PathInPackage should be a relative path within the pack (not absolutized)
                var pathInPackage = task.AppHost[0].GetMetadata(MetadataKeys.PathInPackage);
                pathInPackage.Should().NotBeNullOrEmpty();
                Path.IsPathRooted(pathInPackage).Should().BeFalse(
                    "PathInPackage should remain a relative path, not be affected by absolutization of TargetingPackRoot");
                pathInPackage.Should().Be(Path.Combine("runtimes", "win-x64", "native", "apphost.exe"));

                // Path should be the fully resolved absolute path
                var resolvedPath = task.AppHost[0].GetMetadata(MetadataKeys.Path);
                Path.IsPathRooted(resolvedPath).Should().BeTrue(
                    "Path metadata should be an absolute path after TargetingPackRoot is absolutized");
                resolvedPath.Should().StartWith(projectDir,
                    "Path should be resolved relative to projectDir via TaskEnvironment");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        [Fact]
        public void ItHandlesEmptyTargetingPackRoot()
        {
            var projectDir = Path.Combine(Path.GetTempPath(), "apphost-emptytpr-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            try
            {
                var runtimeGraphPath = Path.Combine(projectDir, "runtime.json");
                File.WriteAllText(runtimeGraphPath, "{\"runtimes\":{\"win-x64\":{\"#import\":[\"win\",\"any\"]},\"win\":{\"#import\":[\"any\"]},\"any\":{}}}");

                var knownAppHostPack = new Microsoft.Build.Utilities.TaskItem("Microsoft.NETCore.App.Host");
                knownAppHostPack.SetMetadata("TargetFramework", "net8.0");
                knownAppHostPack.SetMetadata("AppHostRuntimeIdentifiers", "win-x64;linux-x64;osx-x64");
                knownAppHostPack.SetMetadata("AppHostPackNamePattern", "Microsoft.NETCore.App.Host.**RID**");
                knownAppHostPack.SetMetadata("AppHostPackVersion", "8.0.0");
                knownAppHostPack.SetMetadata(MetadataKeys.ExcludedRuntimeIdentifiers, "");

                var task = new ResolveAppHosts
                {
                    TargetFrameworkIdentifier = ".NETCoreApp",
                    TargetFrameworkVersion = "8.0",
                    TargetingPackRoot = "",
                    AppHostRuntimeIdentifier = "win-x64",
                    RuntimeFrameworkVersion = null,
                    DotNetAppHostExecutableNameWithoutExtension = "apphost",
                    DotNetSingleFileHostExecutableNameWithoutExtension = "singlefilehost",
                    DotNetComHostLibraryNameWithoutExtension = "comhost",
                    DotNetIjwHostLibraryNameWithoutExtension = "ijwhost",
                    RuntimeGraphPath = runtimeGraphPath,
                    KnownAppHostPacks = new ITaskItem[] { knownAppHostPack },
                    NuGetRestoreSupported = true,
                    EnableAppHostPackDownload = false,
                };
                var mockEngine = new MockBuildEngine();
                task.BuildEngine = mockEngine;
                task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

                Exception? caught = null;
                try { task.Execute(); } catch (Exception ex) { caught = ex; }

                // Empty TargetingPackRoot is guarded by a string.IsNullOrEmpty check in the task,
                // so it should not throw NullReferenceException or ArgumentException from GetAbsolutePath.
                if (caught != null)
                {
                    caught.Should().NotBeOfType<NullReferenceException>(
                        "empty TargetingPackRoot should not cause NullReferenceException");
                    caught.Should().NotBeOfType<ArgumentException>(
                        "empty TargetingPackRoot should be handled gracefully, not passed to GetAbsolutePath");
                }
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        [Fact]
        public void ItPreservesRelativePathInAllHostOutputs()
        {
            // Verify path format preservation across all host output types (AppHost, SingleFileHost,
            // ComHost, IjwHost) — PathInPackage should remain relative, Path should be absolute.
            var projectDir = Path.Combine(Path.GetTempPath(), "apphost-alloutputs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            try
            {
                var runtimeGraphPath = Path.Combine(projectDir, "runtime.json");
                File.WriteAllText(runtimeGraphPath, "{\"runtimes\":{\"win-x64\":{\"#import\":[\"win\",\"any\"]},\"win\":{\"#import\":[\"any\"]},\"any\":{}}}");

                var targetingPackRelative = "packs";
                var hostPackName = "Microsoft.NETCore.App.Host.win-x64";
                var hostPackVersion = "8.0.0";
                var packPath = Path.Combine(projectDir, targetingPackRelative, hostPackName, hostPackVersion);
                var hostBinaryDir = Path.Combine(packPath, "runtimes", "win-x64", "native");
                Directory.CreateDirectory(hostBinaryDir);
                File.WriteAllText(Path.Combine(hostBinaryDir, "apphost.exe"), "fake");
                File.WriteAllText(Path.Combine(hostBinaryDir, "singlefilehost.exe"), "fake");
                File.WriteAllText(Path.Combine(hostBinaryDir, "comhost.dll"), "fake");
                File.WriteAllText(Path.Combine(hostBinaryDir, "ijwhost.dll"), "fake");

                var knownAppHostPack = new Microsoft.Build.Utilities.TaskItem("Microsoft.NETCore.App.Host");
                knownAppHostPack.SetMetadata("TargetFramework", "net8.0");
                knownAppHostPack.SetMetadata("AppHostRuntimeIdentifiers", "win-x64;linux-x64;osx-x64");
                knownAppHostPack.SetMetadata("AppHostPackNamePattern", "Microsoft.NETCore.App.Host.**RID**");
                knownAppHostPack.SetMetadata("AppHostPackVersion", hostPackVersion);
                knownAppHostPack.SetMetadata(MetadataKeys.ExcludedRuntimeIdentifiers, "");

                var task = new ResolveAppHosts
                {
                    TargetFrameworkIdentifier = ".NETCoreApp",
                    TargetFrameworkVersion = "8.0",
                    TargetingPackRoot = targetingPackRelative,
                    AppHostRuntimeIdentifier = "win-x64",
                    RuntimeFrameworkVersion = null,
                    DotNetAppHostExecutableNameWithoutExtension = "apphost",
                    DotNetSingleFileHostExecutableNameWithoutExtension = "singlefilehost",
                    DotNetComHostLibraryNameWithoutExtension = "comhost",
                    DotNetIjwHostLibraryNameWithoutExtension = "ijwhost",
                    RuntimeGraphPath = runtimeGraphPath,
                    KnownAppHostPacks = new ITaskItem[] { knownAppHostPack },
                    NuGetRestoreSupported = true,
                    EnableAppHostPackDownload = false,
                };
                var mockEngine = new MockBuildEngine();
                task.BuildEngine = mockEngine;
                task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

                task.Execute().Should().BeTrue(
                    string.Join("; ", mockEngine.Errors.Select(e => e.Message)));

                // Verify all host types share the same path format contract
                var hostOutputs = new (string Name, ITaskItem[]? Items)[]
                {
                    ("AppHost", task.AppHost),
                    ("SingleFileHost", task.SingleFileHost),
                    ("ComHost", task.ComHost),
                    ("IjwHost", task.IjwHost),
                };

                foreach (var (name, items) in hostOutputs)
                {
                    items.Should().NotBeNull($"{name} should be resolved").And.HaveCount(1);
                    var item = items![0];

                    var pathInPackage = item.GetMetadata(MetadataKeys.PathInPackage);
                    pathInPackage.Should().NotBeNullOrEmpty($"{name} should have PathInPackage metadata");
                    Path.IsPathRooted(pathInPackage).Should().BeFalse(
                        $"{name}.PathInPackage should remain a relative path within the pack");

                    var resolvedPath = item.GetMetadata(MetadataKeys.Path);
                    resolvedPath.Should().NotBeNullOrEmpty($"{name} should have Path metadata");
                    Path.IsPathRooted(resolvedPath).Should().BeTrue(
                        $"{name}.Path should be an absolute path after TargetingPackRoot is absolutized");
                    resolvedPath.Should().StartWith(projectDir,
                        $"{name}.Path should be resolved relative to projectDir via TaskEnvironment");

                    var packageDir = item.GetMetadata(MetadataKeys.PackageDirectory);
                    packageDir.Should().NotBeNullOrEmpty($"{name} should have PackageDirectory metadata");
                    Path.IsPathRooted(packageDir).Should().BeTrue(
                        $"{name}.PackageDirectory should be an absolute path");
                }
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        [Fact]
        public void ItProducesSameOutputInSingleProcessAndMultiProcessMode()
        {
            var projectDir = Path.Combine(Path.GetTempPath(), "apphost-sp-mp-" + Guid.NewGuid().ToString("N"));
            var otherDir = Path.Combine(Path.GetTempPath(), "apphost-other-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(otherDir);

            var savedCwd = Directory.GetCurrentDirectory();
            try
            {
                // Create a RuntimeGraph file under projectDir
                var runtimeGraphPath = Path.Combine(projectDir, "runtime.json");
                File.WriteAllText(runtimeGraphPath, "{\"runtimes\":{\"win-x64\":{\"#import\":[\"win\",\"any\"]},\"win\":{\"#import\":[\"any\"]},\"any\":{}}}");

                // Create a targeting pack root with an app host pack structure under projectDir
                var targetingPackRelative = "packs";
                var hostPackName = "Microsoft.NETCore.App.Host.win-x64";
                var hostPackVersion = "8.0.0";
                var packPath = Path.Combine(projectDir, targetingPackRelative, hostPackName, hostPackVersion);
                var hostBinaryDir = Path.Combine(packPath, "runtimes", "win-x64", "native");
                Directory.CreateDirectory(hostBinaryDir);
                File.WriteAllText(Path.Combine(hostBinaryDir, "apphost.exe"), "fake");
                File.WriteAllText(Path.Combine(hostBinaryDir, "singlefilehost.exe"), "fake");
                File.WriteAllText(Path.Combine(hostBinaryDir, "comhost.dll"), "fake");
                File.WriteAllText(Path.Combine(hostBinaryDir, "ijwhost.dll"), "fake");

                // --- Single-process run: CWD == projectDir ---
                ITaskItem[] singleProcessAppHost;
                Directory.SetCurrentDirectory(projectDir);
                try
                {
                    var knownAppHostPack = new Microsoft.Build.Utilities.TaskItem("Microsoft.NETCore.App.Host");
                    knownAppHostPack.SetMetadata("TargetFramework", "net8.0");
                    knownAppHostPack.SetMetadata("AppHostRuntimeIdentifiers", "win-x64;linux-x64;osx-x64");
                    knownAppHostPack.SetMetadata("AppHostPackNamePattern", "Microsoft.NETCore.App.Host.**RID**");
                    knownAppHostPack.SetMetadata("AppHostPackVersion", hostPackVersion);
                    knownAppHostPack.SetMetadata(MetadataKeys.ExcludedRuntimeIdentifiers, "");

                    var task = new ResolveAppHosts
                    {
                        TargetFrameworkIdentifier = ".NETCoreApp",
                        TargetFrameworkVersion = "8.0",
                        TargetingPackRoot = targetingPackRelative,
                        AppHostRuntimeIdentifier = "win-x64",
                        RuntimeFrameworkVersion = null,
                        DotNetAppHostExecutableNameWithoutExtension = "apphost",
                        DotNetSingleFileHostExecutableNameWithoutExtension = "singlefilehost",
                        DotNetComHostLibraryNameWithoutExtension = "comhost",
                        DotNetIjwHostLibraryNameWithoutExtension = "ijwhost",
                        RuntimeGraphPath = runtimeGraphPath,
                        KnownAppHostPacks = new ITaskItem[] { knownAppHostPack },
                        NuGetRestoreSupported = true,
                        EnableAppHostPackDownload = false,
                    };
                    var mockEngine = new MockBuildEngine();
                    task.BuildEngine = mockEngine;
                    task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

                    task.Execute().Should().BeTrue(
                        "single-process run should succeed: " + string.Join("; ", mockEngine.Errors.Select(e => e.Message)));
                    singleProcessAppHost = task.AppHost;
                }
                finally
                {
                    Directory.SetCurrentDirectory(savedCwd);
                }

                // --- Multi-process run: CWD != projectDir ---
                ITaskItem[] multiProcessAppHost;
                Directory.SetCurrentDirectory(otherDir);
                try
                {
                    var knownAppHostPack = new Microsoft.Build.Utilities.TaskItem("Microsoft.NETCore.App.Host");
                    knownAppHostPack.SetMetadata("TargetFramework", "net8.0");
                    knownAppHostPack.SetMetadata("AppHostRuntimeIdentifiers", "win-x64;linux-x64;osx-x64");
                    knownAppHostPack.SetMetadata("AppHostPackNamePattern", "Microsoft.NETCore.App.Host.**RID**");
                    knownAppHostPack.SetMetadata("AppHostPackVersion", hostPackVersion);
                    knownAppHostPack.SetMetadata(MetadataKeys.ExcludedRuntimeIdentifiers, "");

                    var task = new ResolveAppHosts
                    {
                        TargetFrameworkIdentifier = ".NETCoreApp",
                        TargetFrameworkVersion = "8.0",
                        TargetingPackRoot = targetingPackRelative,
                        AppHostRuntimeIdentifier = "win-x64",
                        RuntimeFrameworkVersion = null,
                        DotNetAppHostExecutableNameWithoutExtension = "apphost",
                        DotNetSingleFileHostExecutableNameWithoutExtension = "singlefilehost",
                        DotNetComHostLibraryNameWithoutExtension = "comhost",
                        DotNetIjwHostLibraryNameWithoutExtension = "ijwhost",
                        RuntimeGraphPath = runtimeGraphPath,
                        KnownAppHostPacks = new ITaskItem[] { knownAppHostPack },
                        NuGetRestoreSupported = true,
                        EnableAppHostPackDownload = false,
                    };
                    var mockEngine = new MockBuildEngine();
                    task.BuildEngine = mockEngine;
                    task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

                    task.Execute().Should().BeTrue(
                        "multi-process run should succeed: " + string.Join("; ", mockEngine.Errors.Select(e => e.Message)));
                    multiProcessAppHost = task.AppHost;
                }
                finally
                {
                    Directory.SetCurrentDirectory(savedCwd);
                }

                // Assert both runs produced the same AppHost output
                singleProcessAppHost.Should().NotBeNull().And.HaveCount(1);
                multiProcessAppHost.Should().NotBeNull().And.HaveCount(1);

                var spItem = singleProcessAppHost[0];
                var mpItem = multiProcessAppHost[0];

                mpItem.GetMetadata(MetadataKeys.Path).Should().Be(
                    spItem.GetMetadata(MetadataKeys.Path),
                    "Path metadata should match between single-process and multi-process runs");
                mpItem.GetMetadata(MetadataKeys.PackageDirectory).Should().Be(
                    spItem.GetMetadata(MetadataKeys.PackageDirectory),
                    "PackageDirectory metadata should match between single-process and multi-process runs");
                mpItem.GetMetadata(MetadataKeys.PathInPackage).Should().Be(
                    spItem.GetMetadata(MetadataKeys.PathInPackage),
                    "PathInPackage metadata should match between single-process and multi-process runs");
                mpItem.GetMetadata(MetadataKeys.RuntimeIdentifier).Should().Be(
                    spItem.GetMetadata(MetadataKeys.RuntimeIdentifier),
                    "RuntimeIdentifier metadata should match between single-process and multi-process runs");
            }
            finally
            {
                Directory.SetCurrentDirectory(savedCwd);
                if (Directory.Exists(projectDir)) Directory.Delete(projectDir, true);
                if (Directory.Exists(otherDir)) Directory.Delete(otherDir, true);
            }
        }
    }
}
