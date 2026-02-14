// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    /// <summary>
    /// Multithreading tests for tasks annotated with [MSBuildMultiThreadableTask].
    /// These tasks have no forbidden API usage and are expected to be inherently
    /// thread-safe. Tests verify the attribute and concurrent execution correctness.
    /// </summary>
    public class GivenPreMigratedTasksMultiThreading
    {
        #region Attribute Tests

        [Theory]
        [InlineData(typeof(CheckForDuplicateFrameworkReferences))]
        [InlineData(typeof(CheckForImplicitPackageReferenceOverrides))]
        [InlineData(typeof(CheckForUnsupportedWinMDReferences))]
        [InlineData(typeof(CheckIfPackageReferenceShouldBeFrameworkReference))]
        [InlineData(typeof(CollatePackageDownloads))]
        public void ItHasMSBuildMultiThreadableTaskAttribute(Type taskType)
        {
            taskType.Should().BeDecoratedWith<MSBuildMultiThreadableTaskAttribute>();
        }

        #endregion

        #region CheckForDuplicateFrameworkReferences

        [Fact]
        public void CheckForDuplicateFrameworkReferences_DetectsDuplicates()
        {
            var task = new CheckForDuplicateFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                MoreInformationLink = "https://example.com",
                FrameworkReferences = new ITaskItem[]
                {
                    new MockTaskItem("Microsoft.AspNetCore.App", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "true" }
                    }),
                    new MockTaskItem("Microsoft.AspNetCore.App", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "false" }
                    }),
                },
            };

            task.Execute().Should().BeTrue();
            task.ItemsToRemove.Should().HaveCount(1, "the implicit duplicate should be removed");
        }

        [Fact]
        public void CheckForDuplicateFrameworkReferences_NoDuplicates_NoOutput()
        {
            var task = new CheckForDuplicateFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                MoreInformationLink = "https://example.com",
                FrameworkReferences = new ITaskItem[]
                {
                    new MockTaskItem("Microsoft.AspNetCore.App", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "true" }
                    }),
                    new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "true" }
                    }),
                },
            };

            task.Execute().Should().BeTrue();
            task.ItemsToRemove.Should().BeNullOrEmpty();
        }

        [Theory]
        [InlineData(4)]
        [InlineData(16)]
        public void CheckForDuplicateFrameworkReferences_ConcurrentExecution(int parallelism)
        {
            var errors = new ConcurrentBag<string>();
            var barrier = new Barrier(parallelism);

            Parallel.For(0, parallelism, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, i =>
            {
                try
                {
                    var task = new CheckForDuplicateFrameworkReferences
                    {
                        BuildEngine = new MockBuildEngine(),
                        MoreInformationLink = "https://example.com",
                        FrameworkReferences = new ITaskItem[]
                        {
                            new MockTaskItem($"Framework.{i}", new Dictionary<string, string>
                            {
                                { MetadataKeys.IsImplicitlyDefined, "true" }
                            }),
                            new MockTaskItem($"Framework.{i}", new Dictionary<string, string>
                            {
                                { MetadataKeys.IsImplicitlyDefined, "false" }
                            }),
                        },
                    };

                    barrier.SignalAndWait();
                    task.Execute();

                    if (task.ItemsToRemove == null || task.ItemsToRemove.Length != 1)
                    {
                        errors.Add($"Thread {i}: Expected 1 item to remove, got {task.ItemsToRemove?.Length ?? 0}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Thread {i}: {ex.Message}");
                }
            });

            errors.Should().BeEmpty();
        }

        #endregion

        #region CheckForImplicitPackageReferenceOverrides

        [Fact]
        public void CheckForImplicitPackageReferenceOverrides_DetectsOverrides()
        {
            var task = new CheckForImplicitPackageReferenceOverrides
            {
                BuildEngine = new MockBuildEngine(),
                MoreInformationLink = "https://example.com",
                PackageReferenceItems = new ITaskItem[]
                {
                    new MockTaskItem("Newtonsoft.Json", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "true" }
                    }),
                    new MockTaskItem("Newtonsoft.Json", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "false" }
                    }),
                },
            };

            task.Execute().Should().BeTrue();
            task.ItemsToRemove.Should().HaveCount(2, "both the implicit and explicit duplicates are removed");
            task.ItemsToAdd.Should().HaveCount(1, "the explicit item is re-added with AllowExplicitVersion");
        }

        [Theory]
        [InlineData(4)]
        [InlineData(16)]
        public void CheckForImplicitPackageReferenceOverrides_ConcurrentExecution(int parallelism)
        {
            var errors = new ConcurrentBag<string>();
            var barrier = new Barrier(parallelism);

            Parallel.For(0, parallelism, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, i =>
            {
                try
                {
                    var task = new CheckForImplicitPackageReferenceOverrides
                    {
                        BuildEngine = new MockBuildEngine(),
                        MoreInformationLink = "https://example.com",
                        PackageReferenceItems = new ITaskItem[]
                        {
                            new MockTaskItem($"Package.{i}", new Dictionary<string, string>
                            {
                                { MetadataKeys.IsImplicitlyDefined, "true" }
                            }),
                            new MockTaskItem($"Package.{i}", new Dictionary<string, string>
                            {
                                { MetadataKeys.IsImplicitlyDefined, "false" }
                            }),
                        },
                    };

                    barrier.SignalAndWait();
                    task.Execute();

                    if (task.ItemsToRemove == null || task.ItemsToRemove.Length != 2)
                    {
                        errors.Add($"Thread {i}: Expected 2 items to remove, got {task.ItemsToRemove?.Length ?? 0}");
                    }
                    if (task.ItemsToAdd == null || task.ItemsToAdd.Length != 1)
                    {
                        errors.Add($"Thread {i}: Expected 1 item to add, got {task.ItemsToAdd?.Length ?? 0}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Thread {i}: {ex.Message}");
                }
            });

            errors.Should().BeEmpty();
        }

        #endregion

        #region CheckIfPackageReferenceShouldBeFrameworkReference

        [Fact]
        public void CheckIfPackageReferenceShouldBeFrameworkReference_MatchingPackage()
        {
            var task = new CheckIfPackageReferenceShouldBeFrameworkReference
            {
                BuildEngine = new MockBuildEngine(),
                PackageReferenceToReplace = "Microsoft.AspNetCore.All",
                FrameworkReferenceToUse = "Microsoft.AspNetCore.App",
                PackageReferences = new ITaskItem[]
                {
                    new MockTaskItem("Microsoft.AspNetCore.All", new Dictionary<string, string>()),
                },
                FrameworkReferences = Array.Empty<ITaskItem>(),
            };

            task.Execute().Should().BeTrue();
            task.ShouldRemovePackageReference.Should().BeTrue();
            task.ShouldAddFrameworkReference.Should().BeTrue();
        }

        [Fact]
        public void CheckIfPackageReferenceShouldBeFrameworkReference_FrameworkAlreadyExists()
        {
            var task = new CheckIfPackageReferenceShouldBeFrameworkReference
            {
                BuildEngine = new MockBuildEngine(),
                PackageReferenceToReplace = "Microsoft.AspNetCore.All",
                FrameworkReferenceToUse = "Microsoft.AspNetCore.App",
                PackageReferences = new ITaskItem[]
                {
                    new MockTaskItem("Microsoft.AspNetCore.All", new Dictionary<string, string>()),
                },
                FrameworkReferences = new ITaskItem[]
                {
                    new MockTaskItem("Microsoft.AspNetCore.App", new Dictionary<string, string>()),
                },
            };

            task.Execute().Should().BeTrue();
            task.ShouldRemovePackageReference.Should().BeTrue();
            task.ShouldAddFrameworkReference.Should().BeFalse("framework reference already exists");
        }

        [Fact]
        public void CheckIfPackageReferenceShouldBeFrameworkReference_NoMatch()
        {
            var task = new CheckIfPackageReferenceShouldBeFrameworkReference
            {
                BuildEngine = new MockBuildEngine(),
                PackageReferenceToReplace = "Microsoft.AspNetCore.All",
                FrameworkReferenceToUse = "Microsoft.AspNetCore.App",
                PackageReferences = new ITaskItem[]
                {
                    new MockTaskItem("SomeOtherPackage", new Dictionary<string, string>()),
                },
                FrameworkReferences = Array.Empty<ITaskItem>(),
            };

            task.Execute().Should().BeTrue();
            task.ShouldRemovePackageReference.Should().BeFalse();
            task.ShouldAddFrameworkReference.Should().BeFalse();
        }

        [Theory]
        [InlineData(4)]
        [InlineData(16)]
        public void CheckIfPackageReferenceShouldBeFrameworkReference_ConcurrentExecution(int parallelism)
        {
            var errors = new ConcurrentBag<string>();
            var barrier = new Barrier(parallelism);

            Parallel.For(0, parallelism, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, i =>
            {
                try
                {
                    var task = new CheckIfPackageReferenceShouldBeFrameworkReference
                    {
                        BuildEngine = new MockBuildEngine(),
                        PackageReferenceToReplace = $"Package.{i}",
                        FrameworkReferenceToUse = $"Framework.{i}",
                        PackageReferences = new ITaskItem[]
                        {
                            new MockTaskItem($"Package.{i}", new Dictionary<string, string>()),
                        },
                        FrameworkReferences = Array.Empty<ITaskItem>(),
                    };

                    barrier.SignalAndWait();
                    task.Execute();

                    if (!task.ShouldRemovePackageReference)
                    {
                        errors.Add($"Thread {i}: ShouldRemovePackageReference should be true");
                    }
                    if (!task.ShouldAddFrameworkReference)
                    {
                        errors.Add($"Thread {i}: ShouldAddFrameworkReference should be true");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Thread {i}: {ex.Message}");
                }
            });

            errors.Should().BeEmpty();
        }

        #endregion

        #region CollatePackageDownloads

        [Fact]
        public void CollatePackageDownloads_GroupsVersions()
        {
            var task = new CollatePackageDownloads
            {
                BuildEngine = new MockBuildEngine(),
                Packages = new ITaskItem[]
                {
                    new MockTaskItem("NuGet.Common", new Dictionary<string, string> { { "Version", "5.0.0" } }),
                    new MockTaskItem("NuGet.Common", new Dictionary<string, string> { { "Version", "6.0.0" } }),
                    new MockTaskItem("NuGet.Protocol", new Dictionary<string, string> { { "Version", "5.0.0" } }),
                },
            };

            task.Execute().Should().BeTrue();
            task.PackageDownloads.Should().HaveCount(2, "two distinct package names");

            var nugetCommon = Array.Find(task.PackageDownloads, p => p.ItemSpec == "NuGet.Common");
            nugetCommon.Should().NotBeNull();
            nugetCommon!.GetMetadata("Version").Should().Contain("[5.0.0]").And.Contain("[6.0.0]");
        }

        [Fact]
        public void CollatePackageDownloads_SingleVersion()
        {
            var task = new CollatePackageDownloads
            {
                BuildEngine = new MockBuildEngine(),
                Packages = new ITaskItem[]
                {
                    new MockTaskItem("MyPackage", new Dictionary<string, string> { { "Version", "1.0.0" } }),
                },
            };

            task.Execute().Should().BeTrue();
            task.PackageDownloads.Should().HaveCount(1);
            task.PackageDownloads[0].GetMetadata("Version").Should().Be("[1.0.0]");
        }

        [Theory]
        [InlineData(4)]
        [InlineData(16)]
        public void CollatePackageDownloads_ConcurrentExecution(int parallelism)
        {
            var errors = new ConcurrentBag<string>();
            var barrier = new Barrier(parallelism);

            Parallel.For(0, parallelism, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, i =>
            {
                try
                {
                    var task = new CollatePackageDownloads
                    {
                        BuildEngine = new MockBuildEngine(),
                        Packages = new ITaskItem[]
                        {
                            new MockTaskItem($"Pkg.{i}", new Dictionary<string, string> { { "Version", "1.0.0" } }),
                            new MockTaskItem($"Pkg.{i}", new Dictionary<string, string> { { "Version", "2.0.0" } }),
                            new MockTaskItem($"Other.{i}", new Dictionary<string, string> { { "Version", "3.0.0" } }),
                        },
                    };

                    barrier.SignalAndWait();
                    task.Execute();

                    if (task.PackageDownloads == null || task.PackageDownloads.Length != 2)
                    {
                        errors.Add($"Thread {i}: Expected 2 downloads, got {task.PackageDownloads?.Length ?? 0}");
                    }

                    var pkg = Array.Find(task.PackageDownloads!, p => p.ItemSpec == $"Pkg.{i}");
                    if (pkg == null)
                    {
                        errors.Add($"Thread {i}: Missing Pkg.{i} in output");
                    }
                    else
                    {
                        var version = pkg.GetMetadata("Version");
                        if (!version.Contains("[1.0.0]") || !version.Contains("[2.0.0]"))
                        {
                            errors.Add($"Thread {i}: Version metadata wrong: {version}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Thread {i}: {ex.Message}");
                }
            });

            errors.Should().BeEmpty();
        }

        #endregion

        #region CheckForUnsupportedWinMDReferences

        [Fact]
        public void CheckForUnsupportedWinMDReferences_NoReferences_Succeeds()
        {
            var task = new CheckForUnsupportedWinMDReferences
            {
                BuildEngine = new MockBuildEngine(),
                TargetFrameworkMoniker = ".NETCoreApp,Version=v5.0",
                ReferencePaths = Array.Empty<ITaskItem>(),
            };

            task.Execute().Should().BeTrue();
        }

        [Theory]
        [InlineData(4)]
        [InlineData(16)]
        public void CheckForUnsupportedWinMDReferences_ConcurrentExecution(int parallelism)
        {
            var errors = new ConcurrentBag<string>();
            var barrier = new Barrier(parallelism);

            Parallel.For(0, parallelism, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, i =>
            {
                try
                {
                    var task = new CheckForUnsupportedWinMDReferences
                    {
                        BuildEngine = new MockBuildEngine(),
                        TargetFrameworkMoniker = ".NETCoreApp,Version=v5.0",
                        ReferencePaths = Array.Empty<ITaskItem>(),
                    };

                    barrier.SignalAndWait();
                    var result = task.Execute();

                    if (!result)
                    {
                        errors.Add($"Thread {i}: Expected Execute to return true with no references");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Thread {i}: {ex.Message}");
                }
            });

            errors.Should().BeEmpty();
        }

        #endregion
    }
}
