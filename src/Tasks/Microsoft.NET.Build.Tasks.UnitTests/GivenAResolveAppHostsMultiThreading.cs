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
        public void ItImplementsIMultiThreadableTask()
        {
            var task = new ResolveAppHosts();
            task.Should().BeAssignableTo<IMultiThreadableTask>();
        }

        [Fact]
        public void ItHasMSBuildMultiThreadableTaskAttribute()
        {
            typeof(ResolveAppHosts).Should().BeDecoratedWith<MSBuildMultiThreadableTaskAttribute>();
        }

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
                var teProp = task.GetType().GetProperty("TaskEnvironment");
                teProp.Should().NotBeNull("task must have a TaskEnvironment property (from IMultiThreadableTask)");
                teProp!.SetValue(task, TaskEnvironmentHelper.CreateForTest(projectDir));

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
    }
}
