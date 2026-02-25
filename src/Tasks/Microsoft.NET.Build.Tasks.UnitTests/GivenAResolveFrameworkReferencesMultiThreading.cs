// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAResolveFrameworkReferencesMultiThreading
    {
        [Fact]
        public void EmptyInputs_DoesNotCrash()
        {
            var task = new ResolveFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                FrameworkReferences = Array.Empty<ITaskItem>(),
                ResolvedTargetingPacks = Array.Empty<ITaskItem>(),
                ResolvedRuntimePacks = Array.Empty<ITaskItem>(),
            };

            var result = task.Execute();

            result.Should().BeTrue("empty inputs should succeed with no output");
            (task.ResolvedFrameworkReferences ?? Array.Empty<ITaskItem>()).Should().BeEmpty();
        }

        [Fact]
        public void ResolvesFrameworkReferences_WithMatchingPacks()
        {
            var fwRef = new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>());

            var targetingPack = new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>());
            targetingPack.SetMetadata("FrameworkName", "Microsoft.NETCore.App");
            targetingPack.SetMetadata("NuGetPackageVersion", "8.0.0");
            targetingPack.SetMetadata("Path", @"C:\packs\targeting");

            var runtimePack = new MockTaskItem("Microsoft.NETCore.App.Runtime.win-x64", new Dictionary<string, string>());
            runtimePack.SetMetadata("FrameworkName", "Microsoft.NETCore.App");
            runtimePack.SetMetadata("NuGetPackageVersion", "8.0.0");
            runtimePack.SetMetadata("PackageDirectory", @"C:\packs\runtime");

            var task = new ResolveFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                FrameworkReferences = new ITaskItem[] { fwRef },
                ResolvedTargetingPacks = new ITaskItem[] { targetingPack },
                ResolvedRuntimePacks = new ITaskItem[] { runtimePack },
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.ResolvedFrameworkReferences.Should().NotBeEmpty();
        }
        [Theory]
        [InlineData(4)]
        [InlineData(16)]
        public async System.Threading.Tasks.Task ResolveFrameworkReferences_ConcurrentExecution(int parallelism)
        {
            var errors = new ConcurrentBag<string>();
            using var startGate = new ManualResetEventSlim(false);
            var tasks = new System.Threading.Tasks.Task[parallelism];
            for (int i = 0; i < parallelism; i++)
            {
                int idx = i;
                tasks[idx] = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var task = new ResolveFrameworkReferences
                    {
                        BuildEngine = new MockBuildEngine(),
                        FrameworkReferences = Array.Empty<ITaskItem>(),
                        ResolvedTargetingPacks = Array.Empty<ITaskItem>(),
                        ResolvedRuntimePacks = Array.Empty<ITaskItem>(),
                    };
                    startGate.Wait();
                    task.Execute();
                }
                catch (Exception ex) { errors.Add($"Thread {idx}: {ex.Message}"); }
            });
            }
            startGate.Set();
            await System.Threading.Tasks.Task.WhenAll(tasks);

            errors.Should().BeEmpty();
        }
    }
}
