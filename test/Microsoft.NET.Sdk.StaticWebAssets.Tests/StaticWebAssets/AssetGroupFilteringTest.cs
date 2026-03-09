// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

/// <summary>
/// Unit tests for asset-group filtering logic across UpdatePackageStaticWebAssets,
/// UpdateExternallyDefinedStaticWebAssets, ComputeReferenceStaticWebAssetItems and
/// DefineStaticWebAssets.ApplyGroupDefinitions.
/// </summary>
public class AssetGroupFilteringTest : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IBuildEngine> _buildEngine;
    private readonly List<string> _errorMessages;
    private readonly List<string> _logMessages;

    public AssetGroupFilteringTest()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AssetGroupTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _errorMessages = new List<string>();
        _logMessages = new List<string>();
        _buildEngine = new Mock<IBuildEngine>();
        _buildEngine.Setup(e => e.ProjectFileOfTaskNode).Returns(Path.Combine(_tempDir, "test.csproj"));
        _buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => _errorMessages.Add(args.Message));
        _buildEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
            .Callback<BuildMessageEventArgs>(args => _logMessages.Add(args.Message));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // UpdatePackageStaticWebAssets – Group Filtering
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void UpdatePackage_AssetWithGroups_MatchingDeclaration_IsIncluded()
    {
        var file = CreateTempFile("v5", "css", "site.css", "body{}");
        var asset = CreatePackageAssetWithGroups(file, "IdentityUI", "_content/id", "css/site.css", "BootstrapVersion=V5");

        var task = new UpdatePackageStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { asset },
            StaticWebAssetGroups = new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V5",
                    ["SourceId"] = "IdentityUI"
                })
            },
            IntermediateOutputPath = Path.Combine(_tempDir, "obj"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.UpdatedAssets.Should().HaveCount(1);
    }

    [Fact]
    public void UpdatePackage_AssetWithGroups_NoDeclarations_IsExcluded()
    {
        var file = CreateTempFile("v5", "css", "site.css", "body{}");
        var asset = CreatePackageAssetWithGroups(file, "IdentityUI", "_content/id", "css/site.css", "BootstrapVersion=V5");

        var task = new UpdatePackageStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { asset },
            // No StaticWebAssetGroups declared at all
            IntermediateOutputPath = Path.Combine(_tempDir, "obj"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.UpdatedAssets.Should().HaveCount(0, "grouped assets should be excluded when no group declarations exist");
        task.OriginalAssets.Should().HaveCount(1, "excluded assets should still appear in OriginalAssets for removal");
    }

    [Fact]
    public void UpdatePackage_AssetWithMultiGroup_PartialMatch_IsExcluded()
    {
        var file = CreateTempFile("v5", "css", "site.css", "body{}");
        var asset = CreatePackageAssetWithGroups(
            file, "IdentityUI", "_content/id", "css/site.css",
            "BootstrapVersion=V5;DebugAssets=true");

        var task = new UpdatePackageStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { asset },
            StaticWebAssetGroups = new ITaskItem[]
            {
                // Only declares BootstrapVersion, not DebugAssets
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V5",
                    ["SourceId"] = "IdentityUI"
                })
            },
            IntermediateOutputPath = Path.Combine(_tempDir, "obj"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.UpdatedAssets.Should().HaveCount(0, "AND-matching: all group entries must be satisfied");
    }

    [Fact]
    public void UpdatePackage_AssetWithoutGroups_AlwaysIncluded()
    {
        var file = CreateTempFile("plain", "site.js", "alert(1);");
        var asset = CreatePackageAsset(file, "SomeLib", "_content/somelib", "site.js");

        var task = new UpdatePackageStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { asset },
            // Declaring groups for a different library shouldn't affect ungrouped assets
            StaticWebAssetGroups = new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string> { ["Value"] = "V5", ["SourceId"] = "IdentityUI" })
            },
            IntermediateOutputPath = Path.Combine(_tempDir, "obj"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.UpdatedAssets.Should().HaveCount(1, "assets without AssetGroups are always included");
    }

    [Fact]
    public void UpdatePackage_SourceIdScoping_WrongSourceId_IsExcluded()
    {
        var file = CreateTempFile("v5", "css", "site.css", "body{}");
        var asset = CreatePackageAssetWithGroups(file, "IdentityUI", "_content/id", "css/site.css", "BootstrapVersion=V5");

        var task = new UpdatePackageStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { asset },
            StaticWebAssetGroups = new ITaskItem[]
            {
                // Declaration targets a different SourceId
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V5",
                    ["SourceId"] = "OtherLibrary"
                })
            },
            IntermediateOutputPath = Path.Combine(_tempDir, "obj"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.UpdatedAssets.Should().HaveCount(0, "declaration with wrong SourceId should not satisfy the group requirement");
    }

    [Fact]
    public void UpdatePackage_CascadingExclusion_RelatedAssetExcludedWithPrimary()
    {
        var primaryFile = CreateTempFile("v5", "css", "site.css", "body{}");
        var relatedFile = CreateTempFile("v5", "css", "site.css.gz", "compressed");

        var primary = CreatePackageAssetWithGroups(primaryFile, "IdentityUI", "_content/id", "css/site.css", "BootstrapVersion=V5");
        var related = CreatePackageAsset(relatedFile, "IdentityUI", "_content/id", "css/site.css.gz");
        related.SetMetadata("AssetRole", "Alternative");
        related.SetMetadata("AssetTraitName", "Content-Encoding");
        related.SetMetadata("AssetTraitValue", "gzip");
        related.SetMetadata("RelatedAsset", Path.GetFullPath(primaryFile));

        var task = new UpdatePackageStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { primary, related },
            // No group declarations: primary will be excluded
            IntermediateOutputPath = Path.Combine(_tempDir, "obj"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.UpdatedAssets.Should().HaveCount(0, "both primary and its related asset should be excluded via cascading");
    }

    [Fact]
    public void UpdatePackage_EndpointFiltering_ExcludedAssetsEndpointsRemoved()
    {
        var includedFile = CreateTempFile("plain", "app.js", "var x;");
        var excludedFile = CreateTempFile("v5", "css", "site.css", "body{}");

        var includedAsset = CreatePackageAsset(includedFile, "SomeLib", "_content/somelib", "app.js");
        var excludedAsset = CreatePackageAssetWithGroups(excludedFile, "IdentityUI", "_content/id", "css/site.css", "BootstrapVersion=V5");

        var includedEndpoint = new TaskItem("app.js", new Dictionary<string, string>
        {
            ["AssetFile"] = Path.GetFullPath(includedFile),
            ["Selectors"] = "[]",
            ["EndpointProperties"] = "[]",
            ["ResponseHeaders"] = "[]",
        });
        var excludedEndpoint = new TaskItem("css/site.css", new Dictionary<string, string>
        {
            ["AssetFile"] = Path.GetFullPath(excludedFile),
            ["Selectors"] = "[]",
            ["EndpointProperties"] = "[]",
            ["ResponseHeaders"] = "[]",
        });

        var task = new UpdatePackageStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { includedAsset, excludedAsset },
            Endpoints = new[] { includedEndpoint, excludedEndpoint },
            // No group declarations → grouped asset excluded
            IntermediateOutputPath = Path.Combine(_tempDir, "obj"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.FilteredEndpoints.Should().HaveCount(1, "endpoint for excluded asset should be removed");
        task.FilteredEndpoints[0].ItemSpec.Should().Be("app.js");
    }

    // ──────────────────────────────────────────────────────────────────────
    // UpdateExternallyDefinedStaticWebAssets – Group Filtering
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void UpdateExternal_AssetWithGroups_MatchingDeclaration_IsIncluded()
    {
        var file = CreateTempFile("ext", "site.css", "body{}");
        var asset = CreateExternalAssetWithGroups(file, "IdentityUI", "css/site.css", "BootstrapVersion=V5");

        var task = new UpdateExternallyDefinedStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { asset },
            Endpoints = Array.Empty<ITaskItem>(),
            StaticWebAssetGroups = new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V5",
                    ["SourceId"] = "IdentityUI"
                })
            }
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.UpdatedAssets.Should().HaveCount(1);
    }

    [Fact]
    public void UpdateExternal_AssetWithGroups_NoDeclarations_IsExcluded()
    {
        var file = CreateTempFile("ext", "site.css", "body{}");
        var asset = CreateExternalAssetWithGroups(file, "IdentityUI", "css/site.css", "BootstrapVersion=V5");

        var task = new UpdateExternallyDefinedStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { asset },
            Endpoints = Array.Empty<ITaskItem>(),
            // No StaticWebAssetGroups
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.UpdatedAssets.Should().HaveCount(0, "grouped assets should be excluded when no declarations exist");
    }

    [Fact]
    public void UpdateExternal_MultiGroup_PartialMatch_IsExcluded()
    {
        var file = CreateTempFile("ext", "site.css", "body{}");
        var asset = CreateExternalAssetWithGroups(file, "IdentityUI", "css/site.css", "BootstrapVersion=V5;DebugAssets=true");

        var task = new UpdateExternallyDefinedStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { asset },
            Endpoints = Array.Empty<ITaskItem>(),
            StaticWebAssetGroups = new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V5",
                    ["SourceId"] = "IdentityUI"
                })
            }
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.UpdatedAssets.Should().HaveCount(0, "AND-matching requires all entries satisfied");
    }

    [Fact]
    public void UpdateExternal_CascadingExclusion_RelatedAssetExcludedWithPrimary()
    {
        var primaryFile = CreateTempFile("ext", "css", "site.css", "body{}");
        var relatedFile = CreateTempFile("ext", "css", "site.css.gz", "compressed");

        var primary = CreateExternalAssetWithGroups(primaryFile, "IdentityUI", "css/site.css", "BootstrapVersion=V5");
        var related = CreateExternalAsset(relatedFile, "IdentityUI", "css/site.css.gz");
        related.SetMetadata("AssetRole", "Alternative");
        related.SetMetadata("AssetTraitName", "Content-Encoding");
        related.SetMetadata("AssetTraitValue", "gzip");
        related.SetMetadata("RelatedAsset", Path.GetFullPath(primaryFile));

        var task = new UpdateExternallyDefinedStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { primary, related },
            Endpoints = Array.Empty<ITaskItem>(),
            // No declarations → primary excluded → related cascades
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.UpdatedAssets.Should().HaveCount(0, "related asset should cascade-exclude with primary");
    }

    [Fact]
    public void UpdateExternal_EndpointFiltering_ExcludedAssetEndpointsRemoved()
    {
        var includedFile = CreateTempFile("ext", "app.js", "var x;");
        var excludedFile = CreateTempFile("ext2", "site.css", "body{}");

        var includedAsset = CreateExternalAsset(includedFile, "SomeLib", "app.js");
        var excludedAsset = CreateExternalAssetWithGroups(excludedFile, "IdentityUI", "css/site.css", "BootstrapVersion=V5");

        var includedEndpoint = new TaskItem("app.js", new Dictionary<string, string>
        {
            ["AssetFile"] = Path.GetFullPath(includedFile),
            ["Route"] = "app.js",
            ["Selectors"] = "[]",
            ["EndpointProperties"] = "[]",
            ["ResponseHeaders"] = "[]",
        });
        var excludedEndpoint = new TaskItem("css/site.css", new Dictionary<string, string>
        {
            ["AssetFile"] = Path.GetFullPath(excludedFile),
            ["Route"] = "css/site.css",
            ["Selectors"] = "[]",
            ["EndpointProperties"] = "[]",
            ["ResponseHeaders"] = "[]",
        });

        var task = new UpdateExternallyDefinedStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { includedAsset, excludedAsset },
            Endpoints = new[] { includedEndpoint, excludedEndpoint },
            // No declarations → grouped asset excluded
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.UpdatedEndpoints.Should().HaveCount(1, "only endpoints for included assets should remain");
    }

    // ──────────────────────────────────────────────────────────────────────
    // ComputeReferenceStaticWebAssetItems – SourceType Preservation
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeReference_GroupedFrameworkAsset_PreservesSourceType()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        // Two grouped framework assets at the same target path
        var asset1 = CreateReferenceAsset("item1.css", "FrameworkLib", "Framework", "css/site.css", "All", "All", "BootstrapVersion=V4");
        var asset2 = CreateReferenceAsset("item2.css", "FrameworkLib", "Framework", "css/site.css", "All", "All", "BootstrapVersion=V5");

        var task = new ComputeReferenceStaticWebAssetItems
        {
            BuildEngine = buildEngine.Object,
            Source = "FrameworkLib",
            Assets = new[] { asset1, asset2 },
            Patterns = Array.Empty<ITaskItem>(),
            AssetKind = "Build",
            ProjectMode = "Default",
            UpdateSourceType = true
        };

        var result = task.Execute();

        result.Should().BeTrue();
        // Both should be included (distinct groups)
        task.StaticWebAssets.Should().HaveCount(2);
        // And both should STILL be Framework, not overwritten to Project
        foreach (var asset in task.StaticWebAssets)
        {
            asset.GetMetadata("SourceType").Should().Be("Framework",
                "grouped framework assets should preserve SourceType=Framework");
        }
    }

    [Fact]
    public void ComputeReference_NonGroupedFrameworkAsset_PreservesSourceType()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var asset = CreateReferenceAsset("framework.js", "FrameworkLib", "Framework", "js/framework.js", "All", "All");

        var task = new ComputeReferenceStaticWebAssetItems
        {
            BuildEngine = buildEngine.Object,
            Source = "FrameworkLib",
            Assets = new[] { asset },
            Patterns = Array.Empty<ITaskItem>(),
            AssetKind = "Build",
            ProjectMode = "Default",
            UpdateSourceType = true
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.StaticWebAssets.Should().HaveCount(1);
        task.StaticWebAssets[0].GetMetadata("SourceType").Should().Be("Framework",
            "non-grouped framework assets should also preserve SourceType=Framework");
    }

    // ──────────────────────────────────────────────────────────────────────
    // DefineStaticWebAssets – ApplyGroupDefinitions Order+SourceId validation
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void ApplyGroupDefinitions_SameOrderSameSourceId_ProducesError()
    {
        var file = CreateTempFile("wwwroot", "V5", "css", "site.css", "body{}");

        var task = new DefineStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            TestResolveFileDetails = (_, _) => (null, 10, new DateTimeOffset(2023, 10, 1, 0, 0, 0, TimeSpan.Zero)),
            CandidateAssets = new[]
            {
                new TaskItem(file, new Dictionary<string, string>
                {
                    ["RelativePath"] = "",
                    ["TargetPath"] = "",
                    ["Link"] = "",
                    ["CopyToOutputDirectory"] = "",
                    ["CopyToPublishDirectory"] = "",
                    ["Integrity"] = "integrity",
                    ["Fingerprint"] = "fingerprint",
                    ["LastWriteTime"] = DateTime.UtcNow.ToString(StaticWebAsset.DateTimeAssetFormat),
                    ["FileLength"] = "10",
                })
            },
            RelativePathPattern = "wwwroot\\**",
            SourceType = "Discovered",
            SourceId = "MyProject",
            ContentRoot = Path.Combine(_tempDir, "wwwroot"),
            BasePath = "_content/myproject",
            StaticWebAssetGroupDefinitions = new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V5",
                    ["Order"] = "0",
                    ["SourceId"] = "IdentityUI",
                    ["IncludePattern"] = "V5/**",
                    ["RelativePathPattern"] = "V5/**"
                }),
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V4",
                    ["Order"] = "0",
                    ["SourceId"] = "IdentityUI",
                    ["IncludePattern"] = "V4/**",
                    ["RelativePathPattern"] = "V4/**"
                })
            }
        };

        var result = task.Execute();

        result.Should().BeFalse("same Order + same SourceId should produce an error");
        _errorMessages.Should().ContainMatch("*same Order*");
    }

    [Fact]
    public void ApplyGroupDefinitions_DifferentOrderSameSourceId_NoError()
    {
        var file = CreateTempFile("wwwroot", "V5", "css", "site.css", "body{}");

        var task = new DefineStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            TestResolveFileDetails = (_, _) => (null, 10, new DateTimeOffset(2023, 10, 1, 0, 0, 0, TimeSpan.Zero)),
            CandidateAssets = new[]
            {
                new TaskItem(file, new Dictionary<string, string>
                {
                    ["RelativePath"] = "",
                    ["TargetPath"] = "",
                    ["Link"] = "",
                    ["CopyToOutputDirectory"] = "",
                    ["CopyToPublishDirectory"] = "",
                    ["Integrity"] = "integrity",
                    ["Fingerprint"] = "fingerprint",
                    ["LastWriteTime"] = DateTime.UtcNow.ToString(StaticWebAsset.DateTimeAssetFormat),
                    ["FileLength"] = "10",
                })
            },
            RelativePathPattern = "wwwroot\\**",
            SourceType = "Discovered",
            SourceId = "MyProject",
            ContentRoot = Path.Combine(_tempDir, "wwwroot"),
            BasePath = "_content/myproject",
            StaticWebAssetGroupDefinitions = new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V5",
                    ["Order"] = "0",
                    ["SourceId"] = "IdentityUI",
                    ["IncludePattern"] = "V5/**",
                    ["RelativePathPattern"] = "V5/**"
                }),
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V4",
                    ["Order"] = "1",
                    ["SourceId"] = "IdentityUI",
                    ["IncludePattern"] = "V4/**",
                    ["RelativePathPattern"] = "V4/**"
                })
            }
        };

        var result = task.Execute();

        result.Should().BeTrue("different Orders should not trigger the same-Order-same-SourceId validation");
        _errorMessages.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────
    // StaticWebAsset – AllAssetsHaveDistinctGroups
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void AllAssetsHaveDistinctGroups_AllDistinct_ReturnsTrue()
    {
        var assets = new List<StaticWebAsset>
        {
            CreateStaticWebAsset("a.css", "BootstrapVersion=V4"),
            CreateStaticWebAsset("b.css", "BootstrapVersion=V5")
        };

        StaticWebAsset.AllAssetsHaveDistinctGroups(assets).Should().BeTrue();
    }

    [Fact]
    public void AllAssetsHaveDistinctGroups_OneEmpty_ReturnsFalse()
    {
        var assets = new List<StaticWebAsset>
        {
            CreateStaticWebAsset("a.css", "BootstrapVersion=V4"),
            CreateStaticWebAsset("b.css", "")
        };

        StaticWebAsset.AllAssetsHaveDistinctGroups(assets).Should().BeFalse();
    }

    [Fact]
    public void AllAssetsHaveDistinctGroups_Duplicate_ReturnsFalse()
    {
        var assets = new List<StaticWebAsset>
        {
            CreateStaticWebAsset("a.css", "BootstrapVersion=V5"),
            CreateStaticWebAsset("b.css", "BootstrapVersion=V5")
        };

        StaticWebAsset.AllAssetsHaveDistinctGroups(assets).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────

    private string CreateTempFile(params string[] pathParts)
    {
        var content = pathParts[^1];
        var segments = pathParts[..^1];

        var dir = Path.Combine(new[] { _tempDir }.Concat(segments[..^1]).ToArray());
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, segments[^1]);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    private ITaskItem CreatePackageAsset(string filePath, string sourceId, string basePath, string relativePath)
    {
        var contentRoot = Path.GetDirectoryName(filePath) + Path.DirectorySeparatorChar;
        return new TaskItem(filePath, new Dictionary<string, string>
        {
            ["SourceType"] = "Package",
            ["SourceId"] = sourceId,
            ["ContentRoot"] = contentRoot,
            ["BasePath"] = basePath,
            ["RelativePath"] = relativePath,
            ["AssetKind"] = "All",
            ["AssetMode"] = "All",
            ["AssetRole"] = "Primary",
            ["RelatedAsset"] = "",
            ["AssetTraitName"] = "",
            ["AssetTraitValue"] = "",
            ["CopyToOutputDirectory"] = "Never",
            ["CopyToPublishDirectory"] = "PreserveNewest",
            ["OriginalItemSpec"] = filePath,
            ["Fingerprint"] = "test-fingerprint",
            ["Integrity"] = "test-integrity",
            ["FileLength"] = "10",
            ["LastWriteTime"] = new DateTimeOffset(new DateTime(1990, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc)).ToString(StaticWebAsset.DateTimeAssetFormat),
        });
    }

    private ITaskItem CreatePackageAssetWithGroups(string filePath, string sourceId, string basePath, string relativePath, string assetGroups)
    {
        var item = CreatePackageAsset(filePath, sourceId, basePath, relativePath);
        item.SetMetadata("AssetGroups", assetGroups);
        return item;
    }

    private ITaskItem CreateExternalAsset(string filePath, string sourceId, string relativePath)
    {
        var contentRoot = Path.GetDirectoryName(filePath) + Path.DirectorySeparatorChar;
        return new TaskItem(filePath, new Dictionary<string, string>
        {
            ["SourceType"] = "Discovered",
            ["SourceId"] = sourceId,
            ["ContentRoot"] = contentRoot,
            ["BasePath"] = "",
            ["RelativePath"] = relativePath,
            ["AssetKind"] = "All",
            ["AssetMode"] = "All",
            ["AssetRole"] = "Primary",
            ["RelatedAsset"] = "",
            ["AssetTraitName"] = "",
            ["AssetTraitValue"] = "",
            ["CopyToOutputDirectory"] = "PreserveNewest",
            ["CopyToPublishDirectory"] = "PreserveNewest",
            ["OriginalItemSpec"] = filePath,
        });
    }

    private ITaskItem CreateExternalAssetWithGroups(string filePath, string sourceId, string relativePath, string assetGroups)
    {
        var item = CreateExternalAsset(filePath, sourceId, relativePath);
        item.SetMetadata("AssetGroups", assetGroups);
        return item;
    }

    private static ITaskItem CreateReferenceAsset(
        string itemSpec,
        string sourceId,
        string sourceType,
        string relativePath,
        string assetKind,
        string assetMode,
        string assetGroups = null)
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
            Integrity = "integrity",
            Fingerprint = "fingerprint",
            FileLength = 10,
            LastWriteTime = DateTime.UtcNow,
        };

        if (!string.IsNullOrEmpty(assetGroups))
        {
            result.AssetGroups = assetGroups;
        }

        result.ApplyDefaults();
        result.Normalize();

        return result.ToTaskItem();
    }

    private static StaticWebAsset CreateStaticWebAsset(string identity, string assetGroups)
    {
        var asset = new StaticWebAsset
        {
            Identity = Path.GetFullPath(identity),
            SourceId = "TestLib",
            SourceType = "Package",
            ContentRoot = Directory.GetCurrentDirectory(),
            BasePath = "base",
            RelativePath = identity,
            AssetKind = "All",
            AssetMode = "All",
            AssetRole = "Primary",
            RelatedAsset = "",
            AssetTraitName = "",
            AssetTraitValue = "",
            CopyToOutputDirectory = "",
            CopyToPublishDirectory = "",
            OriginalItemSpec = identity,
            Integrity = "integrity",
            Fingerprint = "fingerprint",
            FileLength = 10,
            LastWriteTime = DateTime.UtcNow,
            AssetGroups = assetGroups,
        };

        asset.ApplyDefaults();
        asset.Normalize();
        return asset;
    }

    // ──────────────────────────────────────────────────────────────────────
    // DefineStaticWebAssets – ContentRootSuffix + RelativePathPrefix
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void ApplyGroupDefinitions_ContentRootSuffix_AdjustsContentRoot()
    {
        var file = CreateTempFile("wwwroot", "V4", "css", "site.css", "body-v4{}");
        var wwwrootPath = Path.Combine(_tempDir, "wwwroot") + Path.DirectorySeparatorChar;

        var task = new DefineStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            TestResolveFileDetails = (_, _) => (null, 10, new DateTimeOffset(2023, 10, 1, 0, 0, 0, TimeSpan.Zero)),
            CandidateAssets = new[]
            {
                new TaskItem(file, new Dictionary<string, string>
                {
                    ["RelativePath"] = "",
                    ["TargetPath"] = "",
                    ["Link"] = "",
                    ["CopyToOutputDirectory"] = "",
                    ["CopyToPublishDirectory"] = "",
                    ["Integrity"] = "integrity",
                    ["Fingerprint"] = "fingerprint",
                    ["LastWriteTime"] = DateTime.UtcNow.ToString(StaticWebAsset.DateTimeAssetFormat),
                    ["FileLength"] = "10",
                })
            },
            RelativePathPattern = "wwwroot/**",
            SourceType = "Discovered",
            SourceId = "IdentityUI",
            ContentRoot = wwwrootPath,
            BasePath = "Identity",
            StaticWebAssetGroupDefinitions = new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V4",
                    ["Order"] = "0",
                    ["IncludePattern"] = "V4/**",
                    ["RelativePathPattern"] = "V4/**",
                    ["ContentRootSuffix"] = "V4"
                })
            }
        };

        var result = task.Execute();

        result.Should().BeTrue();
        _errorMessages.Should().BeEmpty();
        task.Assets.Should().HaveCount(1);

        var asset = StaticWebAsset.FromTaskItem(task.Assets[0]);
        asset.RelativePath.Should().Be("css/site.css", "RelativePathPattern stripped the V4/ prefix");
        asset.ContentRoot.Should().Be(wwwrootPath + "V4" + Path.DirectorySeparatorChar,
            "ContentRootSuffix appended V4 to wwwroot path");
        asset.AssetGroups.Should().Contain("BootstrapVersion=V4");
    }

    [Fact]
    public void ApplyGroupDefinitions_PatternOnly_NoAutoFileOnlyToken()
    {
        var file = CreateTempFile("wwwroot", "V4", "css", "site.css", "body-v4{}");
        var wwwrootPath = Path.Combine(_tempDir, "wwwroot") + Path.DirectorySeparatorChar;

        var task = new DefineStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            TestResolveFileDetails = (_, _) => (null, 10, new DateTimeOffset(2023, 10, 1, 0, 0, 0, TimeSpan.Zero)),
            CandidateAssets = new[]
            {
                new TaskItem(file, new Dictionary<string, string>
                {
                    ["RelativePath"] = "",
                    ["TargetPath"] = "",
                    ["Link"] = "",
                    ["CopyToOutputDirectory"] = "",
                    ["CopyToPublishDirectory"] = "",
                    ["Integrity"] = "integrity",
                    ["Fingerprint"] = "fingerprint",
                    ["LastWriteTime"] = DateTime.UtcNow.ToString(StaticWebAsset.DateTimeAssetFormat),
                    ["FileLength"] = "10",
                })
            },
            RelativePathPattern = "wwwroot/**",
            SourceType = "Discovered",
            SourceId = "IdentityUI",
            ContentRoot = wwwrootPath,
            BasePath = "Identity",
            StaticWebAssetGroupDefinitions = new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V4",
                    ["Order"] = "0",
                    ["IncludePattern"] = "V4/**",
                    ["RelativePathPattern"] = "V4/**"
                    // No RelativePathPrefix, no ContentRootSuffix
                })
            }
        };

        var result = task.Execute();

        result.Should().BeTrue();
        _errorMessages.Should().BeEmpty();
        task.Assets.Should().HaveCount(1);

        var asset = StaticWebAsset.FromTaskItem(task.Assets[0]);
        asset.RelativePath.Should().Be("css/site.css",
            "RelativePathPattern stripped V4/ prefix — no auto-injection of file-only ~ token");
        asset.RelativePath.Should().NotContain("~", "SDK must not auto-inject file-only tokens");
        asset.ContentRoot.Should().Be(wwwrootPath, "ContentRoot unchanged when no ContentRootSuffix");
    }

    [Fact]
    public void ApplyGroupDefinitions_RelativePathPrefix_FileOnlyToken()
    {
        var file = CreateTempFile("wwwroot", "V4", "css", "site.css", "body-v4{}");
        var wwwrootPath = Path.Combine(_tempDir, "wwwroot") + Path.DirectorySeparatorChar;

        var task = new DefineStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            TestResolveFileDetails = (_, _) => (null, 10, new DateTimeOffset(2023, 10, 1, 0, 0, 0, TimeSpan.Zero)),
            CandidateAssets = new[]
            {
                new TaskItem(file, new Dictionary<string, string>
                {
                    ["RelativePath"] = "",
                    ["TargetPath"] = "",
                    ["Link"] = "",
                    ["CopyToOutputDirectory"] = "",
                    ["CopyToPublishDirectory"] = "",
                    ["Integrity"] = "integrity",
                    ["Fingerprint"] = "fingerprint",
                    ["LastWriteTime"] = DateTime.UtcNow.ToString(StaticWebAsset.DateTimeAssetFormat),
                    ["FileLength"] = "10",
                })
            },
            RelativePathPattern = "wwwroot/**",
            SourceType = "Discovered",
            SourceId = "IdentityUI",
            ContentRoot = wwwrootPath,
            BasePath = "Identity",
            StaticWebAssetGroupDefinitions = new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V4",
                    ["Order"] = "0",
                    ["IncludePattern"] = "V4/**",
                    ["RelativePathPattern"] = "V4/**",
                    ["RelativePathPrefix"] = "#[{BootstrapVersion}/]~"
                })
            }
        };

        var result = task.Execute();

        result.Should().BeTrue();
        _errorMessages.Should().BeEmpty();
        task.Assets.Should().HaveCount(1);

        var asset = StaticWebAsset.FromTaskItem(task.Assets[0]);
        asset.RelativePath.Should().Be("#[{BootstrapVersion}/]~css/site.css",
            "pattern strips V4/, prefix prepends file-only token expression");
        asset.ContentRoot.Should().Be(wwwrootPath, "ContentRoot unchanged when no ContentRootSuffix");
        asset.AssetGroups.Should().Contain("BootstrapVersion=V4");
    }

    [Fact]
    public void ApplyGroupDefinitions_RelativePathPrefix_LiteralPrepend()
    {
        var file = CreateTempFile("wwwroot", "V4", "css", "site.css", "body-v4{}");
        var wwwrootPath = Path.Combine(_tempDir, "wwwroot") + Path.DirectorySeparatorChar;

        var task = new DefineStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            TestResolveFileDetails = (_, _) => (null, 10, new DateTimeOffset(2023, 10, 1, 0, 0, 0, TimeSpan.Zero)),
            CandidateAssets = new[]
            {
                new TaskItem(file, new Dictionary<string, string>
                {
                    ["RelativePath"] = "",
                    ["TargetPath"] = "",
                    ["Link"] = "",
                    ["CopyToOutputDirectory"] = "",
                    ["CopyToPublishDirectory"] = "",
                    ["Integrity"] = "integrity",
                    ["Fingerprint"] = "fingerprint",
                    ["LastWriteTime"] = DateTime.UtcNow.ToString(StaticWebAsset.DateTimeAssetFormat),
                    ["FileLength"] = "10",
                })
            },
            RelativePathPattern = "wwwroot/**",
            SourceType = "Discovered",
            SourceId = "IdentityUI",
            ContentRoot = wwwrootPath,
            BasePath = "Identity",
            StaticWebAssetGroupDefinitions = new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V4",
                    ["Order"] = "0",
                    ["IncludePattern"] = "V4/**",
                    ["RelativePathPattern"] = "V4/**",
                    ["RelativePathPrefix"] = "shared/"
                })
            }
        };

        var result = task.Execute();

        result.Should().BeTrue();
        _errorMessages.Should().BeEmpty();
        task.Assets.Should().HaveCount(1);

        var asset = StaticWebAsset.FromTaskItem(task.Assets[0]);
        asset.RelativePath.Should().Be("shared/css/site.css",
            "pattern strips V4/, prefix prepends literal 'shared/'");
        asset.ContentRoot.Should().Be(wwwrootPath, "ContentRoot unchanged when no ContentRootSuffix");
    }

    [Fact]
    public void ApplyGroupDefinitions_AllThreeOrthogonal()
    {
        var file = CreateTempFile("wwwroot", "V4", "css", "site.css", "body-v4{}");
        var wwwrootPath = Path.Combine(_tempDir, "wwwroot") + Path.DirectorySeparatorChar;

        var task = new DefineStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            TestResolveFileDetails = (_, _) => (null, 10, new DateTimeOffset(2023, 10, 1, 0, 0, 0, TimeSpan.Zero)),
            CandidateAssets = new[]
            {
                new TaskItem(file, new Dictionary<string, string>
                {
                    ["RelativePath"] = "",
                    ["TargetPath"] = "",
                    ["Link"] = "",
                    ["CopyToOutputDirectory"] = "",
                    ["CopyToPublishDirectory"] = "",
                    ["Integrity"] = "integrity",
                    ["Fingerprint"] = "fingerprint",
                    ["LastWriteTime"] = DateTime.UtcNow.ToString(StaticWebAsset.DateTimeAssetFormat),
                    ["FileLength"] = "10",
                })
            },
            RelativePathPattern = "wwwroot/**",
            SourceType = "Discovered",
            SourceId = "IdentityUI",
            ContentRoot = wwwrootPath,
            BasePath = "Identity",
            StaticWebAssetGroupDefinitions = new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V4",
                    ["Order"] = "0",
                    ["IncludePattern"] = "V4/**",
                    ["RelativePathPattern"] = "V4/**",
                    ["RelativePathPrefix"] = "shared/",
                    ["ContentRootSuffix"] = "V4"
                })
            }
        };

        var result = task.Execute();

        result.Should().BeTrue();
        _errorMessages.Should().BeEmpty();
        task.Assets.Should().HaveCount(1);

        var asset = StaticWebAsset.FromTaskItem(task.Assets[0]);
        asset.RelativePath.Should().Be("shared/css/site.css",
            "pattern strips V4/, prefix prepends 'shared/'");
        asset.ContentRoot.Should().Be(wwwrootPath + "V4" + Path.DirectorySeparatorChar,
            "ContentRootSuffix applied independently");
        asset.AssetGroups.Should().Contain("BootstrapVersion=V4");
    }

    [Fact]
    public void ApplyGroupDefinitions_RelativePathPrefix_WithoutPattern_PrependsToOriginalPath()
    {
        var file = CreateTempFile("wwwroot", "css", "site.css", "body{}");
        var wwwrootPath = Path.Combine(_tempDir, "wwwroot") + Path.DirectorySeparatorChar;

        var task = new DefineStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            TestResolveFileDetails = (_, _) => (null, 10, new DateTimeOffset(2023, 10, 1, 0, 0, 0, TimeSpan.Zero)),
            CandidateAssets = new[]
            {
                new TaskItem(file, new Dictionary<string, string>
                {
                    ["RelativePath"] = "",
                    ["TargetPath"] = "",
                    ["Link"] = "",
                    ["CopyToOutputDirectory"] = "",
                    ["CopyToPublishDirectory"] = "",
                    ["Integrity"] = "integrity",
                    ["Fingerprint"] = "fingerprint",
                    ["LastWriteTime"] = DateTime.UtcNow.ToString(StaticWebAsset.DateTimeAssetFormat),
                    ["FileLength"] = "10",
                })
            },
            RelativePathPattern = "wwwroot/**",
            SourceType = "Discovered",
            SourceId = "MyLib",
            ContentRoot = wwwrootPath,
            BasePath = "mylib",
            StaticWebAssetGroupDefinitions = new ITaskItem[]
            {
                new TaskItem("Theme", new Dictionary<string, string>
                {
                    ["Value"] = "Default",
                    ["Order"] = "0",
                    ["IncludePattern"] = "**",
                    ["RelativePathPrefix"] = "lib/"
                    // No RelativePathPattern — no stripping, just prepend
                })
            }
        };

        var result = task.Execute();

        result.Should().BeTrue();
        _errorMessages.Should().BeEmpty();
        task.Assets.Should().HaveCount(1);

        var asset = StaticWebAsset.FromTaskItem(task.Assets[0]);
        asset.RelativePath.Should().Be("lib/css/site.css",
            "no pattern stripping, but prefix 'lib/' prepended to original path");
        asset.AssetGroups.Should().Contain("Theme=Default");
    }

    [Fact]
    public void ApplyGroupDefinitions_ContentRootSuffix_MultipleGroups_EachGetsOwnContentRoot()
    {
        var fileV4 = CreateTempFile("wwwroot", "V4", "css", "site.css", "body-v4{}");
        var fileV5 = CreateTempFile("wwwroot", "V5", "css", "site.css", "body-v5{}");
        var wwwrootPath = Path.Combine(_tempDir, "wwwroot") + Path.DirectorySeparatorChar;

        var task = new DefineStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            TestResolveFileDetails = (_, _) => (null, 10, new DateTimeOffset(2023, 10, 1, 0, 0, 0, TimeSpan.Zero)),
            CandidateAssets = new[]
            {
                new TaskItem(fileV4, new Dictionary<string, string>
                {
                    ["RelativePath"] = "",
                    ["TargetPath"] = "",
                    ["Link"] = "",
                    ["CopyToOutputDirectory"] = "",
                    ["CopyToPublishDirectory"] = "",
                    ["Integrity"] = "integrity-v4",
                    ["Fingerprint"] = "fingerprint-v4",
                    ["LastWriteTime"] = DateTime.UtcNow.ToString(StaticWebAsset.DateTimeAssetFormat),
                    ["FileLength"] = "10",
                }),
                new TaskItem(fileV5, new Dictionary<string, string>
                {
                    ["RelativePath"] = "",
                    ["TargetPath"] = "",
                    ["Link"] = "",
                    ["CopyToOutputDirectory"] = "",
                    ["CopyToPublishDirectory"] = "",
                    ["Integrity"] = "integrity-v5",
                    ["Fingerprint"] = "fingerprint-v5",
                    ["LastWriteTime"] = DateTime.UtcNow.ToString(StaticWebAsset.DateTimeAssetFormat),
                    ["FileLength"] = "11",
                })
            },
            RelativePathPattern = "wwwroot/**",
            SourceType = "Discovered",
            SourceId = "IdentityUI",
            ContentRoot = wwwrootPath,
            BasePath = "Identity",
            StaticWebAssetGroupDefinitions = new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V5",
                    ["Order"] = "0",
                    ["IncludePattern"] = "V5/**",
                    ["RelativePathPattern"] = "V5/**",
                    ["ContentRootSuffix"] = "V5"
                }),
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V4",
                    ["Order"] = "1",
                    ["IncludePattern"] = "V4/**",
                    ["RelativePathPattern"] = "V4/**",
                    ["ContentRootSuffix"] = "V4"
                })
            }
        };

        var result = task.Execute();

        result.Should().BeTrue();
        _errorMessages.Should().BeEmpty();
        task.Assets.Should().HaveCount(2);

        var assets = task.Assets.Select(a => StaticWebAsset.FromTaskItem(a)).ToList();
        var v4Asset = assets.Single(a => a.AssetGroups.Contains("BootstrapVersion=V4"));
        var v5Asset = assets.Single(a => a.AssetGroups.Contains("BootstrapVersion=V5"));

        v4Asset.ContentRoot.Should().Be(wwwrootPath + "V4" + Path.DirectorySeparatorChar,
            "V4 asset gets its own ContentRoot with V4 suffix");
        v4Asset.RelativePath.Should().Be("css/site.css");

        v5Asset.ContentRoot.Should().Be(wwwrootPath + "V5" + Path.DirectorySeparatorChar,
            "V5 asset gets its own ContentRoot with V5 suffix");
        v5Asset.RelativePath.Should().Be("css/site.css");
    }
}
