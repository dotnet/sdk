// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAGetAssemblyVersionMultiThreading
    {
        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            typeof(GetAssemblyVersion).GetInterfaces().Should().Contain(typeof(IMultiThreadableTask));
        }

        [Fact]
        public void TaskEnvironmentPropertyExistsAndCanBeSet()
        {
            var task = new GetAssemblyVersion();
            var engine = new MockBuildEngine();
            task.BuildEngine = engine;

            task.Should().BeAssignableTo<IMultiThreadableTask>();

            var multiThreadable = task as IMultiThreadableTask;
            multiThreadable.Should().NotBeNull();

            var property = typeof(GetAssemblyVersion).GetProperty(nameof(IMultiThreadableTask.TaskEnvironment));
            property.Should().NotBeNull();
            property.CanRead.Should().BeTrue();
            property.CanWrite.Should().BeTrue();
        }

        [Fact]
        public void ParsesSemanticVersionCorrectly()
        {
            var task = new GetAssemblyVersion();
            var engine = new MockBuildEngine();
            task.BuildEngine = engine;
            task.NuGetVersion = "1.2.3-preview.1+build.123";

            bool result = task.Execute();

            result.Should().BeTrue();
            task.AssemblyVersion.Should().Be("1.2.3.0");
            engine.Errors.Should().BeEmpty();
        }

        [Fact]
        public void ParsesSimpleVersionCorrectly()
        {
            var task = new GetAssemblyVersion();
            var engine = new MockBuildEngine();
            task.BuildEngine = engine;
            task.NuGetVersion = "5.0.0";

            bool result = task.Execute();

            result.Should().BeTrue();
            task.AssemblyVersion.Should().Be("5.0.0.0");
            engine.Errors.Should().BeEmpty();
        }

        [Fact]
        public void HandlesInvalidVersionGracefully()
        {
            var task = new GetAssemblyVersion();
            var engine = new MockBuildEngine();
            task.BuildEngine = engine;
            task.NuGetVersion = "not-a-version";

            bool result = task.Execute();

            result.Should().BeFalse();
            engine.Errors.Should().NotBeEmpty();
            engine.Errors.Should().Contain(e => e.Message != null && e.Message.Contains("not-a-version"));
        }

        [Fact]
        public void TaskProducesSameOutputWithAndWithoutExplicitTaskEnvironment()
        {
            var engine1 = new MockBuildEngine();
            var task1 = new GetAssemblyVersion
            {
                BuildEngine = engine1,
                NuGetVersion = "2.1.0-rc.1"
            };
            task1.Execute().Should().BeTrue();

            var engine2 = new MockBuildEngine();
            var task2 = new GetAssemblyVersion
            {
                BuildEngine = engine2,
                NuGetVersion = "2.1.0-rc.1",
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(Directory.GetCurrentDirectory())
            };
            task2.Execute().Should().BeTrue();

            task1.AssemblyVersion.Should().Be(task2.AssemblyVersion);
            task1.AssemblyVersion.Should().Be("2.1.0.0");
        }

        [Fact]
        public async Task ConcurrentExecutionProducesCorrectResults()
        {
            const int concurrency = 16;
            var taskInstances = new GetAssemblyVersion[concurrency];
            var executeTasks = new Task<bool>[concurrency];
            var engines = new MockBuildEngine[concurrency];

            var startGate = new TaskCompletionSource<bool>();
            var readyTasks = new Task[concurrency];

            for (int i = 0; i < concurrency; i++)
            {
                engines[i] = new MockBuildEngine();
                taskInstances[i] = new GetAssemblyVersion
                {
                    BuildEngine = engines[i],
                    NuGetVersion = "3.1.4-beta.2+sha.abc123",
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(Directory.GetCurrentDirectory())
                };
            }

            for (int i = 0; i < concurrency; i++)
            {
                int index = i;
                var readySource = new TaskCompletionSource<bool>();
                readyTasks[index] = readySource.Task;

                executeTasks[index] = Task.Run(async () =>
                {
                    readySource.SetResult(true);
                    await startGate.Task;
                    return taskInstances[index].Execute();
                });
            }

            await Task.WhenAll(readyTasks);
            startGate.SetResult(true);
            await Task.WhenAll(executeTasks);

            for (int i = 0; i < concurrency; i++)
            {
                (await executeTasks[i]).Should().BeTrue($"task {i} should succeed");
                taskInstances[i].AssemblyVersion.Should().Be("3.1.4.0", $"task {i} should produce correct version");
                engines[i].Errors.Should().BeEmpty($"task {i} should not log errors");
            }

            var allVersions = taskInstances.Select(t => t.AssemblyVersion).Distinct().ToArray();
            allVersions.Should().ContainSingle("all tasks should produce the same version");
            allVersions[0].Should().Be("3.1.4.0");
        }

        [Fact]
        public async Task ConcurrentExecutionWithDifferentInputsProducesCorrectResults()
        {
            const int concurrency = 8;
            var versions = new[]
            {
                "1.0.0",
                "2.1.3-alpha",
                "3.5.7-beta.1",
                "10.20.30-rc.5+build",
                "0.0.1",
                "99.99.99",
                "5.4.3-preview",
                "7.8.9"
            };

            var expected = new[]
            {
                "1.0.0.0",
                "2.1.3.0",
                "3.5.7.0",
                "10.20.30.0",
                "0.0.1.0",
                "99.99.99.0",
                "5.4.3.0",
                "7.8.9.0"
            };

            var taskInstances = new GetAssemblyVersion[concurrency];
            var executeTasks = new Task<bool>[concurrency];
            var engines = new MockBuildEngine[concurrency];

            for (int i = 0; i < concurrency; i++)
            {
                engines[i] = new MockBuildEngine();
                taskInstances[i] = new GetAssemblyVersion
                {
                    BuildEngine = engines[i],
                    NuGetVersion = versions[i],
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(Directory.GetCurrentDirectory())
                };
            }

            for (int i = 0; i < concurrency; i++)
            {
                int index = i;
                executeTasks[index] = Task.Run(() => taskInstances[index].Execute());
            }

            await Task.WhenAll(executeTasks);

            for (int i = 0; i < concurrency; i++)
            {
                (await executeTasks[i]).Should().BeTrue($"task {i} should succeed");
                taskInstances[i].AssemblyVersion.Should().Be(expected[i], $"task {i} should produce correct version for input '{versions[i]}'");
                engines[i].Errors.Should().BeEmpty($"task {i} should not log errors");
            }
        }
    }
}
