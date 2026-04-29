// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Security;
using FluentAssertions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAFindItemsFromPackagesMultiThreading
    {
        private const int StressThreadCount = 64;
        private static readonly TimeSpan ConcurrentOperationTimeout = TimeSpan.FromSeconds(30);

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            typeof(FindItemsFromPackages)
                .GetCustomAttributes(typeof(MSBuildMultiThreadableTaskAttribute), inherit: false)
                .Should()
                .ContainSingle();
        }

        [Fact]
        public void MSBuildMultiThreadedBuildRunsTaskInProcessAndResolvesRelativeMetadataFromProjectDirectory()
        {
            const string targetName = "RunFindItems";
            string relativeItem = Path.Combine("relative", "ItemFromProject.txt");

            string testRoot = CreateScratchDirectory();
            try
            {
                string projectDirectory = Path.Combine(testRoot, "project");
                string cwdDirectory = Path.Combine(testRoot, "cwd");
                Directory.CreateDirectory(projectDirectory);
                Directory.CreateDirectory(cwdDirectory);
                Directory.CreateDirectory(Path.Combine(projectDirectory, "relative"));
                Directory.CreateDirectory(Path.Combine(cwdDirectory, "relative"));
                File.WriteAllText(Path.Combine(projectDirectory, relativeItem), "project item");
                File.WriteAllText(Path.Combine(cwdDirectory, relativeItem), "cwd item");

                string projectFile = Path.Combine(projectDirectory, "FindItemsFromPackages.proj");
                File.WriteAllText(projectFile, CreateFindItemsFromPackagesProject(targetName, relativeItem));

                var logger = new RecordingLogger();
                string originalDirectory = Directory.GetCurrentDirectory();
                BuildResult result;
                try
                {
                    Directory.SetCurrentDirectory(cwdDirectory);
                    result = BuildProject(projectFile, targetName, logger);
                }
                finally
                {
                    Directory.SetCurrentDirectory(originalDirectory);
                }

                result.OverallResult.Should().Be(BuildResultCode.Success, "build log: {0}", logger.FullLog);
                result.ResultsByTarget.ContainsKey(targetName).Should().BeTrue("build log: {0}", logger.FullLog);

                TargetResult targetResult = result.ResultsByTarget[targetName];
                targetResult.ResultCode.Should().Be(TargetResultCode.Success, "build log: {0}", logger.FullLog);

                targetResult.Items.Should().ContainSingle();
                ITaskItem outputItem = targetResult.Items.Single();
                outputItem.ItemSpec.Should().Be(relativeItem);
                outputItem.GetMetadata("CustomKey").Should().Be("CustomValue");
                outputItem.GetMetadata("ResolvedFullPath").Should().Be(Path.GetFullPath(Path.Combine(projectDirectory, relativeItem)));
                outputItem.GetMetadata("ResolvedFullPath").Should().NotBe(Path.GetFullPath(Path.Combine(cwdDirectory, relativeItem)));

                logger.FullLog.Should().NotContain("Launching task \"FindItemsFromPackages\"");
                logger.FullLog.Should().NotContain("external task host");
            }
            finally
            {
                DeleteScratchDirectory(testRoot);
            }
        }

        [Fact]
#pragma warning disable xUnit1031 // Test methods should not use blocking task operations
        public void ConcurrentExecutionWithStartGateProducesCorrectResults()
        {
            const int threadCount = StressThreadCount;
            const int itemsPerPackage = 10;

            // Create shared package set
            var packages = new[]
            {
                CreatePackageItem("PackageA", "1.0.0"),
                CreatePackageItem("PackageB", "2.0.0"),
                CreatePackageItem("PackageC", "3.0.0")
            };

            // Create different item sets for each thread
            var allItems = new ITaskItem[threadCount][];
            for (int i = 0; i < threadCount; i++)
            {
                var items = new List<ITaskItem>();

                // Items from packages
                for (int j = 0; j < itemsPerPackage; j++)
                {
                    items.Add(CreateItemWithPackage($"Item{i}_{j}_A", "PackageA", "1.0.0"));
                    items.Add(CreateItemWithPackage($"Item{i}_{j}_B", "PackageB", "2.0.0"));
                }

                // Items NOT from packages (should be filtered out)
                items.Add(CreateItemWithPackage($"Item{i}_Other", "OtherPackage", "9.9.9"));
                items.Add(CreateItemWithoutPackage($"Item{i}_NoPackage"));

                allItems[i] = items.ToArray();
            }

            var tasks = new FindItemsFromPackages[threadCount];
            var engines = new MockBuildEngine[threadCount];
            var results = new ConcurrentBag<ITaskItem[]>();

            // Create tasks
            for (int i = 0; i < threadCount; i++)
            {
                tasks[i] = new FindItemsFromPackages
                {
                    BuildEngine = engines[i] = new MockBuildEngine(),
                    Items = allItems[i],
                    Packages = packages
                };
            }

            RunConcurrently(threadCount, taskIndex =>
            {
                bool success = tasks[taskIndex].Execute();
                success.Should().BeTrue($"Task {taskIndex} should succeed");
                results.Add(tasks[taskIndex].ItemsFromPackages);
            });

            // Verify each thread got correct results
            results.Should().HaveCount(threadCount);

            foreach (var result in results)
            {
                // Each thread should have exactly 20 items (10 from PackageA + 10 from PackageB)
                result.Should().HaveCount(itemsPerPackage * 2,
                    "Only items from PackageA and PackageB should be included");

                // Verify all results have correct package metadata
                foreach (var item in result)
                {
                    var packageId = item.GetMetadata(MetadataKeys.NuGetPackageId);
                    var packageVersion = item.GetMetadata(MetadataKeys.NuGetPackageVersion);

                    packageId.Should().BeOneOf("PackageA", "PackageB",
                        "Result items should only be from the specified packages");

                    if (packageId == "PackageA")
                        packageVersion.Should().Be("1.0.0");
                    else if (packageId == "PackageB")
                        packageVersion.Should().Be("2.0.0");
                }
            }
        }
#pragma warning restore xUnit1031

        [Fact]
#pragma warning disable xUnit1031 // Test methods should not use blocking task operations
        public void ConcurrentExecutionWithDifferentPackageSetsProducesIndependentResults()
        {
            const int threadCount = 3;

            var packageSets = new[]
            {
                new[] { CreatePackageItem("PackageA", "1.0.0") },
                new[] { CreatePackageItem("PackageB", "2.0.0") },
                new[] { CreatePackageItem("PackageC", "3.0.0") }
            };

            // Shared items that reference all three packages
            var sharedItems = new[]
            {
                CreateItemWithPackage("ItemA", "PackageA", "1.0.0"),
                CreateItemWithPackage("ItemB", "PackageB", "2.0.0"),
                CreateItemWithPackage("ItemC", "PackageC", "3.0.0")
            };

            var tasks = new FindItemsFromPackages[threadCount];
            var engines = new MockBuildEngine[threadCount];
            var results = new ConcurrentDictionary<int, ITaskItem[]>();

            for (int i = 0; i < threadCount; i++)
            {
                tasks[i] = new FindItemsFromPackages
                {
                    BuildEngine = engines[i] = new MockBuildEngine(),
                    Items = sharedItems,
                    Packages = packageSets[i]
                };
            }

            RunConcurrently(threadCount, taskIndex =>
            {
                bool success = tasks[taskIndex].Execute();
                success.Should().BeTrue();
                results[taskIndex] = tasks[taskIndex].ItemsFromPackages;
            });

            results.Should().HaveCount(threadCount);

            // Thread 0 should find only ItemA
            results[0].Should().HaveCount(1);
            results[0][0].ItemSpec.Should().Be("ItemA");

            // Thread 1 should find only ItemB
            results[1].Should().HaveCount(1);
            results[1][0].ItemSpec.Should().Be("ItemB");

            // Thread 2 should find only ItemC
            results[2].Should().HaveCount(1);
            results[2][0].ItemSpec.Should().Be("ItemC");
        }
#pragma warning restore xUnit1031

        [Fact]
#pragma warning disable xUnit1031 // Test methods should not use blocking task operations
        public void ConcurrentExecutionHandlesEmptyInputs()
        {
            const int threadCount = 4;

            var tasks = new FindItemsFromPackages[threadCount];
            var engines = new MockBuildEngine[threadCount];

            tasks[0] = new FindItemsFromPackages
            {
                BuildEngine = engines[0] = new MockBuildEngine(),
                Items = Array.Empty<ITaskItem>(),
                Packages = new[] { CreatePackageItem("PackageA", "1.0.0") }
            };

            tasks[1] = new FindItemsFromPackages
            {
                BuildEngine = engines[1] = new MockBuildEngine(),
                Items = new[] { CreateItemWithPackage("Item1", "PackageA", "1.0.0") },
                Packages = Array.Empty<ITaskItem>()
            };

            tasks[2] = new FindItemsFromPackages
            {
                BuildEngine = engines[2] = new MockBuildEngine(),
                Items = Array.Empty<ITaskItem>(),
                Packages = Array.Empty<ITaskItem>()
            };

            tasks[3] = new FindItemsFromPackages
            {
                BuildEngine = engines[3] = new MockBuildEngine(),
                Items = new[] { CreateItemWithoutPackage("ItemWithoutPackage") },
                Packages = new[] { CreatePackageItem("PackageA", "1.0.0") }
            };

            var results = new ConcurrentBag<ITaskItem[]>();

            RunConcurrently(threadCount, taskIndex =>
            {
                bool success = tasks[taskIndex].Execute();
                success.Should().BeTrue();
                results.Add(tasks[taskIndex].ItemsFromPackages);
            });

            results.Should().HaveCount(threadCount);
            results.Should().OnlyContain(r => r.Length == 0,
                "Empty inputs should produce empty results");
        }
#pragma warning restore xUnit1031

        [Fact]
#pragma warning disable xUnit1031 // Test methods should not use blocking task operations
        public void ConcurrentExecutionPreservesItemMetadata()
        {
            const int threadCount = StressThreadCount;

            var packages = new[] { CreatePackageItem("PackageA", "1.0.0") };

            var items = new[]
            {
                CreateItemWithPackageAndMetadata("relative/Item1.txt", "PackageA", "1.0.0", "CustomKey", "CustomValue1"),
                CreateItemWithPackageAndMetadata("nested/Item2.txt", "PackageA", "1.0.0", "CustomKey", "CustomValue2")
            };

            var tasks = new FindItemsFromPackages[threadCount];
            var engines = new MockBuildEngine[threadCount];
            var results = new ConcurrentBag<ITaskItem[]>();

            for (int i = 0; i < threadCount; i++)
            {
                tasks[i] = new FindItemsFromPackages
                {
                    BuildEngine = engines[i] = new MockBuildEngine(),
                    Items = items,
                    Packages = packages
                };
            }

            RunConcurrently(threadCount, taskIndex =>
            {
                bool success = tasks[taskIndex].Execute();
                success.Should().BeTrue();
                results.Add(tasks[taskIndex].ItemsFromPackages);
            });

            results.Should().HaveCount(threadCount);

            foreach (var result in results)
            {
                result.Should().HaveCount(2);
                result[0].Should().BeSameAs(items[0]);
                result[1].Should().BeSameAs(items[1]);
                result[0].ItemSpec.Should().Be("relative/Item1.txt");
                result[1].ItemSpec.Should().Be("nested/Item2.txt");
                result[0].GetMetadata("CustomKey").Should().Be("CustomValue1");
                result[1].GetMetadata("CustomKey").Should().Be("CustomValue2");
            }
        }
#pragma warning restore xUnit1031

        [Fact]
#pragma warning disable xUnit1031 // Test methods should not use blocking task operations
        public void ConcurrentExecutionWithVersionMismatchFiltersCorrectly()
        {
            const int threadCount = StressThreadCount;

            var packages = new[]
            {
                CreatePackageItem("PackageA", "1.0.0"),
                CreatePackageItem("PackageB", "2.0.0")
            };

            var items = new[]
            {
                CreateItemWithPackage("Item1", "PackageA", "1.0.0"),      // Match
                CreateItemWithPackage("Item2", "PackageA", "1.1.0"),      // No match - version differs
                CreateItemWithPackage("Item3", "PackageB", "2.0.0"),      // Match
                CreateItemWithPackage("Item4", "PackageC", "3.0.0"),      // No match - different package
            };

            var tasks = new FindItemsFromPackages[threadCount];
            var engines = new MockBuildEngine[threadCount];
            var results = new ConcurrentBag<ITaskItem[]>();

            for (int i = 0; i < threadCount; i++)
            {
                tasks[i] = new FindItemsFromPackages
                {
                    BuildEngine = engines[i] = new MockBuildEngine(),
                    Items = items,
                    Packages = packages
                };
            }

            RunConcurrently(threadCount, taskIndex =>
            {
                bool success = tasks[taskIndex].Execute();
                success.Should().BeTrue();
                results.Add(tasks[taskIndex].ItemsFromPackages);
            });

            results.Should().HaveCount(threadCount);

            foreach (var result in results)
            {
                result.Should().HaveCount(2, "Only exact package ID and version matches should be included");
                result.Should().Contain(i => i.ItemSpec == "Item1");
                result.Should().Contain(i => i.ItemSpec == "Item3");
            }
        }
#pragma warning restore xUnit1031

#pragma warning disable xUnit1031 // Test helper intentionally blocks with a timeout to coordinate workers
        private static void RunConcurrently(int threadCount, Action<int> action)
        {
            ThreadPool.GetMinThreads(out int previousMinWorkerThreads, out int previousMinCompletionPortThreads);
            ThreadPool.GetMaxThreads(out int maxWorkerThreads, out _);
            ThreadPool.SetMinThreads(
                Math.Min(Math.Max(previousMinWorkerThreads, threadCount), maxWorkerThreads),
                previousMinCompletionPortThreads).Should().BeTrue();

            using var startGate = new ManualResetEventSlim(false);
            using var readyGate = new CountdownEvent(threadCount);
            var exceptions = new ConcurrentQueue<Exception>();
            var workerTasks = new Task[threadCount];

            try
            {
                for (int i = 0; i < threadCount; i++)
                {
                    int taskIndex = i;
                    workerTasks[i] = Task.Run(() =>
                    {
                        try
                        {
                            readyGate.Signal();
                            startGate.Wait(ConcurrentOperationTimeout).Should().BeTrue(
                                "worker {0} should receive the start signal",
                                taskIndex);

                            action(taskIndex);
                        }
                        catch (Exception ex)
                        {
                            exceptions.Enqueue(ex);
                        }
                    });
                }

                bool allWorkersReady = readyGate.Wait(ConcurrentOperationTimeout);
                startGate.Set();
                bool allWorkersCompleted = Task.WaitAll(workerTasks, ConcurrentOperationTimeout);

                allWorkersReady.Should().BeTrue("all workers should reach the start gate");
                allWorkersCompleted.Should().BeTrue("all workers should complete");
                exceptions.Should().BeEmpty("all concurrent executions should succeed");
            }
            finally
            {
                startGate.Set();
                ThreadPool.SetMinThreads(previousMinWorkerThreads, previousMinCompletionPortThreads);
            }
        }
#pragma warning restore xUnit1031

        private static ITaskItem CreatePackageItem(string packageId, string version)
        {
            var metadata = new Dictionary<string, string>
            {
                [MetadataKeys.NuGetPackageId] = packageId,
                [MetadataKeys.NuGetPackageVersion] = version
            };
            return new MockTaskItem($"{packageId}.{version}", metadata);
        }

        private static ITaskItem CreateItemWithPackage(string itemSpec, string packageId, string version)
        {
            var metadata = new Dictionary<string, string>
            {
                [MetadataKeys.NuGetPackageId] = packageId,
                [MetadataKeys.NuGetPackageVersion] = version
            };
            return new MockTaskItem(itemSpec, metadata);
        }

        private static ITaskItem CreateItemWithoutPackage(string itemSpec)
        {
            return new MockTaskItem(itemSpec, new Dictionary<string, string>());
        }

        private static ITaskItem CreateItemWithPackageAndMetadata(string itemSpec, string packageId, string version, string metadataKey, string metadataValue)
        {
            var metadata = new Dictionary<string, string>
            {
                [MetadataKeys.NuGetPackageId] = packageId,
                [MetadataKeys.NuGetPackageVersion] = version,
                [metadataKey] = metadataValue
            };
            return new MockTaskItem(itemSpec, metadata);
        }

        private static BuildResult BuildProject(string projectFile, string targetName, RecordingLogger logger)
        {
            var buildParameters = new BuildParameters
            {
                MultiThreaded = true,
                Loggers = new ILogger[] { logger },
                DisableInProcNode = false,
                EnableNodeReuse = false
            };

            var buildRequestData = new BuildRequestData(
                projectFile,
                new Dictionary<string, string?>(),
                null,
                new[] { targetName },
                null);

            return new BuildManager().Build(buildParameters, buildRequestData);
        }

        private static string CreateFindItemsFromPackagesProject(string targetName, string relativeItem)
        {
            string taskAssembly = SecurityElement.Escape(typeof(FindItemsFromPackages).Assembly.Location);

            return $@"
<Project>
  <UsingTask TaskName=""FindItemsFromPackages"" AssemblyFile=""{taskAssembly}"" />

  <ItemGroup>
    <Package Include=""PackageA"">
      <NuGetPackageId>PackageA</NuGetPackageId>
      <NuGetPackageVersion>1.0.0</NuGetPackageVersion>
    </Package>
    <CandidateItem Include=""{relativeItem}"">
      <NuGetPackageId>PackageA</NuGetPackageId>
      <NuGetPackageVersion>1.0.0</NuGetPackageVersion>
      <CustomKey>CustomValue</CustomKey>
    </CandidateItem>
    <CandidateItem Include=""not-from-package.txt"">
      <NuGetPackageId>OtherPackage</NuGetPackageId>
      <NuGetPackageVersion>1.0.0</NuGetPackageVersion>
    </CandidateItem>
  </ItemGroup>

  <Target Name=""{targetName}"" Returns=""@(ItemsFromPackagesWithFullPath)"">
    <FindItemsFromPackages Items=""@(CandidateItem)"" Packages=""@(Package)"">
      <Output TaskParameter=""ItemsFromPackages"" ItemName=""ItemsFromPackages"" />
    </FindItemsFromPackages>
    <ItemGroup>
      <ItemsFromPackagesWithFullPath Include=""@(ItemsFromPackages)"">
        <ResolvedFullPath>%(ItemsFromPackages.FullPath)</ResolvedFullPath>
      </ItemsFromPackagesWithFullPath>
    </ItemGroup>
  </Target>
</Project>";
        }

        private static string CreateScratchDirectory()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "fifp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteScratchDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }

        private sealed class RecordingLogger : ILogger
        {
            private readonly ConcurrentQueue<string> _messages = new();

            public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Diagnostic;

            public string? Parameters { get; set; }

            public string FullLog => string.Join(Environment.NewLine, _messages);

            public void Initialize(IEventSource eventSource)
            {
                eventSource.ErrorRaised += (_, e) => Record(e.Message);
                eventSource.WarningRaised += (_, e) => Record(e.Message);
                eventSource.MessageRaised += (_, e) => Record(e.Message);
            }

            public void Shutdown()
            {
            }

            private void Record(string? message)
            {
                if (!string.IsNullOrEmpty(message))
                {
                    _messages.Enqueue(message);
                }
            }
        }
    }
}
