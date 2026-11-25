// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests
{
    [TestClass]
    public class ComputeReferenceStaticWebAssetItemsTest
    {
        [TestMethod]
        [DataRow(StaticWebAsset.AssetModes.Local, 1)]
        [DataRow(StaticWebAsset.AssetModes.All, 3)]
        [DataRow(StaticWebAsset.AssetModes.CurrentProject, 3)]
        [DataRow(StaticWebAsset.AssetModes.Reference, 3)]
        public void FiltersDiscoveryRootsContainingLocalFiles(string assetMode, int expectedPatternCount)
        {
            var local = CreateCandidate(Path.Combine("wwwroot", "private", "local.js"), "MyPackage", "Discovered",
                "private/local.js", "All", assetMode);
            ITaskItem[] patterns =
            [
                CreatePatternCandidate("root", "base", Path.GetFullPath("wwwroot"), "**", "MyPackage"),
                CreatePatternCandidate("private", "base/private", Path.GetFullPath(Path.Combine("wwwroot", "private")), "**", "MyPackage"),
                CreatePatternCandidate("public", "base/public", Path.GetFullPath(Path.Combine("wwwroot", "public")), "**", "MyPackage")
            ];
            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = Mock.Of<IBuildEngine>(),
                Source = "MyPackage",
                Assets = [local],
                Patterns = patterns,
                AssetKind = "Build",
                ProjectMode = "Root"
            };

            task.Execute().Should().BeTrue();
            task.DiscoveryPatterns.Should().HaveCount(expectedPatternCount);
            task.DiscoveryPatterns.Should().Contain(p => p.ItemSpec == "public");
            task.Patterns.Should().HaveCount(3);
        }

        [TestMethod]
        public void ComputedLocalAssetsDoNotDisableWebRootDiscovery()
        {
            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = Mock.Of<IBuildEngine>(),
                Source = "MyPackage",
                Assets = [CreateCandidate(Path.Combine("obj", "local.js"), "MyPackage", "Computed",
                    "local.js", "All", StaticWebAsset.AssetModes.Local)],
                Patterns = [CreatePatternCandidate("wwwroot", "base", Path.GetFullPath("wwwroot"), "**", "MyPackage")],
                AssetKind = "Build",
                ProjectMode = "Root"
            };

            task.Execute().Should().BeTrue();
            task.DiscoveryPatterns.Should().ContainSingle();
        }

        [TestMethod]
        [DataRow("Default", "Build")]
        [DataRow("Root", "Build")]
        [DataRow("SelfContained", "Build")]
        [DataRow("Default", "Publish")]
        [DataRow("Root", "Publish")]
        [DataRow("SelfContained", "Publish")]
        public void FiltersLocalAssetsAndTheirEndpoints(string projectMode, string assetKind)
        {
            var local = CreateCandidate(Path.Combine("obj", "local.js"), "MyPackage", "Computed",
                "local.js", "All", StaticWebAsset.AssetModes.Local);
            var compressed = CreateCandidate(Path.Combine("obj", "local.js.gz"), "MyPackage", "Computed",
                "local.js.gz", "All", StaticWebAsset.AssetModes.Local);
            compressed.SetMetadata("AssetRole", StaticWebAsset.AssetRoles.Alternative);
            compressed.SetMetadata("RelatedAsset", local.ItemSpec);
            compressed.SetMetadata("AssetTraitName", "Content-Encoding");
            compressed.SetMetadata("AssetTraitValue", "gzip");
            var shared = CreateCandidate(Path.Combine("wwwroot", "shared.js"), "MyPackage", "Discovered",
                "shared.js", "All", "All");
            ITaskItem[] assets = [local, compressed, shared];
            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = Mock.Of<IBuildEngine>(),
                Source = "MyPackage",
                Assets = assets,
                AssetKind = assetKind,
                ProjectMode = projectMode
            };

            task.Execute().Should().BeTrue();
            task.StaticWebAssets.Should().NotContain(a => a.GetMetadata("AssetMode") == StaticWebAsset.AssetModes.Local);
            task.StaticWebAssets.Should().HaveCount(projectMode == "SelfContained" ? 0 : 1);

            var endpoints = new ComputeEndpointsForReferenceStaticWebAssets
            {
                BuildEngine = task.BuildEngine,
                Assets = task.StaticWebAssets,
                CandidateEndpoints = assets.Select(a => new StaticWebAssetEndpoint
                {
                    Route = a.GetMetadata("RelativePath"),
                    AssetFile = a.ItemSpec,
                    Selectors = [],
                    ResponseHeaders = [],
                    EndpointProperties = []
                }.ToTaskItem()).ToArray()
            };

            endpoints.Execute().Should().BeTrue();
            endpoints.Endpoints.Should().HaveCount(task.StaticWebAssets.Length);
            endpoints.Endpoints.Should().OnlyContain(e => e.GetMetadata("AssetFile") == shared.ItemSpec);
        }

        [TestMethod]
        public void IncludesAssetsFromCurrentProjectAsReferencedAssets()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "MyPackage", "Discovered", "candidate.js", "All", "All") },
                Patterns = new ITaskItem[] { },
                AssetKind = "Build",
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(1);
        }

        [TestMethod]
        public void IncludesPatternsFromCurrentProject()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "MyPackage", "Discovered", "candidate.js", "All", "All") },
                Patterns = new[] { CreatePatternCandidate("MyPackage\\wwwroot", "base", Directory.GetCurrentDirectory(), "wwwroot\\**", "MyPackage") },
                AssetKind = "Build",
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.DiscoveryPatterns.Should().HaveCount(1);
        }

        [TestMethod]
        public void FiltersPatternsFromReferencedProjects()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "MyPackage", "Discovered", "candidate.js", "All", "All") },
                Patterns = new[] { CreatePatternCandidate("Other\\wwwroot", "base", Directory.GetCurrentDirectory(), "wwwroot\\**", "Other") },
                AssetKind = "Build",
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.DiscoveryPatterns.Should().HaveCount(0);
        }

        [TestMethod]
        public void PrefersSpecificKindAssetsOverAllKindAssets()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[]
                {
                    CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "MyPackage", "Discovered", "candidate.js", "All", "All"),
                    CreateCandidate(Path.Combine("wwwroot", "candidate.other.js"), "MyPackage", "Discovered", "candidate.js", "Build", "All")
                },
                Patterns = new ITaskItem[] { },
                AssetKind = "Build",
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(1);
            task.StaticWebAssets[0].ItemSpec.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.other.js")));
        }

        [TestMethod]
        public void AllAssetGetsIgnoredWhenBuildAndPublishAssetsAreDefined()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[]
                {
                    CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "MyPackage", "Discovered", "candidate.js", "All", "All"),
                    CreateCandidate(Path.Combine("wwwroot", "candidate.other.js"), "MyPackage", "Discovered", "candidate.js", "Build", "All"),
                    CreateCandidate(Path.Combine("wwwroot", "candidate.publish.js"), "MyPackage", "Discovered", "candidate.js", "Publish", "All")
                },
                Patterns = new ITaskItem[] { },
                AssetKind = "Build",
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(1);
            task.StaticWebAssets[0].ItemSpec.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.other.js")));
        }

        [TestMethod]
        [DataRow("Build", "Publish")]
        [DataRow("Publish", "Build")]
        public void FiltersAssetsForOppositeKind(string assetKind, string manifestKind)
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "MyPackage", "Discovered", "candidate.js", assetKind, "All") },
                Patterns = new ITaskItem[] { },
                AssetKind = manifestKind,
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(0);
        }

        [TestMethod]
        public void FiltersCurrentProjectOnlyAssetsInDefaultMode()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "MyPackage", "Discovered", "candidate.js", "All", "CurrentProject") },
                Patterns = new ITaskItem[] { },
                AssetKind = "Default",
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(0);
        }

        [TestMethod]
        public void IncludesReferenceAssetsInDefaultMode()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "MyPackage", "Discovered", "candidate.js", "All", "Reference") },
                Patterns = new ITaskItem[] { },
                AssetKind = "Default",
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(1);
        }

        [TestMethod]
        public void IncludesCurrentProjectAssetsInRootMode()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "MyPackage", "Discovered", "candidate.js", "All", "CurrentProject") },
                Patterns = new ITaskItem[] { },
                AssetKind = "Default",
                ProjectMode = "Root"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(1);
        }

        [TestMethod]
        public void FiltersReferenceOnlyAssetsInRootMode()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "MyPackage", "Discovered", "candidate.js", "All", "Reference") },
                Patterns = new ITaskItem[] { },
                AssetKind = "Default",
                ProjectMode = "Root"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(0);
        }

        [TestMethod]
        public void FiltersAssetsFromOtherProjects()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "Other", "Project", "candidate.js", "All", "All") },
                Patterns = new ITaskItem[] { },
                AssetKind = "Build",
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(0);
        }

        [TestMethod]
        public void FiltersAssetsFromPackages()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "Other", "Package", "candidate.js", "All", "All") },
                Patterns = new ITaskItem[] { },
                AssetKind = "Build",
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(0);
        }

        [TestMethod]
        public void AppliesFrameworkPatternToDiscoveredAssets()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[]
                {
                    CreateCandidate(Path.Combine("wwwroot", "framework.js"), "MyPackage", "Discovered", "framework.js", "All", "All"),
                    CreateCandidate(Path.Combine("wwwroot", "app.css"), "MyPackage", "Discovered", "app.css", "All", "All")
                },
                Patterns = new ITaskItem[] { },
                AssetKind = "Build",
                ProjectMode = "Default",
                FrameworkPattern = "**/*.js"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(2);
            task.StaticWebAssets[0].GetMetadata("SourceType").Should().Be("Framework");
            task.StaticWebAssets[1].GetMetadata("SourceType").Should().Be("Project");
        }

        [TestMethod]
        public void FrameworkPatternDoesNotAffectNonDiscoveredAssets()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "MyPackage", "Project", "candidate.js", "All", "All") },
                Patterns = new ITaskItem[] { },
                AssetKind = "Build",
                ProjectMode = "Default",
                UpdateSourceType = false,
                FrameworkPattern = "**/*.js"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(1);
            task.StaticWebAssets[0].GetMetadata("SourceType").Should().Be("Project");
        }

        [TestMethod]
        public void PreservesAssetGroupsOnFrameworkAssets()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var asset = CreateCandidate(Path.Combine("wwwroot", "framework.js"), "MyPackage", "Framework", "framework.js", "All", "All");
            asset.SetMetadata("AssetGroups", "MyGroup");

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { asset },
                Patterns = new ITaskItem[] { },
                AssetKind = "Build",
                ProjectMode = "Default",
                UpdateSourceType = true
            };

            // Act
            var result = task.Execute();

            // Assert — groups are preserved here so FilterByGroup can evaluate them;
            // clearing happens later during MaterializeFrameworkAsset.
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(1);
            task.StaticWebAssets[0].GetMetadata("SourceType").Should().Be("Framework");
            task.StaticWebAssets[0].GetMetadata("AssetGroups").Should().Be("MyGroup");
        }

        // ResourceLock cannot protect unrelated tests that implicitly read the process current directory which this test mutates.
        [DoNotParallelize]
        [TestMethod]
        public void MakeReferencedAssetOriginalItemSpecAbsolute_ResolvesAgainstProjectDirectoryNotProcessCurrentDirectory()
        {
            var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(ComputeReferenceStaticWebAssetItemsTest), Guid.NewGuid().ToString("N"));
            var projectDir = Path.Combine(testRoot, "project");
            var spawnDir = Path.Combine(testRoot, "decoy", "spawn");
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(spawnDir);

            var relativeOriginalItemSpec = Path.Combine("wwwroot", "candidate.js");

            var projectRootedOriginalItemSpec = Path.GetFullPath(Path.Combine(projectDir, relativeOriginalItemSpec));
            var spawnRootedOriginalItemSpec = Path.GetFullPath(Path.Combine(spawnDir, relativeOriginalItemSpec));
            projectRootedOriginalItemSpec.Should().NotBe(spawnRootedOriginalItemSpec,
                "the test setup must place project and decoy in different parents so a relative path resolves differently against each");

            var originalCurrentDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(spawnDir);

                var errorMessages = new List<string>();
                var buildEngine = new Mock<IBuildEngine>();
                buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                    .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

                var task = new ComputeReferenceStaticWebAssetItems
                {
                    BuildEngine = buildEngine.Object,
                    TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                    Source = "MyPackage",
                    Assets = new[] { CreateCandidate(relativeOriginalItemSpec, "MyPackage", "Discovered", "candidate.js", "All", "All") },
                    Patterns = new ITaskItem[] { },
                    AssetKind = "Build",
                    ProjectMode = "Default",
                    MakeReferencedAssetOriginalItemSpecAbsolute = true
                };

                var result = task.Execute();

                result.Should().Be(true);
                errorMessages.Should().BeEmpty();
                task.StaticWebAssets.Should().HaveCount(1);

                // OriginalItemSpec must resolve against TaskEnvironment.ProjectDirectory, not the process CWD (the decoy spawnDir).
                task.StaticWebAssets[0].GetMetadata("OriginalItemSpec").Should().Be(projectRootedOriginalItemSpec);
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

        private static ITaskItem CreateCandidate(
            string itemSpec,
            string sourceId,
            string sourceType,
            string relativePath,
            string assetKind,
            string assetMode)
        {
            var result = new StaticWebAsset()
            {
                Identity = Path.GetFullPath(itemSpec),
                SourceId = sourceId,
                SourceType = sourceType,
                ContentRoot = Directory.GetCurrentDirectory(),
                BasePath = "base",
                RelativePath = relativePath,
                AssetKind = assetKind,
                AssetMode = assetMode,
                AssetRole = "Primary",
                RelatedAsset = "",
                AssetTraitName = "",
                AssetTraitValue = "",
                CopyToOutputDirectory = "",
                CopyToPublishDirectory = "",
                OriginalItemSpec = itemSpec,
                // Add these to avoid accessing the disk to compute them
                Integrity = "integrity",
                Fingerprint = "fingerprint",
                FileLength = 10,
                LastWriteTime = DateTime.UtcNow,
            };

            result.ApplyDefaults();
            result.Normalize();

            return result.ToTaskItem();
        }

        private static ITaskItem CreatePatternCandidate(
            string name,
            string basePath,
            string contentRoot,
            string pattern,
            string source)
        {
            var result = new StaticWebAssetsDiscoveryPattern()
            {
                Name = name,
                BasePath = basePath,
                ContentRoot = contentRoot,
                Pattern = pattern,
                Source = source
            };

            return result.ToTaskItem();
        }
    }
}
