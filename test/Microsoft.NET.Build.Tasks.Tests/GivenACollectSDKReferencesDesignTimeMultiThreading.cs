// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenACollectSDKReferencesDesignTimeMultiThreading
    {
        [Fact]
        public async Task ConcurrentExecutionProducesCorrectResults()
        {
            const int concurrency = 8;
            var taskInstances = new CollectSDKReferencesDesignTime[concurrency];
            var executeTasks = new Task<bool>[concurrency];
            var startGate = new ManualResetEventSlim(false);

            // Create unique inputs for each task instance
            for (int i = 0; i < concurrency; i++)
            {
                var engine = new MockBuildEngine();
                var task = CreateTask(i, engine);
                task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest();
                taskInstances[i] = task;
            }

            // Start all tasks using explicit Task.Run with a start gate
            for (int i = 0; i < concurrency; i++)
            {
                int localI = i;
                var t = taskInstances[localI];
                executeTasks[localI] = Task.Run(() =>
                {
                    startGate.Wait();
                    return t.Execute();
                });
            }

            // Release all tasks simultaneously
            startGate.Set();
            await Task.WhenAll(executeTasks);
            startGate.Dispose();

            // Verify all tasks succeeded
            for (int i = 0; i < concurrency; i++)
            {
                (await executeTasks[i]).Should().BeTrue($"task {i} should succeed");
            }

            // Verify each task produced the expected output based on its unique inputs
            for (int i = 0; i < concurrency; i++)
            {
                var task = taskInstances[i];
                int expectedCount = 2; // 1 from SdkReferences + 1 implicit package based on IsImplicitlyDefined

                task.SDKReferencesDesignTime.Should().NotBeNull($"task {i} should produce output");
                task.SDKReferencesDesignTime.Length.Should().Be(expectedCount, $"task {i} should aggregate SDK refs and implicit packages");

                // Verify the SDK reference is present
                task.SDKReferencesDesignTime.Should().Contain(
                    item => item.ItemSpec == $"TestSDK{i}",
                    $"task {i} should include its SDK reference");

                // Verify the implicit package reference is present with correct metadata
                var implicitPackage = task.SDKReferencesDesignTime.FirstOrDefault(item => item.ItemSpec == $"ImplicitPackage{i}");
                implicitPackage.Should().NotBeNull($"task {i} should include implicit package");
                implicitPackage.GetMetadata(MetadataKeys.SDKPackageItemSpec).Should().Be(string.Empty);
                implicitPackage.GetMetadata(MetadataKeys.Name).Should().Be($"ImplicitPackage{i}");
                implicitPackage.GetMetadata(MetadataKeys.IsImplicitlyDefined).Should().Be("True");
                implicitPackage.GetMetadata(MetadataKeys.Version).Should().Be($"1.0.{i}");
            }
        }

        [Fact]
        public void TaskProducesSameOutputsWithAndWithoutExplicitTaskEnvironment()
        {
            // Run without explicitly setting TaskEnvironment (uses default)
            var engine1 = new MockBuildEngine();
            var task1 = CreateTask(1, engine1);
            task1.Execute().Should().BeTrue();

            // Run with explicitly set TaskEnvironment
            var engine2 = new MockBuildEngine();
            var task2 = CreateTask(1, engine2);
            task2.TaskEnvironment = TaskEnvironmentHelper.CreateForTest();
            task2.Execute().Should().BeTrue();

            // Outputs should be identical
            task1.SDKReferencesDesignTime.Length.Should().Be(task2.SDKReferencesDesignTime.Length);
            task1.SDKReferencesDesignTime.Select(r => r.ItemSpec).Should()
                .BeEquivalentTo(task2.SDKReferencesDesignTime.Select(r => r.ItemSpec));
        }

        [Fact]
        public void ImplicitPackagesAreIdentifiedByMetadata()
        {
            var engine = new MockBuildEngine();
            var task = new CollectSDKReferencesDesignTime
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                SdkReferences = new[]
                {
                    new MockTaskItem("ExistingSDK", new Dictionary<string, string>())
                },
                PackageReferences = new[]
                {
                    // This package is marked as implicit via metadata
                    new MockTaskItem("PackageA", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "True" },
                        { MetadataKeys.Version, "2.0.0" }
                    }),
                    // This package is not implicit
                    new MockTaskItem("PackageB", new Dictionary<string, string>
                    {
                        { MetadataKeys.Version, "3.0.0" }
                    })
                },
                DefaultImplicitPackages = ""
            };

            task.Execute().Should().BeTrue();

            task.SDKReferencesDesignTime.Length.Should().Be(2, "should include 1 SDK ref + 1 implicit package");
            
            var implicitPkg = task.SDKReferencesDesignTime.FirstOrDefault(i => i.ItemSpec == "PackageA");
            implicitPkg.Should().NotBeNull("PackageA should be included as implicit");
            implicitPkg.GetMetadata(MetadataKeys.IsImplicitlyDefined).Should().Be("True");
            implicitPkg.GetMetadata(MetadataKeys.Version).Should().Be("2.0.0");
            
            task.SDKReferencesDesignTime.Should().NotContain(i => i.ItemSpec == "PackageB",
                "PackageB should not be included as it's not implicit");
        }

        [Fact]
        public void ImplicitPackagesAreIdentifiedByDefaultImplicitPackagesList()
        {
            var engine = new MockBuildEngine();
            var task = new CollectSDKReferencesDesignTime
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                SdkReferences = new[]
                {
                    new MockTaskItem("ExistingSDK", new Dictionary<string, string>())
                },
                PackageReferences = new[]
                {
                    // This package is in the DefaultImplicitPackages list (no metadata)
                    new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>
                    {
                        { MetadataKeys.Version, "5.0.0" }
                    }),
                    // This package is not implicit
                    new MockTaskItem("Newtonsoft.Json", new Dictionary<string, string>
                    {
                        { MetadataKeys.Version, "13.0.1" }
                    })
                },
                DefaultImplicitPackages = "Microsoft.NETCore.App;Microsoft.AspNetCore.App"
            };

            task.Execute().Should().BeTrue();

            task.SDKReferencesDesignTime.Length.Should().Be(2, "should include 1 SDK ref + 1 implicit package");
            
            var implicitPkg = task.SDKReferencesDesignTime.FirstOrDefault(i => i.ItemSpec == "Microsoft.NETCore.App");
            implicitPkg.Should().NotBeNull("Microsoft.NETCore.App should be included as implicit");
            implicitPkg.GetMetadata(MetadataKeys.IsImplicitlyDefined).Should().Be("True");
            implicitPkg.GetMetadata(MetadataKeys.Name).Should().Be("Microsoft.NETCore.App");
            implicitPkg.GetMetadata(MetadataKeys.Version).Should().Be("5.0.0");
            
            task.SDKReferencesDesignTime.Should().NotContain(i => i.ItemSpec == "Newtonsoft.Json",
                "Newtonsoft.Json should not be included as it's not implicit");
        }

        [Fact]
        public void EmptyDefaultImplicitPackagesHandledCorrectly()
        {
            var engine = new MockBuildEngine();
            var task = new CollectSDKReferencesDesignTime
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                SdkReferences = new[]
                {
                    new MockTaskItem("SDK1", new Dictionary<string, string>())
                },
                PackageReferences = new[]
                {
                    new MockTaskItem("Package1", new Dictionary<string, string>
                    {
                        { MetadataKeys.Version, "1.0.0" }
                    })
                },
                DefaultImplicitPackages = ""
            };

            task.Execute().Should().BeTrue();

            task.SDKReferencesDesignTime.Length.Should().Be(1, "should only include the SDK reference");
            task.SDKReferencesDesignTime[0].ItemSpec.Should().Be("SDK1");
        }

        [Fact]
        public void MetadataOverridesDefaultImplicitPackagesList()
        {
            var engine = new MockBuildEngine();
            var task = new CollectSDKReferencesDesignTime
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                SdkReferences = new ITaskItem[0],
                PackageReferences = new[]
                {
                    // This package has explicit metadata saying it's not implicit
                    new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "False" },
                        { MetadataKeys.Version, "5.0.0" }
                    })
                },
                // Even though it's in the default list, metadata takes precedence
                DefaultImplicitPackages = "Microsoft.NETCore.App"
            };

            task.Execute().Should().BeTrue();

            task.SDKReferencesDesignTime.Should().BeEmpty(
                "package with IsImplicitlyDefined=False should not be included even if in DefaultImplicitPackages");
        }

        [Fact]
        public void CaseInsensitivePackageNameMatching()
        {
            var engine = new MockBuildEngine();
            var task = new CollectSDKReferencesDesignTime
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                SdkReferences = new ITaskItem[0],
                PackageReferences = new[]
                {
                    // Package name has different casing than the DefaultImplicitPackages list
                    new MockTaskItem("microsoft.netcore.app", new Dictionary<string, string>
                    {
                        { MetadataKeys.Version, "5.0.0" }
                    })
                },
                DefaultImplicitPackages = "Microsoft.NETCore.App"
            };

            task.Execute().Should().BeTrue();

            task.SDKReferencesDesignTime.Length.Should().Be(1,
                "case-insensitive matching should identify the package as implicit");
            task.SDKReferencesDesignTime[0].ItemSpec.Should().Be("microsoft.netcore.app");
        }

        [Fact]
        public void MultipleImplicitPackagesFromBothSourcesAreIncluded()
        {
            var engine = new MockBuildEngine();
            var task = new CollectSDKReferencesDesignTime
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                SdkReferences = new[]
                {
                    new MockTaskItem("SDK1", new Dictionary<string, string>()),
                    new MockTaskItem("SDK2", new Dictionary<string, string>())
                },
                PackageReferences = new[]
                {
                    // From DefaultImplicitPackages
                    new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>
                    {
                        { MetadataKeys.Version, "5.0.0" }
                    }),
                    // From metadata
                    new MockTaskItem("CustomImplicit", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "True" },
                        { MetadataKeys.Version, "1.0.0" }
                    }),
                    // Not implicit
                    new MockTaskItem("ExplicitPackage", new Dictionary<string, string>
                    {
                        { MetadataKeys.Version, "2.0.0" }
                    })
                },
                DefaultImplicitPackages = "Microsoft.NETCore.App"
            };

            task.Execute().Should().BeTrue();

            task.SDKReferencesDesignTime.Length.Should().Be(4,
                "should include 2 SDK refs + 2 implicit packages");
            
            task.SDKReferencesDesignTime.Select(i => i.ItemSpec).Should().Contain(new[]
            {
                "SDK1", "SDK2", "Microsoft.NETCore.App", "CustomImplicit"
            });
        }

        private static CollectSDKReferencesDesignTime CreateTask(int instanceId, IBuildEngine buildEngine)
        {
            return new CollectSDKReferencesDesignTime
            {
                BuildEngine = buildEngine,
                SdkReferences = new[]
                {
                    new MockTaskItem($"TestSDK{instanceId}", new Dictionary<string, string>())
                },
                PackageReferences = new[]
                {
                    new MockTaskItem($"ImplicitPackage{instanceId}", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "True" },
                        { MetadataKeys.Version, $"1.0.{instanceId}" }
                    }),
                    new MockTaskItem($"ExplicitPackage{instanceId}", new Dictionary<string, string>
                    {
                        { MetadataKeys.Version, $"2.0.{instanceId}" }
                    })
                },
                DefaultImplicitPackages = $"SomeOtherPackage{instanceId}"
            };
        }
    }
}
