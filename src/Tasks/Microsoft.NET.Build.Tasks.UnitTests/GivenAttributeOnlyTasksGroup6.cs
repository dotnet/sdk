// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Reflection;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    /// <summary>
    /// Behavioral and attribute-presence tests for tasks in merge-group-6.
    /// GetDefaultPlatformTargetForNetFramework, GetEmbeddedApphostPaths, and
    /// GetNuGetShortFolderName are attribute-only. ProduceContentAssets and
    /// ResolveCopyLocalAssets were migrated to Pattern B (IMultiThreadableTask).
    /// </summary>
    public class GivenAttributeOnlyTasksGroup6
    {
        #region Attribute Presence

        [Fact]
        public void GetDefaultPlatformTargetForNetFramework_HasMultiThreadableAttribute()
        {
            typeof(GetDefaultPlatformTargetForNetFramework).GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>()
                .Should().NotBeNull("task must be decorated with [MSBuildMultiThreadableTask]");
        }

        [Fact]
        public void GetEmbeddedApphostPaths_HasMultiThreadableAttribute()
        {
            typeof(GetEmbeddedApphostPaths).GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>()
                .Should().NotBeNull("task must be decorated with [MSBuildMultiThreadableTask]");
        }

        [Fact]
        public void GetNuGetShortFolderName_HasMultiThreadableAttribute()
        {
            typeof(GetNuGetShortFolderName).GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>()
                .Should().NotBeNull("task must be decorated with [MSBuildMultiThreadableTask]");
        }

        [Fact]
        public void ProduceContentAssets_HasMultiThreadableAttribute()
        {
            typeof(ProduceContentAssets).GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>()
                .Should().NotBeNull("task must be decorated with [MSBuildMultiThreadableTask]");
        }

        [Fact]
        public void ResolveCopyLocalAssets_HasMultiThreadableAttribute()
        {
            typeof(ResolveCopyLocalAssets).GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>()
                .Should().NotBeNull("task must be decorated with [MSBuildMultiThreadableTask]");
        }

        #endregion

        #region GetDefaultPlatformTargetForNetFramework

        [Fact]
        public void GetDefaultPlatformTargetForNetFramework_ReturnsAnyCPU_WhenNoNativeAssets()
        {
            var task = new GetDefaultPlatformTargetForNetFramework
            {
                BuildEngine = new MockBuildEngine(),
                PackageDependencies = Array.Empty<ITaskItem>(),
                NativeCopyLocalItems = Array.Empty<ITaskItem>()
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.DefaultPlatformTarget.Should().Be("AnyCPU");
        }

        [Fact]
        public void GetDefaultPlatformTargetForNetFramework_ReturnsAnyCPU_WhenNullNativeAssets()
        {
            var task = new GetDefaultPlatformTargetForNetFramework
            {
                BuildEngine = new MockBuildEngine(),
                PackageDependencies = null,
                NativeCopyLocalItems = null
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.DefaultPlatformTarget.Should().Be("AnyCPU");
        }

        [Fact]
        public void GetDefaultPlatformTargetForNetFramework_ReturnsX86_WhenPlatformsPackagePresent()
        {
            var platformsPkg = new TaskItem("Microsoft.NETCore.Platforms");
            var nativeItem = new MockTaskItem("native.dll", new Dictionary<string, string>
            {
                { "PathInPackage", "runtimes/win-x64/native/native.dll" }
            });

            var task = new GetDefaultPlatformTargetForNetFramework
            {
                BuildEngine = new MockBuildEngine(),
                PackageDependencies = new ITaskItem[] { platformsPkg },
                NativeCopyLocalItems = new ITaskItem[] { nativeItem }
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.DefaultPlatformTarget.Should().Be("x86",
                "presence of NETCore.Platforms package with native assets implies x86");
        }

        [Fact]
        public void GetDefaultPlatformTargetForNetFramework_ReturnsX86_WhenWin7X86NativeAsset()
        {
            var nativeItem = new MockTaskItem("native.dll", new Dictionary<string, string>
            {
                { "PathInPackage", "runtimes/win7-x86/native/native.dll" }
            });

            var task = new GetDefaultPlatformTargetForNetFramework
            {
                BuildEngine = new MockBuildEngine(),
                PackageDependencies = Array.Empty<ITaskItem>(),
                NativeCopyLocalItems = new ITaskItem[] { nativeItem }
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.DefaultPlatformTarget.Should().Be("x86",
                "win7-x86 native assets without Platforms package should still return x86");
        }

        [Fact]
        public void GetDefaultPlatformTargetForNetFramework_ReturnsAnyCPU_WhenNonX86NativeAsset()
        {
            var nativeItem = new MockTaskItem("native.dll", new Dictionary<string, string>
            {
                { "PathInPackage", "runtimes/win-x64/native/native.dll" }
            });

            var task = new GetDefaultPlatformTargetForNetFramework
            {
                BuildEngine = new MockBuildEngine(),
                PackageDependencies = Array.Empty<ITaskItem>(),
                NativeCopyLocalItems = new ITaskItem[] { nativeItem }
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.DefaultPlatformTarget.Should().Be("AnyCPU",
                "non-win7-x86 native assets without Platforms package should return AnyCPU");
        }

        #endregion

        #region GetEmbeddedApphostPaths

        [Fact]
        public void GetEmbeddedApphostPaths_ProducesPathsForEachRid()
        {
            var rids = new[]
            {
                new TaskItem("win-x64"),
                new TaskItem("linux-x64"),
                new TaskItem("osx-x64")
            };

            var task = new GetEmbeddedApphostPaths
            {
                BuildEngine = new MockBuildEngine(),
                ToolCommandName = "mytool",
                PackagedShimOutputDirectory = "shims",
                ShimRuntimeIdentifiers = rids
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.EmbeddedApphostPaths.Should().HaveCount(3);
        }

        [Fact]
        public void GetEmbeddedApphostPaths_WindowsRid_HasExeExtension()
        {
            var rids = new[] { new TaskItem("win-x64") };

            var task = new GetEmbeddedApphostPaths
            {
                BuildEngine = new MockBuildEngine(),
                ToolCommandName = "mytool",
                PackagedShimOutputDirectory = "shims",
                ShimRuntimeIdentifiers = rids
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.EmbeddedApphostPaths.Should().HaveCount(1);
            task.EmbeddedApphostPaths[0].ItemSpec.Should().EndWith(".exe");
            task.EmbeddedApphostPaths[0].GetMetadata("ShimRuntimeIdentifier").Should().Be("win-x64");
        }

        [Fact]
        public void GetEmbeddedApphostPaths_LinuxRid_HasNoExtension()
        {
            var rids = new[] { new TaskItem("linux-x64") };

            var task = new GetEmbeddedApphostPaths
            {
                BuildEngine = new MockBuildEngine(),
                ToolCommandName = "mytool",
                PackagedShimOutputDirectory = "shims",
                ShimRuntimeIdentifiers = rids
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.EmbeddedApphostPaths[0].ItemSpec.Should().NotEndWith(".exe");
            task.EmbeddedApphostPaths[0].ItemSpec.Should().EndWith("mytool");
        }

        [Fact]
        public void GetEmbeddedApphostPaths_CombinesOutputDirRidAndToolName()
        {
            var rids = new[] { new TaskItem("win-x64") };

            var task = new GetEmbeddedApphostPaths
            {
                BuildEngine = new MockBuildEngine(),
                ToolCommandName = "mytool",
                PackagedShimOutputDirectory = "output",
                ShimRuntimeIdentifiers = rids
            };

            task.Execute();

            var path = task.EmbeddedApphostPaths[0].ItemSpec;
            path.Should().Contain("output");
            path.Should().Contain("win-x64");
            path.Should().Contain("mytool");
        }

        #endregion

        #region GetNuGetShortFolderName

        [Fact]
        public void GetNuGetShortFolderName_ReturnsNet80()
        {
            var task = new GetNuGetShortFolderName
            {
                BuildEngine = new MockBuildEngine(),
                TargetFrameworkMoniker = ".NETCoreApp,Version=v8.0"
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.NuGetShortFolderName.Should().Be("net8.0");
        }

        [Fact]
        public void GetNuGetShortFolderName_ReturnsNetStandard20()
        {
            var task = new GetNuGetShortFolderName
            {
                BuildEngine = new MockBuildEngine(),
                TargetFrameworkMoniker = ".NETStandard,Version=v2.0"
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.NuGetShortFolderName.Should().Be("netstandard2.0");
        }

        [Fact]
        public void GetNuGetShortFolderName_ReturnsNet472()
        {
            var task = new GetNuGetShortFolderName
            {
                BuildEngine = new MockBuildEngine(),
                TargetFrameworkMoniker = ".NETFramework,Version=v4.7.2"
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.NuGetShortFolderName.Should().Be("net472");
        }

        [Fact]
        public void GetNuGetShortFolderName_WithPlatformMoniker_IncludesPlatform()
        {
            var task = new GetNuGetShortFolderName
            {
                BuildEngine = new MockBuildEngine(),
                TargetFrameworkMoniker = ".NETCoreApp,Version=v8.0",
                TargetPlatformMoniker = "Windows,Version=10.0.19041.0"
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.NuGetShortFolderName.Should().Contain("windows");
        }

        #endregion

        #region ProduceContentAssets

        [Fact]
        public void ProduceContentAssets_WithCompileAsset_ProducesContentItem()
        {
            var contentFile = new MockTaskItem("path/to/content.cs", new Dictionary<string, string>
            {
                { "NuGetPackageId", "MyPackage" },
                { "NuGetPackageVersion", "1.0.0" },
                { "BuildAction", "Compile" },
                { "CodeLanguage", "any" },
                { "CopyToOutput", "false" },
                { "PPOutputPath", "" },
                { "OutputPath", "" }
            });

            var task = new ProduceContentAssets
            {
                BuildEngine = new MockBuildEngine(),
                ContentFileDependencies = new ITaskItem[] { contentFile },
                ProjectLanguage = "C#"
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.ProcessedContentItems.Should().HaveCount(1);
            task.ProcessedContentItems[0].GetMetadata("ProcessedItemType").Should().Be("Compile");
        }

        [Fact]
        public void ProduceContentAssets_WithNoneBuildAction_ProducesNoContentItem()
        {
            var contentFile = new MockTaskItem("path/to/readme.txt", new Dictionary<string, string>
            {
                { "NuGetPackageId", "MyPackage" },
                { "NuGetPackageVersion", "1.0.0" },
                { "BuildAction", "None" },
                { "CodeLanguage", "any" },
                { "CopyToOutput", "false" },
                { "PPOutputPath", "" },
                { "OutputPath", "" }
            });

            var task = new ProduceContentAssets
            {
                BuildEngine = new MockBuildEngine(),
                ContentFileDependencies = new ITaskItem[] { contentFile },
                ProjectLanguage = "C#"
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.ProcessedContentItems.Should().BeEmpty(
                "items with BuildAction=None should not produce content items");
        }

        [Fact]
        public void ProduceContentAssets_WithCopyToOutput_ProducesCopyLocalItem()
        {
            var contentFile = new MockTaskItem("path/to/data.json", new Dictionary<string, string>
            {
                { "NuGetPackageId", "MyPackage" },
                { "NuGetPackageVersion", "1.0.0" },
                { "BuildAction", "Content" },
                { "CodeLanguage", "any" },
                { "CopyToOutput", "true" },
                { "PPOutputPath", "" },
                { "OutputPath", "data.json" }
            });

            var task = new ProduceContentAssets
            {
                BuildEngine = new MockBuildEngine(),
                ContentFileDependencies = new ITaskItem[] { contentFile },
                ProjectLanguage = "C#"
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.CopyLocalItems.Should().HaveCount(1);
            task.CopyLocalItems[0].GetMetadata("TargetPath").Should().Be("data.json");
        }

        [Fact]
        public void ProduceContentAssets_FiltersLanguageSpecificAssets()
        {
            var csharpFile = new MockTaskItem("path/to/code.cs", new Dictionary<string, string>
            {
                { "NuGetPackageId", "MyPackage" },
                { "NuGetPackageVersion", "1.0.0" },
                { "BuildAction", "Compile" },
                { "CodeLanguage", "cs" },
                { "CopyToOutput", "false" },
                { "PPOutputPath", "" },
                { "OutputPath", "" }
            });

            var vbFile = new MockTaskItem("path/to/code.vb", new Dictionary<string, string>
            {
                { "NuGetPackageId", "MyPackage" },
                { "NuGetPackageVersion", "1.0.0" },
                { "BuildAction", "Compile" },
                { "CodeLanguage", "vb" },
                { "CopyToOutput", "false" },
                { "PPOutputPath", "" },
                { "OutputPath", "" }
            });

            var task = new ProduceContentAssets
            {
                BuildEngine = new MockBuildEngine(),
                ContentFileDependencies = new ITaskItem[] { csharpFile, vbFile },
                ProjectLanguage = "C#"
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.ProcessedContentItems.Should().HaveCount(1,
                "only C# language-specific content should be included for a C# project");
        }

        [Fact]
        public void ProduceContentAssets_EmptyDependencies_Succeeds()
        {
            var task = new ProduceContentAssets
            {
                BuildEngine = new MockBuildEngine(),
                ContentFileDependencies = Array.Empty<ITaskItem>(),
                ProjectLanguage = "C#"
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.ProcessedContentItems.Should().BeEmpty();
            task.CopyLocalItems.Should().BeEmpty();
            task.FileWrites.Should().BeEmpty();
        }

        #endregion

        #region ResolveCopyLocalAssets

        [Fact]
        public void ResolveCopyLocalAssets_WithMinimalAssetsFile_Succeeds()
        {
            var projectDir = Path.Combine(Path.GetTempPath(), $"resolve-copy-{Guid.NewGuid():N}");
            Directory.CreateDirectory(projectDir);
            try
            {
                var assetsContent = @"{
                    ""version"": 3,
                    ""targets"": { "".NETCoreApp,Version=v8.0"": {} },
                    ""libraries"": {},
                    ""packageFolders"": {},
                    ""projectFileDependencyGroups"": { "".NETCoreApp,Version=v8.0"": [] },
                    ""project"": { ""version"": ""1.0.0"", ""frameworks"": { ""net8.0"": {} } }
                }";
                var assetsPath = Path.Combine(projectDir, "project.assets.json");
                File.WriteAllText(assetsPath, assetsContent);

                var task = new ResolveCopyLocalAssets
                {
                    BuildEngine = new MockBuildEngine(),
                    AssetsFilePath = assetsPath,
                    TargetFramework = ".NETCoreApp,Version=v8.0",
                    RuntimeIdentifier = "",
                    IsSelfContained = false,
                    ResolveRuntimeTargets = false
                };

                var result = task.Execute();

                result.Should().BeTrue();
                task.ResolvedAssets.Should().BeEmpty("no packages in the lock file means no assets");
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }

        #endregion
    }
}
