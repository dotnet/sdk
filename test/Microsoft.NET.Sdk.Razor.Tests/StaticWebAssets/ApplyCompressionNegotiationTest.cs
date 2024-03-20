// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.NET.Sdk.StaticWebAssets.Tasks;
using Moq;

namespace Microsoft.NET.Sdk.Razor.Tests.StaticWebAssets;

public class ApplyCompressionNegotiationTest
{
    [Fact]
    public void AppliesContentNegotiationRules_ForExistingAssets()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var task = new ApplyCompressionNegotiation
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets =
            [
                CreateCandidate(
                    Path.Combine("wwwroot", "candidate.js"),
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "original"
                ),
                CreateCandidate(
                    Path.Combine("compressed", "candidate.js.gz"),
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "compressed",
                    Path.Combine("wwwroot", "candidate.js"),
                    "Content-Encoding",
                    "gzip"
                )
            ],
            CandidateEndpoints =
            [
                CreateCandidateEndpoint(
                    "candidate.js",
                    Path.Combine("wwwroot", "candidate.js"),
                    CreateHeaders("text/javascript")),

                CreateCandidateEndpoint(
                    "candidate.js.gz",
                    Path.Combine("compressed", "candidate.js.gz"),
                    CreateHeaders("text/javascript"))
            ],
            TestResolveFileLength = value => value switch
            {
                string candidateGz when candidateGz.EndsWith(Path.Combine("compressed", "candidate.js.gz")) => 9,
                string candidate when candidate.EndsWith(Path.Combine("compressed", "candidate.js")) => 20,
                _ => throw new InvalidOperationException()
            }
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().Be(true);
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.UpdatedEndpoints);
        endpoints.Should().BeEquivalentTo([
            new StaticWebAssetEndpoint
            {
                Route = "candidate.js",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                ResponseHeaders =
                [
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Encoding", Value = "gzip" },
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Type", Value = "text/javascript" },
                    new StaticWebAssetEndpointResponseHeader { Name = "Vary", Value = "Content-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [ new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "gzip", Quality = "0.100000" } ],
            },
            new StaticWebAssetEndpoint
            {
                Route = "candidate.js",
                AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                ResponseHeaders =
                [
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Type", Value = "text/javascript" }
                ],
                EndpointProperties = [],
                Selectors = [],
            },
            new StaticWebAssetEndpoint
            {
                Route = "candidate.js.gz",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                ResponseHeaders =
                [
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Encoding", Value = "gzip" },
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Type", Value = "text/javascript" },
                    new StaticWebAssetEndpointResponseHeader { Name = "Vary", Value = "Content-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = []
            }
        ]);
    }

    [Fact]
    public void AppliesContentNegotiationRules_ToAllRelatedAssetEndpoints()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var task = new ApplyCompressionNegotiation
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets =
            [
                CreateCandidate(
                    Path.Combine("wwwroot", "candidate.js"),
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "original"
                ),
                CreateCandidate(
                    Path.Combine("compressed", "candidate.js.gz"),
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "compressed",
                    Path.Combine("wwwroot", "candidate.js"),
                    "Content-Encoding",
                    "gzip"
                )
            ],
            CandidateEndpoints =
            [
                CreateCandidateEndpoint(
                    "candidate.js",
                    Path.Combine("wwwroot", "candidate.js"),
                    CreateHeaders("text/javascript")),
                CreateCandidateEndpoint(
                    "candidate.fingerprint.js",
                    Path.Combine("wwwroot", "candidate.js"),
                    CreateHeaders("text/javascript")),
                CreateCandidateEndpoint(
                    "candidate.js.gz",
                    Path.Combine("compressed", "candidate.js.gz"),
                    CreateHeaders("text/javascript"))
            ],
            TestResolveFileLength = value => value switch
            {
                string candidateGz when candidateGz.EndsWith(Path.Combine("compressed", "candidate.js.gz")) => 9,
                string candidate when candidate.EndsWith(Path.Combine("compressed", "candidate.js")) => 20,
                _ => throw new InvalidOperationException()
            }
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().Be(true);
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.UpdatedEndpoints);
        endpoints.Should().BeEquivalentTo([
            new StaticWebAssetEndpoint
            {
                Route = "candidate.js",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                ResponseHeaders =
                [
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Encoding", Value = "gzip" },
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Type", Value = "text/javascript" },
                    new StaticWebAssetEndpointResponseHeader { Name = "Vary", Value = "Content-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [ new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "gzip", Quality = "0.100000" } ],
            },
            new StaticWebAssetEndpoint
            {
                Route = "candidate.js",
                AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                ResponseHeaders =
                [
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Type", Value = "text/javascript" }
                ],
                EndpointProperties = [],
                Selectors = [],
            },
            new StaticWebAssetEndpoint
            {
                Route = "candidate.fingerprint.js",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                ResponseHeaders =
                [
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Encoding", Value = "gzip" },
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Type", Value = "text/javascript" },
                    new StaticWebAssetEndpointResponseHeader { Name = "Vary", Value = "Content-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [ new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "gzip", Quality = "0.100000" } ],
            },
            new StaticWebAssetEndpoint
            {
                Route = "candidate.fingerprint.js",
                AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                ResponseHeaders =
                [
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Type", Value = "text/javascript" }
                ],
                EndpointProperties = [],
                Selectors = [],
            },
            new StaticWebAssetEndpoint
            {
                Route = "candidate.js.gz",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                ResponseHeaders =
                [
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Encoding", Value = "gzip" },
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Type", Value = "text/javascript" },
                    new StaticWebAssetEndpointResponseHeader { Name = "Vary", Value = "Content-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = []
            }
        ]);
    }


    private StaticWebAssetEndpointResponseHeader[] CreateHeaders(string contentType)
    {
        return
        [
            new StaticWebAssetEndpointResponseHeader {
                Name = "Content-Type",
                Value = contentType
            }
        ];
    }

    private ITaskItem CreateCandidate(
        string itemSpec,
        string sourceId,
        string sourceType,
        string relativePath,
        string assetKind,
        string assetMode,
        string integrity = "",
        string relatedAsset = "",
        string assetTraitName = "",
        string assetTraitValue = "")
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
            RelatedAsset = relatedAsset,
            AssetTraitName = assetTraitName,
            AssetTraitValue = assetTraitValue,
            CopyToOutputDirectory = "",
            CopyToPublishDirectory = "",
            OriginalItemSpec = itemSpec,
            // Add these to avoid accessing the disk to compute them
            Integrity = integrity,
            Fingerprint = "fingerprint",
        };

        result.ApplyDefaults();
        result.Normalize();

        return result.ToTaskItem();
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
