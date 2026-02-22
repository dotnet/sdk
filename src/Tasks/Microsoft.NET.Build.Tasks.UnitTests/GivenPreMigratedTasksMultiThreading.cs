// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
    /// thread-safe. Tests verify concurrent execution correctness.
    /// </summary>
    public class GivenPreMigratedTasksMultiThreading
    {
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

        [Fact]
        public void CheckForDuplicateFrameworkReferences_NullFrameworkReferences_Succeeds()
        {
            var task = new CheckForDuplicateFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                MoreInformationLink = "https://example.com",
                FrameworkReferences = null,
            };

            task.Execute().Should().BeTrue("the null guard in ExecuteCore returns early");
            task.ItemsToRemove.Should().BeNull();
            task.ItemsToAdd.Should().BeNull();
        }

        [Fact]
        public void CheckForDuplicateFrameworkReferences_EmptyArray_Succeeds()
        {
            var task = new CheckForDuplicateFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                MoreInformationLink = "https://example.com",
                FrameworkReferences = Array.Empty<ITaskItem>(),
            };

            task.Execute().Should().BeTrue();
            task.ItemsToRemove.Should().BeNull();
            task.ItemsToAdd.Should().BeNull();
        }

        [Fact]
        public void CheckForDuplicateFrameworkReferences_EmptyItemSpec_HandlesGracefully()
        {
            var task = new CheckForDuplicateFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                MoreInformationLink = "https://example.com",
                FrameworkReferences = new ITaskItem[]
                {
                    new MockTaskItem("", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "true" }
                    }),
                    new MockTaskItem("", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "false" }
                    }),
                },
            };

            task.Execute().Should().BeTrue();
            task.ItemsToRemove.Should().HaveCount(1, "the implicit duplicate with empty ItemSpec should be removed");
        }

        [Fact]
        public void CheckForDuplicateFrameworkReferences_RelativePathItemSpec_PreservesFormat()
        {
            const string relativePathSpec = "some/relative/path.dll";
            var task = new CheckForDuplicateFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                MoreInformationLink = "https://example.com",
                FrameworkReferences = new ITaskItem[]
                {
                    new MockTaskItem(relativePathSpec, new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "true" }
                    }),
                    new MockTaskItem(relativePathSpec, new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "false" }
                    }),
                },
            };

            task.Execute().Should().BeTrue();

            task.ItemsToRemove.Should().HaveCount(1);
            task.ItemsToRemove[0].ItemSpec.Should().Be(relativePathSpec,
                "output ItemSpec must preserve the exact input format without path normalization");

            task.ItemsToAdd.Should().HaveCount(1);
            task.ItemsToAdd[0].ItemSpec.Should().Be(relativePathSpec,
                "output ItemSpec must preserve the exact input format without path normalization");
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

        [Fact]
        public void CheckForImplicitPackageReferenceOverrides_NullPackageReferenceItems_Throws()
        {
            var task = new CheckForImplicitPackageReferenceOverrides
            {
                BuildEngine = new MockBuildEngine(),
                MoreInformationLink = "https://example.com",
                PackageReferenceItems = null,
            };

            Action act = () => task.Execute();
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void CheckForImplicitPackageReferenceOverrides_EmptyArray_Succeeds()
        {
            var task = new CheckForImplicitPackageReferenceOverrides
            {
                BuildEngine = new MockBuildEngine(),
                MoreInformationLink = "https://example.com",
                PackageReferenceItems = Array.Empty<ITaskItem>(),
            };

            task.Execute().Should().BeTrue();
            task.ItemsToRemove.Should().BeNull();
            task.ItemsToAdd.Should().BeNull();
        }

        [Fact]
        public void CheckForImplicitPackageReferenceOverrides_EmptyItemSpec_HandlesGracefully()
        {
            var task = new CheckForImplicitPackageReferenceOverrides
            {
                BuildEngine = new MockBuildEngine(),
                MoreInformationLink = "https://example.com",
                PackageReferenceItems = new ITaskItem[]
                {
                    new MockTaskItem("", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "true" }
                    }),
                    new MockTaskItem("", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "false" }
                    }),
                },
            };

            task.Execute().Should().BeTrue();
            task.ItemsToRemove.Should().HaveCount(2, "both items with empty ItemSpec should be in remove list");
            task.ItemsToAdd.Should().HaveCount(1, "the explicit item should be re-added");
        }

        [Fact]
        public void CheckForImplicitPackageReferenceOverrides_RelativePathItemSpec_PreservesFormat()
        {
            const string pathLikeSpec = "some/path/Newtonsoft.Json";
            var task = new CheckForImplicitPackageReferenceOverrides
            {
                BuildEngine = new MockBuildEngine(),
                MoreInformationLink = "https://example.com",
                PackageReferenceItems = new ITaskItem[]
                {
                    new MockTaskItem(pathLikeSpec, new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "true" }
                    }),
                    new MockTaskItem(pathLikeSpec, new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "false" }
                    }),
                },
            };

            task.Execute().Should().BeTrue();

            task.ItemsToRemove.Should().HaveCount(2);
            task.ItemsToRemove.Should().AllSatisfy(item =>
                item.ItemSpec.Should().Be(pathLikeSpec,
                    "output ItemSpec must preserve the exact input format without path normalization"));

            task.ItemsToAdd.Should().HaveCount(1);
            task.ItemsToAdd[0].ItemSpec.Should().Be(pathLikeSpec,
                "output ItemSpec must preserve the exact input format without path normalization");
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

        [Fact]
        public void CheckIfPackageReferenceShouldBeFrameworkReference_EmptyItemSpec_HandlesGracefully()
        {
            var task = new CheckIfPackageReferenceShouldBeFrameworkReference
            {
                BuildEngine = new MockBuildEngine(),
                PackageReferenceToReplace = "Microsoft.AspNetCore.All",
                FrameworkReferenceToUse = "Microsoft.AspNetCore.App",
                PackageReferences = new ITaskItem[]
                {
                    new MockTaskItem("", new Dictionary<string, string>()),
                },
                FrameworkReferences = Array.Empty<ITaskItem>(),
            };

            task.Execute().Should().BeTrue();
            task.ShouldRemovePackageReference.Should().BeFalse("empty ItemSpec does not match the package to replace");
            task.ShouldAddFrameworkReference.Should().BeFalse();
        }

        [Fact]
        public void CheckIfPackageReferenceShouldBeFrameworkReference_EmptyArrays_Succeeds()
        {
            var task = new CheckIfPackageReferenceShouldBeFrameworkReference
            {
                BuildEngine = new MockBuildEngine(),
                PackageReferenceToReplace = "SomePackage",
                FrameworkReferenceToUse = "SomeFramework",
                PackageReferences = Array.Empty<ITaskItem>(),
                FrameworkReferences = Array.Empty<ITaskItem>(),
            };

            task.Execute().Should().BeTrue();
            task.ShouldRemovePackageReference.Should().BeFalse();
            task.ShouldAddFrameworkReference.Should().BeFalse();
        }

        [Fact]
        public void CheckIfPackageReferenceShouldBeFrameworkReference_RelativePathItemSpec_MatchesCorrectly()
        {
            const string pathLikeSpec = "some/path/Microsoft.AspNetCore.All";
            var task = new CheckIfPackageReferenceShouldBeFrameworkReference
            {
                BuildEngine = new MockBuildEngine(),
                PackageReferenceToReplace = pathLikeSpec,
                FrameworkReferenceToUse = "Microsoft.AspNetCore.App",
                PackageReferences = new ITaskItem[]
                {
                    new MockTaskItem(pathLikeSpec, new Dictionary<string, string>()),
                },
                FrameworkReferences = Array.Empty<ITaskItem>(),
            };

            task.Execute().Should().BeTrue();
            task.ShouldRemovePackageReference.Should().BeTrue(
                "string matching should work correctly with path-like ItemSpec");
            task.ShouldAddFrameworkReference.Should().BeTrue();
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

        [Fact]
        public void CollatePackageDownloads_NullPackages_Throws()
        {
            var task = new CollatePackageDownloads
            {
                BuildEngine = new MockBuildEngine(),
                Packages = null,
            };

            Action act = () => task.Execute();
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void CollatePackageDownloads_EmptyArray_Succeeds()
        {
            var task = new CollatePackageDownloads
            {
                BuildEngine = new MockBuildEngine(),
                Packages = Array.Empty<ITaskItem>(),
            };

            task.Execute().Should().BeTrue();
            task.PackageDownloads.Should().BeEmpty();
        }

        [Fact]
        public void CollatePackageDownloads_EmptyItemSpec_HandlesGracefully()
        {
            var task = new CollatePackageDownloads
            {
                BuildEngine = new MockBuildEngine(),
                Packages = new ITaskItem[]
                {
                    new MockTaskItem("", new Dictionary<string, string> { { "Version", "1.0.0" } }),
                },
            };

            task.Execute().Should().BeTrue();
            task.PackageDownloads.Should().HaveCount(1);
            task.PackageDownloads[0].ItemSpec.Should().BeEmpty();
            task.PackageDownloads[0].GetMetadata("Version").Should().Be("[1.0.0]");
        }

        [Theory]
        [InlineData("packages/NuGet.Common")]
        [InlineData("packages\\NuGet.Common")]
        public void CollatePackageDownloads_PathItemSpec_PreservesFormat(string pathSpec)
        {
            var task = new CollatePackageDownloads
            {
                BuildEngine = new MockBuildEngine(),
                Packages = new ITaskItem[]
                {
                    new MockTaskItem(pathSpec, new Dictionary<string, string> { { "Version", "5.0.0" } }),
                    new MockTaskItem(pathSpec, new Dictionary<string, string> { { "Version", "6.0.0" } }),
                },
            };

            task.Execute().Should().BeTrue();

            task.PackageDownloads.Should().HaveCount(1);
            task.PackageDownloads[0].ItemSpec.Should().Be(pathSpec,
                "output ItemSpec must preserve the exact input format without path normalization");
            task.PackageDownloads[0].GetMetadata("Version").Should().Contain("[5.0.0]").And.Contain("[6.0.0]");
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

        [Fact]
        public void CheckForUnsupportedWinMDReferences_RelativePathItemSpec_HandlesCorrectly()
        {
            const string relativeWinmdPath = "subfolder/something.winmd";
            var task = new CheckForUnsupportedWinMDReferences
            {
                BuildEngine = new MockBuildEngine(),
                TargetFrameworkMoniker = ".NETCoreApp,Version=v5.0",
                ReferencePaths = new ITaskItem[]
                {
                    new MockTaskItem(relativeWinmdPath, new Dictionary<string, string>()),
                },
            };

            // Path.GetExtension and Path.GetFileName handle relative paths correctly.
            // The .winmd extension triggers error logging (no FileStream needed for this path).
            task.Execute().Should().BeFalse("a .winmd reference triggers an error log, causing Execute to return false");
            var engine = (MockBuildEngine)task.BuildEngine;
            engine.Errors.Should().NotBeEmpty("the task should log an error for the unsupported .winmd reference");
        }

        #endregion

        #region Single vs Multi-Process Output Parity

        private const int ParityParallelism = 8;

        #region Helper Methods

        private static CheckForDuplicateFrameworkReferences CreateCheckForDuplicateFrameworkReferencesTask()
        {
            return new CheckForDuplicateFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                MoreInformationLink = "https://example.com/parity",
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
                    new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "true" }
                    }),
                },
            };
        }

        private static CheckForImplicitPackageReferenceOverrides CreateCheckForImplicitPackageReferenceOverridesTask()
        {
            return new CheckForImplicitPackageReferenceOverrides
            {
                BuildEngine = new MockBuildEngine(),
                MoreInformationLink = "https://example.com/parity",
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
        }

        private static CheckForUnsupportedWinMDReferences CreateCheckForUnsupportedWinMDReferencesTask()
        {
            return new CheckForUnsupportedWinMDReferences
            {
                BuildEngine = new MockBuildEngine(),
                TargetFrameworkMoniker = ".NETCoreApp,Version=v5.0",
                ReferencePaths = Array.Empty<ITaskItem>(),
            };
        }

        private static CheckIfPackageReferenceShouldBeFrameworkReference CreateCheckIfPackageReferenceShouldBeFrameworkReferenceTask()
        {
            return new CheckIfPackageReferenceShouldBeFrameworkReference
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
        }

        private static CollatePackageDownloads CreateCollatePackageDownloadsTask()
        {
            return new CollatePackageDownloads
            {
                BuildEngine = new MockBuildEngine(),
                Packages = new ITaskItem[]
                {
                    new MockTaskItem("NuGet.Common", new Dictionary<string, string> { { "Version", "5.0.0" } }),
                    new MockTaskItem("NuGet.Common", new Dictionary<string, string> { { "Version", "6.0.0" } }),
                    new MockTaskItem("NuGet.Protocol", new Dictionary<string, string> { { "Version", "5.0.0" } }),
                },
            };
        }

        private static void AssertTaskItemArraysEqual(ITaskItem[] expected, ITaskItem[] actual, string context)
        {
            if (expected == null && actual == null)
            {
                return;
            }

            actual.Should().NotBeNull($"{context}: expected non-null array");
            expected.Should().NotBeNull($"{context}: baseline was null but actual was not");
            actual.Length.Should().Be(expected.Length, $"{context}: array lengths differ");

            for (int i = 0; i < expected.Length; i++)
            {
                actual[i].ItemSpec.Should().Be(expected[i].ItemSpec, $"{context}[{i}].ItemSpec");

                foreach (string metadataName in expected[i].MetadataNames)
                {
                    actual[i].GetMetadata(metadataName).Should().Be(
                        expected[i].GetMetadata(metadataName),
                        $"{context}[{i}].GetMetadata(\"{metadataName}\")");
                }
            }
        }

        #endregion

        [Fact]
        public void CheckForDuplicateFrameworkReferences_OutputParity()
        {
            // Single-threaded baseline
            var baseline = CreateCheckForDuplicateFrameworkReferencesTask();
            bool baselineResult = baseline.Execute();
            var baselineItemsToAdd = baseline.ItemsToAdd;
            var baselineItemsToRemove = baseline.ItemsToRemove;
            var baselineEngine = (MockBuildEngine)baseline.BuildEngine;

            // Concurrent execution
            var errors = new ConcurrentBag<string>();
            var barrier = new Barrier(ParityParallelism);

            Parallel.For(0, ParityParallelism, new ParallelOptions { MaxDegreeOfParallelism = ParityParallelism }, i =>
            {
                try
                {
                    var task = CreateCheckForDuplicateFrameworkReferencesTask();

                    barrier.SignalAndWait();
                    bool result = task.Execute();

                    if (result != baselineResult)
                    {
                        errors.Add($"Thread {i}: Execute() returned {result}, expected {baselineResult}");
                        return;
                    }

                    var engine = (MockBuildEngine)task.BuildEngine;
                    if (engine.Errors.Count != baselineEngine.Errors.Count)
                    {
                        errors.Add($"Thread {i}: Error count {engine.Errors.Count}, expected {baselineEngine.Errors.Count}");
                    }
                    if (engine.Warnings.Count != baselineEngine.Warnings.Count)
                    {
                        errors.Add($"Thread {i}: Warning count {engine.Warnings.Count}, expected {baselineEngine.Warnings.Count}");
                    }

                    if ((baselineItemsToRemove?.Length ?? 0) != (task.ItemsToRemove?.Length ?? 0))
                    {
                        errors.Add($"Thread {i}: ItemsToRemove length {task.ItemsToRemove?.Length ?? 0}, expected {baselineItemsToRemove?.Length ?? 0}");
                    }
                    else if (baselineItemsToRemove != null)
                    {
                        for (int j = 0; j < baselineItemsToRemove.Length; j++)
                        {
                            if (task.ItemsToRemove![j].ItemSpec != baselineItemsToRemove[j].ItemSpec)
                            {
                                errors.Add($"Thread {i}: ItemsToRemove[{j}].ItemSpec = {task.ItemsToRemove![j].ItemSpec}, expected {baselineItemsToRemove[j].ItemSpec}");
                            }
                        }
                    }

                    if ((baselineItemsToAdd?.Length ?? 0) != (task.ItemsToAdd?.Length ?? 0))
                    {
                        errors.Add($"Thread {i}: ItemsToAdd length {task.ItemsToAdd?.Length ?? 0}, expected {baselineItemsToAdd?.Length ?? 0}");
                    }
                    else if (baselineItemsToAdd != null)
                    {
                        for (int j = 0; j < baselineItemsToAdd.Length; j++)
                        {
                            if (task.ItemsToAdd![j].ItemSpec != baselineItemsToAdd[j].ItemSpec)
                            {
                                errors.Add($"Thread {i}: ItemsToAdd[{j}].ItemSpec = {task.ItemsToAdd![j].ItemSpec}, expected {baselineItemsToAdd[j].ItemSpec}");
                            }
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

        [Fact]
        public void CheckForImplicitPackageReferenceOverrides_OutputParity()
        {
            // Single-threaded baseline
            var baseline = CreateCheckForImplicitPackageReferenceOverridesTask();
            bool baselineResult = baseline.Execute();
            var baselineItemsToRemove = baseline.ItemsToRemove;
            var baselineItemsToAdd = baseline.ItemsToAdd;
            var baselineEngine = (MockBuildEngine)baseline.BuildEngine;

            // Concurrent execution
            var errors = new ConcurrentBag<string>();
            var barrier = new Barrier(ParityParallelism);

            Parallel.For(0, ParityParallelism, new ParallelOptions { MaxDegreeOfParallelism = ParityParallelism }, i =>
            {
                try
                {
                    var task = CreateCheckForImplicitPackageReferenceOverridesTask();

                    barrier.SignalAndWait();
                    bool result = task.Execute();

                    if (result != baselineResult)
                    {
                        errors.Add($"Thread {i}: Execute() returned {result}, expected {baselineResult}");
                        return;
                    }

                    var engine = (MockBuildEngine)task.BuildEngine;
                    if (engine.Warnings.Count != baselineEngine.Warnings.Count)
                    {
                        errors.Add($"Thread {i}: Warning count {engine.Warnings.Count}, expected {baselineEngine.Warnings.Count}");
                    }

                    if ((baselineItemsToRemove?.Length ?? 0) != (task.ItemsToRemove?.Length ?? 0))
                    {
                        errors.Add($"Thread {i}: ItemsToRemove length {task.ItemsToRemove?.Length ?? 0}, expected {baselineItemsToRemove?.Length ?? 0}");
                    }
                    else if (baselineItemsToRemove != null)
                    {
                        for (int j = 0; j < baselineItemsToRemove.Length; j++)
                        {
                            if (task.ItemsToRemove![j].ItemSpec != baselineItemsToRemove[j].ItemSpec)
                            {
                                errors.Add($"Thread {i}: ItemsToRemove[{j}].ItemSpec = {task.ItemsToRemove![j].ItemSpec}, expected {baselineItemsToRemove[j].ItemSpec}");
                            }
                        }
                    }

                    if ((baselineItemsToAdd?.Length ?? 0) != (task.ItemsToAdd?.Length ?? 0))
                    {
                        errors.Add($"Thread {i}: ItemsToAdd length {task.ItemsToAdd?.Length ?? 0}, expected {baselineItemsToAdd?.Length ?? 0}");
                    }
                    else if (baselineItemsToAdd != null)
                    {
                        for (int j = 0; j < baselineItemsToAdd.Length; j++)
                        {
                            if (task.ItemsToAdd![j].ItemSpec != baselineItemsToAdd[j].ItemSpec)
                            {
                                errors.Add($"Thread {i}: ItemsToAdd[{j}].ItemSpec = {task.ItemsToAdd![j].ItemSpec}, expected {baselineItemsToAdd[j].ItemSpec}");
                            }
                            if (task.ItemsToAdd![j].GetMetadata(MetadataKeys.AllowExplicitVersion) !=
                                baselineItemsToAdd[j].GetMetadata(MetadataKeys.AllowExplicitVersion))
                            {
                                errors.Add($"Thread {i}: ItemsToAdd[{j}].AllowExplicitVersion mismatch");
                            }
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

        [Fact]
        public void CheckForUnsupportedWinMDReferences_OutputParity()
        {
            // Single-threaded baseline (empty ReferencePaths to avoid file I/O)
            var baseline = CreateCheckForUnsupportedWinMDReferencesTask();
            bool baselineResult = baseline.Execute();
            var baselineEngine = (MockBuildEngine)baseline.BuildEngine;

            // Concurrent execution
            var errors = new ConcurrentBag<string>();
            var barrier = new Barrier(ParityParallelism);

            Parallel.For(0, ParityParallelism, new ParallelOptions { MaxDegreeOfParallelism = ParityParallelism }, i =>
            {
                try
                {
                    var task = CreateCheckForUnsupportedWinMDReferencesTask();

                    barrier.SignalAndWait();
                    bool result = task.Execute();

                    if (result != baselineResult)
                    {
                        errors.Add($"Thread {i}: Execute() returned {result}, expected {baselineResult}");
                        return;
                    }

                    var engine = (MockBuildEngine)task.BuildEngine;
                    if (engine.Errors.Count != baselineEngine.Errors.Count)
                    {
                        errors.Add($"Thread {i}: Error count {engine.Errors.Count}, expected {baselineEngine.Errors.Count}");
                    }
                    if (engine.Warnings.Count != baselineEngine.Warnings.Count)
                    {
                        errors.Add($"Thread {i}: Warning count {engine.Warnings.Count}, expected {baselineEngine.Warnings.Count}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Thread {i}: {ex.Message}");
                }
            });

            errors.Should().BeEmpty();
        }

        [Fact]
        public void CheckIfPackageReferenceShouldBeFrameworkReference_OutputParity()
        {
            // Single-threaded baseline
            var baseline = CreateCheckIfPackageReferenceShouldBeFrameworkReferenceTask();
            bool baselineResult = baseline.Execute();
            bool baselineShouldRemove = baseline.ShouldRemovePackageReference;
            bool baselineShouldAdd = baseline.ShouldAddFrameworkReference;

            // Concurrent execution
            var errors = new ConcurrentBag<string>();
            var barrier = new Barrier(ParityParallelism);

            Parallel.For(0, ParityParallelism, new ParallelOptions { MaxDegreeOfParallelism = ParityParallelism }, i =>
            {
                try
                {
                    var task = CreateCheckIfPackageReferenceShouldBeFrameworkReferenceTask();

                    barrier.SignalAndWait();
                    bool result = task.Execute();

                    if (result != baselineResult)
                    {
                        errors.Add($"Thread {i}: Execute() returned {result}, expected {baselineResult}");
                        return;
                    }
                    if (task.ShouldRemovePackageReference != baselineShouldRemove)
                    {
                        errors.Add($"Thread {i}: ShouldRemovePackageReference = {task.ShouldRemovePackageReference}, expected {baselineShouldRemove}");
                    }
                    if (task.ShouldAddFrameworkReference != baselineShouldAdd)
                    {
                        errors.Add($"Thread {i}: ShouldAddFrameworkReference = {task.ShouldAddFrameworkReference}, expected {baselineShouldAdd}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Thread {i}: {ex.Message}");
                }
            });

            errors.Should().BeEmpty();
        }

        [Fact]
        public void CollatePackageDownloads_OutputParity()
        {
            // Single-threaded baseline
            var baseline = CreateCollatePackageDownloadsTask();
            bool baselineResult = baseline.Execute();
            var baselineDownloads = baseline.PackageDownloads;

            // Concurrent execution
            var errors = new ConcurrentBag<string>();
            var barrier = new Barrier(ParityParallelism);

            Parallel.For(0, ParityParallelism, new ParallelOptions { MaxDegreeOfParallelism = ParityParallelism }, i =>
            {
                try
                {
                    var task = CreateCollatePackageDownloadsTask();

                    barrier.SignalAndWait();
                    bool result = task.Execute();

                    if (result != baselineResult)
                    {
                        errors.Add($"Thread {i}: Execute() returned {result}, expected {baselineResult}");
                        return;
                    }

                    if ((baselineDownloads?.Length ?? 0) != (task.PackageDownloads?.Length ?? 0))
                    {
                        errors.Add($"Thread {i}: PackageDownloads length {task.PackageDownloads?.Length ?? 0}, expected {baselineDownloads?.Length ?? 0}");
                        return;
                    }

                    if (baselineDownloads != null)
                    {
                        // Sort both by ItemSpec for stable comparison since GroupBy order may vary
                        var sortedBaseline = baselineDownloads.OrderBy(p => p.ItemSpec).ToArray();
                        var sortedActual = task.PackageDownloads!.OrderBy(p => p.ItemSpec).ToArray();

                        for (int j = 0; j < sortedBaseline.Length; j++)
                        {
                            if (sortedActual[j].ItemSpec != sortedBaseline[j].ItemSpec)
                            {
                                errors.Add($"Thread {i}: PackageDownloads[{j}].ItemSpec = {sortedActual[j].ItemSpec}, expected {sortedBaseline[j].ItemSpec}");
                            }

                            string expectedVersion = sortedBaseline[j].GetMetadata("Version");
                            string actualVersion = sortedActual[j].GetMetadata("Version");
                            if (actualVersion != expectedVersion)
                            {
                                errors.Add($"Thread {i}: PackageDownloads[{j}].Version = {actualVersion}, expected {expectedVersion}");
                            }
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

        [Fact]
        public void CheckForUnsupportedWinMDReferences_NullReferencePaths_Throws()
        {
            var task = new CheckForUnsupportedWinMDReferences
            {
                BuildEngine = new MockBuildEngine(),
                TargetFrameworkMoniker = ".NETCoreApp,Version=v5.0",
                ReferencePaths = null,
            };

            Action act = () => task.Execute();
            act.Should().Throw<NullReferenceException>();
        }

        [Fact]
        public void CheckForUnsupportedWinMDReferences_EmptyItemSpec_HandlesGracefully()
        {
            var task = new CheckForUnsupportedWinMDReferences
            {
                BuildEngine = new MockBuildEngine(),
                TargetFrameworkMoniker = ".NETCoreApp,Version=v5.0",
                ReferencePaths = new ITaskItem[]
                {
                    new MockTaskItem("", new Dictionary<string, string>()),
                },
            };

            // Path.GetExtension("") returns "" which does not match ".winmd"
            task.Execute().Should().BeTrue("empty ItemSpec has no .winmd extension");
        }

        [Fact]
        public void CheckForUnsupportedWinMDReferences_WinMDExtensionOnly_LogsError()
        {
            var task = new CheckForUnsupportedWinMDReferences
            {
                BuildEngine = new MockBuildEngine(),
                TargetFrameworkMoniker = ".NETCoreApp,Version=v5.0",
                ReferencePaths = new ITaskItem[]
                {
                    new MockTaskItem(".winmd", new Dictionary<string, string>()),
                },
            };

            // Path.GetExtension(".winmd") returns ".winmd", triggering the WinMD check path.
            // The task logs an error for the unsupported WinMD reference.
            task.Execute().Should().BeFalse("a bare .winmd reference triggers an error");
        }

        [Fact]
        public void CheckForUnsupportedWinMDReferences_ErrorPath_OutputParity()
        {
            // Uses .winmd ItemSpec to trigger the error-logging path.
            // Verifies that error count and messages match between sequential and concurrent execution.

            // --- Sequential baseline ---
            var baselineEngine = new MockBuildEngine();
            var baselineTask = new CheckForUnsupportedWinMDReferences
            {
                BuildEngine = baselineEngine,
                TargetFrameworkMoniker = ".NETCoreApp,Version=v5.0",
                ReferencePaths = new ITaskItem[]
                {
                    new MockTaskItem("Windows.Foundation.winmd", new Dictionary<string, string>()),
                },
            };

            bool baselineResult = baselineTask.Execute();
            int baselineErrorCount = baselineEngine.Errors.Count;
            var baselineErrorMessages = baselineEngine.Errors.Select(e => e.Message).OrderBy(s => s).ToArray();

            baselineResult.Should().BeFalse("the baseline should log an error for WinMD references");
            baselineErrorCount.Should().BeGreaterThan(0);

            // --- Concurrent execution ---
            var errors = new ConcurrentBag<string>();
            var barrier = new Barrier(ParityParallelism);

            Parallel.For(0, ParityParallelism, new ParallelOptions { MaxDegreeOfParallelism = ParityParallelism }, i =>
            {
                try
                {
                    var engine = new MockBuildEngine();
                    var task = new CheckForUnsupportedWinMDReferences
                    {
                        BuildEngine = engine,
                        TargetFrameworkMoniker = ".NETCoreApp,Version=v5.0",
                        ReferencePaths = new ITaskItem[]
                        {
                            new MockTaskItem("Windows.Foundation.winmd", new Dictionary<string, string>()),
                        },
                    };

                    barrier.SignalAndWait();
                    bool result = task.Execute();

                    if (result != baselineResult)
                    {
                        errors.Add($"Thread {i}: Execute() returned {result}, expected {baselineResult}");
                    }
                    if (engine.Errors.Count != baselineErrorCount)
                    {
                        errors.Add($"Thread {i}: Error count {engine.Errors.Count} != expected {baselineErrorCount}");
                    }

                    var actualErrorMessages = engine.Errors.Select(e => e.Message).OrderBy(s => s).ToArray();
                    if (!actualErrorMessages.SequenceEqual(baselineErrorMessages))
                    {
                        errors.Add($"Thread {i}: Error messages differ: [{string.Join(", ", actualErrorMessages)}] vs [{string.Join(", ", baselineErrorMessages)}]");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Thread {i}: {ex.Message}");
                }
            });

            errors.Should().BeEmpty();
        }

        [Fact]
        public void CheckForDuplicateFrameworkReferences_ErrorPath_OutputParity()
        {
            // Uses 2+ explicit duplicates of the same name to trigger the LogError path
            // (FrameworkReferenceDuplicateError). Verifies parity between sequential and concurrent.

            ITaskItem[] CreateInputWithMultipleExplicitDuplicates()
            {
                return new ITaskItem[]
                {
                    new MockTaskItem("Microsoft.AspNetCore.App", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "false" }
                    }),
                    new MockTaskItem("Microsoft.AspNetCore.App", new Dictionary<string, string>
                    {
                        { MetadataKeys.IsImplicitlyDefined, "false" }
                    }),
                };
            }

            // --- Sequential baseline ---
            var baselineEngine = new MockBuildEngine();
            var baselineTask = new CheckForDuplicateFrameworkReferences
            {
                BuildEngine = baselineEngine,
                MoreInformationLink = "https://example.com/error-parity",
                FrameworkReferences = CreateInputWithMultipleExplicitDuplicates(),
            };

            bool baselineResult = baselineTask.Execute();
            var baselineItemsToAdd = baselineTask.ItemsToAdd;
            var baselineItemsToRemove = baselineTask.ItemsToRemove;
            int baselineErrorCount = baselineEngine.Errors.Count;
            var baselineErrorMessages = baselineEngine.Errors.Select(e => e.Message).OrderBy(s => s).ToArray();

            baselineErrorCount.Should().BeGreaterThan(0, "multiple explicit duplicates should trigger an error");

            // --- Concurrent execution ---
            var errors = new ConcurrentBag<string>();
            var barrier = new Barrier(ParityParallelism);

            Parallel.For(0, ParityParallelism, new ParallelOptions { MaxDegreeOfParallelism = ParityParallelism }, i =>
            {
                try
                {
                    var engine = new MockBuildEngine();
                    var task = new CheckForDuplicateFrameworkReferences
                    {
                        BuildEngine = engine,
                        MoreInformationLink = "https://example.com/error-parity",
                        FrameworkReferences = CreateInputWithMultipleExplicitDuplicates(),
                    };

                    barrier.SignalAndWait();
                    bool result = task.Execute();

                    if (result != baselineResult)
                    {
                        errors.Add($"Thread {i}: Execute() returned {result}, expected {baselineResult}");
                        return;
                    }

                    if (engine.Errors.Count != baselineErrorCount)
                    {
                        errors.Add($"Thread {i}: Error count {engine.Errors.Count}, expected {baselineErrorCount}");
                    }

                    var actualErrorMessages = engine.Errors.Select(e => e.Message).OrderBy(s => s).ToArray();
                    if (!actualErrorMessages.SequenceEqual(baselineErrorMessages))
                    {
                        errors.Add($"Thread {i}: Error messages differ");
                    }

                    AssertTaskItemArraysEqual(baselineItemsToAdd, task.ItemsToAdd, $"Thread {i} ItemsToAdd");
                    AssertTaskItemArraysEqual(baselineItemsToRemove, task.ItemsToRemove, $"Thread {i} ItemsToRemove");
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
