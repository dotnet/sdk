// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.Metrics;
using System.Diagnostics;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;
using NuGet.Packaging.Core;
using System.Net;

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
            CandidateAssets = [CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "MyPackage", "Discovered", "candidate#[.{fingerprint}]?.js", "All", "All", fingerprint: "1234asdf", integrity: "asdf1234")],
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
                Name = "integrity",
                Value = "sha256-asdf1234"
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
                    Value = "\"asdf1234\""
                },
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "Last-Modified",
                    Value = "Thu, 15 Nov 1990 00:00:00 GMT"
                },
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "Cache-Control",
                    Value = "max-age=31536000, immutable"
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
                    Value = "\"asdf1234\""
                },
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "Last-Modified",
                    Value = "Thu, 15 Nov 1990 00:00:00 GMT"
                }
            ]);
    }

    [Fact]
    public void CanDefineFingerprintedEndpoints_WithEmbeddedFingerprint()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var lastWrite = new DateTime(1990, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc);

        var task = new DefineStaticWebAssetEndpoints
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets = [CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "MyPackage", "Discovered", "candidate#[.{fingerprint=yolo}]?.js", "All", "All", fingerprint: "1234asdf", integrity: "asdf1234")],
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
        var endpoint = endpoints[1];

        endpoint.Route.Should().Be("candidate.yolo.js");
        endpoint.AssetFile.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")));
        endpoint.EndpointProperties.Should().BeEquivalentTo([
            new StaticWebAssetEndpointProperty
            {
                Name = "fingerprint",
                Value = "yolo"
            },
            new StaticWebAssetEndpointProperty
            {
                Name = "integrity",
                Value = "sha256-asdf1234"
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
                    Value = "\"asdf1234\""
                },
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "Last-Modified",
                    Value = "Thu, 15 Nov 1990 00:00:00 GMT"
                },
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "Cache-Control",
                    Value = "max-age=31536000, immutable"
                }
            ]);

        var otherEndpoint = endpoints[0];
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
                    Value = "\"asdf1234\""
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

    [Fact]
    public void ResolvesContentType_ForCompressedAssets()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var lastWrite = new DateTime(1990, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc);

        var task = new DefineStaticWebAssetEndpoints
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets = [
            new TaskItem(
                Path.Combine(AppContext.BaseDirectory, "Client", "obj", "Debug", "net6.0", "compressed", "rdfmaxp4ta-43emfwee4b.gz"),
                new Dictionary<string, string>
                {
                    ["RelativePath"] = "_framework/dotnet.timezones.blat.gz",
                    ["BasePath"] = "/",
                    ["AssetMode"] = "All",
                    ["AssetKind"] = "Build",
                    ["SourceId"] = "BlazorWasmHosted60.Client",
                    ["CopyToOutputDirectory"] = "PreserveNewest",
                    ["Fingerprint"] = "3ji2l2o1xa",
                    ["RelatedAsset"] = Path.Combine(AppContext.BaseDirectory, "Client", "bin", "Debug", "net6.0", "wwwroot", "_framework", "dotnet.timezones.blat"),
                    ["ContentRoot"] = Path.Combine(AppContext.BaseDirectory, "Client", "obj", "Debug", "net6.0", "compressed"),
                    ["SourceType"] = "Computed",
                    ["Integrity"] = "TwfyUDDMyF5dWUB2oRhrZaTk8sEa9o8ezAlKdxypsX4=",
                    ["AssetRole"] = "Alternative",
                    ["AssetTraitValue"] = "gzip",
                    ["AssetTraitName"] = "Content-Encoding",
                    ["OriginalItemSpec"] = Path.Combine("D:", "work", "dotnet-sdk", "artifacts", "tmp", "Release", "Publish60Host---0200F604", "Client", "bin", "Debug", "net6.0", "wwwroot", "_framework", "dotnet.timezones.blat"),
                    ["CopyToPublishDirectory"] = "Never"
                })
            ],
            ExistingEndpoints = [],
            ContentTypeMappings = [],
            TestLengthResolver = asset => asset.EndsWith(".gz") ? 10 : throw new InvalidOperationException(),
            TestLastWriteResolver = asset => asset.EndsWith(".gz") ? lastWrite : throw new InvalidOperationException(),
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().Be(true);
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.Endpoints);
        endpoints.Length.Should().Be(1);
        var endpoint = endpoints[0];
        endpoint.ResponseHeaders.Should().ContainSingle(h => h.Name == "Content-Type" && h.Value == "application/x-gzip");
    }

    [Fact]
    public void ResolvesContentType_ForFingerprintedAssets()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var lastWrite = new DateTime(1990, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc);

        var task = new DefineStaticWebAssetEndpoints
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets = [
                new TaskItem(
                    Path.Combine(AppContext.BaseDirectory, "Client", "obj", "Debug", "net6.0", "compressed", "rdfmaxp4ta-43emfwee4b.gz"),
                    new Dictionary<string, string>
                    {
                        ["RelativePath"] = "RazorPackageLibraryDirectDependency.iiugt355ct.bundle.scp.css.gz",
                        ["BasePath"] = "_content/RazorPackageLibraryDirectDependency",
                        ["AssetMode"] = "Reference",
                        ["AssetKind"] = "All",
                        ["SourceId"] = "RazorPackageLibraryDirectDependency",
                        ["CopyToOutputDirectory"] = "Never",
                        ["Fingerprint"] = "olx7vzw7zz",
                        ["RelatedAsset"] = Path.Combine(AppContext.BaseDirectory, "Client", "obj", "Debug", "net6.0", "compressed", "RazorPackageLibraryDirectDependency.iiugt355ct.bundle.scp.css"),
                        ["ContentRoot"] = Path.Combine(AppContext.BaseDirectory, "Client", "obj", "Debug", "net6.0", "compressed"),
                        ["SourceType"] = "Package",
                        ["Integrity"] = "JK/W3g5zqZGxAM7zbv/pJ3ngpJheT01SXQ+NofKgQcc=",
                        ["AssetRole"] = "Alternative",
                        ["AssetTraitValue"] = "gzip",
                        ["AssetTraitName"] = "Content-Encoding",
                        ["OriginalItemSpec"] = Path.Combine(AppContext.BaseDirectory, "Client", "obj", "Debug", "net6.0", "compressed", "RazorPackageLibraryDirectDependency.iiugt355ct.bundle.scp.css"),
                        ["CopyToPublishDirectory"] = "PreserveNewest"
                    })
            ],
            ExistingEndpoints = [],
            ContentTypeMappings = [],
            TestLengthResolver = asset => asset.EndsWith(".gz") ? 10 : throw new InvalidOperationException(),
            TestLastWriteResolver = asset => asset.EndsWith(".gz") ? lastWrite : throw new InvalidOperationException(),
        };

        // Act
        var result = task.Execute();
        result.Should().Be(true);
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.Endpoints);
        endpoints.Length.Should().Be(1);
        var endpoint = endpoints[0];
        endpoint.ResponseHeaders.Should().ContainSingle(h => h.Name == "Content-Type" && h.Value == "text/css");
    }

    [Fact]
    public void Produces_TheExpectedEndpoint_ForExternalAssets()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var lastWrite = new DateTime(1990, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc);

        var assetIdentity = Path.Combine(AppContext.BaseDirectory, "dist", "assets", "index-C5tBAdQX.css");
        var task = new DefineStaticWebAssetEndpoints
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets = [
                new TaskItem(
                    assetIdentity,
                    new Dictionary<string, string>
                    {
                        ["RelativePath"] = "assets/index-#[{fingerprint}].css",
                        ["BasePath"] = "",
                        ["AssetMode"] = "All",
                        ["AssetKind"] = "Publish",
                        ["SourceId"] = "MyProject",
                        ["CopyToOutputDirectory"] = "PreserveNewest",
                        ["RelatedAsset"] = "",
                        ["ContentRoot"] = Path.Combine(AppContext.BaseDirectory, "dist"),
                        ["SourceType"] = "Discovered",
                        ["AssetRole"] = "Primary",
                        ["AssetTraitValue"] = "",
                        ["AssetTraitName"] = "",
                        ["Integrity"] = "asdf1234",
                        ["Fingerprint"] = "C5tBAdQX",
                        ["OriginalItemSpec"] = assetIdentity,
                        ["CopyToPublishDirectory"] = "PreserveNewest"
                    }),
                ],
            ExistingEndpoints = [],
            ContentTypeMappings = [CreateContentMapping("**/*.css", "text/css")],
            TestLengthResolver = asset => asset.EndsWith(".css") ? 10 : throw new InvalidOperationException(),
            TestLastWriteResolver = asset => asset.EndsWith(".css") ? lastWrite : throw new InvalidOperationException(),
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().Be(true);
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.Endpoints);
        endpoints.Length.Should().Be(1);
        var endpoint = endpoints[0];

        endpoint.Route.Should().Be("assets/index-C5tBAdQX.css");
        endpoint.AssetFile.Should().Be(assetIdentity);
        endpoint.EndpointProperties.Should().BeEquivalentTo([
            new StaticWebAssetEndpointProperty
            {
                Name = "fingerprint",
                Value = "C5tBAdQX"
            },
            new StaticWebAssetEndpointProperty
            {
                Name = "integrity",
                Value = "sha256-asdf1234"
            },
            new StaticWebAssetEndpointProperty
            {
                Name = "label",
                Value = "assets/index-.css"
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
                    Value = "text/css"
                },
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "ETag",
                    Value = "\"asdf1234\""
                },
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "Last-Modified",
                    Value = "Thu, 15 Nov 1990 00:00:00 GMT"
                },
                new StaticWebAssetEndpointResponseHeader
                {
                    Name = "Cache-Control",
                    Value = "max-age=31536000, immutable"
                }
            ]);
    }

    private static ITaskItem CreateCandidate(
        string itemSpec,
        string sourceId,
        string sourceType,
        string relativePath,
        string assetKind,
        string assetMode,
        string fingerprint = null,
        string integrity = null)
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
            Integrity = integrity ?? "integrity",
            Fingerprint = fingerprint ?? "fingerprint",
        };

        result.ApplyDefaults();
        result.Normalize();

        return result.ToTaskItem();
    }

    private static TaskItem CreateContentMapping(string pattern, string contentType)
    {
        return new TaskItem(contentType, new Dictionary<string, string>
        {
            { "Pattern", pattern },
            { "Priority", "0" }
        });
    }

    private static ITaskItem CreateCandidateEndpoint(
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
