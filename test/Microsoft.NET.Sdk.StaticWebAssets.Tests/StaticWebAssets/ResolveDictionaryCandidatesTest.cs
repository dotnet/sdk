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

public class ResolveDictionaryCandidatesTest
{
    private readonly string _testDir;
    private readonly List<string> _errorMessages;
    private readonly List<string> _messages;
    private readonly Mock<IBuildEngine> _buildEngine;

    public ResolveDictionaryCandidatesTest()
    {
        _testDir = Path.Combine(
            SdkTestContext.Current.TestExecutionDirectory,
            nameof(ResolveDictionaryCandidatesTest),
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
    public void ReturnsEmptyWhenPackDoesNotExist()
    {
        var task = new ResolveDictionaryCandidates
        {
            BuildEngine = _buildEngine.Object,
            AssetPackPath = Path.Combine(_testDir, "nonexistent.zip"),
            CurrentAssets = new[] { CreateAssetItem("js/site.js", "abc123") },
            OutputPath = Path.Combine(_testDir, "output"),
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.DictionaryCandidates.Should().BeEmpty();
    }

    [Fact]
    public void ReturnsEmptyWhenCurrentAssetsIsEmpty()
    {
        var packPath = CreateTestPack(new[]
        {
            CreateManifestAsset("js/site.js", "hash1"),
        }, new Dictionary<string, string> { ["js/site.js"] = "old content" });

        var task = new ResolveDictionaryCandidates
        {
            BuildEngine = _buildEngine.Object,
            AssetPackPath = packPath,
            CurrentAssets = Array.Empty<ITaskItem>(),
            OutputPath = Path.Combine(_testDir, "output"),
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.DictionaryCandidates.Should().BeEmpty();
    }

    [Fact]
    public void MatchesCurrentAssetToPreviousVersion()
    {
        var packPath = CreateTestPack(new[]
        {
            CreateManifestAsset("js/site.js", "hRQyftXiu1lLX2P9Ly9xa4gHJgLeR1uGN5qegUobtGo="),
        }, new Dictionary<string, string> { ["js/site.js"] = "old content" });

        var currentAsset = CreateAssetItem("js/site.js", "newHash");
        var task = new ResolveDictionaryCandidates
        {
            BuildEngine = _buildEngine.Object,
            AssetPackPath = packPath,
            CurrentAssets = new[] { currentAsset },
            OutputPath = Path.Combine(_testDir, "output"),
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.DictionaryCandidates.Should().HaveCount(1);

        var candidate = task.DictionaryCandidates[0];
        candidate.ItemSpec.Should().Be(currentAsset.ItemSpec);
        candidate.GetMetadata("DictionaryHash").Should().Be(":hRQyftXiu1lLX2P9Ly9xa4gHJgLeR1uGN5qegUobtGo=:");
        candidate.GetMetadata("RelativePath").Should().Be("js/site.js");
        candidate.GetMetadata("DictionaryPath").Should().NotBeEmpty();
        File.Exists(candidate.GetMetadata("DictionaryPath")).Should().BeTrue();
    }

    [Fact]
    public void ExtractsCorrectPreviousFileContent()
    {
        var expectedContent = "previous version content for dictionary";
        var packPath = CreateTestPack(new[]
        {
            CreateManifestAsset("css/app.css", "integrityValue"),
        }, new Dictionary<string, string> { ["css/app.css"] = expectedContent });

        var currentAsset = CreateAssetItem("css/app.css", "newHash");
        var task = new ResolveDictionaryCandidates
        {
            BuildEngine = _buildEngine.Object,
            AssetPackPath = packPath,
            CurrentAssets = new[] { currentAsset },
            OutputPath = Path.Combine(_testDir, "output"),
        };

        task.Execute();

        var extractedPath = task.DictionaryCandidates[0].GetMetadata("DictionaryPath");
        File.ReadAllText(extractedPath).Should().Be(expectedContent);
    }

    [Fact]
    public void SkipsNewAssetsWithNoPreviousVersion()
    {
        var packPath = CreateTestPack(new[]
        {
            CreateManifestAsset("js/old.js", "hash1"),
        }, new Dictionary<string, string> { ["js/old.js"] = "old content" });

        // Current asset has a different RelativePath than what's in the pack
        var currentAsset = CreateAssetItem("js/brand-new.js", "hash2");
        var task = new ResolveDictionaryCandidates
        {
            BuildEngine = _buildEngine.Object,
            AssetPackPath = packPath,
            CurrentAssets = new[] { currentAsset },
            OutputPath = Path.Combine(_testDir, "output"),
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.DictionaryCandidates.Should().BeEmpty();
    }

    [Fact]
    public void SkipsRemovedAssetsNoLongerInCurrent()
    {
        // Pack has two assets but only one exists in current
        var packPath = CreateTestPack(new[]
        {
            CreateManifestAsset("js/kept.js", "hash1"),
            CreateManifestAsset("js/removed.js", "hash2"),
        }, new Dictionary<string, string>
        {
            ["js/kept.js"] = "kept",
            ["js/removed.js"] = "removed"
        });

        var currentAsset = CreateAssetItem("js/kept.js", "newHash");
        var task = new ResolveDictionaryCandidates
        {
            BuildEngine = _buildEngine.Object,
            AssetPackPath = packPath,
            CurrentAssets = new[] { currentAsset },
            OutputPath = Path.Combine(_testDir, "output"),
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.DictionaryCandidates.Should().HaveCount(1);
        task.DictionaryCandidates[0].GetMetadata("RelativePath").Should().Be("js/kept.js");
    }

    [Fact]
    public void MatchesMultipleAssets()
    {
        var packPath = CreateTestPack(new[]
        {
            CreateManifestAsset("js/site.js", "hashA"),
            CreateManifestAsset("css/app.css", "hashB"),
            CreateManifestAsset("lib/jquery.js", "hashC"),
        }, new Dictionary<string, string>
        {
            ["js/site.js"] = "js old",
            ["css/app.css"] = "css old",
            ["lib/jquery.js"] = "jquery old"
        });

        var task = new ResolveDictionaryCandidates
        {
            BuildEngine = _buildEngine.Object,
            AssetPackPath = packPath,
            CurrentAssets = new[]
            {
                CreateAssetItem("js/site.js", "new1"),
                CreateAssetItem("css/app.css", "new2"),
                CreateAssetItem("lib/jquery.js", "new3"),
            },
            OutputPath = Path.Combine(_testDir, "output"),
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.DictionaryCandidates.Should().HaveCount(3);
        task.DictionaryCandidates.Select(c => c.GetMetadata("RelativePath"))
            .Should().BeEquivalentTo("js/site.js", "css/app.css", "lib/jquery.js");
    }

    [Fact]
    public void SkipsCompressedAssetsInPreviousManifest()
    {
        // Include a compressed asset in the manifest — it should be ignored
        var compressedAsset = CreateManifestAsset("js/site.js.gz", "gzHash");
        compressedAsset.AssetTraitName = "Content-Encoding";
        compressedAsset.AssetTraitValue = "gzip";

        var packPath = CreateTestPack(new[]
        {
            CreateManifestAsset("js/site.js", "origHash"),
            compressedAsset,
        }, new Dictionary<string, string>
        {
            ["js/site.js"] = "original"
            // No file for the compressed asset
        });

        var currentAsset = CreateAssetItem("js/site.js", "newHash");
        var task = new ResolveDictionaryCandidates
        {
            BuildEngine = _buildEngine.Object,
            AssetPackPath = packPath,
            CurrentAssets = new[] { currentAsset },
            OutputPath = Path.Combine(_testDir, "output"),
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.DictionaryCandidates.Should().HaveCount(1);
        task.DictionaryCandidates[0].GetMetadata("DictionaryHash").Should().Be(":origHash:");
    }

    [Fact]
    public void SkipsCompressedAssetsInCurrentAssets()
    {
        var packPath = CreateTestPack(new[]
        {
            CreateManifestAsset("js/site.js", "origHash"),
        }, new Dictionary<string, string> { ["js/site.js"] = "original" });

        // Create a compressed current asset — should be skipped
        var compressedAsset = CreateAssetItem("js/site.js.gz", "gzHash");
        compressedAsset.SetMetadata(nameof(StaticWebAsset.AssetTraitName), "Content-Encoding");
        compressedAsset.SetMetadata(nameof(StaticWebAsset.AssetTraitValue), "gzip");

        var uncompressedAsset = CreateAssetItem("js/site.js", "newHash");

        var task = new ResolveDictionaryCandidates
        {
            BuildEngine = _buildEngine.Object,
            AssetPackPath = packPath,
            CurrentAssets = new[] { uncompressedAsset, compressedAsset },
            OutputPath = Path.Combine(_testDir, "output"),
        };

        var result = task.Execute();

        result.Should().BeTrue();
        // Only the uncompressed asset should produce a candidate
        task.DictionaryCandidates.Should().HaveCount(1);
        task.DictionaryCandidates[0].ItemSpec.Should().Be(uncompressedAsset.ItemSpec);
    }

    [Fact]
    public void FailsGracefullyWhenManifestMissingFromPack()
    {
        // Create a zip without manifest.json
        var packPath = Path.Combine(_testDir, "bad.zip");
        using (var zipStream = new FileStream(packPath, FileMode.Create))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("assets/js/site.js");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("content");
        }

        var task = new ResolveDictionaryCandidates
        {
            BuildEngine = _buildEngine.Object,
            AssetPackPath = packPath,
            CurrentAssets = new[] { CreateAssetItem("js/site.js", "hash") },
            OutputPath = Path.Combine(_testDir, "output"),
        };

        var result = task.Execute();

        result.Should().BeFalse();
        _errorMessages.Should().Contain(m => m.Contains("manifest.json"));
    }

    [Fact]
    public void SkipsAssetWhenPreviousHasNoIntegrity()
    {
        var assetWithNoIntegrity = CreateManifestAsset("js/site.js", "");

        var packPath = CreateTestPack(new[]
        {
            assetWithNoIntegrity,
        }, new Dictionary<string, string> { ["js/site.js"] = "content" });

        var task = new ResolveDictionaryCandidates
        {
            BuildEngine = _buildEngine.Object,
            AssetPackPath = packPath,
            CurrentAssets = new[] { CreateAssetItem("js/site.js", "hash") },
            OutputPath = Path.Combine(_testDir, "output"),
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.DictionaryCandidates.Should().BeEmpty();
    }

    // --- Helpers ---

    private TaskItem CreateAssetItem(string relativePath, string integrity)
    {
        var identity = Path.Combine(_testDir, "wwwroot", relativePath.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(identity);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(identity, "current content for " + relativePath);

        var item = new TaskItem(identity);
        item.SetMetadata(nameof(StaticWebAsset.RelativePath), relativePath);
        item.SetMetadata(nameof(StaticWebAsset.Integrity), integrity);
        item.SetMetadata(nameof(StaticWebAsset.ContentRoot), Path.Combine(_testDir, "wwwroot"));
        item.SetMetadata(nameof(StaticWebAsset.SourceType), StaticWebAsset.SourceTypes.Discovered);
        item.SetMetadata(nameof(StaticWebAsset.SourceId), "TestApp");
        item.SetMetadata(nameof(StaticWebAsset.AssetKind), StaticWebAsset.AssetKinds.All);
        item.SetMetadata(nameof(StaticWebAsset.AssetMode), StaticWebAsset.AssetModes.All);
        item.SetMetadata(nameof(StaticWebAsset.AssetRole), StaticWebAsset.AssetRoles.Primary);
        item.SetMetadata(nameof(StaticWebAsset.AssetTraitName), "");
        item.SetMetadata(nameof(StaticWebAsset.AssetTraitValue), "");
        item.SetMetadata(nameof(StaticWebAsset.Fingerprint), "fp" + Guid.NewGuid().ToString("N").Substring(0, 8));
        item.SetMetadata(nameof(StaticWebAsset.OriginalItemSpec), identity);
        item.SetMetadata(nameof(StaticWebAsset.BasePath), "_content/TestApp");
        item.SetMetadata(nameof(StaticWebAsset.CopyToOutputDirectory), StaticWebAsset.AssetCopyOptions.Never);
        item.SetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory), StaticWebAsset.AssetCopyOptions.PreserveNewest);
        item.SetMetadata(nameof(StaticWebAsset.FileLength), "100");
        item.SetMetadata(nameof(StaticWebAsset.LastWriteTime), DateTime.UtcNow.Ticks.ToString());

        return item;
    }

    private static StaticWebAsset CreateManifestAsset(string relativePath, string integrity)
    {
        return new StaticWebAsset
        {
            Identity = Path.Combine("C:\\prev\\wwwroot", relativePath.Replace('/', '\\')),
            OriginalItemSpec = Path.Combine("wwwroot", relativePath.Replace('/', '\\')),
            RelativePath = relativePath,
            ContentRoot = "C:\\prev\\wwwroot",
            SourceType = StaticWebAsset.SourceTypes.Discovered,
            SourceId = "PrevApp",
            BasePath = "_content/PrevApp",
            AssetKind = StaticWebAsset.AssetKinds.Publish,
            AssetMode = StaticWebAsset.AssetModes.All,
            AssetRole = StaticWebAsset.AssetRoles.Primary,
            AssetTraitName = "",
            AssetTraitValue = "",
            Fingerprint = "prevfp",
            Integrity = integrity,
            FileLength = 50,
            LastWriteTime = DateTime.UtcNow,
        };
    }

    private string CreateTestPack(StaticWebAsset[] assets, Dictionary<string, string> files)
    {
        var packPath = Path.Combine(_testDir, Guid.NewGuid().ToString("N") + ".zip");

        var manifest = StaticWebAssetsManifest.Create(
            source: "PrevApp",
            basePath: "/",
            mode: "Default",
            manifestType: "Publish",
            referencedProjectConfigurations: Array.Empty<StaticWebAssetsManifest.ReferencedProjectConfiguration>(),
            discoveryPatterns: Array.Empty<StaticWebAssetsDiscoveryPattern>(),
            assets: assets,
            endpoints: Array.Empty<StaticWebAssetEndpoint>());

        using var zipStream = new FileStream(packPath, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

        // Add manifest
        var manifestEntry = archive.CreateEntry("manifest.json");
        using (var entryStream = manifestEntry.Open())
        {
            JsonSerializer.Serialize(entryStream, manifest);
        }

        // Add asset files
        foreach (var kvp in files)
        {
            var entryPath = "assets/" + kvp.Key.Replace('\\', '/');
            var entry = archive.CreateEntry(entryPath);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(kvp.Value);
        }

        return packPath;
    }
}
