// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenACreateWindowsSdkKnownFrameworkReferencesMultiThreading
    {
        [Fact]
        public void TaskDeclaresMSBuildMultiThreadableContract()
        {
            var taskType = typeof(CreateWindowsSdkKnownFrameworkReferences);

            taskType.CustomAttributes
                .Select(attribute => attribute.AttributeType.FullName)
                .Should().Contain("Microsoft.Build.Framework.MSBuildMultiThreadableTaskAttribute",
                    "MSBuild only runs tasks concurrently when they are marked as multi-threadable");

            taskType.GetInterfaces()
                .Select(interfaceType => interfaceType.FullName)
                .Should().Contain("Microsoft.Build.Framework.IMultiThreadableTask",
                    "multi-threadable tasks must receive isolated task execution environments");
        }

        [Fact]
        public void TaskProducesSameOutputsWithAndWithoutExplicitTaskEnvironment()
        {
            // Run without explicitly setting TaskEnvironment (uses default null)
            var engine1 = new MockBuildEngine();
            var task1 = CreateBasicTask(engine1);
            task1.Execute().Should().BeTrue();

            // Run with explicitly set TaskEnvironment
            var engine2 = new MockBuildEngine();
            var task2 = CreateBasicTask(engine2);
            task2.TaskEnvironment = TaskEnvironmentHelper.CreateForTest();
            task2.Execute().Should().BeTrue();

            task1.KnownFrameworkReferences.Length.Should().Be(task2.KnownFrameworkReferences.Length,
                "both runs should produce the same number of framework references");

            var refs1 = task1.KnownFrameworkReferences.Select(r => r.ItemSpec).OrderBy(x => x).ToArray();
            var refs2 = task2.KnownFrameworkReferences.Select(r => r.ItemSpec).OrderBy(x => x).ToArray();

            refs1.Should().BeEquivalentTo(refs2,
                "both runs should produce identical framework reference item specs");
        }

        [Fact]
        public async Task ConcurrentExecutionWithDifferentInputsProducesCorrectResults()
        {
            const int scenarioCount = 8;
            const int repetitionsPerScenario = 8;
            const int concurrency = scenarioCount * repetitionsPerScenario;
            var taskInstances = new CreateWindowsSdkKnownFrameworkReferences[concurrency];
            var executeTasks = new Task<bool>[concurrency];
            using var readyGate = new CountdownEvent(concurrency);
            using var startGate = new ManualResetEventSlim(false);

            // Scenario 1: WindowsSdkPackageVersion directly specified
            taskInstances[0] = new CreateWindowsSdkKnownFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                WindowsSdkPackageVersion = "10.0.19041.35",
                TargetFrameworkVersion = "8.0",
                TargetPlatformVersion = "10.0.19041.0",
            };

            // Scenario 2: UseWindowsSDKPreview set
            taskInstances[1] = new CreateWindowsSdkKnownFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                UseWindowsSDKPreview = true,
                TargetFrameworkVersion = "9.0",
                TargetPlatformVersion = "10.0.22621.0",
            };

            // Scenario 3: WindowsSdkSupportedTargetPlatformVersions with single version
            taskInstances[2] = new CreateWindowsSdkKnownFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                TargetFrameworkVersion = "8.0",
                TargetPlatformVersion = "10.0.19041.0",
                WindowsSdkSupportedTargetPlatformVersions = new ITaskItem[]
                {
                    new MockTaskItem("10.0.19041.0", new Dictionary<string, string>
                    {
                        { "WindowsSdkPackageVersion", "10.0.19041.8" },
                        { "MinimumNETVersion", "6.0" }
                    })
                }
            };

            // Scenario 4: WindowsSdkSupportedTargetPlatformVersions with multiple versions
            taskInstances[3] = new CreateWindowsSdkKnownFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                TargetFrameworkVersion = "8.0",
                TargetPlatformVersion = "10.0.22621.0",
                WindowsSdkSupportedTargetPlatformVersions = new ITaskItem[]
                {
                    new MockTaskItem("10.0.19041.0", new Dictionary<string, string>
                    {
                        { "WindowsSdkPackageVersion", "10.0.19041.8" }
                    }),
                    new MockTaskItem("10.0.22621.0", new Dictionary<string, string>
                    {
                        { "WindowsSdkPackageVersion", "10.0.22621.15" }
                    })
                }
            };

            // Scenario 5: With WindowsSdkPackageMinimumRevision
            taskInstances[4] = new CreateWindowsSdkKnownFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                TargetFrameworkVersion = "8.0",
                TargetPlatformVersion = "10.0.19041.0",
                WindowsSdkPackageMinimumRevision = "10",
                WindowsSdkSupportedTargetPlatformVersions = new ITaskItem[]
                {
                    new MockTaskItem("10.0.19041.0", new Dictionary<string, string>
                    {
                        { "WindowsSdkPackageVersion", "10.0.19041.5" }
                    })
                }
            };

            // Scenario 6: MinimumNETVersion filtering (should skip version)
            taskInstances[5] = new CreateWindowsSdkKnownFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                TargetFrameworkVersion = "7.0",
                TargetPlatformVersion = "10.0.19041.0",
                WindowsSdkSupportedTargetPlatformVersions = new ITaskItem[]
                {
                    new MockTaskItem("10.0.19041.0", new Dictionary<string, string>
                    {
                        { "WindowsSdkPackageVersion", "10.0.19041.8" },
                        { "MinimumNETVersion", "8.0" }
                    })
                }
            };

            // Scenario 7: Duplicate of scenario 1 to verify consistency
            taskInstances[6] = new CreateWindowsSdkKnownFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                WindowsSdkPackageVersion = "10.0.19041.35",
                TargetFrameworkVersion = "8.0",
                TargetPlatformVersion = "10.0.19041.0",
            };

            // Scenario 8: Another duplicate to stress-test race conditions
            taskInstances[7] = new CreateWindowsSdkKnownFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                UseWindowsSDKPreview = true,
                TargetFrameworkVersion = "9.0",
                TargetPlatformVersion = "10.0.22621.0",
            };

            for (int i = scenarioCount; i < concurrency; i++)
            {
                taskInstances[i] = CloneTask(taskInstances[i % scenarioCount]);
            }

            for (int i = 0; i < concurrency; i++)
            {
                var taskIndex = i;
                executeTasks[i] = Task.Factory.StartNew(() =>
                {
                    readyGate.Signal();
                    startGate.Wait();
                    return taskInstances[taskIndex].Execute();
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            var allTasksReady = readyGate.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            startGate.Set();

            var results = await Task.WhenAll(executeTasks);
            allTasksReady.Should().BeTrue("all concurrent tasks should be waiting before the shared start gate opens");

            // Verify all tasks succeeded
            for (int i = 0; i < concurrency; i++)
            {
                results[i].Should().BeTrue($"task {i} should succeed");
            }

            // Scenario 1: WindowsSdkPackageVersion directly specified
            taskInstances[0].KnownFrameworkReferences.Should().HaveCount(5,
                "should produce 5 profiles: no-profile, Windows, Xaml, CsWinRT3.Windows, CsWinRT3.Xaml");
            var scenario1Refs = taskInstances[0].KnownFrameworkReferences;
            scenario1Refs.Should().Contain(r => r.ItemSpec == "Microsoft.Windows.SDK.NET.Ref");
            scenario1Refs.Should().Contain(r => r.ItemSpec == "Microsoft.Windows.SDK.NET.Ref.Windows");
            scenario1Refs.Should().Contain(r => r.ItemSpec == "Microsoft.Windows.SDK.NET.Ref.Xaml");
            scenario1Refs.Should().Contain(r => r.ItemSpec == "Microsoft.Windows.SDK.NET.Ref.CsWinRT3.Windows");
            scenario1Refs.Should().Contain(r => r.ItemSpec == "Microsoft.Windows.SDK.NET.Ref.CsWinRT3.Xaml");

            var scenario1Base = scenario1Refs.First(r => r.ItemSpec == "Microsoft.Windows.SDK.NET.Ref");
            scenario1Base.GetMetadata(MetadataKeys.TargetFramework).Should().Be("net8.0-windows10.0.19041.0");
            scenario1Base.GetMetadata(MetadataKeys.RuntimeFrameworkName).Should().Be("Microsoft.Windows.SDK.NET.Ref");
            scenario1Base.GetMetadata("DefaultRuntimeFrameworkVersion").Should().Be("10.0.19041.35");
            scenario1Base.GetMetadata("LatestRuntimeFrameworkVersion").Should().Be("10.0.19041.35");
            scenario1Base.GetMetadata("TargetingPackName").Should().Be("Microsoft.Windows.SDK.NET.Ref");
            scenario1Base.GetMetadata("TargetingPackVersion").Should().Be("10.0.19041.35");
            scenario1Base.GetMetadata(MetadataKeys.RuntimePackAlwaysCopyLocal).Should().Be("true");
            scenario1Base.GetMetadata("RuntimePackNamePatterns").Should().Be("Microsoft.Windows.SDK.NET.Ref");
            scenario1Base.GetMetadata(MetadataKeys.RuntimePackRuntimeIdentifiers).Should().Be("any");
            scenario1Base.GetMetadata("IsWindowsOnly").Should().Be("true");
            scenario1Base.GetMetadata("Profile").Should().BeEmpty("base reference should not have Profile metadata");

            var scenario1Windows = scenario1Refs.First(r => r.ItemSpec == "Microsoft.Windows.SDK.NET.Ref.Windows");
            scenario1Windows.GetMetadata("Profile").Should().Be("Windows");

            // Scenario 2: UseWindowsSDKPreview
            taskInstances[1].KnownFrameworkReferences.Should().HaveCount(5);
            var scenario2Base = taskInstances[1].KnownFrameworkReferences.First(r => r.ItemSpec == "Microsoft.Windows.SDK.NET.Ref");
            scenario2Base.GetMetadata("DefaultRuntimeFrameworkVersion").Should().Be("10.0.22621-preview",
                "preview version should be constructed from TargetPlatformVersion");
            scenario2Base.GetMetadata(MetadataKeys.TargetFramework).Should().Be("net9.0-windows10.0.22621.0");

            // Scenario 3: WindowsSdkSupportedTargetPlatformVersions with single version
            taskInstances[2].KnownFrameworkReferences.Should().HaveCount(5);
            var scenario3Base = taskInstances[2].KnownFrameworkReferences.First(r => r.ItemSpec == "Microsoft.Windows.SDK.NET.Ref");
            scenario3Base.GetMetadata("DefaultRuntimeFrameworkVersion").Should().Be("10.0.19041.8");

            // Scenario 4: Multiple supported platform versions produce a 5-item group per SDK version (10 total).
            // The task does not filter by TargetPlatformVersion; that filtering happens later in ProcessFrameworkReferences.
            taskInstances[3].KnownFrameworkReferences.Should().HaveCount(10,
                "each supported Windows SDK version produces its own 5-item profile group");
            var scenario4BaseVersions = taskInstances[3].KnownFrameworkReferences
                .Where(r => r.ItemSpec == "Microsoft.Windows.SDK.NET.Ref")
                .Select(r => r.GetMetadata("DefaultRuntimeFrameworkVersion"))
                .ToArray();
            scenario4BaseVersions.Should().BeEquivalentTo(new[] { "10.0.19041.8", "10.0.22621.15" });

            // Scenario 5: WindowsSdkPackageMinimumRevision bumps revision
            taskInstances[4].KnownFrameworkReferences.Should().HaveCount(5);
            var scenario5Base = taskInstances[4].KnownFrameworkReferences.First(r => r.ItemSpec == "Microsoft.Windows.SDK.NET.Ref");
            scenario5Base.GetMetadata("DefaultRuntimeFrameworkVersion").Should().Be("10.0.19041.10",
                "revision should be bumped from 5 to 10 by WindowsSdkPackageMinimumRevision");

            // Scenario 6: MinimumNETVersion filtering results in no items
            taskInstances[5].KnownFrameworkReferences.Should().BeEmpty(
                "TargetFrameworkVersion 7.0 is less than MinimumNETVersion 8.0, so no references should be produced");

            // Scenario 7: Duplicate of scenario 1 should produce identical results
            var scenario7Base = taskInstances[6].KnownFrameworkReferences.First(r => r.ItemSpec == "Microsoft.Windows.SDK.NET.Ref");
            scenario7Base.GetMetadata("DefaultRuntimeFrameworkVersion").Should().Be(
                scenario1Base.GetMetadata("DefaultRuntimeFrameworkVersion"),
                "duplicate scenario should produce identical version");

            // Scenario 8: Duplicate of scenario 2 should produce identical results
            var scenario8Base = taskInstances[7].KnownFrameworkReferences.First(r => r.ItemSpec == "Microsoft.Windows.SDK.NET.Ref");
            scenario8Base.GetMetadata("DefaultRuntimeFrameworkVersion").Should().Be(
                scenario2Base.GetMetadata("DefaultRuntimeFrameworkVersion"),
                "duplicate scenario should produce identical preview version");

            var expectedReferenceIdentities = Enumerable.Range(0, scenarioCount)
                .Select(i => GetReferenceIdentities(taskInstances[i]))
                .ToArray();

            for (int i = scenarioCount; i < concurrency; i++)
            {
                GetReferenceIdentities(taskInstances[i]).Should().Equal(expectedReferenceIdentities[i % scenarioCount],
                    $"repeated task {i} should match scenario {i % scenarioCount}");
            }
        }

        [Fact]
        public async Task ErrorCasesAreConcurrentlySafe()
        {
            var startGate = new ManualResetEventSlim(false);
            var tasks = new List<Task<bool>>();

            // Error case 1: Both WindowsSdkPackageVersion and WindowsSdkPackageMinimumRevision set
            var engine1 = new MockBuildEngine();
            var task1 = new CreateWindowsSdkKnownFrameworkReferences
            {
                BuildEngine = engine1,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                WindowsSdkPackageVersion = "10.0.19041.35",
                WindowsSdkPackageMinimumRevision = "10",
                TargetFrameworkVersion = "8.0",
                TargetPlatformVersion = "10.0.19041.0",
            };
            tasks.Add(Task.Run(() => { startGate.Wait(); return task1.Execute(); }));

            // Error case 2: Both UseWindowsSDKPreview and WindowsSdkPackageMinimumRevision set
            var engine2 = new MockBuildEngine();
            var task2 = new CreateWindowsSdkKnownFrameworkReferences
            {
                BuildEngine = engine2,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                UseWindowsSDKPreview = true,
                WindowsSdkPackageMinimumRevision = "10",
                TargetFrameworkVersion = "8.0",
                TargetPlatformVersion = "10.0.19041.0",
            };
            tasks.Add(Task.Run(() => { startGate.Wait(); return task2.Execute(); }));

            // Valid case for comparison
            var engine3 = new MockBuildEngine();
            var task3 = new CreateWindowsSdkKnownFrameworkReferences
            {
                BuildEngine = engine3,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                WindowsSdkPackageVersion = "10.0.19041.35",
                TargetFrameworkVersion = "8.0",
                TargetPlatformVersion = "10.0.19041.0",
            };
            tasks.Add(Task.Run(() => { startGate.Wait(); return task3.Execute(); }));

            startGate.Set();
            var results = await Task.WhenAll(tasks);

            results[0].Should().BeFalse("task 1 should fail due to conflicting properties");
            engine1.Errors.Should().ContainSingle();

            results[1].Should().BeFalse("task 2 should fail due to conflicting properties");
            engine2.Errors.Should().ContainSingle();

            results[2].Should().BeTrue("task 3 should succeed with valid inputs");
            engine3.Errors.Should().BeEmpty();
        }

        private static CreateWindowsSdkKnownFrameworkReferences CreateBasicTask(IBuildEngine buildEngine)
        {
            return new CreateWindowsSdkKnownFrameworkReferences
            {
                BuildEngine = buildEngine,
                WindowsSdkPackageVersion = "10.0.19041.21",
                TargetFrameworkVersion = "8.0",
                TargetPlatformVersion = "10.0.19041.0",
            };
        }

        private static CreateWindowsSdkKnownFrameworkReferences CloneTask(CreateWindowsSdkKnownFrameworkReferences task)
        {
            return new CreateWindowsSdkKnownFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                UseWindowsSDKPreview = task.UseWindowsSDKPreview,
                WindowsSdkPackageVersion = task.WindowsSdkPackageVersion,
                WindowsSdkPackageMinimumRevision = task.WindowsSdkPackageMinimumRevision,
                TargetFrameworkIdentifier = task.TargetFrameworkIdentifier,
                TargetFrameworkVersion = task.TargetFrameworkVersion,
                TargetPlatformIdentifier = task.TargetPlatformIdentifier,
                TargetPlatformVersion = task.TargetPlatformVersion,
                WindowsSdkSupportedTargetPlatformVersions = CloneTaskItems(task.WindowsSdkSupportedTargetPlatformVersions),
            };
        }

        private static ITaskItem[] CloneTaskItems(ITaskItem[] items)
        {
            return items?.Select(item => new MockTaskItem(
                item.ItemSpec,
                item.MetadataNames.Cast<string>().ToDictionary(name => name, name => item.GetMetadata(name)))).ToArray();
        }

        private static string[] GetReferenceIdentities(CreateWindowsSdkKnownFrameworkReferences task)
        {
            return task.KnownFrameworkReferences
                .Select(reference => string.Join(
                    "|",
                    reference.ItemSpec,
                    reference.GetMetadata(MetadataKeys.TargetFramework),
                    reference.GetMetadata(MetadataKeys.RuntimeFrameworkName),
                    reference.GetMetadata("DefaultRuntimeFrameworkVersion"),
                    reference.GetMetadata("LatestRuntimeFrameworkVersion"),
                    reference.GetMetadata("TargetingPackName"),
                    reference.GetMetadata("TargetingPackVersion"),
                    reference.GetMetadata(MetadataKeys.RuntimePackAlwaysCopyLocal),
                    reference.GetMetadata("RuntimePackNamePatterns"),
                    reference.GetMetadata(MetadataKeys.RuntimePackRuntimeIdentifiers),
                    reference.GetMetadata("IsWindowsOnly"),
                    reference.GetMetadata("Profile")))
                .OrderBy(identity => identity)
                .ToArray();
        }
    }
}
