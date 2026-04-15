// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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
