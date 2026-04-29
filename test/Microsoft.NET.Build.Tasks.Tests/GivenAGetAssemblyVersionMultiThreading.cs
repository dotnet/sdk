// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAGetAssemblyVersionMultiThreading
    {
        private const int Concurrency = 64;

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

            var taskEnvironment = TaskEnvironmentHelper.CreateForTest(GetProjectDirectory(nameof(TaskEnvironmentPropertyExistsAndCanBeSet)));
            multiThreadable.TaskEnvironment = taskEnvironment;
            multiThreadable.TaskEnvironment.Should().BeSameAs(taskEnvironment);
        }

        [Fact]
        public void RunsInProcessWhenMSBuildRecognizesMultiThreadableTask()
        {
            string projectDirectory = GetProjectDirectory(nameof(RunsInProcessWhenMSBuildRecognizesMultiThreadableTask));
            Directory.CreateDirectory(projectDirectory);

            string projectFile = Path.Combine(projectDirectory, "GetAssemblyVersion.proj");
            File.WriteAllText(projectFile, $"""
                <Project>
                  <UsingTask TaskName="{nameof(GetAssemblyVersion)}" AssemblyFile="{typeof(GetAssemblyVersion).Assembly.Location}" />

                  <Target Name="RunGetAssemblyVersion">
                    <GetAssemblyVersion NuGetVersion="1.2.3">
                      <Output TaskParameter="{nameof(GetAssemblyVersion.AssemblyVersion)}" PropertyName="AssemblyVersion" />
                    </GetAssemblyVersion>
                  </Target>
                </Project>
                """);

            var log = new StringBuilder();
            var logger = new Microsoft.Build.Logging.ConsoleLogger(LoggerVerbosity.Diagnostic, message => log.AppendLine(message), null, null);
            var buildParameters = new BuildParameters
            {
                MultiThreaded = true,
                Loggers = new[] { logger },
                DisableInProcNode = false,
                EnableNodeReuse = false
            };

            var buildRequestData = new BuildRequestData(
                projectFile,
                new Dictionary<string, string?>(),
                null,
                new[] { "RunGetAssemblyVersion" },
                null);

            using var buildManager = new BuildManager(nameof(RunsInProcessWhenMSBuildRecognizesMultiThreadableTask));
            var result = buildManager.Build(buildParameters, buildRequestData);
            string fullLog = log.ToString();

            result.OverallResult.Should().Be(BuildResultCode.Success, fullLog);
            fullLog.Should().NotContain($"Launching task \"{nameof(GetAssemblyVersion)}\"",
                "MSBuild should recognize GetAssemblyVersion as multi-threadable and avoid TaskHost routing");
        }

        [Fact]
        public void ParsesSemanticVersionCorrectly()
        {
            var task = new GetAssemblyVersion();
            var engine = new MockBuildEngine();
            task.BuildEngine = engine;
            task.NuGetVersion = "1.2.3-preview.1+build.123";
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(GetProjectDirectory(nameof(ParsesSemanticVersionCorrectly)));

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
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(GetProjectDirectory(nameof(ParsesSimpleVersionCorrectly)));

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
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(GetProjectDirectory(nameof(HandlesInvalidVersionGracefully)));

            bool result = task.Execute();

            result.Should().BeFalse();
            engine.Errors.Should().NotBeEmpty();
            engine.Errors.Should().Contain(e => e.Message != null && e.Message.Contains("not-a-version"));
        }

        [Fact]
        public void TaskProducesSameOutputWithDifferentNonDefaultProjectDirectories()
        {
            var engine1 = new MockBuildEngine();
            var task1 = new GetAssemblyVersion
            {
                BuildEngine = engine1,
                NuGetVersion = "2.1.0-rc.1",
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(GetProjectDirectory("project-one"))
            };
            task1.Execute().Should().BeTrue();

            var engine2 = new MockBuildEngine();
            var task2 = new GetAssemblyVersion
            {
                BuildEngine = engine2,
                NuGetVersion = "2.1.0-rc.1",
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(GetProjectDirectory("project-two"))
            };
            task2.Execute().Should().BeTrue();

            task1.AssemblyVersion.Should().Be(task2.AssemblyVersion);
            task1.AssemblyVersion.Should().Be("2.1.0.0");
            task1.TaskEnvironment.ProjectDirectory.Value.Should().NotBe(task2.TaskEnvironment.ProjectDirectory.Value);
        }

        [Fact]
        public async Task ConcurrentExecutionProducesCorrectResults()
        {
            CancellationToken cancellationToken = TestContext.Current.CancellationToken;
            var taskInstances = new GetAssemblyVersion[Concurrency];
            var executeTasks = new Task<bool>[Concurrency];
            var engines = new MockBuildEngine[Concurrency];

            using var startGate = new Barrier(Concurrency + 1);

            for (int i = 0; i < Concurrency; i++)
            {
                engines[i] = new MockBuildEngine();
                taskInstances[i] = new GetAssemblyVersion
                {
                    BuildEngine = engines[i],
                    NuGetVersion = "3.1.4-beta.2+sha.abc123",
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(GetProjectDirectory($"same-input-{i}"))
                };
            }

            for (int i = 0; i < Concurrency; i++)
            {
                int index = i;
                executeTasks[index] = Task.Factory.StartNew(() =>
                {
                    startGate.SignalAndWait(cancellationToken);
                    return taskInstances[index].Execute();
                }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            startGate.SignalAndWait(cancellationToken);
            bool[] results = await Task.WhenAll(executeTasks);

            for (int i = 0; i < Concurrency; i++)
            {
                results[i].Should().BeTrue($"task {i} should succeed");
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
            CancellationToken cancellationToken = TestContext.Current.CancellationToken;
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

            var taskInstances = new GetAssemblyVersion[Concurrency];
            var executeTasks = new Task<bool>[Concurrency];
            var engines = new MockBuildEngine[Concurrency];

            using var startGate = new Barrier(Concurrency + 1);

            for (int i = 0; i < Concurrency; i++)
            {
                int versionIndex = i % versions.Length;
                engines[i] = new MockBuildEngine();
                taskInstances[i] = new GetAssemblyVersion
                {
                    BuildEngine = engines[i],
                    NuGetVersion = versions[versionIndex],
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(GetProjectDirectory($"different-input-{i}"))
                };
            }

            for (int i = 0; i < Concurrency; i++)
            {
                int index = i;
                executeTasks[index] = Task.Factory.StartNew(() =>
                {
                    startGate.SignalAndWait(cancellationToken);
                    return taskInstances[index].Execute();
                }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            startGate.SignalAndWait(cancellationToken);
            bool[] results = await Task.WhenAll(executeTasks);

            for (int i = 0; i < Concurrency; i++)
            {
                int versionIndex = i % versions.Length;
                results[i].Should().BeTrue($"task {i} should succeed");
                taskInstances[i].AssemblyVersion.Should().Be(expected[versionIndex], $"task {i} should produce correct version for input '{versions[versionIndex]}'");
                engines[i].Errors.Should().BeEmpty($"task {i} should not log errors");
            }
        }

        private static string GetProjectDirectory(string name)
        {
            return Path.Combine(AppContext.BaseDirectory, nameof(GivenAGetAssemblyVersionMultiThreading), name);
        }
    }
}
