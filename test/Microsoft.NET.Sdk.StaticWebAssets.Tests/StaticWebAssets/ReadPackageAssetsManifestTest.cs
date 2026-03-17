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
        var packageRoot = SetupPackageRoot("MyLib",
            CreateManifestAsset("staticwebassets/css/site.css", "css/site.css", "_content/mylib", ""));

        var manifestItem = CreateManifestItem(packageRoot, "MyLib");

        var task = CreateReadManifestTask(new[] { manifestItem });
        task.Execute().Should().BeTrue();

        task.Assets.Should().HaveCount(1);

        var emitted = task.Assets[0];
        emitted.GetMetadata("SourceType").Should().Be("Package");
        emitted.GetMetadata("SourceId").Should().Be("MyLib");
        emitted.GetMetadata("BasePath").Should().Be("_content/mylib");
        emitted.GetMetadata("RelativePath").Should().Be("css/site.css");
        emitted.GetMetadata("Fingerprint").Should().Be("test");
    }

    [Fact]
    public void UngroupedAssets_AlwaysIncluded()
    {
        var packageRoot = SetupPackageRoot("MyLib",
            CreateManifestAsset("staticwebassets/app.js", "app.js", "_content/mylib", ""));

        var manifestItem = CreateManifestItem(packageRoot, "MyLib");

        var task = CreateReadManifestTask(
            new[] { manifestItem },
            new[] { CreateGroup("SomeGroup", "SomeValue", "OtherLib") });
        task.Execute().Should().BeTrue();

        task.Assets.Should().HaveCount(1, "ungrouped assets should always be included");
    }

    [Fact]
    public void GroupedAsset_MatchingDeclaration_IsIncluded()
    {
        var packageRoot = SetupPackageRoot("IdentityUI",
            CreateManifestAsset("staticwebassets/css/site.css", "css/site.css", "_content/id", "BootstrapVersion=V5"));

        var manifestItem = CreateManifestItem(packageRoot, "IdentityUI");

        var task = CreateReadManifestTask(
            new[] { manifestItem },
            new[] { CreateGroup("BootstrapVersion", "V5", "IdentityUI") });
        task.Execute().Should().BeTrue();

        task.Assets.Should().HaveCount(1);
    }

    [Fact]
    public void GroupedAsset_NoDeclarations_IsExcluded()
    {
        var packageRoot = SetupPackageRoot("IdentityUI",
            CreateManifestAsset("staticwebassets/css/site.css", "css/site.css", "_content/id", "BootstrapVersion=V5"));

        var manifestItem = CreateManifestItem(packageRoot, "IdentityUI");

        // No StaticWebAssetGroups
        var task = CreateReadManifestTask(new[] { manifestItem });
        task.Execute().Should().BeTrue();

        task.Assets.Should().HaveCount(0, "grouped assets should be excluded with no declarations");
    }

    [Fact]
    public void MultiGroup_PartialMatch_IsExcluded()
    {
        var packageRoot = SetupPackageRoot("IdentityUI",
            CreateManifestAsset("staticwebassets/css/site.css", "css/site.css", "_content/id",
                "BootstrapVersion=V5;DebugAssets=true"));

        var manifestItem = CreateManifestItem(packageRoot, "IdentityUI");

        // BootstrapVersion declared but DebugAssets not declared
        var task = CreateReadManifestTask(
            new[] { manifestItem },
            new[] { CreateGroup("BootstrapVersion", "V5", "IdentityUI") });
        task.Execute().Should().BeTrue();

        task.Assets.Should().HaveCount(0, "AND-matching: all group entries must be satisfied");
    }

    [Fact]
    public void CascadingExclusion_RelatedAssetExcludedWithPrimary()
    {
        var primaryAsset = CreateManifestAsset(
            "staticwebassets/css/site.css", "css/site.css", "_content/id", "BootstrapVersion=V5");
        var relatedAsset = CreateManifestAsset(
            "staticwebassets/css/site.css.gz", "css/site.css.gz", "_content/id", "");
        relatedAsset.Value.AssetRole = "Alternative";
        relatedAsset.Value.AssetTraitName = "Content-Encoding";
        relatedAsset.Value.AssetTraitValue = "gzip";
        relatedAsset.Value.RelatedAsset = "staticwebassets/css/site.css";

        var packageRoot = SetupPackageRoot("IdentityUI", primaryAsset, relatedAsset);
        var manifestItem = CreateManifestItem(packageRoot, "IdentityUI");

        // No group declarations → primary excluded
        var task = CreateReadManifestTask(new[] { manifestItem });
        task.Execute().Should().BeTrue();

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

        // No group declarations → grouped asset excluded
        var task = CreateReadManifestTask(new[] { manifestItem });
        task.Execute().Should().BeTrue();

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

        var task = CreateReadManifestTask(
            new[] { manifestItem },
            new[] { CreateGroup("ServerRendering", "", "IdentityUI", deferred: true) });
        task.Execute().Should().BeTrue();

        task.Assets.Should().HaveCount(1,
            "deferred group requirements should be skipped during eager filtering");
    }

    [Fact]
    public void FrameworkAssets_MaterializedToIntermediateDirectory()
    {
        var packageRoot = SetupPackageRoot("MyLib",
            CreateManifestAsset("staticwebassets/js/framework.js", "js/framework.js", "_content/mylib", "", "Framework"));

        var manifestItem = CreateManifestItem(packageRoot, "MyLib");

        var task = CreateReadManifestTask(new[] { manifestItem });
        task.Execute().Should().BeTrue();

        task.Assets.Should().HaveCount(1);

        var emitted = task.Assets[0];
        // Framework assets should be materialized to the fx directory
        var expectedDir = Path.Combine(_tempDir, "obj", "fx", "MyLib");
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

        var badManifest = new { Version = 2, ManifestType = "Package", Assets = new Dictionary<string, object>(), Endpoints = Array.Empty<object>() };
        var manifestPath = Path.Combine(buildDir, "BadLib.PackageAssets.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(badManifest));

        var manifestItem = new TaskItem(manifestPath, new Dictionary<string, string>
        {
            ["SourceId"] = "BadLib",
            ["ContentRoot"] = swDir + Path.DirectorySeparatorChar,
            ["PackageRoot"] = packageDir,
        });

        var task = CreateReadManifestTask(new[] { manifestItem });
        var result = task.Execute();

        result.Should().BeFalse();
        _errorMessages.Should().ContainSingle(m => m.Contains("Unsupported package manifest version"));
    }

    [Fact]
    public void MissingIntermediateOutputPath_ProducesError()
    {
        var packageRoot = SetupPackageRoot("MyLib",
            CreateManifestAsset("staticwebassets/css/site.css", "css/site.css", "_content/mylib", ""));
        var manifestItem = CreateManifestItem(packageRoot, "MyLib");

        var task = new ReadPackageAssetsManifest
        {
            BuildEngine = _buildEngine.Object,
            PackageManifests = new[] { manifestItem },
            StaticWebAssetGroups = Array.Empty<ITaskItem>(),
            IntermediateOutputPath = null,
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };

        var result = task.Execute();

        result.Should().BeFalse();
        _errorMessages.Should().ContainSingle(m => m.Contains("IntermediateOutputPath is required"));
    }

    private ReadPackageAssetsManifest CreateReadManifestTask(
        ITaskItem[] manifests,
        ITaskItem[] groups = null)
    {
        return new ReadPackageAssetsManifest
        {
            BuildEngine = _buildEngine.Object,
            PackageManifests = manifests,
            StaticWebAssetGroups = groups ?? Array.Empty<ITaskItem>(),
            IntermediateOutputPath = Path.Combine(_tempDir, "obj"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };
    }

    private static ITaskItem CreateGroup(string name, string value, string sourceId, bool deferred = false)
    {
        var dict = new Dictionary<string, string>
        {
            ["Value"] = value,
            ["SourceId"] = sourceId,
        };
        if (deferred)
            dict["Deferred"] = "true";
        return new TaskItem(name, dict);
    }

    private string SetupPackageRoot(string packageId, params KeyValuePair<string, StaticWebAsset>[] assets)
    {
        return SetupPackageRootWithEndpoints(packageId, assets, Array.Empty<StaticWebAssetEndpoint>());
    }

    private string SetupPackageRootWithEndpoints(string packageId, KeyValuePair<string, StaticWebAsset>[] assets, StaticWebAssetEndpoint[] endpoints)
    {
        var packageDir = Path.Combine(_tempDir, "packages", packageId);
        var buildDir = Path.Combine(packageDir, "build");
        Directory.CreateDirectory(buildDir);

        // Create actual files for each asset and fill in SourceId/ContentRoot
        // the way GeneratePackageAssetsManifestFile does (via copy constructor).
        var contentRoot = Path.Combine(packageDir, "staticwebassets") + Path.DirectorySeparatorChar;
        foreach (var asset in assets)
        {
            var filePath = Path.Combine(packageDir, asset.Key.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, "content-" + asset.Key);

            asset.Value.SourceId = packageId;
            asset.Value.ContentRoot = contentRoot;
        }

        var manifest = new StaticWebAssetPackageManifest
        {
            Version = 1,
            ManifestType = "Package",
            Assets = assets.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase),
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

    private static KeyValuePair<string, StaticWebAsset> CreateManifestAsset(
        string packagePath, string relativePath, string basePath, string assetGroups, string sourceType = "Package")
    {
        var asset = new StaticWebAsset
        {
            Identity = packagePath,
            RelativePath = relativePath,
            BasePath = basePath,
            SourceType = sourceType,
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
            FileLength = 6,
            LastWriteTime = new DateTimeOffset(1990, 11, 15, 0, 0, 0, TimeSpan.Zero),
        };

        return new KeyValuePair<string, StaticWebAsset>(packagePath, asset);
    }

    // Scenario 6: Custom .targets author override
    // A package author disables the auto-generated .targets and manually provides
    // their own .targets that still registers a StaticWebAssetPackageManifest item.
    // The consumer's ReadPackageAssetsManifest should still work correctly.
    [Fact]
    public void CustomTargetsOverride_ManualManifestItem_WorksCorrectly()
    {
        // Setup: simulate a package that has its manifest at a custom location
        // (as if the author wrote their own .targets pointing to the manifest)
        var packageRoot = SetupPackageRoot("CustomLib",
            CreateManifestAsset("staticwebassets/js/custom.js", "js/custom.js", "_content/customlib", ""),
            CreateManifestAsset("staticwebassets/css/theme.css", "css/theme.css", "_content/customlib", ""));

        // The manifest item metadata mirrors what a hand-authored .targets would produce.
        // The key difference from auto-generated: the author controls the paths.
        var manifestItem = CreateManifestItem(packageRoot, "CustomLib");

        var task = CreateReadManifestTask(new[] { manifestItem });
        task.Execute().Should().BeTrue();

        task.Assets.Should().HaveCount(2);

        // Both assets should have the custom package's SourceId
        task.Assets.Should().OnlyContain(a => a.GetMetadata("SourceId") == "CustomLib");
        // Both should resolve Identity paths under the package root
        task.Assets.Should().OnlyContain(a => a.ItemSpec.StartsWith(
            Path.Combine(packageRoot, "staticwebassets")));
    }

    // Scenario 7: Multiple packages contributing manifests to the same consumer
    // Two packages each provide a StaticWebAssetPackageManifest item.
    // Group filtering should be applied independently per SourceId.
    [Fact]
    public void MultiplePackages_IndependentGroupFilteringPerSourceId()
    {
        // Package A: has grouped assets (BootstrapVersion=V5)
        var packageRootA = SetupPackageRoot("PkgA",
            CreateManifestAsset("staticwebassets/css/a.css", "css/a.css", "_content/pkga", "BootstrapVersion=V5"),
            CreateManifestAsset("staticwebassets/js/a.js", "js/a.js", "_content/pkga", ""));

        // Package B: has grouped assets (Theme=Dark) and ungrouped
        var packageRootB = SetupPackageRoot("PkgB",
            CreateManifestAsset("staticwebassets/css/b.css", "css/b.css", "_content/pkgb", "Theme=Dark"),
            CreateManifestAsset("staticwebassets/js/b.js", "js/b.js", "_content/pkgb", ""));

        var manifestA = CreateManifestItem(packageRootA, "PkgA");
        var manifestB = CreateManifestItem(packageRootB, "PkgB");

        // Consumer declares BootstrapVersion=V5 for PkgA but NOT Theme for PkgB
        var task = CreateReadManifestTask(
            new[] { manifestA, manifestB },
            new[] { CreateGroup("BootstrapVersion", "V5", "PkgA") });
        task.Execute().Should().BeTrue();

        // PkgA: css/a.css included (group matched), js/a.js included (ungrouped)
        // PkgB: css/b.css excluded (Theme=Dark not declared), js/b.js included (ungrouped)
        task.Assets.Should().HaveCount(3);

        var assetPaths = task.Assets.Select(a => a.GetMetadata("RelativePath")).ToList();
        assetPaths.Should().Contain("css/a.css");
        assetPaths.Should().Contain("js/a.js");
        assetPaths.Should().Contain("js/b.js");
        assetPaths.Should().NotContain("css/b.css");

        // Verify SourceIds are correct
        task.Assets.Where(a => a.GetMetadata("SourceId") == "PkgA").Should().HaveCount(2);
        task.Assets.Where(a => a.GetMetadata("SourceId") == "PkgB").Should().HaveCount(1);
    }
}
