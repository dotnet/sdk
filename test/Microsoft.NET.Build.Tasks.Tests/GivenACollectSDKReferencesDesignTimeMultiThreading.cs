// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    [Collection(nameof(CurrentDirectoryMutatingTestCollection))]
    public class GivenACollectSDKReferencesDesignTimeMultiThreading
    {
        [Fact]
        public async Task ConcurrentExecutionWithInjectedTaskEnvironmentsProducesCorrectResults()
        {
            const int concurrency = 64;
            var originalCurrentDirectory = Directory.GetCurrentDirectory();
            var sharedSdkReferences = new ITaskItem[]
            {
                new MockTaskItem(@"relative\SharedSDK", new Dictionary<string, string>
                {
                    { "RelativeMetadata", @"metadata\value" }
                })
            };
            var sharedPackageReferences = new ITaskItem[]
            {
                new MockTaskItem(@"relative\ImplicitByMetadata", new Dictionary<string, string>
                {
                    { MetadataKeys.IsImplicitlyDefined, "True" },
                    { MetadataKeys.Version, "1.0.0" }
                }),
                new MockTaskItem(@"relative\ImplicitByDefaultList", new Dictionary<string, string>
                {
                    { MetadataKeys.Version, "2.0.0" }
                }),
                new MockTaskItem(@"relative\ExplicitPackage", new Dictionary<string, string>
                {
                    { MetadataKeys.Version, "3.0.0" }
                })
            };
            var taskInstances = new CollectSDKReferencesDesignTime[concurrency];
            var executeTasks = new Task<bool>[concurrency];
            using var startBarrier = new Barrier(concurrency);

            for (int i = 0; i < concurrency; i++)
            {
                var uniqueSdkReference = $@"relative\UniqueSDK\{i}";
                var uniqueImplicitByMetadata = $@"relative\UniqueImplicitByMetadata\{i}";
                var uniqueImplicitByDefaultList = $@"relative\UniqueImplicitByDefaultList\{i}";
                var uniqueExplicitPackage = $@"relative\UniqueExplicitPackage\{i}";
                var task = new CollectSDKReferencesDesignTime
                {
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(Path.Combine(
                        originalCurrentDirectory,
                        "non-default-project",
                        i.ToString())),
                    SdkReferences = sharedSdkReferences.Concat(new ITaskItem[]
                    {
                        new MockTaskItem(uniqueSdkReference, new Dictionary<string, string>
                        {
                            { "RelativeMetadata", $@"metadata\unique\{i}" }
                        })
                    }).ToArray(),
                    PackageReferences = sharedPackageReferences.Concat(new ITaskItem[]
                    {
                        new MockTaskItem(uniqueImplicitByMetadata, new Dictionary<string, string>
                        {
                            { MetadataKeys.IsImplicitlyDefined, "True" },
                            { MetadataKeys.Version, $"4.0.{i}" }
                        }),
                        new MockTaskItem(uniqueImplicitByDefaultList, new Dictionary<string, string>
                        {
                            { MetadataKeys.Version, $"5.0.{i}" }
                        }),
                        new MockTaskItem(uniqueExplicitPackage, new Dictionary<string, string>
                        {
                            { MetadataKeys.Version, $"6.0.{i}" }
                        })
                    }).ToArray(),
                    DefaultImplicitPackages = $@"relative\ImplicitByDefaultList;{uniqueImplicitByDefaultList}"
                };
                taskInstances[i] = task;
            }

            for (int i = 0; i < concurrency; i++)
            {
                int localI = i;
                var t = taskInstances[localI];
                executeTasks[localI] = Task.Factory.StartNew(
                    () =>
                    {
                        startBarrier.SignalAndWait();
                        return t.Execute();
                    },
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
            }

            var executionResults = await Task.WhenAll(executeTasks);

            Directory.GetCurrentDirectory().Should().Be(originalCurrentDirectory);
            for (int i = 0; i < concurrency; i++)
            {
                executionResults[i].Should().BeTrue($"task {i} should succeed");

                var task = taskInstances[i];
                task.SDKReferencesDesignTime.Should().NotBeNull($"task {i} should produce output");
                task.SDKReferencesDesignTime.Length.Should().Be(6, $"task {i} should aggregate shared and unique SDK refs and implicit packages");

                task.SDKReferencesDesignTime.Should().Contain(
                    item => item.ItemSpec == @"relative\SharedSDK",
                    $"task {i} should preserve the shared relative SDK reference");
                task.SDKReferencesDesignTime.Should().Contain(
                    item => item.ItemSpec == $@"relative\UniqueSDK\{i}",
                    $"task {i} should preserve its unique relative SDK reference");

                var implicitByMetadata = task.SDKReferencesDesignTime.FirstOrDefault(item => item.ItemSpec == @"relative\ImplicitByMetadata");
                implicitByMetadata.Should().NotBeNull($"task {i} should include implicit package from metadata");
                implicitByMetadata.GetMetadata(MetadataKeys.SDKPackageItemSpec).Should().Be(string.Empty);
                implicitByMetadata.GetMetadata(MetadataKeys.Name).Should().Be(@"relative\ImplicitByMetadata");
                implicitByMetadata.GetMetadata(MetadataKeys.IsImplicitlyDefined).Should().Be("True");
                implicitByMetadata.GetMetadata(MetadataKeys.Version).Should().Be("1.0.0");

                var implicitByDefaultList = task.SDKReferencesDesignTime.FirstOrDefault(item => item.ItemSpec == @"relative\ImplicitByDefaultList");
                implicitByDefaultList.Should().NotBeNull($"task {i} should include implicit package from DefaultImplicitPackages");
                implicitByDefaultList.GetMetadata(MetadataKeys.Name).Should().Be(@"relative\ImplicitByDefaultList");
                implicitByDefaultList.GetMetadata(MetadataKeys.IsImplicitlyDefined).Should().Be("True");
                implicitByDefaultList.GetMetadata(MetadataKeys.Version).Should().Be("2.0.0");

                var uniqueImplicitByMetadata = $@"relative\UniqueImplicitByMetadata\{i}";
                var uniqueImplicitByMetadataItem = task.SDKReferencesDesignTime.FirstOrDefault(item => item.ItemSpec == uniqueImplicitByMetadata);
                uniqueImplicitByMetadataItem.Should().NotBeNull($"task {i} should include its unique implicit package from metadata");
                uniqueImplicitByMetadataItem.GetMetadata(MetadataKeys.Name).Should().Be(uniqueImplicitByMetadata);
                uniqueImplicitByMetadataItem.GetMetadata(MetadataKeys.IsImplicitlyDefined).Should().Be("True");
                uniqueImplicitByMetadataItem.GetMetadata(MetadataKeys.Version).Should().Be($"4.0.{i}");

                var uniqueImplicitByDefaultList = $@"relative\UniqueImplicitByDefaultList\{i}";
                var uniqueImplicitByDefaultListItem = task.SDKReferencesDesignTime.FirstOrDefault(item => item.ItemSpec == uniqueImplicitByDefaultList);
                uniqueImplicitByDefaultListItem.Should().NotBeNull($"task {i} should include its unique implicit package from DefaultImplicitPackages");
                uniqueImplicitByDefaultListItem.GetMetadata(MetadataKeys.Name).Should().Be(uniqueImplicitByDefaultList);
                uniqueImplicitByDefaultListItem.GetMetadata(MetadataKeys.IsImplicitlyDefined).Should().Be("True");
                uniqueImplicitByDefaultListItem.GetMetadata(MetadataKeys.Version).Should().Be($"5.0.{i}");

                task.SDKReferencesDesignTime.Should().NotContain(
                    item => item.ItemSpec == $@"relative\UniqueExplicitPackage\{i}",
                    $"task {i} should not include its unique explicit package");
            }
        }

        [Fact]
        public void NonDefaultProjectDirectoryDoesNotChangeRelativeOutputsOrCurrentDirectory()
        {
            var originalCurrentDirectory = Directory.GetCurrentDirectory();
            var testRoot = Path.Combine(
                originalCurrentDirectory,
                $"{nameof(NonDefaultProjectDirectoryDoesNotChangeRelativeOutputsOrCurrentDirectory)}-{Guid.NewGuid():N}");
            var processCurrentDirectory = Path.Combine(testRoot, "current-directory");
            var projectDirectory = Path.Combine(testRoot, "project-directory");

            try
            {
                Directory.CreateDirectory(processCurrentDirectory);
                Directory.CreateDirectory(projectDirectory);
                Directory.SetCurrentDirectory(processCurrentDirectory);

                var task = new CollectSDKReferencesDesignTime
                {
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDirectory),
                    SdkReferences = new[]
                    {
                        new MockTaskItem(@"relative\ExistingSDK", new Dictionary<string, string>())
                    },
                    PackageReferences = new[]
                    {
                        new MockTaskItem(@"relative\ImplicitPackage", new Dictionary<string, string>
                        {
                            { MetadataKeys.Version, @"relative\version.txt" }
                        })
                    },
                    DefaultImplicitPackages = @"relative\ImplicitPackage"
                };

                Directory.GetCurrentDirectory().Should().Be(processCurrentDirectory);
                task.TaskEnvironment.ProjectDirectory.Value.Should().Be(projectDirectory);
                task.TaskEnvironment.ProjectDirectory.Value.Should().NotBe(Directory.GetCurrentDirectory());

                task.Execute().Should().BeTrue();

                Directory.GetCurrentDirectory().Should().Be(processCurrentDirectory);
                task.SDKReferencesDesignTime.Select(r => r.ItemSpec).Should().Equal(
                    @"relative\ExistingSDK",
                    @"relative\ImplicitPackage");

                var implicitPackage = task.SDKReferencesDesignTime.Single(i => i.ItemSpec == @"relative\ImplicitPackage");
                implicitPackage.GetMetadata(MetadataKeys.Name).Should().Be(@"relative\ImplicitPackage");
                implicitPackage.GetMetadata(MetadataKeys.Version).Should().Be(@"relative\version.txt");
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCurrentDirectory);
                if (Directory.Exists(testRoot))
                {
                    Directory.Delete(testRoot, recursive: true);
                }
            }
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

    }

    [CollectionDefinition(nameof(CurrentDirectoryMutatingTestCollection), DisableParallelization = true)]
    public sealed class CurrentDirectoryMutatingTestCollection
    {
    }
}
