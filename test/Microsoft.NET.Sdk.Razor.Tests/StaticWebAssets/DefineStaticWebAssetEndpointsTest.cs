// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Sdk.StaticWebAssets.Tasks;
using Moq;

namespace Microsoft.NET.Sdk.Razor.Tests;

public class DefineStaticWebAssetEndpointsTest
{
    [Theory]
    [InlineData(StaticWebAsset.SourceTypes.Discovered)]
    [InlineData(StaticWebAsset.SourceTypes.Computed)]
    public void DefinesEndpointsForAssets(string sourceType)
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var lastWrite = new DateTime(1990, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc);

        var task = new DefineStaticWebAssetEndpoints
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets = [CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "MyPackage", sourceType, "candidate.js", "All", "All")],
            ExistingEndpoints = [],
            ContentTypeMappings = [CreateContentMapping("**/*.js", "text/javascript")],
            TestLengthResolver = asset => asset.EndsWith("candidate.js") ? 10 : throw new InvalidOperationException(),
            TestLastWriteResolver = asset => asset.EndsWith("candidate.js") ? lastWrite : throw new InvalidOperationException(),
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().Be(true);
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.Endpoints);
        endpoints.Should().ContainSingle();
        var endpoint = endpoints[0];

        endpoint.Route.Should().Be("candidate.js");
        endpoint.AssetFile.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")));
        endpoint.ResponseHeaders.Should().BeEquivalentTo(
            [
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "Accept-Ranges",
                    Value = "bytes"
                },
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "Cache-Control",
                    Value = "no-cache"
                },
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "Content-Length",
                    Value = "10"
                },
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "Content-Type",
                    Value = "text/javascript"
                },
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "ETag",
                    Value = "\"integrity\""
                },
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "Last-Modified",
                    Value = "Thu, 15 Nov 1990 00:00:00 GMT"
                }
            ]);
    }

    [Fact]
    public void CanDefineFingerprintedEndpoints()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var lastWrite = new DateTime(1990, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc);

        var task = new DefineStaticWebAssetEndpoints
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets = [CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "MyPackage", "Discovered", "candidate#[.{fingerprint}]?.js", "All", "All", fingerprint: "1234asdf")],
            ExistingEndpoints = [],
            ContentTypeMappings = [CreateContentMapping("**/*.js", "text/javascript")],
            TestLengthResolver = asset => asset.EndsWith("candidate.js") ? 10 : throw new InvalidOperationException(),
            TestLastWriteResolver = asset => asset.EndsWith("candidate.js") ? lastWrite : throw new InvalidOperationException(),
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().Be(true);
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.Endpoints);
        endpoints.Length.Should().Be(2);
        var endpoint = endpoints[0];

        endpoint.Route.Should().Be("candidate.1234asdf.js");
        endpoint.AssetFile.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")));
        endpoint.EndpointProperties.Should().BeEquivalentTo([
            new StaticWebAssetEndpointProperty
            {
                Name = "fingerprint",
                Value = "1234asdf"
            },
            new StaticWebAssetEndpointProperty
            {
                Name = "label",
                Value = "candidate.js"
            }
            ]);
        endpoint.ResponseHeaders.Should().BeEquivalentTo(
            [
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "Accept-Ranges",
                    Value = "bytes"
                },
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "Content-Length",
                    Value = "10"
                },
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "Content-Type",
                    Value = "text/javascript"
                },
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "ETag",
                    Value = "\"integrity\""
                },
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "Last-Modified",
                    Value = "Thu, 15 Nov 1990 00:00:00 GMT"
                },
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "Cache-Control",
                    Value = "max-age=604800, immutable"
                }
            ]);

        var otherEndpoint = endpoints[1];
        otherEndpoint.Route.Should().Be("candidate.js");
        otherEndpoint.AssetFile.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")));
        otherEndpoint.ResponseHeaders.Should().BeEquivalentTo(
                [
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "Accept-Ranges",
                    Value = "bytes"
                },
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "Cache-Control", 
                    Value = "no-cache"
                }, 
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "Content-Length",
                    Value = "10"
                },
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "Content-Type",
                    Value = "text/javascript"
                },
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "ETag",
                    Value = "\"integrity\""
                },
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "Last-Modified",
                    Value = "Thu, 15 Nov 1990 00:00:00 GMT"
                }
            ]);
    }

    [Fact]
    public void DoesNotDefineNewEndpointsWhenAnExistingEndpointAlreadyExists()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var lastWrite = new DateTime(1990, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc);
        var headers = new StaticWebAssetEndpointResponseHeader[]
        {
            new() {
                Name = "Accept-Ranges",
                Value = "bytes"
            },
            new() {
                Name = "Content-Length",
                Value = "10"
            },
            new() {
                Name = "Content-Type",
                Value = "text/javascript"
            },
            new() {
                Name = "ETag",
                Value = "integrity"
            },
            new() {
                Name = "Last-Modified",
                Value = "Thu, 15 Nov 1990 00:00:00 GMT"
            }
        };

        var task = new DefineStaticWebAssetEndpoints
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets = [CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "MyPackage", "Discovered", "candidate.js", "All", "All")],
            ExistingEndpoints = [
                CreateCandidateEndpoint(
                    "candidate.js",
                    Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                    headers)],
            ContentTypeMappings = [CreateContentMapping("**/*.js", "text/javascript")],
            TestLengthResolver = asset => asset.EndsWith("candidate.js") ? 10 : throw new InvalidOperationException(),
            TestLastWriteResolver = asset => asset.EndsWith("candidate.js") ? lastWrite : throw new InvalidOperationException(),
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().Be(true);
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.Endpoints);
        endpoints.Should().BeEmpty();
    }

    private ITaskItem CreateCandidate(
        string itemSpec,
        string sourceId,
        string sourceType,
        string relativePath,
        string assetKind,
        string assetMode,
        string fingerprint = null)
    {
        var result = new StaticWebAsset()
        {
            Identity = Path.GetFullPath(itemSpec),
            SourceId = sourceId,
            SourceType = sourceType,
            ContentRoot = Directory.GetCurrentDirectory(),
            BasePath = "base",
            RelativePath = relativePath,
            AssetKind = assetKind,
            AssetMode = assetMode,
            AssetRole = "Primary",
            RelatedAsset = "",
            AssetTraitName = "",
            AssetTraitValue = "",
            CopyToOutputDirectory = "",
            CopyToPublishDirectory = "",
            OriginalItemSpec = itemSpec,
            // Add these to avoid accessing the disk to compute them
            Integrity = "integrity",
            Fingerprint = fingerprint ?? "fingerprint",
        };

        result.ApplyDefaults();
        result.Normalize();

        return result.ToTaskItem();
    }

    private TaskItem CreateContentMapping(string pattern, string contentType)
    {
        return new TaskItem(contentType, new Dictionary<string, string>
        {
            { "Pattern", pattern },
            { "Priority", "0" }
        });
    }

    private ITaskItem CreateCandidateEndpoint(
        string route,
        string assetFile,
        StaticWebAssetEndpointResponseHeader[] responseHeaders = null,
        StaticWebAssetEndpointSelector[] responseSelector = null,
        StaticWebAssetEndpointProperty[] properties = null)
    {
        return new StaticWebAssetEndpoint
        {
            Route = route,
            AssetFile = Path.GetFullPath(assetFile),
            ResponseHeaders = responseHeaders ?? [],
            EndpointProperties = properties ?? [],
            Selectors = responseSelector ?? []
        }.ToTaskItem();
    }
}
