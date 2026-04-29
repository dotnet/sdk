// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.NET.TestFramework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAGenerateSupportedTargetFrameworkAliasMultiThreading
    {
        [Fact]
        public void TaskEnvironment_getter_remains_null_because_alias_generation_is_path_independent()
        {
            var task = CreateBasicTask();

            task.TaskEnvironment.Should().BeNull(
                "GenerateSupportedTargetFrameworkAlias does not resolve paths or read project-directory-relative data");

            task.Execute().Should().BeTrue();

            task.TaskEnvironment.Should().BeNull(
                "the removed NETFRAMEWORK fallback TaskEnvironment should not be recreated for this path-independent task");
            GetOutputSignatures(task).Should().Equal(
                "net6.0|.NET 6.0",
                "net7.0|.NET 7.0",
                "net8.0|.NET 8.0");
        }

        [Fact]
        public void Two_instances_with_identical_inputs_produce_identical_outputs_without_TaskEnvironment()
        {
            var taskA = CreateBasicTask();
            var taskB = CreateBasicTask();

            taskA.Execute().Should().BeTrue();
            taskB.Execute().Should().BeTrue();

            GetOutputSignatures(taskA).Should().Equal(GetOutputSignatures(taskB));
            GetOutputSignatures(taskA).Should().Equal(
                "net6.0|.NET 6.0",
                "net7.0|.NET 7.0",
                "net8.0|.NET 8.0");
        }

        [Fact]
        public async Task Concurrent_threads_produce_exact_independent_results()
        {
            const int threadCount = 64;

            var testCases = CreateConcurrentTestCases();
            var expectedOutputs = new string[threadCount][];
            var observedOutputs = new string[]?[threadCount];
            var results = new bool[threadCount];
            var exceptions = new Exception?[threadCount];
            var cancellationToken = TestContext.Current.CancellationToken;

            for (int i = 0; i < threadCount; i++)
            {
                expectedOutputs[i] = GetExpectedOutput(testCases[i % testCases.Length], i);
            }

            using var ready = new System.Threading.CountdownEvent(threadCount);
            using var startGate = new System.Threading.ManualResetEventSlim(false);

            var tasks = new Task[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                int idx = i;
                tasks[i] = Task.Factory.StartNew(() =>
                {
                    try
                    {
                        var testCase = testCases[idx % testCases.Length];
                        var task = CreateTask(
                            testCase.TargetFrameworkMoniker,
                            testCase.TargetPlatformMoniker,
                            testCase.UseWpf,
                            testCase.UseWindowsForms,
                            CreateSupportedTargetFrameworkItems(testCase.Frameworks, idx));

                        ready.Signal();
                        if (!startGate.Wait(TimeSpan.FromSeconds(60), cancellationToken))
                        {
                            throw new TimeoutException($"Thread {idx} timed out waiting for the start gate.");
                        }

                        results[idx] = task.Execute();
                        observedOutputs[idx] = GetOutputSignatures(task);
                    }
                    catch (Exception ex)
                    {
                        exceptions[idx] = ex;
                    }
                }, cancellationToken, System.Threading.Tasks.TaskCreationOptions.LongRunning, System.Threading.Tasks.TaskScheduler.Default);
            }

            var allWorkersReady = ready.Wait(TimeSpan.FromSeconds(60), cancellationToken);
            startGate.Set();

            allWorkersReady.Should().BeTrue(
                "all worker tasks should reach the start gate before alias generation begins");
            await Task.WhenAll(tasks);

            for (int i = 0; i < threadCount; i++)
            {
                exceptions[i].Should().BeNull($"thread {i} should not throw");
                results[i].Should().BeTrue($"thread {i} should succeed");
                observedOutputs[i].Should().Equal(expectedOutputs[i],
                    $"thread {i} should produce its exact aliases and display names");
            }
        }

        [Fact]
        public void DisplayName_metadata_is_correctly_processed()
        {
            var task = CreateTask(
                ".NETCoreApp,Version=v8.0",
                targetPlatformMoniker: string.Empty,
                useWpf: false,
                useWindowsForms: false,
                new ITaskItem[]
                {
                    new MockTaskItem(".NETCoreApp,Version=v6.0", new Dictionary<string, string>
                    {
                        { MetadataKeys.DisplayName, ".NET 6.0 Custom" }
                    }),
                    new MockTaskItem(".NETCoreApp,Version=v7.0", new Dictionary<string, string>()),
                    new MockTaskItem(".NETCoreApp,Version=v8.0", new Dictionary<string, string>
                    {
                        { MetadataKeys.DisplayName, ".NET 8.0 LTS" }
                    })
                });

            task.Execute().Should().BeTrue();

            GetOutputSignatures(task).Should().Equal(
                "net6.0|.NET 6.0 Custom",
                "net7.0|net7.0",
                "net8.0|.NET 8.0 LTS");
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void Wpf_and_WindowsForms_apply_versionless_windows_suffix_for_netcoreapp5_plus(bool useWpf, bool useWindowsForms)
        {
            var task = CreateTask(
                ".NETCoreApp,Version=v8.0",
                targetPlatformMoniker: "Windows,Version=10.0",
                useWpf,
                useWindowsForms,
                new ITaskItem[]
                {
                    new MockTaskItem(".NETCoreApp,Version=v5.0", new Dictionary<string, string>()),
                    new MockTaskItem(".NETCoreApp,Version=v6.0", new Dictionary<string, string>()),
                    new MockTaskItem(".NETCoreApp,Version=v8.0", new Dictionary<string, string>())
                });

            task.Execute().Should().BeTrue();

            GetOutputSignatures(task).Should().Equal(
                "net5.0-windows|net5.0-windows",
                "net6.0-windows|net6.0-windows",
                "net8.0-windows|net8.0-windows");
        }

        [Fact]
        public void Only_matching_framework_families_are_included()
        {
            var task = CreateTask(
                ".NETCoreApp,Version=v8.0",
                targetPlatformMoniker: string.Empty,
                useWpf: false,
                useWindowsForms: false,
                new ITaskItem[]
                {
                    new MockTaskItem(".NETCoreApp,Version=v6.0", new Dictionary<string, string>()),
                    new MockTaskItem(".NETStandard,Version=v2.0", new Dictionary<string, string>()),
                    new MockTaskItem(".NETCoreApp,Version=v8.0", new Dictionary<string, string>())
                });

            task.Execute().Should().BeTrue();

            GetOutputSignatures(task).Should().Equal(
                "net6.0|net6.0",
                "net8.0|net8.0");
        }

        private static GenerateSupportedTargetFrameworkAlias CreateBasicTask()
        {
            return CreateTask(
                ".NETCoreApp,Version=v8.0",
                targetPlatformMoniker: string.Empty,
                useWpf: false,
                useWindowsForms: false,
                new ITaskItem[]
                {
                    new MockTaskItem(".NETCoreApp,Version=v6.0", new Dictionary<string, string>
                    {
                        { MetadataKeys.DisplayName, ".NET 6.0" }
                    }),
                    new MockTaskItem(".NETCoreApp,Version=v7.0", new Dictionary<string, string>
                    {
                        { MetadataKeys.DisplayName, ".NET 7.0" }
                    }),
                    new MockTaskItem(".NETCoreApp,Version=v8.0", new Dictionary<string, string>
                    {
                        { MetadataKeys.DisplayName, ".NET 8.0" }
                    })
                });
        }

        private static GenerateSupportedTargetFrameworkAlias CreateTask(
            string targetFrameworkMoniker,
            string targetPlatformMoniker,
            bool useWpf,
            bool useWindowsForms,
            ITaskItem[] supportedTargetFramework)
        {
            return new GenerateSupportedTargetFrameworkAlias
            {
                BuildEngine = new MockBuildEngine(),
                TargetFrameworkMoniker = targetFrameworkMoniker,
                TargetPlatformMoniker = targetPlatformMoniker,
                UseWpf = useWpf,
                UseWindowsForms = useWindowsForms,
                SupportedTargetFramework = supportedTargetFramework
            };
        }

        private static string[] GetOutputSignatures(GenerateSupportedTargetFrameworkAlias task)
        {
            return task.SupportedTargetFrameworkAlias
                .Select(alias => $"{alias.ItemSpec}|{alias.GetMetadata(MetadataKeys.DisplayName)}")
                .ToArray();
        }

        private static ConcurrentAliasTestCase[] CreateConcurrentTestCases()
        {
            return new[]
            {
                new ConcurrentAliasTestCase(
                    ".NETCoreApp,Version=v8.0",
                    "Windows,Version=10.0",
                    useWpf: false,
                    useWindowsForms: false,
                    new[]
                    {
                        Include(".NETCoreApp,Version=v6.0", "netcoreapp6", "net6.0-windows10.0"),
                        Exclude(".NETStandard,Version=v2.0", "netstandard2"),
                        Include(".NETCoreApp,Version=v8.0", "netcoreapp8", "net8.0-windows10.0")
                    }),
                new ConcurrentAliasTestCase(
                    ".NETCoreApp,Version=v9.0",
                    string.Empty,
                    useWpf: false,
                    useWindowsForms: false,
                    new[]
                    {
                        Include(".NETCoreApp,Version=v7.0", "netcoreapp7", "net7.0"),
                        Include(".NETCoreApp,Version=v8.0", "netcoreapp8", "net8.0"),
                        Include(".NETCoreApp,Version=v9.0", "netcoreapp9", "net9.0")
                    }),
                new ConcurrentAliasTestCase(
                    ".NETCoreApp,Version=v8.0",
                    "Windows,Version=10.0",
                    useWpf: true,
                    useWindowsForms: false,
                    new[]
                    {
                        Include(".NETCoreApp,Version=v6.0", "wpf6", "net6.0-windows"),
                        Include(".NETCoreApp,Version=v8.0", "wpf8", "net8.0-windows"),
                        Exclude(".NETFramework,Version=v4.8", "net48")
                    }),
                new ConcurrentAliasTestCase(
                    ".NETCoreApp,Version=v9.0",
                    string.Empty,
                    useWpf: false,
                    useWindowsForms: true,
                    new[]
                    {
                        Include(".NETCoreApp,Version=v7.0", "winforms7", "net7.0-windows"),
                        Include(".NETCoreApp,Version=v9.0", "winforms9", "net9.0-windows")
                    }),
                new ConcurrentAliasTestCase(
                    ".NETStandard,Version=v2.1",
                    "Windows,Version=10.0",
                    useWpf: false,
                    useWindowsForms: false,
                    new[]
                    {
                        Include(".NETStandard,Version=v2.0", "netstandard20", "netstandard2.0"),
                        Include(".NETStandard,Version=v2.1", "netstandard21", "netstandard2.1"),
                        Exclude(".NETCoreApp,Version=v8.0", "net8")
                    }),
                new ConcurrentAliasTestCase(
                    ".NETFramework,Version=v4.8.1",
                    string.Empty,
                    useWpf: false,
                    useWindowsForms: false,
                    new[]
                    {
                        Include(".NETFramework,Version=v4.7.1", "net471", "net471"),
                        Include(".NETFramework,Version=v4.8", "net48", "net48"),
                        Exclude(".NETCoreApp,Version=v8.0", "net8")
                    })
            };
        }

        private static FrameworkInput Include(string itemSpec, string displayName, string expectedAlias) =>
            new FrameworkInput(itemSpec, displayName, expectedAlias);

        private static FrameworkInput Exclude(string itemSpec, string displayName) =>
            new FrameworkInput(itemSpec, displayName, expectedAlias: null);

        private static ITaskItem[] CreateSupportedTargetFrameworkItems(FrameworkInput[] frameworks, int threadIndex)
        {
            return frameworks
                .Select(framework => new MockTaskItem(framework.ItemSpec, new Dictionary<string, string>
                {
                    { MetadataKeys.DisplayName, $"thread {threadIndex}: {framework.DisplayName}" }
                }))
                .ToArray<ITaskItem>();
        }

        private static string[] GetExpectedOutput(ConcurrentAliasTestCase testCase, int threadIndex)
        {
            return testCase.Frameworks
                .Where(framework => framework.ExpectedAlias != null)
                .Select(framework => $"{framework.ExpectedAlias!}|thread {threadIndex}: {framework.DisplayName}")
                .ToArray();
        }

        private sealed class ConcurrentAliasTestCase
        {
            public ConcurrentAliasTestCase(
                string targetFrameworkMoniker,
                string targetPlatformMoniker,
                bool useWpf,
                bool useWindowsForms,
                FrameworkInput[] frameworks)
            {
                TargetFrameworkMoniker = targetFrameworkMoniker;
                TargetPlatformMoniker = targetPlatformMoniker;
                UseWpf = useWpf;
                UseWindowsForms = useWindowsForms;
                Frameworks = frameworks;
            }

            public string TargetFrameworkMoniker { get; }
            public string TargetPlatformMoniker { get; }
            public bool UseWpf { get; }
            public bool UseWindowsForms { get; }
            public FrameworkInput[] Frameworks { get; }
        }

        private sealed class FrameworkInput
        {
            public FrameworkInput(string itemSpec, string displayName, string? expectedAlias)
            {
                ItemSpec = itemSpec;
                DisplayName = displayName;
                ExpectedAlias = expectedAlias;
            }

            public string ItemSpec { get; }
            public string DisplayName { get; }
            public string? ExpectedAlias { get; }
        }
    }
}
