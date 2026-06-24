// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

[TestClass]

public class GeneratePackageAssetsManifestFileTest : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IBuildEngine> _buildEngine;
    private readonly List<string> _errorMessages;

    public GeneratePackageAssetsManifestFileTest()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "GenPkgManifest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _errorMessages = new List<string>();
        _buildEngine = new Mock<IBuildEngine>();
        _buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => _errorMessages.Add(args.Message));
        _buildEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public void EmptyAssets_DoesNotGenerateManifestFile()
    {
        var manifestPath = Path.Combine(_tempDir, "empty.json");

        var task = new GeneratePackageAssetsManifestFile
        {
            BuildEngine = _buildEngine.Object,
            StaticWebAssets = Array.Empty<ITaskItem>(),
            StaticWebAssetEndpoints = Array.Empty<ITaskItem>(),
            TargetManifestPath = manifestPath,
        };

        var result = task.Execute();

        result.Should().BeTrue();
        File.Exists(manifestPath).Should().BeFalse();
    }

    [TestMethod]
    public void Assets_SerializedWithCorrectPackagePaths()
    {
        var file = CreateTempFile("wwwroot", "css", "site.css", "body{}");
        var contentRoot = Path.Combine(_tempDir, "wwwroot") + Path.DirectorySeparatorChar;

        var asset = CreateAsset(file, contentRoot, "css/site#[.{fingerprint}]?.css", "abc123");

        var task = CreateManifestTask(new[] { asset.ToTaskItem() });
        task.Execute().Should().BeTrue();
        var manifest = DeserializeManifest(task.TargetManifestPath);

        manifest.Assets.Should().HaveCount(1);
        var manifestAsset = manifest.Assets.Values.Single();
        var packagePath = manifest.Assets.Keys.Single();

        // Discovered assets don't include BasePath in the target path
        packagePath.Should().EndWith("css/site.css");
        manifestAsset.RelativePath.Should().Be("css/site#[.{fingerprint}]?.css");
        manifestAsset.AssetRole.Should().Be("Primary");
    }

    [TestMethod]
    public void RelatedAsset_RemappedToPackageRelativePath()
    {
        var primaryFile = CreateTempFile("wwwroot", "css", "site.css", "body{}");
        var relatedFile = CreateTempFile("wwwroot", "css", "site.css.gz", "compressed");
        var contentRoot = Path.Combine(_tempDir, "wwwroot") + Path.DirectorySeparatorChar;

        var primary = CreateAsset(primaryFile, contentRoot, "css/site.css", "abc");

        var related = CreateAsset(relatedFile, contentRoot, "css/site.css.gz", "def");
        related.AssetRole = "Alternative";
        related.RelatedAsset = primaryFile;
        related.AssetTraitName = "Content-Encoding";
        related.AssetTraitValue = "gzip";

        var task = CreateManifestTask(
            new[] { primary.ToTaskItem(), related.ToTaskItem() });
        task.Execute().Should().BeTrue();
        var manifest = DeserializeManifest(task.TargetManifestPath);

        manifest.Assets.Should().HaveCount(2);

        var relatedAsset = manifest.Assets.Values.First(a => a.AssetRole == "Alternative");
        // The RelatedAsset should be remapped from the absolute path to a package-relative path
        relatedAsset.RelatedAsset.Should().NotBe(primaryFile);
        relatedAsset.RelatedAsset.Should().NotBeNullOrEmpty();
        // It should match the primary's PackagePath
        var primaryAssetPath = manifest.Assets.First(kvp => kvp.Value.AssetRole == "Primary").Key;
        relatedAsset.RelatedAsset.Should().Be(primaryAssetPath);
    }

    [TestMethod]
    public void Endpoints_AssetFileRemappedToPackageRelativePath()
    {
        var file = CreateTempFile("wwwroot", "js", "app.js", "var x;");
        var contentRoot = Path.Combine(_tempDir, "wwwroot") + Path.DirectorySeparatorChar;

        var asset = CreateAsset(file, contentRoot, "js/app.js", "abc");

        var endpoint = new StaticWebAssetEndpoint
        {
            Route = "_content/mylib/js/app.js",
            AssetFile = file,
            Selectors = [],
            ResponseHeaders = [new() { Name = "Content-Type", Value = "text/javascript" }],
            EndpointProperties = [],
        };

        var task = CreateManifestTask(
            new[] { asset.ToTaskItem() },
            StaticWebAssetEndpoint.ToTaskItems(new[] { endpoint }));
        task.Execute().Should().BeTrue();
        var manifest = DeserializeManifest(task.TargetManifestPath);

        manifest.Endpoints.Should().HaveCount(1);
        var ep = manifest.Endpoints[0];
        // AssetFile should be remapped from absolute to package-relative
        ep.AssetFile.Should().NotBe(file);
        ep.AssetFile.Should().Be(manifest.Assets.Keys.Single());
    }

    [TestMethod]
    public void FrameworkPattern_TagsMatchingAssetsAsFramework()
    {
        var fwFile = CreateTempFile("wwwroot", "js", "framework.js", "fw");
        var nonFwFile = CreateTempFile("wwwroot", "js", "app.js", "app");
        var contentRoot = Path.Combine(_tempDir, "wwwroot") + Path.DirectorySeparatorChar;

        var fwAsset = CreateAsset(fwFile, contentRoot, "js/framework.js", "abc");
        var nonFwAsset = CreateAsset(nonFwFile, contentRoot, "js/app.js", "def");

        var task = CreateManifestTask(
            new[] { fwAsset.ToTaskItem(), nonFwAsset.ToTaskItem() },
            frameworkPattern: "js/framework*");
        task.Execute().Should().BeTrue();
        var manifest = DeserializeManifest(task.TargetManifestPath);

        manifest.Assets.Should().HaveCount(2);

        var fwManifestAsset = manifest.Assets.Values.First(a => a.RelativePath == "js/framework.js");
        fwManifestAsset.SourceType.Should().Be("Framework");

        var nonFwManifestAsset = manifest.Assets.Values.First(a => a.RelativePath == "js/app.js");
        nonFwManifestAsset.SourceType.Should().Be("Package");
    }

    [TestMethod]
    public void AssetGroups_PreservedInManifest()
    {
        var file = CreateTempFile("wwwroot", "css", "site.css", "body{}");
        var contentRoot = Path.Combine(_tempDir, "wwwroot") + Path.DirectorySeparatorChar;

        var asset = CreateAsset(file, contentRoot, "css/site.css", "abc");
        asset.AssetGroups = "BootstrapVersion=V5";

        var task = CreateManifestTask(new[] { asset.ToTaskItem() });
        task.Execute().Should().BeTrue();
        var manifest = DeserializeManifest(task.TargetManifestPath);

        manifest.Assets.Should().HaveCount(1);
        manifest.Assets.Values.Single().AssetGroups.Should().Be("BootstrapVersion=V5");
    }

    [TestMethod]
    public void RelatedAsset_Unmapped_ProducesError()
    {
        // Symmetric to Endpoints_UnmappedAssetFile_ProducesError: exercises the
        // GeneratePackageAssetsManifestFile.cs error branch for RelatedAsset that
        // can't be remapped to a package-relative path (i.e., points outside the
        // packaged asset set). Without this test the RelatedAsset error branch is
        // unexercised by automated tests.
        var primaryFile = CreateTempFile("wwwroot", "css", "site.css", "body{}");
        var relatedFile = CreateTempFile("wwwroot", "css", "site.css.gz", "gz");
        var contentRoot = Path.Combine(_tempDir, "wwwroot") + Path.DirectorySeparatorChar;

        var primary = CreateAsset(primaryFile, contentRoot, "css/site.css", "abc");

        var related = CreateAsset(relatedFile, contentRoot, "css/site.css.gz", "def");
        related.AssetRole = "Alternative";
        related.AssetTraitName = "Content-Encoding";
        related.AssetTraitValue = "gzip";
        // RelatedAsset points to a file that is NOT in the StaticWebAssets input set,
        // so it has no entry in identityToPackagePath and cannot be remapped.
        related.RelatedAsset = Path.Combine(_tempDir, "nonexistent", "primary.css");

        var task = CreateManifestTask(
            new[] { primary.ToTaskItem(), related.ToTaskItem() });
        var result = task.Execute();

        result.Should().BeFalse();
        _errorMessages.Should().ContainSingle(m =>
            m.Contains("could not be mapped to a package-relative path") &&
            m.Contains("RelatedAsset"));
        File.Exists(task.TargetManifestPath).Should().BeFalse(
            "the manifest must not be written when a referential integrity error is detected");
    }

    [TestMethod]
    public void RoundTrip_GenerateThenRead_RelatedAssetResolvesToConsumerAbsolutePath()
    {
        // End-to-end cross-boundary test. Proves the contract between
        // GeneratePackageAssetsManifestFile (producer) and ReadPackageAssetsManifest
        // (consumer): an absolute build-time RelatedAsset is remapped to a
        // package-relative form on the producer side, then re-resolved to an
        // absolute path under the consumer's packageRoot on the consumer side.
        // Two distinct directory trees stand in for producer and consumer machines.
        var primaryFile = CreateTempFile("source", "wwwroot", "css", "site.css", "body{}");
        var relatedFile = CreateTempFile("source", "wwwroot", "css", "site.css.gz", "gz");
        var contentRoot = Path.Combine(_tempDir, "source", "wwwroot") + Path.DirectorySeparatorChar;

        var primary = CreateAsset(primaryFile, contentRoot, "css/site.css", "abc");
        var related = CreateAsset(relatedFile, contentRoot, "css/site.css.gz", "def");
        related.AssetRole = "Alternative";
        related.AssetTraitName = "Content-Encoding";
        related.AssetTraitValue = "gzip";
        related.RelatedAsset = primaryFile;

        // Producer side: write the manifest at the layout ReadPackageAssetsManifest expects:
        //   packageRoot/build/<PackageId>.PackageAssets.json
        var packageRoot = Path.Combine(_tempDir, "packages", "MyLib");
        var buildDir = Path.Combine(packageRoot, "build");
        Directory.CreateDirectory(buildDir);
        var manifestPath = Path.Combine(buildDir, "MyLib.PackageAssets.json");

        var generateTask = new GeneratePackageAssetsManifestFile
        {
            BuildEngine = _buildEngine.Object,
            StaticWebAssets = new[] { primary.ToTaskItem(), related.ToTaskItem() },
            StaticWebAssetEndpoints = Array.Empty<ITaskItem>(),
            TargetManifestPath = manifestPath,
        };
        generateTask.Execute().Should().BeTrue();
        File.Exists(manifestPath).Should().BeTrue();

        // Consumer side: feed the producer's manifest into ReadPackageAssetsManifest
        // pretending we're on a different machine (different packageRoot than the
        // producer's contentRoot). The whole point of producer-side package-relative
        // remap is that the consumer can re-anchor without knowing the producer's CWD.
        var consumerContentRoot = Path.Combine(packageRoot, "staticwebassets") + Path.DirectorySeparatorChar;
        var manifestItem = new TaskItem(manifestPath, new Dictionary<string, string>
        {
            ["SourceId"] = "MyLib",
            ["ContentRoot"] = consumerContentRoot,
            ["PackageRoot"] = packageRoot,
        });

        var readTask = new ReadPackageAssetsManifest
        {
            BuildEngine = _buildEngine.Object,
            PackageManifests = new[] { manifestItem },
            StaticWebAssetGroups = Array.Empty<ITaskItem>(),
            IntermediateOutputPath = Path.Combine(_tempDir, "obj"),
            ProjectPackageId = "ConsumerApp",
            ProjectBasePath = "_content/consumerapp",
        };
        readTask.Execute().Should().BeTrue();
        readTask.Assets.Should().HaveCount(2);

        var emittedRelated = readTask.Assets.Single(a => a.GetMetadata("AssetRole") == "Alternative");
        var emittedPrimary = readTask.Assets.Single(a => a.GetMetadata("AssetRole") == "Primary");

        // The producer's contentRoot is _tempDir/source/wwwroot — the consumer must
        // not see any leakage of it. RelatedAsset on the consumer side must be the
        // primary's Identity (absolute path under the consumer's packageRoot).
        emittedRelated.GetMetadata("RelatedAsset").Should().Be(emittedPrimary.ItemSpec,
            "consumer's RelatedAsset must equal primary's Identity after package-root re-resolution");
        emittedRelated.GetMetadata("RelatedAsset").Should().StartWith(packageRoot,
            "RelatedAsset must be re-anchored to the consumer's packageRoot, not the producer's contentRoot");
        emittedRelated.GetMetadata("RelatedAsset").Should().NotContain(
            Path.Combine(_tempDir, "source"),
            "no producer-side build-time path may leak through the manifest to the consumer");
    }

    [TestMethod]
    public void Endpoints_UnmappedAssetFile_ProducesError()
    {
        var file = CreateTempFile("wwwroot", "js", "app.js", "var x;");
        var contentRoot = Path.Combine(_tempDir, "wwwroot") + Path.DirectorySeparatorChar;

        var asset = CreateAsset(file, contentRoot, "js/app.js", "abc");

        var endpoint = new StaticWebAssetEndpoint
        {
            Route = "_content/mylib/js/missing.js",
            AssetFile = Path.Combine(_tempDir, "nonexistent", "missing.js"),
            Selectors = [],
            ResponseHeaders = [],
            EndpointProperties = [],
        };

        var task = CreateManifestTask(
            new[] { asset.ToTaskItem() },
            StaticWebAssetEndpoint.ToTaskItems(new[] { endpoint }));
        var result = task.Execute();

        result.Should().BeFalse();
        _errorMessages.Should().ContainSingle(m => m.Contains("could not be mapped to a package-relative path"));
    }

    private GeneratePackageAssetsManifestFile CreateManifestTask(
        ITaskItem[] assets,
        ITaskItem[] endpoints = null,
        string frameworkPattern = null)
    {
        var task = new GeneratePackageAssetsManifestFile
        {
            BuildEngine = _buildEngine.Object,
            StaticWebAssets = assets,
            StaticWebAssetEndpoints = endpoints ?? Array.Empty<ITaskItem>(),
            TargetManifestPath = Path.Combine(_tempDir, "manifest.json"),
        };

        if (frameworkPattern != null)
            task.FrameworkPattern = frameworkPattern;

        return task;
    }

    private static StaticWebAssetPackageManifest DeserializeManifest(string manifestPath)
    {
        return JsonSerializer.Deserialize(
            File.ReadAllBytes(manifestPath),
            StaticWebAssetsJsonSerializerContext.Default.StaticWebAssetPackageManifest);
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

    private StaticWebAsset CreateAsset(string filePath, string contentRoot, string relativePath, string fingerprint)
    {
        var asset = new StaticWebAsset
        {
            Identity = filePath,
            SourceType = "Discovered",
            SourceId = "MyLib",
            ContentRoot = contentRoot,
            BasePath = "_content/mylib",
            RelativePath = relativePath,
            AssetKind = "All",
            AssetMode = "All",
            AssetRole = "Primary",
            RelatedAsset = "",
            AssetTraitName = "",
            AssetTraitValue = "",
            CopyToOutputDirectory = "Never",
            CopyToPublishDirectory = "PreserveNewest",
            OriginalItemSpec = filePath,
            Fingerprint = fingerprint,
            Integrity = "sha256-" + fingerprint,
            FileLength = 6,
            LastWriteTime = DateTime.UtcNow,
        };
        asset.ApplyDefaults();
        asset.Normalize();
        return asset;
    }
}
