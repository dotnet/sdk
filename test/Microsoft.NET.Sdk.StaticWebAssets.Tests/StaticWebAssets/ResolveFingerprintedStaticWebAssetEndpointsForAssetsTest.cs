// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests.StaticWebAssets;

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

        var endpoints = CreateEndpoints(candidateAssets.Select(a => StaticWebAsset.FromTaskItem(a)).ToArray());

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

        var endpoints = CreateEndpoints(candidateAssets.Select(a => StaticWebAsset.FromTaskItem(a)).ToArray());
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

        var endpoints = CreateEndpoints(candidateAssets.Select(a => StaticWebAsset.FromTaskItem(a)).ToArray());

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

        var endpoints = CreateEndpoints(candidateAssets.Select(a => StaticWebAsset.FromTaskItem(a)).ToArray());

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

        var endpoints = CreateEndpoints(candidateAssets.Select(a => StaticWebAsset.FromTaskItem(a)).ToArray());
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

    [Fact]
    public void ResolvesEndpoints_WhenTaskEnvironmentProjectDirectoryDiffersFromProcessCurrentDirectory()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(ResolveFingerprintedStaticWebAssetEndpointsForAssetsTest), Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(testRoot, "project");
        var spawnDir = Path.Combine(testRoot, "decoy", "spawn");
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(spawnDir);

        const string relativeContentRoot = "wwwroot";
        var projectAbsoluteContentRoot = Path.GetFullPath(Path.Combine(projectDir, relativeContentRoot));
        var spawnAbsoluteContentRoot = Path.GetFullPath(Path.Combine(spawnDir, relativeContentRoot));
        projectAbsoluteContentRoot.Should().NotBe(spawnAbsoluteContentRoot,
            "the test setup must place project and decoy in different parents so a relative ContentRoot resolves differently against each");

        var assetIdentity = Path.Combine(projectAbsoluteContentRoot, "candidate.js");

        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(spawnDir);

            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var projectEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir);

            // The candidate carries a *relative* ContentRoot. When the task forwards its
            // TaskEnvironment into StaticWebAsset.FromTaskItemGroup, the asset is rooted against
            // the project directory rather than the process current directory (the decoy).
            var candidateAsset = CreateCandidateWithRelativeContentRoot(
                assetIdentity,
                relativeContentRoot,
                relativePath: "candidate#[.{fingerprint}]!.js",
                fingerprint: "asdf1234");

            var endpoints = CreateEndpoints([StaticWebAsset.FromTaskItem(candidateAsset, projectEnvironment)]);

            var resolvedEndpoints = new ResolveFingerprintedStaticWebAssetEndpointsForAssets
            {
                CandidateAssets = [candidateAsset],
                CandidateEndpoints = [.. endpoints.Select(e => e.ToTaskItem())],
                IsStandalone = false,
                BuildEngine = buildEngine.Object,
                TaskEnvironment = projectEnvironment,
            };

            // Act
            var result = resolvedEndpoints.Execute();

            // Assert
            result.Should().BeTrue("the task must run to completion when TaskEnvironment.ProjectDirectory differs from the process CWD");
            errorMessages.Should().BeEmpty();
            resolvedEndpoints.ResolvedEndpoints.Should().ContainSingle();

            var endpoint = StaticWebAssetEndpoint.FromTaskItem(resolvedEndpoints.ResolvedEndpoints[0]);
            endpoint.Route.Should().Be("candidate.asdf1234.js");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCurrentDirectory);
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
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
            FileLength = 10,
            LastWriteTime = DateTime.UtcNow,
        };

        result.ApplyDefaults();
        result.Normalize();

        return result.ToTaskItem();
    }

    // Builds a candidate with a relative ContentRoot that is intentionally left un-normalized so
    // that the relative path reaches the task under test and is rooted against its
    // TaskEnvironment.ProjectDirectory rather than the process current directory.
    private static ITaskItem CreateCandidateWithRelativeContentRoot(
        string identity,
        string relativeContentRoot,
        string relativePath,
        string fingerprint)
    {
        var asset = new StaticWebAsset()
        {
            Identity = identity,
            SourceId = "MyPackage",
            SourceType = "Discovered",
            ContentRoot = relativeContentRoot,
            BasePath = "base",
            RelativePath = relativePath,
            AssetKind = "All",
            AssetMode = "All",
            AssetRole = "Primary",
            RelatedAsset = "",
            AssetTraitName = "",
            AssetTraitValue = "",
            CopyToOutputDirectory = "",
            CopyToPublishDirectory = "",
            OriginalItemSpec = identity,
            // Preset to avoid accessing the disk to compute them.
            Integrity = "integrity",
            Fingerprint = fingerprint,
            FileLength = 10,
            LastWriteTime = DateTime.UtcNow,
        };

        return asset.ToTaskItem();
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
