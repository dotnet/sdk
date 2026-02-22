// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAResolveTargetingPackAssetsMultiThreading
    {
        [Fact]
        public void EmptyTargetingPacks_DoesNotCrash()
        {
            var task = new ResolveTargetingPackAssets
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                FrameworkReferences = Array.Empty<ITaskItem>(),
                ResolvedTargetingPacks = Array.Empty<ITaskItem>(),
                RuntimeFrameworks = Array.Empty<ITaskItem>(),
                GenerateErrorForMissingTargetingPacks = false,
            };

            var result = task.Execute();

            result.Should().BeTrue("empty targeting packs should succeed");
            task.ReferencesToAdd.Should().BeEmpty();
        }

        [Fact]
        public void TaskEnvironmentProperty_CanBeSet()
        {
            var task = new ResolveTargetingPackAssets();
            var te = TaskEnvironmentHelper.CreateForTest();

            var act = () => task.TaskEnvironment = te;
            act.Should().NotThrow("TaskEnvironment property should be settable");
            task.TaskEnvironment.Should().Be(te);
        }
    }
}
