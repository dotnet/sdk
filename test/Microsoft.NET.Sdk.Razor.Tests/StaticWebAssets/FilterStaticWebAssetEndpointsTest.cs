// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.NET.Sdk.Razor.Tests.StaticWebAssets;
public class FilterStaticWebAssetEndpointsTest
{
    [Fact]
    public void CanFilterEndpoints_ByAssetFile()
    {
        var assets = new[] {
            CreateAsset("index.html", relativePath: "index#[.{fingerprint}]?.html"),
            CreateAsset("index.js", relativePath: "index#[.{fingerprint}]?.js"),
            CreateAsset("index.css", relativePath: "index#[.{fingerprint}]?.css"),
            CreateAsset("other.html", relativePath: "other#[.{fingerprint}]?.html"),
            CreateAsset("other.js", relativePath: "other#[.{fingerprint}]?.js"),
            CreateAsset("other.css", relativePath: "other#[.{fingerprint}]?.css"),
        };
        Array.Sort(assets, (l, r) => string.Compare(l.Identity, r.Identity, StringComparison.Ordinal));

        var endpoints = CreateEndpoints(assets);
        var expectedEndpoints = new[] {
            endpoints[0], //index.css
            endpoints[1], //index.fingerprint.css
            endpoints[8], // other.html
            endpoints[10] // other.fingerprint.html
        };
        Array.Sort(expectedEndpoints);
        var filterEndpointsTask = new FilterStaticWebAssetEndpoints()
        {
            Endpoints = endpoints.Select(endpoints => endpoints.ToTaskItem()).ToArray(),
            Assets =
            [
                // index.css
                assets[0].ToTaskItem(),
                // other.html
                assets[4].ToTaskItem(),
            ],
        };

        // Act
        var result = filterEndpointsTask.Execute();

        // Assert
        result.Should().BeTrue();
        var filteredEndpoints = StaticWebAssetEndpoint.FromItemGroup(filterEndpointsTask.FilteredEndpoints);
        Array.Sort(filteredEndpoints);
        filteredEndpoints.Should().HaveCount(4);
        filteredEndpoints.Should().BeEquivalentTo(expectedEndpoints);
    }

    [Fact]
    public void CanFilterEndpoints_ByProperty()
    {
        var assets = new[] {
            CreateAsset("index.html", relativePath: "index#[.{fingerprint}]?.html"),
            CreateAsset("index.js", relativePath: "index#[.{fingerprint}]?.js"),
            CreateAsset("index.css", relativePath: "index#[.{fingerprint}]?.css"),
            CreateAsset("other.html", relativePath: "other#[.{fingerprint}]?.html"),
            CreateAsset("other.js", relativePath: "other#[.{fingerprint}]?.js"),
            CreateAsset("other.css", relativePath: "other#[.{fingerprint}]?.css"),
        };
        Array.Sort(assets, (l, r) => string.Compare(l.Identity, r.Identity, StringComparison.Ordinal));

        var endpoints = CreateEndpoints(assets);
        var filterEndpointsTask = new FilterStaticWebAssetEndpoints()
        {
            Endpoints = endpoints.Select(endpoints => endpoints.ToTaskItem()).ToArray(),
            Filters = [
                new TaskItem("Property", new Dictionary<string,string>{
                    ["Name"] = "fingerprint"
                })
            ],
            BuildEngine = Mock.Of<IBuildEngine>()
        };

        // Act
        var result = filterEndpointsTask.Execute();

        // Assert
        result.Should().BeTrue();
        var filteredEndpoints = StaticWebAssetEndpoint.FromItemGroup(filterEndpointsTask.FilteredEndpoints);
        Array.Sort(filteredEndpoints);
        filteredEndpoints.Should().HaveCount(6);
        filteredEndpoints.Should().AllSatisfy(e => e.EndpointProperties.Should().ContainSingle(p => p.Name == "fingerprint"));
    }

    [Fact]
    public void CanFilterEndpoints_ByResponseHeader()
    {
        var assets = new[] {
            CreateAsset("index.html", relativePath: "index#[.{fingerprint}]?.html"),
            CreateAsset("index.js", relativePath: "index#[.{fingerprint}]?.js"),
            CreateAsset("index.css", relativePath: "index#[.{fingerprint}]?.css"),
            CreateAsset("other.html", relativePath: "other#[.{fingerprint}]?.html"),
            CreateAsset("other.js", relativePath: "other#[.{fingerprint}]?.js"),
            CreateAsset("other.css", relativePath: "other#[.{fingerprint}]?.css"),
        };
        Array.Sort(assets, (l, r) => string.Compare(l.Identity, r.Identity, StringComparison.Ordinal));

        var endpoints = CreateEndpoints(assets);
        var filterEndpointsTask = new FilterStaticWebAssetEndpoints()
        {
            Endpoints = endpoints.Select(endpoints => endpoints.ToTaskItem()).ToArray(),
            Filters = [
                new TaskItem("Header", new Dictionary<string,string>{
                    ["Name"] = "Content-Type",
                    ["Value"] = "text/html"
                })
            ],
            BuildEngine = Mock.Of<IBuildEngine>()
        };

        // Act
        var result = filterEndpointsTask.Execute();

        // Assert
        result.Should().BeTrue();
        var filteredEndpoints = StaticWebAssetEndpoint.FromItemGroup(filterEndpointsTask.FilteredEndpoints);
        Array.Sort(filteredEndpoints);
        filteredEndpoints.Should().HaveCount(4);
        filteredEndpoints.Should().AllSatisfy(e => e.ResponseHeaders.Should().ContainSingle(p => p.Name == "Content-Type" && p.Value == "text/html"));
    }

    [Fact]
    public void CanFilterEndpoints_Standalone()
    {
        var assets = new[] {
            CreateAsset("index.html", relativePath: "index#[.{fingerprint}]?.html"),
            CreateAsset("other.js", relativePath: "other#[.{fingerprint}]!.js"),
        };
        Array.Sort(assets, (l, r) => string.Compare(l.Identity, r.Identity, StringComparison.Ordinal));

        var endpoints = CreateEndpoints(assets);
        var filterEndpointsTask = new FilterStaticWebAssetEndpoints()
        {
            Endpoints = endpoints.Select(endpoints => endpoints.ToTaskItem()).ToArray(),
            Assets = [.. assets.Select(a => a.ToTaskItem())],
            Filters = [
                new TaskItem("Standalone", new Dictionary<string,string>{ })
            ],
            BuildEngine = Mock.Of<IBuildEngine>()
        };

        // Act
        var result = filterEndpointsTask.Execute();

        // Assert
        result.Should().BeTrue();
        var filteredEndpoints = StaticWebAssetEndpoint.FromItemGroup(filterEndpointsTask.FilteredEndpoints);
        Array.Sort(filteredEndpoints);
        filteredEndpoints.Should().HaveCount(2);
        filteredEndpoints.Where(e => e.Route == "index.html").Should().ContainSingle();
        filteredEndpoints.Where(e => e.Route == "other.fingerprint.js").Should().ContainSingle();
    }

    [Fact]
    public void CanFilterEndpoints_BySelector()
    {
        var assets = new[] {
            CreateAsset("index.html", relativePath: "index#[.{fingerprint}]?.html"),
            CreateAsset("index.js", relativePath: "index#[.{fingerprint}]?.js"),
            CreateAsset("index.css", relativePath: "index#[.{fingerprint}]?.css"),
            CreateAsset("other.html", relativePath: "other#[.{fingerprint}]?.html"),
            CreateAsset("other.js", relativePath: "other#[.{fingerprint}]?.js"),
            CreateAsset("other.css", relativePath: "other#[.{fingerprint}]?.css"),
        };
        Array.Sort(assets, (l, r) => string.Compare(l.Identity, r.Identity, StringComparison.Ordinal));

        var endpoints = CreateEndpoints(assets);
        endpoints[0].Selectors = [new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "gzip" }];
        var filterEndpointsTask = new FilterStaticWebAssetEndpoints()
        {
            Endpoints = endpoints.Select(endpoints => endpoints.ToTaskItem()).ToArray(),
            Filters = [
                new TaskItem("Selector", new Dictionary<string,string>{
                    ["Name"] = "Content-Encoding",
                    ["Value"] = "gzip"
                })
            ],
            BuildEngine = Mock.Of<IBuildEngine>()
        };

        // Act
        var result = filterEndpointsTask.Execute();

        // Assert
        result.Should().BeTrue();
        var filteredEndpoints = StaticWebAssetEndpoint.FromItemGroup(filterEndpointsTask.FilteredEndpoints);
        Array.Sort(filteredEndpoints);
        filteredEndpoints.Should().ContainSingle();
        filteredEndpoints[0].Route.Should().Be(endpoints[0].Route);
    }

    [Fact]
    public void CanFilterEndpoints_ByMultipleCriteria()
    {
        var assets = new[] {
            CreateAsset("index.html", relativePath: "index#[.{fingerprint}]?.html"),
            CreateAsset("index.js", relativePath: "index#[.{fingerprint}]?.js"),
            CreateAsset("index.css", relativePath: "index#[.{fingerprint}]?.css"),
            CreateAsset("other.html", relativePath: "other#[.{fingerprint}]?.html"),
            CreateAsset("other.js", relativePath: "other#[.{fingerprint}]?.js"),
            CreateAsset("other.css", relativePath: "other#[.{fingerprint}]?.css"),
        };
        Array.Sort(assets, (l, r) => string.Compare(l.Identity, r.Identity, StringComparison.Ordinal));

        var endpoints = CreateEndpoints(assets);
        var filterEndpointsTask = new FilterStaticWebAssetEndpoints()
        {
            Endpoints = endpoints.Select(endpoints => endpoints.ToTaskItem()).ToArray(),
            Filters = [
                new TaskItem("Header", new Dictionary<string,string>{
                    ["Name"] = "Content-Type",
                    ["Value"] = "text/html"
                }),
                new TaskItem("Property", new Dictionary<string,string>{
                    ["Name"] = "fingerprint"
                })
            ],
            BuildEngine = Mock.Of<IBuildEngine>()
        };

        // Act
        var result = filterEndpointsTask.Execute();

        // Assert
        result.Should().BeTrue();
        var filteredEndpoints = StaticWebAssetEndpoint.FromItemGroup(filterEndpointsTask.FilteredEndpoints);
        Array.Sort(filteredEndpoints);
        filteredEndpoints.Should().HaveCount(2);
        filteredEndpoints.Should().AllSatisfy(e => e.ResponseHeaders.Should().ContainSingle(p => p.Name == "Content-Type" && p.Value == "text/html"));
        filteredEndpoints.Should().AllSatisfy(e => e.EndpointProperties.Should().ContainSingle(p => p.Name == "fingerprint"));
    }

    private StaticWebAssetEndpoint[] CreateEndpoints(StaticWebAsset[] assets)
    {
        var defineStaticWebAssetEndpoints = new DefineStaticWebAssetEndpoints
        {
            CandidateAssets = assets.Select(a => a.ToTaskItem()).ToArray(),
            ExistingEndpoints = [],
            ContentTypeMappings =
            [
                CreateContentMapping("*.html", "text/html"),
                CreateContentMapping("*.js", "application/javascript"),
                CreateContentMapping("*.css", "text/css"),
            ]
        };
        defineStaticWebAssetEndpoints.BuildEngine = Mock.Of<IBuildEngine>();
        defineStaticWebAssetEndpoints.TestLengthResolver = name => 10;
        defineStaticWebAssetEndpoints.TestLastWriteResolver = name => DateTime.UtcNow;

        defineStaticWebAssetEndpoints.Execute();
        return StaticWebAssetEndpoint.FromItemGroup(defineStaticWebAssetEndpoints.Endpoints);
    }

    private static TaskItem CreateContentMapping(string pattern, string contentType)
    {
        return new TaskItem(contentType, new Dictionary<string, string>
        {
            { "Pattern", pattern },
            { "Priority", "0" }
        });
    }

    private static StaticWebAsset CreateAsset(
        string itemSpec,
        string sourceId = "MyApp",
        string sourceType = "Discovered",
        string relativePath = null,
        string assetKind = "All",
        string assetMode = "All",
        string basePath = "base",
        string assetRole = "Primary",
        string relatedAsset = "",
        string assetTraitName = "",
        string assetTraitValue = "",
        string copyToOutputDirectory = "Never",
        string copytToPublishDirectory = "PreserveNewest")
    {
        var result = new StaticWebAsset()
        {
            Identity = Path.GetFullPath(itemSpec),
            SourceId = sourceId,
            SourceType = sourceType,
            ContentRoot = Directory.GetCurrentDirectory(),
            BasePath = basePath,
            RelativePath = relativePath ?? itemSpec,
            AssetKind = assetKind,
            AssetMode = assetMode,
            AssetRole = assetRole,
            AssetMergeBehavior = StaticWebAsset.MergeBehaviors.PreferTarget,
            AssetMergeSource = "",
            RelatedAsset = relatedAsset,
            AssetTraitName = assetTraitName,
            AssetTraitValue = assetTraitValue,
            CopyToOutputDirectory = copyToOutputDirectory,
            CopyToPublishDirectory = copytToPublishDirectory,
            OriginalItemSpec = itemSpec,
            // Add these to avoid accessing the disk to compute them
            Integrity = "integrity",
            Fingerprint = "fingerprint",
        };

        result.ApplyDefaults();
        result.Normalize();

        return result;
    }
}
