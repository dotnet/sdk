// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.NET.Sdk.Razor.Tests.StaticWebAssets;

public class ResolveFingerprintedStaticWebAssetEndpointsForAssetsTest
{
    [Theory]
    [InlineData("candidate#[.{fingerprint}]?.js", "candidate.js")]
    [InlineData("candidate#[.{fingerprint}]!.js", "candidate.asdf1234.js")]
    public void Standalone_Selects_EndpointMatching_FilePath(string pattern, string expectedRoute)
    {
        var now = DateTime.Now;
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        List<ITaskItem> candidateAssets = [
            CreateCandidate(
                Path.Combine("wwwroot", "candidate.js"),
                "MyPackage",
                "Discovered",
                pattern,
                "All",
                "All",
                "asdf1234",
                "integrity"
            )
        ];

        var endpoints = CreateEndpoints(candidateAssets.Select(StaticWebAsset.FromTaskItem).ToArray());

        var resolvedEndpoints = new ResolveFingerprintedStaticWebAssetEndpointsForAssets
        {
            CandidateAssets = [.. candidateAssets],
            CandidateEndpoints = [..endpoints.Select(e => e.ToTaskItem())],
            IsStandalone = true,
            BuildEngine = buildEngine.Object
        };

        // Act
        var result = resolvedEndpoints.Execute();
        result.Should().BeTrue();

        // Assert
        resolvedEndpoints.ResolvedEndpoints.Should().HaveCount(1);
        var endpoint = StaticWebAssetEndpoint.FromTaskItem(resolvedEndpoints.ResolvedEndpoints[0]);

        endpoint.Route.Should().Be(expectedRoute);
    }

    [Fact]
    public void StandaloneFails_MatchingEndpointNotFound()
    {
        var now = DateTime.Now;
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        List<ITaskItem> candidateAssets = [
            CreateCandidate(
                Path.Combine("wwwroot", "candidate.js"),
                "MyPackage",
                "Discovered",
                "candidate#[.{fingerprint}]!.js",
                "All",
                "All",
                "asdf1234",
                "integrity"
            )
        ];

        var endpoints = CreateEndpoints(candidateAssets.Select(StaticWebAsset.FromTaskItem).ToArray());
        endpoints = endpoints.Where(e => !e.Route.Contains("asdf1234")).ToArray();

        var resolvedEndpoints = new ResolveFingerprintedStaticWebAssetEndpointsForAssets
        {
            CandidateAssets = [.. candidateAssets],
            CandidateEndpoints = [.. endpoints.Select(e => e.ToTaskItem())],
            IsStandalone = true,
            BuildEngine = buildEngine.Object
        };

        // Act
        var result = resolvedEndpoints.Execute();
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("candidate#[.{fingerprint}]?.js", "candidate.asdf1234.js")]
    [InlineData("candidate#[.{fingerprint}]!.js", "candidate.asdf1234.js")]
    public void Hosted_AlwaysPrefers_FingerprintedEndpoint(string pattern, string expectedRoute)
    {
        var now = DateTime.Now;
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        List<ITaskItem> candidateAssets = [
            CreateCandidate(
                Path.Combine("wwwroot", "candidate.js"),
                "MyPackage",
                "Discovered",
                pattern,
                "All",
                "All",
                "asdf1234",
                "integrity"
            )
        ];

        var endpoints = CreateEndpoints(candidateAssets.Select(StaticWebAsset.FromTaskItem).ToArray());

        var resolvedEndpoints = new ResolveFingerprintedStaticWebAssetEndpointsForAssets
        {
            CandidateAssets = [.. candidateAssets],
            CandidateEndpoints = [.. endpoints.Select(e => e.ToTaskItem())],
            IsStandalone = false,
            BuildEngine = buildEngine.Object
        };

        // Act
        var result = resolvedEndpoints.Execute();
        result.Should().BeTrue();

        // Assert
        resolvedEndpoints.ResolvedEndpoints.Should().HaveCount(1);
        var endpoint = StaticWebAssetEndpoint.FromTaskItem(resolvedEndpoints.ResolvedEndpoints[0]);

        endpoint.Route.Should().Be(expectedRoute);
    }

    [Fact]
    public void Hosted_FallsBackToNonFingerprintedEndpoint_WhenFingerprintedVersionNotAvailable()
    {
        var now = DateTime.Now;
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        List<ITaskItem> candidateAssets = [
            CreateCandidate(
                Path.Combine("wwwroot", "candidate.js"),
                "MyPackage",
                "Discovered",
                "candidate.js",
                "All",
                "All",
                "asdf1234",
                "integrity"
            )
        ];

        var endpoints = CreateEndpoints(candidateAssets.Select(StaticWebAsset.FromTaskItem).ToArray());

        var resolvedEndpoints = new ResolveFingerprintedStaticWebAssetEndpointsForAssets
        {
            CandidateAssets = [.. candidateAssets],
            CandidateEndpoints = [.. endpoints.Select(e => e.ToTaskItem())],
            IsStandalone = false,
            BuildEngine = buildEngine.Object
        };

        // Act
        var result = resolvedEndpoints.Execute();
        result.Should().BeTrue();

        // Assert
        resolvedEndpoints.ResolvedEndpoints.Should().HaveCount(1);
        var endpoint = StaticWebAssetEndpoint.FromTaskItem(resolvedEndpoints.ResolvedEndpoints[0]);

        endpoint.Route.Should().Be("candidate.js");
    }

    [Fact]
    public void Hosted_FailsWhen_DoesnotFindMatchingEndpoint()
    {
        var now = DateTime.Now;
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        List<ITaskItem> candidateAssets = [
            CreateCandidate(
                Path.Combine("wwwroot", "candidate.js"),
                "MyPackage",
                "Discovered",
                "candidate.js",
                "All",
                "All",
                "asdf1234",
                "integrity"
            )
        ];

        var endpoints = CreateEndpoints(candidateAssets.Select(StaticWebAsset.FromTaskItem).ToArray());
        endpoints = endpoints.Where(e => !e.Route.Contains("asdf1234")).ToArray();
        endpoints[0].AssetFile = Path.GetFullPath("other.js");

        var resolvedEndpoints = new ResolveFingerprintedStaticWebAssetEndpointsForAssets
        {
            CandidateAssets = [.. candidateAssets],
            CandidateEndpoints = [.. endpoints.Select(e => e.ToTaskItem())],
            IsStandalone = false,
            BuildEngine = buildEngine.Object
        };

        // Act
        var result = resolvedEndpoints.Execute();
        result.Should().BeFalse();
    }

    private static ITaskItem CreateCandidate(
        string itemSpec,
        string sourceId,
        string sourceType,
        string relativePath,
        string assetKind,
        string assetMode,
        string fingerprint = "",
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
            Fingerprint = fingerprint,
        };

        result.ApplyDefaults();
        result.Normalize();

        return result.ToTaskItem();
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
}
