// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
            task.ResolvedFrameworkReferences.Should().BeEmpty();
        }

        [Fact]
        public void ResolvesFrameworkReferences_WithMatchingPacks()
        {
            var fwRef = new MockTaskItem("Microsoft.NETCore.App");

            var targetingPack = new MockTaskItem("Microsoft.NETCore.App.Ref");
            targetingPack.SetMetadata("FrameworkName", "Microsoft.NETCore.App");
            targetingPack.SetMetadata("NuGetPackageVersion", "8.0.0");
            targetingPack.SetMetadata("Path", @"C:\packs\targeting");

            var runtimePack = new MockTaskItem("Microsoft.NETCore.App.Runtime.win-x64");
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
    }
}
