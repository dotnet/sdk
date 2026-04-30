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

public class ResolveDictionaryCandidatesTest : IDisposable
{
    private static readonly string PrevRoot = Path.Combine(Path.GetTempPath(), "prev", "wwwroot");

    private readonly string _testDir;
    private readonly List<string> _errorMessages;
    private readonly List<string> _logMessages;
    private readonly Mock<IBuildEngine> _buildEngine;

    public ResolveDictionaryCandidatesTest()
    {
        _testDir = Path.Combine(
            SdkTestContext.Current.TestExecutionDirectory,
            nameof(ResolveDictionaryCandidatesTest),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        _errorMessages = new List<string>();
        _logMessages = new List<string>();
        _buildEngine = new Mock<IBuildEngine>();
        _buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => _errorMessages.Add(args.Message));
        _buildEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
            .Callback<BuildMessageEventArgs>(args => _logMessages.Add(args.Message));
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
        var oldAsset = CreateManifestAsset("js/site.js", "hRQyftXiu1lLX2P9Ly9xa4gHJgLeR1uGN5qegUobtGo=");
        var oldEndpoint = CreateEndpoint("js/site.js", oldAsset.Identity);

        var packPath = CreateTestPack(
            new[] { oldAsset },
            new Dictionary<string, string> { ["js/site.js"] = "old content" },
            new[] { oldEndpoint });

        var currentAsset = CreateAssetItem("js/site.js", "newHash");
        var currentEndpoint = CreateEndpointItem("js/site.js", currentAsset.ItemSpec);
        var task = new ResolveDictionaryCandidates
        {
            BuildEngine = _buildEngine.Object,
            AssetPackPath = packPath,
            CurrentAssets = new[] { currentAsset },
            CurrentEndpoints = new ITaskItem[] { currentEndpoint },
            OutputPath = Path.Combine(_testDir, "output"),
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.DictionaryCandidates.Should().HaveCount(1);

        var candidate = task.DictionaryCandidates[0];
        // Identity is the extracted dictionary bytes path
        File.Exists(candidate.ItemSpec).Should().BeTrue();
        candidate.GetMetadata("Hash").Should().Be(":hRQyftXiu1lLX2P9Ly9xa4gHJgLeR1uGN5qegUobtGo=:");
        candidate.GetMetadata("TargetAsset").Should().Be(currentAsset.ItemSpec);
        candidate.GetMetadata("MatchPattern").Should().Be("/_content/PrevApp/js/site.js");
        candidate.GetMetadata("OldFileFingerprint").Should().Be("prevfp");
    }

    [Fact]
    public void ExtractsCorrectPreviousFileContent()
    {
        var expectedContent = "previous version content for dictionary";
        var oldAsset = CreateManifestAsset("css/app.css", "integrityValue");
        var oldEndpoint = CreateEndpoint("css/app.css", oldAsset.Identity);

        var packPath = CreateTestPack(
            new[] { oldAsset },
            new Dictionary<string, string> { ["css/app.css"] = expectedContent },
            new[] { oldEndpoint });

        var currentAsset = CreateAssetItem("css/app.css", "newHash");
        var currentEndpoint = CreateEndpointItem("css/app.css", currentAsset.ItemSpec);
        var task = new ResolveDictionaryCandidates
        {
            BuildEngine = _buildEngine.Object,
            AssetPackPath = packPath,
            CurrentAssets = new[] { currentAsset },
            CurrentEndpoints = new ITaskItem[] { currentEndpoint },
            OutputPath = Path.Combine(_testDir, "output"),
        };

        var result = task.Execute();

        result.Should().BeTrue();

        // ItemSpec IS the extracted dictionary path
        var extractedPath = task.DictionaryCandidates[0].ItemSpec;
        File.ReadAllText(extractedPath).Should().Be(expectedContent);
    }

    [Fact]
    public void SkipsNewAssetsWithNoPreviousVersion()
    {
        var oldAsset = CreateManifestAsset("js/old.js", "hash1");
        var oldEndpoint = CreateEndpoint("js/old.js", oldAsset.Identity);

        var packPath = CreateTestPack(
            new[] { oldAsset },
            new Dictionary<string, string> { ["js/old.js"] = "old content" },
            new[] { oldEndpoint });

        // Current asset has a different route than what's in the pack
        var currentAsset = CreateAssetItem("js/brand-new.js", "hash2");
        var currentEndpoint = CreateEndpointItem("js/brand-new.js", currentAsset.ItemSpec);
        var task = new ResolveDictionaryCandidates
        {
            BuildEngine = _buildEngine.Object,
            AssetPackPath = packPath,
            CurrentAssets = new[] { currentAsset },
            CurrentEndpoints = new ITaskItem[] { currentEndpoint },
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
        var keptAsset = CreateManifestAsset("js/kept.js", "hash1");
        var removedAsset = CreateManifestAsset("js/removed.js", "hash2");
        var keptEndpoint = CreateEndpoint("js/kept.js", keptAsset.Identity);
        var removedEndpoint = CreateEndpoint("js/removed.js", removedAsset.Identity);

        var packPath = CreateTestPack(
            new[] { keptAsset, removedAsset },
            new Dictionary<string, string>
            {
                ["js/kept.js"] = "kept",
                ["js/removed.js"] = "removed"
            },
            new[] { keptEndpoint, removedEndpoint });

        var currentAsset = CreateAssetItem("js/kept.js", "newHash");
        var currentEndpoint = CreateEndpointItem("js/kept.js", currentAsset.ItemSpec);
        var task = new ResolveDictionaryCandidates
        {
            BuildEngine = _buildEngine.Object,
            AssetPackPath = packPath,
            CurrentAssets = new[] { currentAsset },
            CurrentEndpoints = new ITaskItem[] { currentEndpoint },
            OutputPath = Path.Combine(_testDir, "output"),
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.DictionaryCandidates.Should().HaveCount(1);
        task.DictionaryCandidates[0].GetMetadata("MatchPattern").Should().Be("/_content/PrevApp/js/kept.js");
    }

    [Fact]
    public void MatchesMultipleAssets()
    {
        var assetA = CreateManifestAsset("js/site.js", "hashA");
        var assetB = CreateManifestAsset("css/app.css", "hashB");
        var assetC = CreateManifestAsset("lib/jquery.js", "hashC");

        var packPath = CreateTestPack(
            new[] { assetA, assetB, assetC },
            new Dictionary<string, string>
            {
                ["js/site.js"] = "js old",
                ["css/app.css"] = "css old",
                ["lib/jquery.js"] = "jquery old"
            },
            new[]
            {
                CreateEndpoint("js/site.js", assetA.Identity),
                CreateEndpoint("css/app.css", assetB.Identity),
                CreateEndpoint("lib/jquery.js", assetC.Identity),
            });

        var currentA = CreateAssetItem("js/site.js", "new1");
        var currentB = CreateAssetItem("css/app.css", "new2");
        var currentC = CreateAssetItem("lib/jquery.js", "new3");

        var task = new ResolveDictionaryCandidates
        {
            BuildEngine = _buildEngine.Object,
            AssetPackPath = packPath,
            CurrentAssets = new[] { currentA, currentB, currentC },
            CurrentEndpoints = new ITaskItem[]
            {
                CreateEndpointItem("js/site.js", currentA.ItemSpec),
                CreateEndpointItem("css/app.css", currentB.ItemSpec),
                CreateEndpointItem("lib/jquery.js", currentC.ItemSpec),
            },
            OutputPath = Path.Combine(_testDir, "output"),
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.DictionaryCandidates.Should().HaveCount(3);
        task.DictionaryCandidates.Select(c => c.GetMetadata("MatchPattern"))
            .Should().BeEquivalentTo("/_content/PrevApp/js/site.js", "/_content/PrevApp/css/app.css", "/_content/PrevApp/lib/jquery.js");
    }

    [Fact]
    public void SkipsCompressedAssetsInPreviousManifest()
    {
        // Include a compressed asset in the manifest — it should be ignored
        var compressedAsset = CreateManifestAsset("js/site.js.gz", "gzHash");
        compressedAsset.AssetTraitName = "Content-Encoding";
        compressedAsset.AssetTraitValue = "gzip";

        var primaryAsset = CreateManifestAsset("js/site.js", "origHash");
        var oldEndpoint = CreateEndpoint("js/site.js", primaryAsset.Identity);

        var packPath = CreateTestPack(
            new[] { primaryAsset, compressedAsset },
            new Dictionary<string, string>
            {
                ["js/site.js"] = "original"
                // No file for the compressed asset
            },
            new[] { oldEndpoint });

        var currentAsset = CreateAssetItem("js/site.js", "newHash");
        var currentEndpoint = CreateEndpointItem("js/site.js", currentAsset.ItemSpec);
        var task = new ResolveDictionaryCandidates
        {
            BuildEngine = _buildEngine.Object,
            AssetPackPath = packPath,
            CurrentAssets = new[] { currentAsset },
            CurrentEndpoints = new ITaskItem[] { currentEndpoint },
            OutputPath = Path.Combine(_testDir, "output"),
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.DictionaryCandidates.Should().HaveCount(1);
        task.DictionaryCandidates[0].GetMetadata("Hash").Should().Be(":origHash:");
    }

    [Fact]
    public void SkipsCompressedAssetsInCurrentAssets()
    {
        var oldAsset = CreateManifestAsset("js/site.js", "origHash");
        var oldEndpoint = CreateEndpoint("js/site.js", oldAsset.Identity);

        var packPath = CreateTestPack(
            new[] { oldAsset },
            new Dictionary<string, string> { ["js/site.js"] = "original" },
            new[] { oldEndpoint });

        // Create a compressed current asset — should be skipped
        var compressedAsset = CreateAssetItem("js/site.js.gz", "gzHash");
        compressedAsset.SetMetadata(nameof(StaticWebAsset.AssetTraitName), "Content-Encoding");
        compressedAsset.SetMetadata(nameof(StaticWebAsset.AssetTraitValue), "gzip");

        var uncompressedAsset = CreateAssetItem("js/site.js", "newHash");
        var currentEndpoint = CreateEndpointItem("js/site.js", uncompressedAsset.ItemSpec);

        var task = new ResolveDictionaryCandidates
        {
            BuildEngine = _buildEngine.Object,
            AssetPackPath = packPath,
            CurrentAssets = new[] { uncompressedAsset, compressedAsset },
            CurrentEndpoints = new ITaskItem[] { currentEndpoint },
            OutputPath = Path.Combine(_testDir, "output"),
        };

        var result = task.Execute();

        result.Should().BeTrue();
        // Only the uncompressed asset should produce a candidate
        task.DictionaryCandidates.Should().HaveCount(1);
        task.DictionaryCandidates[0].GetMetadata("TargetAsset").Should().Be(uncompressedAsset.ItemSpec);
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
        var oldEndpoint = CreateEndpoint("js/site.js", assetWithNoIntegrity.Identity);

        var packPath = CreateTestPack(
            new[] { assetWithNoIntegrity },
            new Dictionary<string, string> { ["js/site.js"] = "content" },
            new[] { oldEndpoint });

        var currentAsset = CreateAssetItem("js/site.js", "hash");
        var currentEndpoint = CreateEndpointItem("js/site.js", currentAsset.ItemSpec);
        var task = new ResolveDictionaryCandidates
        {
            BuildEngine = _buildEngine.Object,
            AssetPackPath = packPath,
            CurrentAssets = new[] { currentAsset },
            CurrentEndpoints = new ITaskItem[] { currentEndpoint },
            OutputPath = Path.Combine(_testDir, "output"),
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.DictionaryCandidates.Should().BeEmpty();
    }

    [Fact]
    public void SkipsAssetWhenIntegrityMatchesCurrent()
    {
        var sameIntegrity = "sameHashValue";
        var oldAsset = CreateManifestAsset("js/site.js", sameIntegrity);
        var oldEndpoint = CreateEndpoint("js/site.js", oldAsset.Identity);

        var packPath = CreateTestPack(
            new[] { oldAsset },
            new Dictionary<string, string> { ["js/site.js"] = "content" },
            new[] { oldEndpoint });

        // Current asset has the same integrity — dictionary would be pointless
        var currentAsset = CreateAssetItem("js/site.js", sameIntegrity);
        var currentEndpoint = CreateEndpointItem("js/site.js", currentAsset.ItemSpec);
        var task = new ResolveDictionaryCandidates
        {
            BuildEngine = _buildEngine.Object,
            AssetPackPath = packPath,
            CurrentAssets = new[] { currentAsset },
            CurrentEndpoints = new ITaskItem[] { currentEndpoint },
            OutputPath = Path.Combine(_testDir, "output"),
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.DictionaryCandidates.Should().BeEmpty();
        _logMessages.Should().Contain(m => m.Contains("same integrity"));
    }

    [Fact]
    public void RouteBasedMatching_MatchesByEndpointRoute()
    {
        var oldAsset = CreateManifestAsset("js/site.js", "oldHash123");
        var oldEndpoint = CreateEndpoint("js/site.js", oldAsset.Identity);

        var packPath = CreateTestPack(
            new[] { oldAsset },
            new Dictionary<string, string> { ["js/site.js"] = "old content" },
            new[] { oldEndpoint });

        var currentAsset = CreateAssetItem("js/site.js", "newHash");
        var currentEndpoint = CreateEndpointItem("js/site.js", currentAsset.ItemSpec);

        var task = new ResolveDictionaryCandidates
        {
            BuildEngine = _buildEngine.Object,
            AssetPackPath = packPath,
            CurrentAssets = new[] { currentAsset },
            CurrentEndpoints = new ITaskItem[] { currentEndpoint },
            OutputPath = Path.Combine(_testDir, "output"),
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.DictionaryCandidates.Should().HaveCount(1);
        task.DictionaryCandidates[0].GetMetadata("Hash").Should().Be(":oldHash123:");
        task.DictionaryCandidates[0].GetMetadata("TargetAsset").Should().Be(currentAsset.ItemSpec);
    }

    [Fact]
    public void RouteBasedMatching_SkipsEndpointsWithContentEncodingSelector()
    {
        var oldAsset = CreateManifestAsset("js/site.js", "oldHash");
        var oldEndpoint = CreateEndpoint("js/site.js", oldAsset.Identity);

        // Add an old compressed endpoint — should be ignored
        var oldCompressedEndpoint = CreateEndpoint("js/site.js", Path.Combine(PrevRoot, "js", "site.js.gz"));
        oldCompressedEndpoint.Selectors = new[]
        {
            new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "gzip", Quality = "0.9" }
        };

        var packPath = CreateTestPack(
            new[] { oldAsset },
            new Dictionary<string, string> { ["js/site.js"] = "old content" },
            new[] { oldEndpoint, oldCompressedEndpoint });

        var currentAsset = CreateAssetItem("js/site.js", "newHash");
        var currentEndpoint = CreateEndpointItem("js/site.js", currentAsset.ItemSpec);

        var task = new ResolveDictionaryCandidates
        {
            BuildEngine = _buildEngine.Object,
            AssetPackPath = packPath,
            CurrentAssets = new[] { currentAsset },
            CurrentEndpoints = new ITaskItem[] { currentEndpoint },
            OutputPath = Path.Combine(_testDir, "output"),
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.DictionaryCandidates.Should().HaveCount(1);
    }

    [Fact]
    public void ReturnsEmptyWhenNoEndpointsProvided()
    {
        // Route-based matching requires endpoints; without them, no candidates are produced
        var oldAsset = CreateManifestAsset("js/site.js", "someHash");
        var packPath = CreateTestPack(
            new[] { oldAsset },
            new Dictionary<string, string> { ["js/site.js"] = "old content" },
            new[] { CreateEndpoint("js/site.js", oldAsset.Identity) });

        var currentAsset = CreateAssetItem("js/site.js", "newHash");
        var task = new ResolveDictionaryCandidates
        {
            BuildEngine = _buildEngine.Object,
            AssetPackPath = packPath,
            CurrentAssets = new[] { currentAsset },
            // No CurrentEndpoints
            OutputPath = Path.Combine(_testDir, "output"),
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.DictionaryCandidates.Should().BeEmpty();
    }

    [Fact]
    public void MatchPattern_UsesWildcardForFingerprintTokens()
    {
        // Old asset has fingerprint token in RelativePath
        var oldAsset = CreateManifestAsset("js/site.js", "fpHash");
        oldAsset.RelativePath = "js/site#[.{fingerprint}]?.js";

        var oldEndpoint = CreateEndpoint("js/site.js", oldAsset.Identity);

        var packPath = CreateTestPack(
            new[] { oldAsset },
            new Dictionary<string, string> { ["js/site.js"] = "old content" },
            new[] { oldEndpoint });

        var currentAsset = CreateAssetItem("js/site.js", "newHash");
        var currentEndpoint = CreateEndpointItem("js/site.js", currentAsset.ItemSpec);

        var task = new ResolveDictionaryCandidates
        {
            BuildEngine = _buildEngine.Object,
            AssetPackPath = packPath,
            CurrentAssets = new[] { currentAsset },
            CurrentEndpoints = new ITaskItem[] { currentEndpoint },
            OutputPath = Path.Combine(_testDir, "output"),
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.DictionaryCandidates.Should().HaveCount(1);
        // Fingerprint token should be replaced with wildcard
        task.DictionaryCandidates[0].GetMetadata("MatchPattern").Should().Be("/_content/PrevApp/js/site.*.js");
    }

    [Fact]
    public void RouteBasedMatching_AvoidsDuplicatesFromMultipleRoutes()
    {
        // Same asset accessible via two routes
        var oldAsset = CreateManifestAsset("js/site.js", "dedupHash");
        var oldEndpoint1 = CreateEndpoint("js/site.js", oldAsset.Identity);
        var oldEndpoint2 = CreateEndpoint("_content/TestApp/js/site.js", oldAsset.Identity);

        var packPath = CreateTestPack(
            new[] { oldAsset },
            new Dictionary<string, string> { ["js/site.js"] = "old content" },
            new[] { oldEndpoint1, oldEndpoint2 });

        var currentAsset = CreateAssetItem("js/site.js", "newHash");
        var currentEndpoint1 = CreateEndpointItem("js/site.js", currentAsset.ItemSpec);
        var currentEndpoint2 = CreateEndpointItem("_content/TestApp/js/site.js", currentAsset.ItemSpec);

        var task = new ResolveDictionaryCandidates
        {
            BuildEngine = _buildEngine.Object,
            AssetPackPath = packPath,
            CurrentAssets = new[] { currentAsset },
            CurrentEndpoints = new ITaskItem[] { currentEndpoint1, currentEndpoint2 },
            OutputPath = Path.Combine(_testDir, "output"),
        };

        var result = task.Execute();

        result.Should().BeTrue();
        // Should only produce one candidate even though there are two matching routes
        task.DictionaryCandidates.Should().HaveCount(1);
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
            Identity = Path.Combine(PrevRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)),
            OriginalItemSpec = Path.Combine("wwwroot", relativePath.Replace('/', Path.DirectorySeparatorChar)),
            RelativePath = relativePath,
            ContentRoot = PrevRoot,
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

    private string CreateTestPack(StaticWebAsset[] assets, Dictionary<string, string> files, StaticWebAssetEndpoint[] endpoints = null)
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
            endpoints: endpoints ?? Array.Empty<StaticWebAssetEndpoint>());

        using var zipStream = new FileStream(packPath, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

        // Add manifest
        var manifestEntry = archive.CreateEntry("manifest.json");
        using (var entryStream = manifestEntry.Open())
        {
            JsonSerializer.Serialize(entryStream, manifest);
        }

        // Add asset files — keyed by BasePath/RelativePath to match GeneratePublishAssetPack format
        foreach (var kvp in files)
        {
            // Find the asset to get its BasePath
            var matchingAsset = assets.FirstOrDefault(a =>
                string.Equals(a.ComputePathWithoutTokens(a.RelativePath), kvp.Key.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase));
            var assetBasePath = matchingAsset?.BasePath ?? "";
            if (assetBasePath.StartsWith("/", StringComparison.Ordinal))
            {
                assetBasePath = assetBasePath.Substring(1);
            }

            var entryPath = string.IsNullOrEmpty(assetBasePath)
                ? "assets/" + kvp.Key.Replace('\\', '/')
                : "assets/" + assetBasePath.Replace('\\', '/') + "/" + kvp.Key.Replace('\\', '/');
            var entry = archive.CreateEntry(entryPath);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(kvp.Value);
        }

        return packPath;
    }

    private static StaticWebAssetEndpoint CreateEndpoint(string route, string assetFile)
    {
        return new StaticWebAssetEndpoint
        {
            Route = route,
            AssetFile = assetFile,
            Selectors = Array.Empty<StaticWebAssetEndpointSelector>(),
            ResponseHeaders = Array.Empty<StaticWebAssetEndpointResponseHeader>(),
            EndpointProperties = Array.Empty<StaticWebAssetEndpointProperty>(),
        };
    }

    private static ITaskItem CreateEndpointItem(string route, string assetFile)
    {
        var endpoint = CreateEndpoint(route, assetFile);
        return endpoint.ToTaskItem();
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
