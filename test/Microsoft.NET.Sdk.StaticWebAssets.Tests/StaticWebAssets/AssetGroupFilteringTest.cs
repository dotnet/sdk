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

    [Fact]
    public void ComputeReference_GroupedFrameworkAsset_PreservesSourceType()
    {
        // Two grouped framework assets at the same target path
        var asset1 = CreateReferenceAsset("item1.css", "FrameworkLib", "Framework", "css/site.css", "All", "All", "BootstrapVersion=V4");
        var asset2 = CreateReferenceAsset("item2.css", "FrameworkLib", "Framework", "css/site.css", "All", "All", "BootstrapVersion=V5");

        var task = new ComputeReferenceStaticWebAssetItems
        {
            BuildEngine = _buildEngine.Object,
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
        var asset = CreateReferenceAsset("framework.js", "FrameworkLib", "Framework", "js/framework.js", "All", "All");

        var task = new ComputeReferenceStaticWebAssetItems
        {
            BuildEngine = _buildEngine.Object,
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

    [Fact]
    public void ApplyGroupDefinitions_SameOrderSameSourceId_ProducesError()
    {
        var file = CreateTempFile("wwwroot", "V5", "css", "site.css", "body{}");

        var task = CreateDefineStaticWebAssetsTask(
            new[] { CreateCandidateAssetItem(file) },
            new ITaskItem[]
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
            },
            sourceId: "MyProject",
            basePath: "_content/myproject");

        var result = task.Execute();

        result.Should().BeFalse("same Order + same SourceId should produce an error");
        _errorMessages.Should().ContainMatch("*same Order*");
    }

    [Fact]
    public void ApplyGroupDefinitions_DifferentOrderSameSourceId_NoError()
    {
        var file = CreateTempFile("wwwroot", "V5", "css", "site.css", "body{}");

        var task = CreateDefineStaticWebAssetsTask(
            new[] { CreateCandidateAssetItem(file) },
            new ITaskItem[]
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
            },
            sourceId: "MyProject",
            basePath: "_content/myproject");

        var result = task.Execute();

        result.Should().BeTrue("different Orders should not trigger the same-Order-same-SourceId validation");
        _errorMessages.Should().BeEmpty();
    }

    [Fact]
    public void ApplyGroupDefinitions_MissingValue_ProducesError()
    {
        var file = CreateTempFile("wwwroot", "V4", "css", "site.css", "body{}");

        var task = CreateDefineStaticWebAssetsTask(
            new[] { CreateCandidateAssetItem(file) },
            new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    // No "Value"
                    ["Order"] = "0",
                    ["SourceId"] = "IdentityUI",
                    ["IncludePattern"] = "V4/**",
                })
            });

        var result = task.Execute();

        result.Should().BeFalse();
        _errorMessages.Should().ContainSingle(m => m.Contains("missing required metadata 'Value'"));
    }

    [Fact]
    public void ApplyGroupDefinitions_MissingSourceId_ProducesError()
    {
        var file = CreateTempFile("wwwroot", "V4", "css", "site.css", "body{}");

        var task = CreateDefineStaticWebAssetsTask(
            new[] { CreateCandidateAssetItem(file) },
            new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V4",
                    ["Order"] = "0",
                    // No "SourceId"
                    ["IncludePattern"] = "V4/**",
                })
            });

        var result = task.Execute();

        result.Should().BeFalse();
        _errorMessages.Should().ContainSingle(m => m.Contains("missing required metadata 'SourceId'"));
    }

    [Fact]
    public void ApplyGroupDefinitions_InvalidOrder_ProducesError()
    {
        var file = CreateTempFile("wwwroot", "V4", "css", "site.css", "body{}");

        var task = CreateDefineStaticWebAssetsTask(
            new[] { CreateCandidateAssetItem(file) },
            new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V4",
                    ["Order"] = "not-a-number",
                    ["SourceId"] = "IdentityUI",
                    ["IncludePattern"] = "V4/**",
                })
            });

        var result = task.Execute();

        result.Should().BeFalse();
        _errorMessages.Should().ContainSingle(m => m.Contains("invalid or missing 'Order'"));
    }

    [Fact]
    public void ApplyGroupDefinitions_MissingIncludePattern_ProducesError()
    {
        var file = CreateTempFile("wwwroot", "V4", "css", "site.css", "body{}");

        var task = CreateDefineStaticWebAssetsTask(
            new[] { CreateCandidateAssetItem(file) },
            new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V4",
                    ["Order"] = "0",
                    ["SourceId"] = "IdentityUI",
                    // No "IncludePattern"
                })
            });

        var result = task.Execute();

        result.Should().BeFalse();
        _errorMessages.Should().ContainSingle(m => m.Contains("missing required metadata 'IncludePattern'"));
    }

    [Fact]
    public void ApplyGroupDefinitions_SourceIdMismatch_DefinitionsIgnored()
    {
        var file = CreateTempFile("wwwroot", "V4", "css", "site.css", "body-v4{}");

        var task = CreateDefineStaticWebAssetsTask(
            new[] { CreateCandidateAssetItem(file) },
            new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V4",
                    ["Order"] = "0",
                    ["SourceId"] = "OtherLib",
                    ["IncludePattern"] = "V4/**",
                    ["RelativePathPattern"] = "V4/**",
                    ["ContentRootSuffix"] = "V4"
                })
            });

        var result = task.Execute();

        result.Should().BeTrue();
        _errorMessages.Should().BeEmpty();
        task.Assets.Should().HaveCount(1);

        var asset = StaticWebAsset.FromTaskItem(task.Assets[0]);
        asset.AssetGroups.Should().BeNullOrEmpty("definitions with mismatched SourceId should not apply");
        asset.RelativePath.Should().Contain("V4", "RelativePath should not be transformed");
    }

    [Fact]
    public void ApplyGroupDefinitions_MultipleContentRootSuffix_Compose()
    {
        var file = CreateTempFile("wwwroot", "shared", "site.css", "body{}");

        var task = CreateDefineStaticWebAssetsTask(
            new[] { CreateCandidateAssetItem(file) },
            new ITaskItem[]
            {
                new TaskItem("GroupA", new Dictionary<string, string>
                {
                    ["Value"] = "V1",
                    ["Order"] = "0",
                    ["SourceId"] = "IdentityUI",
                    ["IncludePattern"] = "**",
                    ["ContentRootSuffix"] = "suffixA"
                }),
                new TaskItem("GroupB", new Dictionary<string, string>
                {
                    ["Value"] = "V2",
                    ["Order"] = "1",
                    ["SourceId"] = "IdentityUI",
                    ["IncludePattern"] = "**",
                    ["ContentRootSuffix"] = "suffixB"
                })
            });

        var result = task.Execute();

        result.Should().BeTrue();
        var asset = StaticWebAsset.FromTaskItem(task.Assets[0]);
        asset.ContentRoot.Should().Contain("suffixA");
        asset.ContentRoot.Should().Contain("suffixB");
        // Suffixes compose in order: suffixA/suffixB
        asset.ContentRoot.Should().Contain(Path.Combine("suffixA", "suffixB"));
    }

    [Theory]
    [InlineData(new[] { "BootstrapVersion=V4", "BootstrapVersion=V5" }, true)]
    [InlineData(new[] { "BootstrapVersion=V4", "" }, false)]
    [InlineData(new[] { "BootstrapVersion=V5", "BootstrapVersion=V5" }, false)]
    public void AllAssetsHaveDistinctGroups_ReturnsExpectedResult(string[] groups, bool expected)
    {
        var assets = groups.Select((g, i) => CreateStaticWebAsset($"{(char)('a' + i)}.css", g)).ToList();
        var groupSet = new HashSet<string>(StringComparer.Ordinal);
        StaticWebAsset.AllAssetsHaveDistinctGroups(assets, groupSet).Should().Be(expected);
    }

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

    private ITaskItem CreateCandidateAssetItem(string file, string integrity = "integrity", string fingerprint = "fingerprint", string fileLength = "10")
    {
        return new TaskItem(file, new Dictionary<string, string>
        {
            ["RelativePath"] = "",
            ["TargetPath"] = "",
            ["Link"] = "",
            ["CopyToOutputDirectory"] = "",
            ["CopyToPublishDirectory"] = "",
            ["Integrity"] = integrity,
            ["Fingerprint"] = fingerprint,
            ["LastWriteTime"] = DateTime.UtcNow.ToString(StaticWebAsset.DateTimeAssetFormat),
            ["FileLength"] = fileLength,
        });
    }

    private DefineStaticWebAssets CreateDefineStaticWebAssetsTask(
        ITaskItem[] candidates,
        ITaskItem[] groupDefs,
        string sourceId = "IdentityUI",
        string contentRoot = null,
        string basePath = "Identity")
    {
        return new DefineStaticWebAssets
        {
            BuildEngine = _buildEngine.Object,
            TestResolveFileDetails = (_, _) => (null, 10, new DateTimeOffset(2023, 10, 1, 0, 0, 0, TimeSpan.Zero)),
            CandidateAssets = candidates,
            RelativePathPattern = "wwwroot/**",
            SourceType = "Discovered",
            SourceId = sourceId,
            ContentRoot = contentRoot ?? Path.Combine(_tempDir, "wwwroot") + Path.DirectorySeparatorChar,
            BasePath = basePath,
            StaticWebAssetGroupDefinitions = groupDefs
        };
    }

    [Fact]
    public void ApplyGroupDefinitions_ContentRootSuffix_AdjustsContentRoot()
    {
        var file = CreateTempFile("wwwroot", "V4", "css", "site.css", "body-v4{}");
        var wwwrootPath = Path.Combine(_tempDir, "wwwroot") + Path.DirectorySeparatorChar;

        var task = CreateDefineStaticWebAssetsTask(
            new[] { CreateCandidateAssetItem(file) },
            new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V4",
                    ["Order"] = "0",
                    ["SourceId"] = "IdentityUI",
                    ["IncludePattern"] = "V4/**",
                    ["RelativePathPattern"] = "V4/**",
                    ["ContentRootSuffix"] = "V4"
                })
            });

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

        var task = CreateDefineStaticWebAssetsTask(
            new[] { CreateCandidateAssetItem(file) },
            new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V4",
                    ["Order"] = "0",
                    ["SourceId"] = "IdentityUI",
                    ["IncludePattern"] = "V4/**",
                    ["RelativePathPattern"] = "V4/**"
                    // No RelativePathPrefix, no ContentRootSuffix
                })
            });

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

        var task = CreateDefineStaticWebAssetsTask(
            new[] { CreateCandidateAssetItem(file) },
            new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V4",
                    ["Order"] = "0",
                    ["SourceId"] = "IdentityUI",
                    ["IncludePattern"] = "V4/**",
                    ["RelativePathPattern"] = "V4/**",
                    ["RelativePathPrefix"] = "#[{BootstrapVersion}/]~"
                })
            });

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

        var task = CreateDefineStaticWebAssetsTask(
            new[] { CreateCandidateAssetItem(file) },
            new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V4",
                    ["Order"] = "0",
                    ["SourceId"] = "IdentityUI",
                    ["IncludePattern"] = "V4/**",
                    ["RelativePathPattern"] = "V4/**",
                    ["RelativePathPrefix"] = "shared/"
                })
            });

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

        var task = CreateDefineStaticWebAssetsTask(
            new[] { CreateCandidateAssetItem(file) },
            new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V4",
                    ["Order"] = "0",
                    ["SourceId"] = "IdentityUI",
                    ["IncludePattern"] = "V4/**",
                    ["RelativePathPattern"] = "V4/**",
                    ["RelativePathPrefix"] = "shared/",
                    ["ContentRootSuffix"] = "V4"
                })
            });

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

        var task = CreateDefineStaticWebAssetsTask(
            new[] { CreateCandidateAssetItem(file) },
            new ITaskItem[]
            {
                new TaskItem("Theme", new Dictionary<string, string>
                {
                    ["Value"] = "Default",
                    ["Order"] = "0",
                    ["SourceId"] = "MyLib",
                    ["IncludePattern"] = "**",
                    ["RelativePathPrefix"] = "lib/"
                    // No RelativePathPattern — no stripping, just prepend
                })
            },
            sourceId: "MyLib",
            basePath: "mylib");

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

        var task = CreateDefineStaticWebAssetsTask(
            new[]
            {
                CreateCandidateAssetItem(fileV4, integrity: "integrity-v4", fingerprint: "fingerprint-v4"),
                CreateCandidateAssetItem(fileV5, integrity: "integrity-v5", fingerprint: "fingerprint-v5", fileLength: "11")
            },
            new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V5",
                    ["Order"] = "0",
                    ["SourceId"] = "IdentityUI",
                    ["IncludePattern"] = "V5/**",
                    ["RelativePathPattern"] = "V5/**",
                    ["ContentRootSuffix"] = "V5"
                }),
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V4",
                    ["Order"] = "1",
                    ["SourceId"] = "IdentityUI",
                    ["IncludePattern"] = "V4/**",
                    ["RelativePathPattern"] = "V4/**",
                    ["ContentRootSuffix"] = "V4"
                })
            });

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
