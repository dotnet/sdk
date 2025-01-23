// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.NET.Sdk.Razor.Tests.StaticWebAssets;

public class UpdateStaticWebAssetEndpointsTest
{
    [Fact]
    public void CanUpdateEndpoint_AppendResponseHeaders()
    {
        // Arrrange
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
        var fingerprintedEndpoints = endpoints.Where(e => e.EndpointProperties.Any(p => string.Equals(p.Name, "fingerprint", StringComparison.Ordinal))).ToArray();
        foreach (var endpoint in fingerprintedEndpoints)
        {
            endpoint.ResponseHeaders = endpoint.ResponseHeaders.Where(h => !string.Equals(h.Name, "Cache-Control", StringComparison.Ordinal)).ToArray();
        }

        var filterStaticWebAssetEndpoints = new UpdateStaticWebAssetEndpoints
        {
            EndpointsToUpdate = fingerprintedEndpoints.Select(e => e.ToTaskItem()).ToArray(),
            AllEndpoints = endpoints.Select(e => e.ToTaskItem()).ToArray(),
            Operations =
            [
                CreateOperation("Append", "Header", "Cache-Control", "immutable")
            ],
            BuildEngine = Mock.Of<IBuildEngine>()
        };

        // Act
        var result = filterStaticWebAssetEndpoints.Execute();
        result.Should().BeTrue();

        // Assert
        var updatedEndpoints = StaticWebAssetEndpoint.FromItemGroup(filterStaticWebAssetEndpoints.UpdatedEndpoints);
        updatedEndpoints.Should().HaveCount(fingerprintedEndpoints.Length);
        foreach (var updatedEndpoint in updatedEndpoints)
        {
            updatedEndpoint.ResponseHeaders.Should().ContainSingle(h => string.Equals(h.Name, "Cache-Control", StringComparison.Ordinal) && string.Equals(h.Value, "immutable"));
        }
    }

    [Fact]
    public void CanUpdateEndpoint_RemoveResponseHeaders()
    {
        // Arrrange
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
        var fingerprintedEndpoints = endpoints.Where(e => e.EndpointProperties.Any(p => string.Equals(p.Name, "fingerprint", StringComparison.Ordinal))).ToArray();

        var filterStaticWebAssetEndpoints = new UpdateStaticWebAssetEndpoints
        {
            EndpointsToUpdate = fingerprintedEndpoints.Select(e => e.ToTaskItem()).ToArray(),
            AllEndpoints = endpoints.Select(e => e.ToTaskItem()).ToArray(),
            Operations =
            [
                CreateOperation("Remove", "Header", "Cache-Control", null)
            ],
            BuildEngine = Mock.Of<IBuildEngine>()
        };

        // Act
        var result = filterStaticWebAssetEndpoints.Execute();
        result.Should().BeTrue();

        // Assert
        var updatedEndpoints = StaticWebAssetEndpoint.FromItemGroup(filterStaticWebAssetEndpoints.UpdatedEndpoints);
        updatedEndpoints.Should().HaveCount(fingerprintedEndpoints.Length);
        foreach (var updatedEndpoint in updatedEndpoints)
        {
            updatedEndpoint.ResponseHeaders.Should().NotContain(h => string.Equals(h.Name, "Cache-Control", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void CanUpdateEndpoint_RemoveAllResponseHeaders()
    {
        // Arrrange
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
        var fingerprintedEndpoints = endpoints.Where(e => e.EndpointProperties.Any(p => string.Equals(p.Name, "fingerprint", StringComparison.Ordinal))).ToArray();
        foreach (var endpoint in fingerprintedEndpoints)
        {
            endpoint.ResponseHeaders = [.. endpoint.ResponseHeaders, new StaticWebAssetEndpointResponseHeader { Name = "ETag", Value = "W/\"integrity\"" }];
        }

        var filterStaticWebAssetEndpoints = new UpdateStaticWebAssetEndpoints
        {
            EndpointsToUpdate = fingerprintedEndpoints.Select(e => e.ToTaskItem()).ToArray(),
            AllEndpoints = endpoints.Select(e => e.ToTaskItem()).ToArray(),
            Operations =
            [
                CreateOperation("RemoveAll", "Header", "ETag", null)
            ],
            BuildEngine = Mock.Of<IBuildEngine>()
        };

        // Act
        var result = filterStaticWebAssetEndpoints.Execute();
        result.Should().BeTrue();

        // Assert
        var updatedEndpoints = StaticWebAssetEndpoint.FromItemGroup(filterStaticWebAssetEndpoints.UpdatedEndpoints);
        updatedEndpoints.Should().HaveCount(fingerprintedEndpoints.Length);
        foreach (var updatedEndpoint in updatedEndpoints)
        {
            updatedEndpoint.ResponseHeaders.Should().NotContain(h => string.Equals(h.Name, "ETag", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void CanUpdateEndpoint_RemoveAllResponseHeadersWithValue()
    {
        // Arrrange
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
        var fingerprintedEndpoints = endpoints.Where(e => e.EndpointProperties.Any(p => string.Equals(p.Name, "fingerprint", StringComparison.Ordinal))).ToArray();
        foreach (var endpoint in fingerprintedEndpoints)
        {
            endpoint.ResponseHeaders = [.. endpoint.ResponseHeaders, new StaticWebAssetEndpointResponseHeader { Name = "ETag", Value = "W/\"integrity\"" }];
        }

        var filterStaticWebAssetEndpoints = new UpdateStaticWebAssetEndpoints
        {
            EndpointsToUpdate = fingerprintedEndpoints.Select(e => e.ToTaskItem()).ToArray(),
            AllEndpoints = endpoints.Select(e => e.ToTaskItem()).ToArray(),
            Operations =
            [
                CreateOperation("RemoveAll", "Header", "ETag", "W/\"integrity\"")
            ],
            BuildEngine = Mock.Of<IBuildEngine>()
        };

        // Act
        var result = filterStaticWebAssetEndpoints.Execute();
        result.Should().BeTrue();

        // Assert
        var updatedEndpoints = StaticWebAssetEndpoint.FromItemGroup(filterStaticWebAssetEndpoints.UpdatedEndpoints);
        updatedEndpoints.Should().HaveCount(fingerprintedEndpoints.Length);
        foreach (var updatedEndpoint in updatedEndpoints)
        {
            updatedEndpoint.ResponseHeaders.Should().ContainSingle(h => string.Equals(h.Name, "ETag", StringComparison.Ordinal) && string.Equals(h.Value, "\"integrity\"", StringComparison.Ordinal));
            updatedEndpoint.ResponseHeaders.Should().NotContain(h => string.Equals(h.Name, "ETag", StringComparison.Ordinal) && string.Equals(h.Value, "W/\"integrity\"", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void CanUpdateEndpoint_ReplaceResponseHeaders()
    {
        // Arrrange
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
        var fingerprintedEndpoints = endpoints.Where(e => e.EndpointProperties.Any(p => string.Equals(p.Name, "fingerprint", StringComparison.Ordinal))).ToArray();

        var filterStaticWebAssetEndpoints = new UpdateStaticWebAssetEndpoints
        {
            EndpointsToUpdate = endpoints.Select(e => e.ToTaskItem()).ToArray(),
            AllEndpoints = endpoints.Select(e => e.ToTaskItem()).ToArray(),
            Operations =
            [
                CreateOperation("Replace", "Header", "Cache-Control", "max-age=31536000, immutable", "immutable")
            ],
            BuildEngine = Mock.Of<IBuildEngine>()
        };

        // Act
        var result = filterStaticWebAssetEndpoints.Execute();
        result.Should().BeTrue();

        // Assert
        var updatedEndpoints = StaticWebAssetEndpoint.FromItemGroup(filterStaticWebAssetEndpoints.UpdatedEndpoints);
        updatedEndpoints.Should().HaveCount(fingerprintedEndpoints.Length);
        foreach (var updatedEndpoint in updatedEndpoints)
        {
            updatedEndpoint.ResponseHeaders.Should().ContainSingle(h => string.Equals(h.Name, "Cache-Control", StringComparison.Ordinal) && string.Equals(h.Value, "immutable"));
        }
    }

    [Fact]
    public void CanUpdateEndpoint_RetainsNonModifiedEndpointsWithSameRoute()
    {
        // Arrrange
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
        var fingerprintedEndpoints = endpoints.Where(e => e.EndpointProperties.Any(p => string.Equals(p.Name, "fingerprint", StringComparison.Ordinal))).ToArray();

        var unmodifiedEndpoint = new StaticWebAssetEndpoint
        {
            Route = fingerprintedEndpoints[0].Route,
            AssetFile = fingerprintedEndpoints[0].AssetFile + ".gz",
            Selectors = [new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "gzip" }],
            ResponseHeaders = [.. fingerprintedEndpoints[0].ResponseHeaders],
            EndpointProperties = [.. fingerprintedEndpoints[0].EndpointProperties]
        };

        endpoints = [..endpoints, unmodifiedEndpoint];

        foreach (var endpoint in fingerprintedEndpoints)
        {
            endpoint.ResponseHeaders = endpoint.ResponseHeaders.Where(h => !string.Equals(h.Name, "Cache-Control", StringComparison.Ordinal)).ToArray();
        }

        var filterStaticWebAssetEndpoints = new UpdateStaticWebAssetEndpoints
        {
            EndpointsToUpdate = fingerprintedEndpoints.Select(e => e.ToTaskItem()).ToArray(),
            AllEndpoints = endpoints.Select(e => e.ToTaskItem()).ToArray(),
            Operations =
            [
                CreateOperation("Append", "Header", "Cache-Control", "immutable")
            ],
            BuildEngine = Mock.Of<IBuildEngine>()
        };

        // Act
        var result = filterStaticWebAssetEndpoints.Execute();
        result.Should().BeTrue();

        // Assert
        var updatedEndpoints = StaticWebAssetEndpoint.FromItemGroup(filterStaticWebAssetEndpoints.UpdatedEndpoints);
        updatedEndpoints.Should().HaveCount(fingerprintedEndpoints.Length + 1);
        var updatedUnmodifiedEndpoint = updatedEndpoints.Where(e => e.AssetFile.EndsWith(".gz"));
        updatedUnmodifiedEndpoint.Should().HaveCount(1);

        var updatedModifiedEndpoints = updatedEndpoints.Where(e => !e.AssetFile.EndsWith(".gz"));
        updatedModifiedEndpoints.Should().HaveCount(fingerprintedEndpoints.Length);
        foreach (var updatedEndpoint in updatedModifiedEndpoints)
        {
            updatedEndpoint.ResponseHeaders.Should().ContainSingle(h => string.Equals(h.Name, "Cache-Control", StringComparison.Ordinal) && string.Equals(h.Value, "immutable"));
        }
    }

    private static ITaskItem CreateOperation(string type, string target, string name, string value, string newValue = null)
    {
        return new TaskItem(type, new Dictionary<string, string>
        {
            { "UpdateTarget", target },
            { "Name", name },
            { "Value", value },
            { "NewValue", newValue }
        });
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
