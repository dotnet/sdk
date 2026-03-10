// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class FilterDeferredStaticWebAssetGroupsTest : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IBuildEngine> _buildEngine;
    private readonly List<string> _errorMessages;
    private readonly List<string> _logMessages;

    public FilterDeferredStaticWebAssetGroupsTest()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FilterDeferred_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _errorMessages = new List<string>();
        _logMessages = new List<string>();
        _buildEngine = new Mock<IBuildEngine>();
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
    public void NoDeferredGroups_Passthrough()
    {
        var asset1 = CreateAssetItem("app.js", "MyLib", "");
        var asset2 = CreateAssetItem("site.css", "MyLib", "BootstrapVersion=V5");

        var endpoint1 = CreateEndpointItem("app.js", asset1.ItemSpec);
        var endpoint2 = CreateEndpointItem("site.css", asset2.ItemSpec);

        var task = new FilterDeferredStaticWebAssetGroups
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { asset1, asset2 },
            Endpoints = new[] { endpoint1, endpoint2 },
            StaticWebAssetGroups = new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V5",
                    ["SourceId"] = "MyLib"
                    // No Deferred metadata
                })
            }
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.FilteredAssets.Should().HaveCount(2, "all assets should pass through when no groups are deferred");
        task.FilteredEndpoints.Should().HaveCount(2);
    }

    [Fact]
    public void DeferredGroupSatisfied_AssetIncluded()
    {
        var asset = CreateAssetItem("server.js", "MyLib", "ServerRendering=true");

        var endpoint = CreateEndpointItem("server.js", asset.ItemSpec);

        var task = new FilterDeferredStaticWebAssetGroups
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { asset },
            Endpoints = new[] { endpoint },
            StaticWebAssetGroups = new ITaskItem[]
            {
                new TaskItem("ServerRendering", new Dictionary<string, string>
                {
                    ["Value"] = "true",
                    ["SourceId"] = "MyLib",
                    ["Deferred"] = "true"
                })
            }
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.FilteredAssets.Should().HaveCount(1);
        task.FilteredEndpoints.Should().HaveCount(1);
    }

    [Fact]
    public void DeferredGroupUnsatisfied_AssetExcluded()
    {
        var asset = CreateAssetItem("server.js", "MyLib", "ServerRendering=true");
        var endpoint = CreateEndpointItem("server.js", asset.ItemSpec);

        var task = new FilterDeferredStaticWebAssetGroups
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { asset },
            Endpoints = new[] { endpoint },
            StaticWebAssetGroups = new ITaskItem[]
            {
                new TaskItem("ServerRendering", new Dictionary<string, string>
                {
                    ["Value"] = "false",  // Value doesn't match
                    ["SourceId"] = "MyLib",
                    ["Deferred"] = "true"
                })
            }
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.FilteredAssets.Should().HaveCount(0, "asset with unsatisfied deferred group should be excluded");
        task.FilteredEndpoints.Should().HaveCount(0, "endpoint for excluded asset should be removed");
    }

    [Fact]
    public void CascadingExclusion_RelatedAssetsExcludedWithPrimary()
    {
        var primaryFile = Path.Combine(_tempDir, "server.js");
        var relatedFile = Path.Combine(_tempDir, "server.js.gz");
        File.WriteAllText(primaryFile, "primary");
        File.WriteAllText(relatedFile, "compressed");

        var primary = CreateAssetItem("server.js", "MyLib", "ServerRendering=true");
        var related = new TaskItem(relatedFile, new Dictionary<string, string>
        {
            ["SourceType"] = "Package",
            ["SourceId"] = "MyLib",
            ["ContentRoot"] = _tempDir + Path.DirectorySeparatorChar,
            ["BasePath"] = "_content/mylib",
            ["RelativePath"] = "server.js.gz",
            ["AssetKind"] = "All",
            ["AssetMode"] = "All",
            ["AssetRole"] = "Alternative",
            ["RelatedAsset"] = primary.ItemSpec,
            ["AssetTraitName"] = "Content-Encoding",
            ["AssetTraitValue"] = "gzip",
            ["AssetGroups"] = "",
            ["Fingerprint"] = "test",
            ["Integrity"] = "sha256-test",
            ["CopyToOutputDirectory"] = "Never",
            ["CopyToPublishDirectory"] = "PreserveNewest",
            ["OriginalItemSpec"] = relatedFile,
        });

        var primaryEndpoint = CreateEndpointItem("server.js", primary.ItemSpec);
        var relatedEndpoint = CreateEndpointItem("server.js.gz", related.ItemSpec);

        var task = new FilterDeferredStaticWebAssetGroups
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { primary, related },
            Endpoints = new[] { primaryEndpoint, relatedEndpoint },
            StaticWebAssetGroups = new ITaskItem[]
            {
                new TaskItem("ServerRendering", new Dictionary<string, string>
                {
                    ["Value"] = "false",  // Not satisfied
                    ["SourceId"] = "MyLib",
                    ["Deferred"] = "true"
                })
            }
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.FilteredAssets.Should().HaveCount(0, "both primary and related should be excluded via cascading");
        task.FilteredEndpoints.Should().HaveCount(0, "endpoints for both excluded assets should be removed");
    }

    [Fact]
    public void EndpointsFiltered_ForExcludedAssets()
    {
        var includedAsset = CreateAssetItem("app.js", "MyLib", "");
        var excludedAsset = CreateAssetItem("server.js", "MyLib", "ServerRendering=true");

        var includedEndpoint = CreateEndpointItem("app.js", includedAsset.ItemSpec);
        var excludedEndpoint = CreateEndpointItem("server.js", excludedAsset.ItemSpec);

        var task = new FilterDeferredStaticWebAssetGroups
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { includedAsset, excludedAsset },
            Endpoints = new[] { includedEndpoint, excludedEndpoint },
            StaticWebAssetGroups = new ITaskItem[]
            {
                new TaskItem("ServerRendering", new Dictionary<string, string>
                {
                    ["Value"] = "false",
                    ["SourceId"] = "MyLib",
                    ["Deferred"] = "true"
                })
            }
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.FilteredAssets.Should().HaveCount(1);
        task.FilteredAssets[0].ItemSpec.Should().Be(includedAsset.ItemSpec);
        task.FilteredEndpoints.Should().HaveCount(1);
        task.FilteredEndpoints[0].ItemSpec.Should().Be("app.js");
    }

    [Fact]
    public void NonDeferredGroupRequirements_NotEvaluated()
    {
        // An asset has both a non-deferred group (already resolved) and a deferred group.
        // The deferred filter should only evaluate the deferred one.
        var asset = CreateAssetItem("site.css", "MyLib", "BootstrapVersion=V5;ServerRendering=true");

        var endpoint = CreateEndpointItem("site.css", asset.ItemSpec);

        var task = new FilterDeferredStaticWebAssetGroups
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { asset },
            Endpoints = new[] { endpoint },
            StaticWebAssetGroups = new ITaskItem[]
            {
                new TaskItem("BootstrapVersion", new Dictionary<string, string>
                {
                    ["Value"] = "V5",
                    ["SourceId"] = "MyLib"
                    // NOT deferred — already resolved in eager phase
                }),
                new TaskItem("ServerRendering", new Dictionary<string, string>
                {
                    ["Value"] = "true",
                    ["SourceId"] = "MyLib",
                    ["Deferred"] = "true"
                })
            }
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.FilteredAssets.Should().HaveCount(1,
            "non-deferred BootstrapVersion should not be re-evaluated; only deferred ServerRendering is checked");
    }

    [Fact]
    public void NullStaticWebAssetGroups_PassesThrough()
    {
        var asset = CreateAssetItem("app.js", "MyLib", "");
        var endpoint = CreateEndpointItem("app.js", asset.ItemSpec);

        var task = new FilterDeferredStaticWebAssetGroups
        {
            BuildEngine = _buildEngine.Object,
            Assets = new[] { asset },
            Endpoints = new[] { endpoint },
            StaticWebAssetGroups = null,
        };

        var result = task.Execute();

        result.Should().BeTrue();
        task.FilteredAssets.Should().HaveCount(1);
        task.FilteredEndpoints.Should().HaveCount(1);
    }

    // Helpers

    private ITaskItem CreateAssetItem(string fileName, string sourceId, string assetGroups)
    {
        var filePath = Path.Combine(_tempDir, fileName);
        if (!File.Exists(filePath))
        {
            File.WriteAllText(filePath, "content-" + fileName);
        }

        return new TaskItem(filePath, new Dictionary<string, string>
        {
            ["SourceType"] = "Package",
            ["SourceId"] = sourceId,
            ["ContentRoot"] = _tempDir + Path.DirectorySeparatorChar,
            ["BasePath"] = "_content/" + sourceId.ToLowerInvariant(),
            ["RelativePath"] = fileName,
            ["AssetKind"] = "All",
            ["AssetMode"] = "All",
            ["AssetRole"] = "Primary",
            ["RelatedAsset"] = "",
            ["AssetTraitName"] = "",
            ["AssetTraitValue"] = "",
            ["AssetGroups"] = assetGroups,
            ["Fingerprint"] = "test",
            ["Integrity"] = "sha256-test",
            ["CopyToOutputDirectory"] = "Never",
            ["CopyToPublishDirectory"] = "PreserveNewest",
            ["OriginalItemSpec"] = filePath,
        });
    }

    private static ITaskItem CreateEndpointItem(string route, string assetFile)
    {
        return new TaskItem(route, new Dictionary<string, string>
        {
            ["AssetFile"] = assetFile,
            ["Selectors"] = "[]",
            ["ResponseHeaders"] = "[]",
            ["EndpointProperties"] = "[]",
        });
    }
}
