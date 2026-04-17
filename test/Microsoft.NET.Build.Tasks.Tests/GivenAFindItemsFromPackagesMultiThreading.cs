// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAFindItemsFromPackagesMultiThreading
    {
        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            typeof(FindItemsFromPackages).GetInterfaces().Should().Contain(typeof(IMultiThreadableTask));
        }

        [Fact]
        public void TaskEnvironmentPropertyExistsAndCanBeSet()
        {
            var task = new FindItemsFromPackages();
            var engine = new MockBuildEngine();
            task.BuildEngine = engine;

            task.Should().BeAssignableTo<IMultiThreadableTask>();
            
            var multiThreadable = task as IMultiThreadableTask;
            multiThreadable.Should().NotBeNull();
            
            var property = typeof(FindItemsFromPackages).GetProperty(nameof(IMultiThreadableTask.TaskEnvironment));
            property.Should().NotBeNull();
            property.CanRead.Should().BeTrue();
            property.CanWrite.Should().BeTrue();
        }

        [Fact]
#pragma warning disable xUnit1031 // Test methods should not use blocking task operations
        public void ConcurrentExecutionWithStartGateProducesCorrectResults()
        {
            const int threadCount = 4;
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
            var exceptions = new ConcurrentBag<Exception>();

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

            // Start-gate pattern to ensure maximum concurrency
            var startGate = new ManualResetEventSlim(false);
            var taskList = new List<Task>();

            for (int i = 0; i < threadCount; i++)
            {
                int taskIndex = i;
                taskList.Add(Task.Run(() =>
                {
                    // Wait for start signal
                    startGate.Wait();
                    
                    try
                    {
                        bool success = tasks[taskIndex].Execute();
                        success.Should().BeTrue($"Task {taskIndex} should succeed");
                        results.Add(tasks[taskIndex].ItemsFromPackages);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }));
            }

            // Release all threads simultaneously
            startGate.Set();

            // Wait for all tasks to complete
            Task.WaitAll(taskList.ToArray());

            // Verify no exceptions occurred
            exceptions.Should().BeEmpty("All concurrent executions should succeed without exceptions");

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

            var startGate = new ManualResetEventSlim(false);
            var taskList = new List<Task>();

            for (int i = 0; i < threadCount; i++)
            {
                int taskIndex = i;
                taskList.Add(Task.Run(() =>
                {
                    startGate.Wait();
                    
                    bool success = tasks[taskIndex].Execute();
                    success.Should().BeTrue();
                    results[taskIndex] = tasks[taskIndex].ItemsFromPackages;
                }));
            }

            startGate.Set();
            Task.WaitAll(taskList.ToArray());

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
            const int threadCount = 2;

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

            var startGate = new ManualResetEventSlim(false);
            var taskList = new List<Task>();
            var results = new ConcurrentBag<ITaskItem[]>();

            for (int i = 0; i < threadCount; i++)
            {
                int taskIndex = i;
                taskList.Add(Task.Run(() =>
                {
                    startGate.Wait();
                    
                    bool success = tasks[taskIndex].Execute();
                    success.Should().BeTrue();
                    results.Add(tasks[taskIndex].ItemsFromPackages);
                }));
            }

            startGate.Set();
            Task.WaitAll(taskList.ToArray());

            results.Should().HaveCount(threadCount);
            results.Should().OnlyContain(r => r.Length == 0, 
                "Empty inputs should produce empty results");
        }
#pragma warning restore xUnit1031

        [Fact]
#pragma warning disable xUnit1031 // Test methods should not use blocking task operations
        public void ConcurrentExecutionPreservesItemMetadata()
        {
            const int threadCount = 2;

            var packages = new[] { CreatePackageItem("PackageA", "1.0.0") };
            
            var items = new[]
            {
                CreateItemWithPackageAndMetadata("Item1", "PackageA", "1.0.0", "CustomKey", "CustomValue1"),
                CreateItemWithPackageAndMetadata("Item2", "PackageA", "1.0.0", "CustomKey", "CustomValue2")
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

            var startGate = new ManualResetEventSlim(false);
            var taskList = new List<Task>();

            for (int i = 0; i < threadCount; i++)
            {
                int taskIndex = i;
                taskList.Add(Task.Run(() =>
                {
                    startGate.Wait();
                    
                    bool success = tasks[taskIndex].Execute();
                    success.Should().BeTrue();
                    results.Add(tasks[taskIndex].ItemsFromPackages);
                }));
            }

            startGate.Set();
            Task.WaitAll(taskList.ToArray());

            results.Should().HaveCount(threadCount);
            
            foreach (var result in results)
            {
                result.Should().HaveCount(2);
                result[0].GetMetadata("CustomKey").Should().Be("CustomValue1");
                result[1].GetMetadata("CustomKey").Should().Be("CustomValue2");
            }
        }
#pragma warning restore xUnit1031

        [Fact]
#pragma warning disable xUnit1031 // Test methods should not use blocking task operations
        public void ConcurrentExecutionWithVersionMismatchFiltersCorrectly()
        {
            const int threadCount = 2;

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

            var startGate = new ManualResetEventSlim(false);
            var taskList = new List<Task>();

            for (int i = 0; i < threadCount; i++)
            {
                int taskIndex = i;
                taskList.Add(Task.Run(() =>
                {
                    startGate.Wait();
                    
                    bool success = tasks[taskIndex].Execute();
                    success.Should().BeTrue();
                    results.Add(tasks[taskIndex].ItemsFromPackages);
                }));
            }

            startGate.Set();
            Task.WaitAll(taskList.ToArray());

            results.Should().HaveCount(threadCount);
            
            foreach (var result in results)
            {
                result.Should().HaveCount(2, "Only exact package ID and version matches should be included");
                result.Should().Contain(i => i.ItemSpec == "Item1");
                result.Should().Contain(i => i.ItemSpec == "Item3");
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
    }
}
