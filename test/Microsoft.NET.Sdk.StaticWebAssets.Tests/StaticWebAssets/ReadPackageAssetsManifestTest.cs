// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class ReadPackageAssetsManifestTest : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IBuildEngine> _buildEngine;
    private readonly List<string> _errorMessages;
    private readonly List<string> _logMessages;

    public ReadPackageAssetsManifestTest()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ReadPkgManifest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _errorMessages = new List<string>();
        _logMessages = new List<string>();
        _buildEngine = new Mock<IBuildEngine>();
        _buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => _errorMessages.Add(args.Message));
        _buildEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
            .Callback<BuildMessageEventArgs>(args => _logMessages.Add(args.Message));
        _buildEngine.Setup(e => e.LogWarningEvent(It.IsAny<BuildWarningEventArgs>()))
            .Callback<BuildWarningEventArgs>(args => _logMessages.Add(args.Message));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void ReadsValidManifest_EmitsAssetsAsTaskItems()
    {
        var packageRoot = SetupPackageRoot("MyLib", new PackageManifestAsset
        {
            PackagePath = "staticwebassets/css/site.css",
            RelativePath = "css/site.css",
            BasePath = "_content/mylib",
            SourceType = "Package",
            AssetKind = "All",
            AssetMode = "All",
            AssetRole = "Primary",
            RelatedAsset = "",
            AssetTraitName = "",
            AssetTraitValue = "",
            AssetGroups = "",
            Fingerprint = "abc",
            Integrity = "sha256-test",
            CopyToOutputDirectory = "Never",
            CopyToPublishDirectory = "PreserveNewest",
            FileLength = "6",
            LastWriteTime = "Mon, 15 Nov 1990 00:00:00 GMT",
        });

        var manifestItem = CreateManifestItem(packageRoot, "MyLib");

        var task = new ReadPackageAssetsManifest
        {
            BuildEngine = _buildEngine.Object,
            PackageManifests = new[] { manifestItem },
            IntermediateOutputPath = Path.Combine(_tempDir, "obj"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.Assets.Should().HaveCount(1);

        var emitted = task.Assets[0];
        emitted.GetMetadata("SourceType").Should().Be("Package");
        emitted.GetMetadata("SourceId").Should().Be("MyLib");
        emitted.GetMetadata("BasePath").Should().Be("_content/mylib");
        emitted.GetMetadata("RelativePath").Should().Be("css/site.css");
        emitted.GetMetadata("Fingerprint").Should().Be("abc");
    }

    [Fact]
    public void UngroupedAssets_AlwaysIncluded()
    {
        var packageRoot = SetupPackageRoot("MyLib", new PackageManifestAsset
        {
            PackagePath = "staticwebassets/app.js",
            RelativePath = "app.js",
            BasePath = "_content/mylib",
            SourceType = "Package",
            AssetKind = "All",
            AssetMode = "All",
            AssetRole = "Primary",
            RelatedAsset = "",
            AssetTraitName = "",
            AssetTraitValue = "",
            AssetGroups = "",
            Fingerprint = "abc",
            Integrity = "sha256-test",
            CopyToOutputDirectory = "Never",
            CopyToPublishDirectory = "PreserveNewest",
            FileLength = "6",
            LastWriteTime = "Mon, 15 Nov 1990 00:00:00 GMT",
        });

        var manifestItem = CreateManifestItem(packageRoot, "MyLib");

        var task = new ReadPackageAssetsManifest
        {
            BuildEngine = _buildEngine.Object,
            PackageManifests = new[] { manifestItem },
            StaticWebAssetGroups = new ITaskItem[]
            {
                new TaskItem("SomeGroup", new Dictionary<string, string>
                {
                    ["Value"] = "SomeValue",
                    ["SourceId"] = "OtherLib"
                })
            },
            IntermediateOutputPath = Path.Combine(_tempDir, "obj"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.Assets.Should().HaveCount(1, "ungrouped assets should always be included");
    }

    [Fact]
    public void GroupedAsset_MatchingDeclaration_IsIncluded()
    {
        var packageRoot = SetupPackageRoot("IdentityUI",
            CreateManifestAsset("staticwebassets/css/site.css", "css/site.css", "_content/id", "BootstrapVersion=V5"));

        var manifestItem = CreateManifestItem(packageRoot, "IdentityUI");

        var task = new ReadPackageAssetsManifest
        {
            BuildEngine = _buildEngine.Object,
            PackageManifests = new[] { manifestItem },
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
        task.Assets.Should().HaveCount(1);
    }

    [Fact]
    public void GroupedAsset_NoDeclarations_IsExcluded()
    {
        var packageRoot = SetupPackageRoot("IdentityUI",
            CreateManifestAsset("staticwebassets/css/site.css", "css/site.css", "_content/id", "BootstrapVersion=V5"));

        var manifestItem = CreateManifestItem(packageRoot, "IdentityUI");

        var task = new ReadPackageAssetsManifest
        {
            BuildEngine = _buildEngine.Object,
            PackageManifests = new[] { manifestItem },
            // No StaticWebAssetGroups
            IntermediateOutputPath = Path.Combine(_tempDir, "obj"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.Assets.Should().HaveCount(0, "grouped assets should be excluded with no declarations");
    }

    [Fact]
    public void MultiGroup_PartialMatch_IsExcluded()
    {
        var packageRoot = SetupPackageRoot("IdentityUI",
            CreateManifestAsset("staticwebassets/css/site.css", "css/site.css", "_content/id",
                "BootstrapVersion=V5;DebugAssets=true"));

        var manifestItem = CreateManifestItem(packageRoot, "IdentityUI");

        var task = new ReadPackageAssetsManifest
        {
            BuildEngine = _buildEngine.Object,
            PackageManifests = new[] { manifestItem },
            StaticWebAssetGroups = new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V5",
                    ["SourceId"] = "IdentityUI"
                })
                // DebugAssets not declared
            },
            IntermediateOutputPath = Path.Combine(_tempDir, "obj"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.Assets.Should().HaveCount(0, "AND-matching: all group entries must be satisfied");
    }

    [Fact]
    public void CascadingExclusion_RelatedAssetExcludedWithPrimary()
    {
        var primaryAsset = CreateManifestAsset(
            "staticwebassets/css/site.css", "css/site.css", "_content/id", "BootstrapVersion=V5");
        var relatedAsset = CreateManifestAsset(
            "staticwebassets/css/site.css.gz", "css/site.css.gz", "_content/id", "");
        relatedAsset.AssetRole = "Alternative";
        relatedAsset.AssetTraitName = "Content-Encoding";
        relatedAsset.AssetTraitValue = "gzip";
        relatedAsset.RelatedAsset = "staticwebassets/css/site.css";

        var packageRoot = SetupPackageRoot("IdentityUI", primaryAsset, relatedAsset);
        var manifestItem = CreateManifestItem(packageRoot, "IdentityUI");

        var task = new ReadPackageAssetsManifest
        {
            BuildEngine = _buildEngine.Object,
            PackageManifests = new[] { manifestItem },
            // No group declarations → primary excluded
            IntermediateOutputPath = Path.Combine(_tempDir, "obj"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.Assets.Should().HaveCount(0, "both primary and related should be excluded via cascading");
    }

    [Fact]
    public void Endpoints_ForExcludedAssets_AreFilteredOut()
    {
        var includedAsset = CreateManifestAsset(
            "staticwebassets/app.js", "app.js", "_content/id", "");
        var excludedAsset = CreateManifestAsset(
            "staticwebassets/css/site.css", "css/site.css", "_content/id", "BootstrapVersion=V5");

        var endpoints = new[]
        {
            new StaticWebAssetEndpoint
            {
                Route = "_content/id/app.js",
                AssetFile = "staticwebassets/app.js",
                Selectors = [],
                ResponseHeaders = [],
                EndpointProperties = []
            },
            new StaticWebAssetEndpoint
            {
                Route = "_content/id/css/site.css",
                AssetFile = "staticwebassets/css/site.css",
                Selectors = [],
                ResponseHeaders = [],
                EndpointProperties = []
            }
        };

        var packageRoot = SetupPackageRootWithEndpoints("IdentityUI",
            new[] { includedAsset, excludedAsset }, endpoints);
        var manifestItem = CreateManifestItem(packageRoot, "IdentityUI");

        var task = new ReadPackageAssetsManifest
        {
            BuildEngine = _buildEngine.Object,
            PackageManifests = new[] { manifestItem },
            // No group declarations → grouped asset excluded
            IntermediateOutputPath = Path.Combine(_tempDir, "obj"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.Assets.Should().HaveCount(1, "only ungrouped asset should be included");
        task.Endpoints.Should().HaveCount(1, "endpoint for excluded asset should be removed");
        task.Endpoints[0].ItemSpec.Should().Be("_content/id/app.js");
    }

    [Fact]
    public void DeferredGroups_SkippedDuringEagerFiltering()
    {
        var packageRoot = SetupPackageRoot("IdentityUI",
            CreateManifestAsset("staticwebassets/css/site.css", "css/site.css", "_content/id",
                "ServerRendering=true"));

        var manifestItem = CreateManifestItem(packageRoot, "IdentityUI");

        var task = new ReadPackageAssetsManifest
        {
            BuildEngine = _buildEngine.Object,
            PackageManifests = new[] { manifestItem },
            StaticWebAssetGroups = new ITaskItem[]
            {
                new TaskItem("ServerRendering", new Dictionary<string, string>
                {
                    ["Value"] = "",  // No value yet — deferred
                    ["SourceId"] = "IdentityUI",
                    ["Deferred"] = "true"
                })
            },
            IntermediateOutputPath = Path.Combine(_tempDir, "obj"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.Assets.Should().HaveCount(1,
            "deferred group requirements should be skipped during eager filtering");
    }

    [Fact]
    public void FrameworkAssets_MaterializedToIntermediateDirectory()
    {
        var packageRoot = SetupPackageRoot("MyLib", new PackageManifestAsset
        {
            PackagePath = "staticwebassets/js/framework.js",
            RelativePath = "js/framework.js",
            BasePath = "_content/mylib",
            SourceType = "Framework",
            AssetKind = "All",
            AssetMode = "All",
            AssetRole = "Primary",
            RelatedAsset = "",
            AssetTraitName = "",
            AssetTraitValue = "",
            AssetGroups = "",
            Fingerprint = "abc",
            Integrity = "sha256-test",
            CopyToOutputDirectory = "Never",
            CopyToPublishDirectory = "PreserveNewest",
            FileLength = "6",
            LastWriteTime = "Mon, 15 Nov 1990 00:00:00 GMT",
        });

        var manifestItem = CreateManifestItem(packageRoot, "MyLib");
        var intermediateOutput = Path.Combine(_tempDir, "obj");

        var task = new ReadPackageAssetsManifest
        {
            BuildEngine = _buildEngine.Object,
            PackageManifests = new[] { manifestItem },
            IntermediateOutputPath = intermediateOutput,
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.Assets.Should().HaveCount(1);

        var emitted = task.Assets[0];
        // Framework assets should be materialized to the fx directory
        var expectedDir = Path.Combine(intermediateOutput, "fx", "MyLib");
        emitted.ItemSpec.Should().StartWith(expectedDir);
        File.Exists(emitted.ItemSpec).Should().BeTrue();

        // SourceType changes to Discovered for framework materialization
        emitted.GetMetadata("SourceType").Should().Be("Discovered");
        emitted.GetMetadata("SourceId").Should().Be("ConsumerApp");
    }

    [Fact]
    public void InvalidManifestVersion_LogsError()
    {
        var packageDir = Path.Combine(_tempDir, "packages", "BadLib");
        var buildDir = Path.Combine(packageDir, "build");
        Directory.CreateDirectory(buildDir);

        // Write a staticwebassets directory with a file
        var swDir = Path.Combine(packageDir, "staticwebassets");
        Directory.CreateDirectory(swDir);

        var badManifest = new { Version = 2, ManifestType = "Package", Assets = Array.Empty<object>(), Endpoints = Array.Empty<object>() };
        var manifestPath = Path.Combine(buildDir, "BadLib.PackageAssets.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(badManifest));

        var manifestItem = new TaskItem(manifestPath, new Dictionary<string, string>
        {
            ["SourceId"] = "BadLib",
            ["ContentRoot"] = swDir + Path.DirectorySeparatorChar,
            ["PackageRoot"] = packageDir,
        });

        var task = new ReadPackageAssetsManifest
        {
            BuildEngine = _buildEngine.Object,
            PackageManifests = new[] { manifestItem },
            IntermediateOutputPath = Path.Combine(_tempDir, "obj"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        var result = task.Execute();

        result.Should().BeFalse();
        _errorMessages.Should().ContainSingle(m => m.Contains("Unsupported package manifest version"));
    }

    // Helpers

    private string SetupPackageRoot(string packageId, params PackageManifestAsset[] assets)
    {
        return SetupPackageRootWithEndpoints(packageId, assets, Array.Empty<StaticWebAssetEndpoint>());
    }

    private string SetupPackageRootWithEndpoints(string packageId, PackageManifestAsset[] assets, StaticWebAssetEndpoint[] endpoints)
    {
        var packageDir = Path.Combine(_tempDir, "packages", packageId);
        var buildDir = Path.Combine(packageDir, "build");
        Directory.CreateDirectory(buildDir);

        // Create actual files for each asset
        foreach (var asset in assets)
        {
            var filePath = Path.Combine(packageDir, asset.PackagePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, "content-" + asset.PackagePath);
        }

        var manifest = new StaticWebAssetPackageManifest
        {
            Version = 1,
            ManifestType = "Package",
            Assets = assets,
            Endpoints = endpoints,
        };

        var manifestPath = Path.Combine(buildDir, packageId + ".PackageAssets.json");
        var json = JsonSerializer.SerializeToUtf8Bytes(manifest,
            StaticWebAssetsJsonSerializerContext.Default.StaticWebAssetPackageManifest);
        File.WriteAllBytes(manifestPath, json);

        return packageDir;
    }

    private ITaskItem CreateManifestItem(string packageRoot, string sourceId)
    {
        var buildDir = Path.Combine(packageRoot, "build");
        var manifestPath = Path.Combine(buildDir, sourceId + ".PackageAssets.json");
        var contentRoot = Path.Combine(packageRoot, "staticwebassets") + Path.DirectorySeparatorChar;

        return new TaskItem(manifestPath, new Dictionary<string, string>
        {
            ["SourceId"] = sourceId,
            ["ContentRoot"] = contentRoot,
            ["PackageRoot"] = packageRoot,
        });
    }

    private static PackageManifestAsset CreateManifestAsset(
        string packagePath, string relativePath, string basePath, string assetGroups)
    {
        return new PackageManifestAsset
        {
            PackagePath = packagePath,
            RelativePath = relativePath,
            BasePath = basePath,
            SourceType = "Package",
            AssetKind = "All",
            AssetMode = "All",
            AssetRole = "Primary",
            RelatedAsset = "",
            AssetTraitName = "",
            AssetTraitValue = "",
            AssetGroups = assetGroups,
            Fingerprint = "test",
            Integrity = "sha256-test",
            CopyToOutputDirectory = "Never",
            CopyToPublishDirectory = "PreserveNewest",
            FileLength = "6",
            LastWriteTime = "Mon, 15 Nov 1990 00:00:00 GMT",
        };
    }
}
