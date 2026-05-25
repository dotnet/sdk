// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class FilterStaticWebAssetGroupsTest : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IBuildEngine> _buildEngine;
    private readonly List<string> _errorMessages;
    private readonly List<string> _logMessages;

    public FilterStaticWebAssetGroupsTest()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FilterGroups_" + Guid.NewGuid().ToString("N"));
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
    public void ConcreteGroupSatisfied_AssetIncluded()
    {
        var asset1 = CreateAssetItem("app.js", "MyLib", "");
        var asset2 = CreateAssetItem("site.css", "MyLib", "BootstrapVersion=V5");

        var endpoint1 = CreateEndpointItem("app.js", asset1.ItemSpec);
        var endpoint2 = CreateEndpointItem("site.css", asset2.ItemSpec);

        var (task, result) = ExecuteFilterTask(
            new[] { asset1, asset2 },
            new[] { endpoint1, endpoint2 },
            new[] { CreateGroup("BootstrapVersion", "V5", "MyLib") });

        result.Should().BeTrue();
        task.FilteredAssets.Should().HaveCount(2, "all assets should pass through when groups are satisfied");
        task.SurvivingEndpoints.Should().HaveCount(2);
    }

    [Fact]
    public void ConcreteGroupUnsatisfied_AssetExcluded()
    {
        var asset = CreateAssetItem("server.js", "MyLib", "ServerRendering=true");
        var endpoint = CreateEndpointItem("server.js", asset.ItemSpec);

        var (task, result) = ExecuteFilterTask(
            new[] { asset },
            new[] { endpoint },
            new[] { CreateGroup("ServerRendering", "false", "MyLib") }); // Value doesn't match

        result.Should().BeTrue();
        task.FilteredAssets.Where(a => a != null).Should().HaveCount(0, "asset with unsatisfied group should be excluded");
        task.SurvivingEndpoints.Should().HaveCount(0, "endpoint for excluded asset should be removed");
    }

    [Fact]
    public void DeferredGroupInFinalPass_Errors()
    {
        var asset = CreateAssetItem("server.js", "MyLib", "ServerRendering=true");
        var endpoint = CreateEndpointItem("server.js", asset.ItemSpec);

        // SkipDeferred defaults to false (final pass)
        var (_, result) = ExecuteFilterTask(
            new[] { asset },
            new[] { endpoint },
            new[] { CreateGroup("ServerRendering", "true", "MyLib", deferred: true) });

        result.Should().BeFalse("deferred groups in the final pass should produce an error");
        _errorMessages.Should().ContainSingle()
            .Which.Should().Contain("Deferred");
    }

    [Fact]
    public void SkipDeferred_DeferredGroupsSkipped_AssetPassesThrough()
    {
        var asset = CreateAssetItem("server.js", "MyLib", "ServerRendering=true");
        var endpoint = CreateEndpointItem("server.js", asset.ItemSpec);

        var (task, result) = ExecuteFilterTask(
            new[] { asset },
            new[] { endpoint },
            new[] { CreateGroup("ServerRendering", "false", "MyLib", deferred: true) }, // Would fail if evaluated
            skipDeferred: true);

        result.Should().BeTrue();
        task.FilteredAssets.Should().HaveCount(1, "deferred groups should be skipped during pre-filter");
        task.SurvivingEndpoints.Should().HaveCount(1);
    }

    [Fact]
    public void CascadingExclusion_RelatedAssetsExcludedWithPrimary()
    {
        var primary = CreateAssetItem("server.js", "MyLib", "ServerRendering=true");
        var related = CreateRelatedAssetItem("server.js.gz", "server.js.gz", "MyLib", primary);

        var primaryEndpoint = CreateEndpointItem("server.js", primary.ItemSpec);
        var relatedEndpoint = CreateEndpointItem("server.js.gz", related.ItemSpec);

        var (task, result) = ExecuteFilterTask(
            new[] { primary, related },
            new[] { primaryEndpoint, relatedEndpoint },
            new[] { CreateGroup("ServerRendering", "false", "MyLib") }); // Not satisfied

        result.Should().BeTrue();
        task.FilteredAssets.Where(a => a != null).Should().HaveCount(0, "both primary and related should be excluded via cascading");
        task.SurvivingEndpoints.Should().HaveCount(0, "endpoints for both excluded assets should be removed");
    }

    [Fact]
    public void EndpointsFiltered_ForExcludedAssets()
    {
        var includedAsset = CreateAssetItem("app.js", "MyLib", "");
        var excludedAsset = CreateAssetItem("server.js", "MyLib", "ServerRendering=true");

        var includedEndpoint = CreateEndpointItem("app.js", includedAsset.ItemSpec);
        var excludedEndpoint = CreateEndpointItem("server.js", excludedAsset.ItemSpec);

        var (task, result) = ExecuteFilterTask(
            new[] { includedAsset, excludedAsset },
            new[] { includedEndpoint, excludedEndpoint },
            new[] { CreateGroup("ServerRendering", "false", "MyLib") });

        result.Should().BeTrue();
        var nonNullAssets = task.FilteredAssets.Where(a => a != null).ToArray();
        nonNullAssets.Should().HaveCount(1);
        nonNullAssets[0].ItemSpec.Should().Be(includedAsset.ItemSpec);
        task.SurvivingEndpoints.Should().HaveCount(1);
        task.SurvivingEndpoints[0].ItemSpec.Should().Be("app.js");
    }

    [Fact]
    public void SkipDeferred_NonDeferredGroupsStillEvaluated()
    {
        // An asset has both a non-deferred group requirement and a deferred group requirement.
        // In SkipDeferred mode, only the non-deferred group is evaluated.
        var asset = CreateAssetItem("site.css", "MyLib", "BootstrapVersion=V5;ServerRendering=true");

        var endpoint = CreateEndpointItem("site.css", asset.ItemSpec);

        var (task, result) = ExecuteFilterTask(
            new[] { asset },
            new[] { endpoint },
            new[]
            {
                CreateGroup("BootstrapVersion", "V5", "MyLib"),
                CreateGroup("ServerRendering", "true", "MyLib", deferred: true)
            },
            skipDeferred: true);

        result.Should().BeTrue();
        task.FilteredAssets.Should().HaveCount(1,
            "non-deferred BootstrapVersion is satisfied; deferred ServerRendering is skipped");
    }

    [Fact]
    public void NullStaticWebAssetGroups_PassesThrough()
    {
        var asset = CreateAssetItem("app.js", "MyLib", "");
        var endpoint = CreateEndpointItem("app.js", asset.ItemSpec);

        var (task, result) = ExecuteFilterTask(
            new[] { asset },
            new[] { endpoint });

        result.Should().BeTrue();
        task.FilteredAssets.Should().HaveCount(1);
        task.SurvivingEndpoints.Should().HaveCount(1);
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

    private (FilterStaticWebAssetGroups Task, bool Result) ExecuteFilterTask(
        ITaskItem[] assets,
        ITaskItem[] endpoints,
        ITaskItem[] groups = null,
        bool skipDeferred = false)
    {
        var task = new FilterStaticWebAssetGroups
        {
            BuildEngine = _buildEngine.Object,
            Assets = assets,
            Endpoints = endpoints,
            SkipDeferred = skipDeferred,
            StaticWebAssetGroups = groups,
        };
        var result = task.Execute();
        return (task, result);
    }

    private ITaskItem CreateRelatedAssetItem(string fileName, string relativePath, string sourceId, ITaskItem primaryAsset, string traitName = "Content-Encoding", string traitValue = "gzip")
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
            ["RelativePath"] = relativePath,
            ["AssetKind"] = "All",
            ["AssetMode"] = "All",
            ["AssetRole"] = "Alternative",
            ["RelatedAsset"] = primaryAsset.ItemSpec,
            ["AssetTraitName"] = traitName,
            ["AssetTraitValue"] = traitValue,
            ["AssetGroups"] = "",
            ["Fingerprint"] = "test",
            ["Integrity"] = "sha256-test",
            ["CopyToOutputDirectory"] = "Never",
            ["CopyToPublishDirectory"] = "PreserveNewest",
            ["OriginalItemSpec"] = filePath,
        });
    }

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
