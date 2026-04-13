// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.IO.Compression;
using System.Text.Json;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

public class GeneratePublishAssetPackTest : IDisposable
{
    private readonly string _testDir;
    private readonly List<string> _errorMessages;
    private readonly List<string> _messages;
    private readonly Mock<IBuildEngine> _buildEngine;

    public GeneratePublishAssetPackTest()
    {
        _testDir = Path.Combine(
            SdkTestContext.Current.TestExecutionDirectory,
            nameof(GeneratePublishAssetPackTest),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        _errorMessages = new List<string>();
        _messages = new List<string>();
        _buildEngine = new Mock<IBuildEngine>();
        _buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => _errorMessages.Add(args.Message));
        _buildEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
            .Callback<BuildMessageEventArgs>(args => _messages.Add(args.Message));
    }

    [Fact]
    public void CreatesPackWithManifestAndAssets()
    {
        var manifestPath = CreateManifestFile(new[] { "js/site.js", "css/app.css" });
        var assets = new[]
        {
            CreateAssetOnDisk("js/site.js", "js content"),
            CreateAssetOnDisk("css/app.css", "css content"),
        };

        var packPath = Path.Combine(_testDir, "output.zip");
        var task = new GeneratePublishAssetPack
        {
            BuildEngine = _buildEngine.Object,
            ManifestPath = manifestPath,
            Assets = assets,
            PackOutputPath = packPath,
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.GeneratedPackPath.Should().NotBeNullOrEmpty();
        File.Exists(task.GeneratedPackPath).Should().BeTrue();

        // Verify zip contents
        using var archive = ZipFile.OpenRead(task.GeneratedPackPath);
        archive.GetEntry("manifest.json").Should().NotBeNull();
        archive.GetEntry("assets/js/site.js").Should().NotBeNull();
        archive.GetEntry("assets/css/app.css").Should().NotBeNull();
    }

    [Fact]
    public void ManifestContentIsPreserved()
    {
        var manifestPath = CreateManifestFile(new[] { "js/site.js" });
        var expectedContent = File.ReadAllText(manifestPath);

        var assets = new[] { CreateAssetOnDisk("js/site.js", "content") };
        var packPath = Path.Combine(_testDir, "output.zip");

        var task = new GeneratePublishAssetPack
        {
            BuildEngine = _buildEngine.Object,
            ManifestPath = manifestPath,
            Assets = assets,
            PackOutputPath = packPath,
        };

        task.Execute();

        using var archive = ZipFile.OpenRead(task.GeneratedPackPath);
        var entry = archive.GetEntry("manifest.json");
        using var reader = new StreamReader(entry.Open());
        var actualContent = reader.ReadToEnd();

        // Both should deserialize to equivalent manifests
        var expected = JsonSerializer.Deserialize<StaticWebAssetsManifest>(expectedContent);
        var actual = JsonSerializer.Deserialize<StaticWebAssetsManifest>(actualContent);
        actual.Assets.Length.Should().Be(expected.Assets.Length);
        actual.Source.Should().Be(expected.Source);
    }

    [Fact]
    public void AssetContentIsPreserved()
    {
        var content = "exact content that should be preserved";
        var manifestPath = CreateManifestFile(new[] { "js/site.js" });
        var assets = new[] { CreateAssetOnDisk("js/site.js", content) };
        var packPath = Path.Combine(_testDir, "output.zip");

        var task = new GeneratePublishAssetPack
        {
            BuildEngine = _buildEngine.Object,
            ManifestPath = manifestPath,
            Assets = assets,
            PackOutputPath = packPath,
        };

        task.Execute();

        using var archive = ZipFile.OpenRead(task.GeneratedPackPath);
        var entry = archive.GetEntry("assets/js/site.js");
        using var reader = new StreamReader(entry.Open());
        reader.ReadToEnd().Should().Be(content);
    }

    [Fact]
    public void ExcludesCompressedAssets()
    {
        var manifestPath = CreateManifestFile(new[] { "js/site.js" });
        var originalAsset = CreateAssetOnDisk("js/site.js", "original");

        // Create a compressed asset
        var compressedAsset = CreateAssetOnDisk("js/site.js.gz", "compressed");
        compressedAsset.SetMetadata(nameof(StaticWebAsset.AssetTraitName), "Content-Encoding");
        compressedAsset.SetMetadata(nameof(StaticWebAsset.AssetTraitValue), "gzip");

        var packPath = Path.Combine(_testDir, "output.zip");
        var task = new GeneratePublishAssetPack
        {
            BuildEngine = _buildEngine.Object,
            ManifestPath = manifestPath,
            Assets = new[] { originalAsset, compressedAsset },
            PackOutputPath = packPath,
        };

        task.Execute();

        using var archive = ZipFile.OpenRead(task.GeneratedPackPath);
        archive.GetEntry("assets/js/site.js").Should().NotBeNull();
        archive.GetEntry("assets/js/site.js.gz").Should().BeNull();
        // manifest + 1 asset = 2 entries total
        archive.Entries.Count.Should().Be(2);
    }

    [Fact]
    public void OverwritesExistingPack()
    {
        var manifestPath = CreateManifestFile(new[] { "js/site.js" });
        var assets = new[] { CreateAssetOnDisk("js/site.js", "content") };
        var packPath = Path.Combine(_testDir, "output.zip");

        // Write garbage to existing file
        File.WriteAllText(packPath, "not a zip");

        var task = new GeneratePublishAssetPack
        {
            BuildEngine = _buildEngine.Object,
            ManifestPath = manifestPath,
            Assets = assets,
            PackOutputPath = packPath,
        };

        var result = task.Execute();

        result.Should().BeTrue();
        // Verify it's a valid zip now
        using var archive = ZipFile.OpenRead(task.GeneratedPackPath);
        archive.GetEntry("manifest.json").Should().NotBeNull();
    }

    [Fact]
    public void FailsWhenManifestDoesNotExist()
    {
        var task = new GeneratePublishAssetPack
        {
            BuildEngine = _buildEngine.Object,
            ManifestPath = Path.Combine(_testDir, "nonexistent.json"),
            Assets = Array.Empty<ITaskItem>(),
            PackOutputPath = Path.Combine(_testDir, "output.zip"),
        };

        var result = task.Execute();

        result.Should().BeFalse();
        _errorMessages.Should().Contain(m => m.Contains("does not exist"));
    }

    [Fact]
    public void SkipsDuplicateRelativePaths()
    {
        var manifestPath = CreateManifestFile(new[] { "js/site.js" });
        // Two assets with same relative path (e.g., from different sources)
        var asset1 = CreateAssetOnDisk("js/site.js", "content1");
        var asset2 = CreateAssetOnDisk("js/site.js", "content2", "alt");

        var packPath = Path.Combine(_testDir, "output.zip");
        var task = new GeneratePublishAssetPack
        {
            BuildEngine = _buildEngine.Object,
            ManifestPath = manifestPath,
            Assets = new[] { asset1, asset2 },
            PackOutputPath = packPath,
        };

        var result = task.Execute();

        result.Should().BeTrue();
        using var archive = ZipFile.OpenRead(task.GeneratedPackPath);
        // Should have manifest + 1 unique asset
        archive.Entries.Count.Should().Be(2);
    }

    [Fact]
    public void HandlesNestedDirectoryStructure()
    {
        var manifestPath = CreateManifestFile(new[]
        {
            "lib/deep/nested/file.js",
            "css/themes/dark/style.css"
        });
        var assets = new[]
        {
            CreateAssetOnDisk("lib/deep/nested/file.js", "deep js"),
            CreateAssetOnDisk("css/themes/dark/style.css", "dark css"),
        };

        var packPath = Path.Combine(_testDir, "output.zip");
        var task = new GeneratePublishAssetPack
        {
            BuildEngine = _buildEngine.Object,
            ManifestPath = manifestPath,
            Assets = assets,
            PackOutputPath = packPath,
        };

        task.Execute();

        using var archive = ZipFile.OpenRead(task.GeneratedPackPath);
        archive.GetEntry("assets/lib/deep/nested/file.js").Should().NotBeNull();
        archive.GetEntry("assets/css/themes/dark/style.css").Should().NotBeNull();
    }

    // --- Helpers ---

    private TaskItem CreateAssetOnDisk(string relativePath, string content, string subfolder = null)
    {
        var root = subfolder != null
            ? Path.Combine(_testDir, subfolder, "wwwroot")
            : Path.Combine(_testDir, "wwwroot");
        var filePath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(filePath, content);

        var item = new TaskItem(filePath);
        item.SetMetadata(nameof(StaticWebAsset.RelativePath), relativePath);
        item.SetMetadata(nameof(StaticWebAsset.ContentRoot), root);
        item.SetMetadata(nameof(StaticWebAsset.SourceType), StaticWebAsset.SourceTypes.Discovered);
        item.SetMetadata(nameof(StaticWebAsset.SourceId), "TestApp");
        item.SetMetadata(nameof(StaticWebAsset.AssetKind), StaticWebAsset.AssetKinds.Publish);
        item.SetMetadata(nameof(StaticWebAsset.AssetMode), StaticWebAsset.AssetModes.All);
        item.SetMetadata(nameof(StaticWebAsset.AssetRole), StaticWebAsset.AssetRoles.Primary);
        item.SetMetadata(nameof(StaticWebAsset.AssetTraitName), "");
        item.SetMetadata(nameof(StaticWebAsset.AssetTraitValue), "");
        item.SetMetadata(nameof(StaticWebAsset.Fingerprint), "fp123");
        item.SetMetadata(nameof(StaticWebAsset.Integrity), "integ123");
        item.SetMetadata(nameof(StaticWebAsset.OriginalItemSpec), filePath);
        item.SetMetadata(nameof(StaticWebAsset.BasePath), "_content/TestApp");
        item.SetMetadata(nameof(StaticWebAsset.CopyToOutputDirectory), StaticWebAsset.AssetCopyOptions.Never);
        item.SetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory), StaticWebAsset.AssetCopyOptions.PreserveNewest);
        item.SetMetadata(nameof(StaticWebAsset.FileLength), content.Length.ToString());
        item.SetMetadata(nameof(StaticWebAsset.LastWriteTime), DateTime.UtcNow.Ticks.ToString());

        return item;
    }

    private string CreateManifestFile(string[] assetRelativePaths)
    {
        var assets = assetRelativePaths.Select(rp => new StaticWebAsset
        {
            Identity = Path.Combine(_testDir, "wwwroot", rp.Replace('/', Path.DirectorySeparatorChar)),
            OriginalItemSpec = Path.Combine("wwwroot", rp.Replace('/', Path.DirectorySeparatorChar)),
            RelativePath = rp,
            ContentRoot = Path.Combine(_testDir, "wwwroot"),
            SourceType = StaticWebAsset.SourceTypes.Discovered,
            SourceId = "TestApp",
            BasePath = "_content/TestApp",
            AssetKind = StaticWebAsset.AssetKinds.Publish,
            AssetMode = StaticWebAsset.AssetModes.All,
            AssetRole = StaticWebAsset.AssetRoles.Primary,
            AssetTraitName = "",
            AssetTraitValue = "",
            Fingerprint = "fp123",
            Integrity = "integ123",
            FileLength = 100,
            LastWriteTime = DateTime.UtcNow,
        }).ToArray();

        var manifest = StaticWebAssetsManifest.Create(
            source: "TestApp",
            basePath: "/",
            mode: "Default",
            manifestType: "Publish",
            referencedProjectConfigurations: Array.Empty<StaticWebAssetsManifest.ReferencedProjectConfiguration>(),
            discoveryPatterns: Array.Empty<StaticWebAssetsDiscoveryPattern>(),
            assets: assets,
            endpoints: Array.Empty<StaticWebAssetEndpoint>());

        var manifestPath = Path.Combine(_testDir, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));
        return manifestPath;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup — don't fail tests if temp files are locked
        }
    }
}
