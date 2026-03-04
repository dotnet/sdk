// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

public class ComputeEndpointsForReferenceStaticWebAssetsTest
{
    [Fact]
    public void IncludesEndpointsForAssetsFromCurrentProject()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var task = new ComputeEndpointsForReferenceStaticWebAssets
        {
            BuildEngine = buildEngine.Object,
            Assets = [CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "MyPackage", "Discovered", "candidate.js", "All", "All")],
            CandidateEndpoints = [CreateCandidateEndpoint("candidate.js", Path.Combine("wwwroot", "candidate.js"))]
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().Be(true);
        task.Endpoints.Should().ContainSingle();
        task.Endpoints[0].ItemSpec.Should().Be("base/candidate.js");
        task.Endpoints[0].GetMetadata("AssetFile").Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")));
    }

    [Fact]
    public void UpdatesLabelAsNecessary_ForChosenEndpoints()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var task = new ComputeEndpointsForReferenceStaticWebAssets
        {
            BuildEngine = buildEngine.Object,
            Assets = [CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "MyPackage", "Discovered", "candidate.js", "All", "All")],
            CandidateEndpoints = [CreateCandidateEndpoint("candidate.js", Path.Combine("wwwroot", "candidate.js"), addLabel: true)]
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().Be(true);
        task.Endpoints.Should().ContainSingle();
        task.Endpoints[0].ItemSpec.Should().Be("base/candidate.js");
        task.Endpoints[0].GetMetadata("AssetFile").Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")));
        var properties = StaticWebAssetEndpointProperty.FromMetadataValue(task.Endpoints[0].GetMetadata("EndpointProperties"));
        properties.Should().ContainSingle();
        properties[0].Name.Should().Be("label");
        properties[0].Value.Should().Be("base/label-value");
    }

    [Fact]
    public void FiltersOutEndpointsForAssetsNotFound()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var task = new ComputeEndpointsForReferenceStaticWebAssets
        {
            BuildEngine = buildEngine.Object,
            Assets = [CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "MyPackage", "Discovered", "candidate.js", "All", "All")],
            CandidateEndpoints = [
                CreateCandidateEndpoint("candidate.js", Path.Combine("wwwroot", "candidate.js")),
                CreateCandidateEndpoint("package.js", Path.Combine("..", "_content", "package-id", "package.js"))
            ]
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().Be(true);
        task.Endpoints.Where(e => e != null).Should().ContainSingle();
        task.Endpoints[0].ItemSpec.Should().Be("base/candidate.js");
        task.Endpoints[0].GetMetadata("AssetFile").Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")));
    }

    private static ITaskItem CreateCandidate(
        string itemSpec,
        string sourceId,
        string sourceType,
        string relativePath,
        string assetKind,
        string assetMode)
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
            Fingerprint = "fingerprint",
            LastWriteTime = DateTime.UtcNow,
            FileLength = 10,
        };

        result.ApplyDefaults();
        result.Normalize();

        return result.ToTaskItem();
    }

    private static ITaskItem CreateCandidateEndpoint(string route, string assetFile, bool addLabel = false)
    {
        return new StaticWebAssetEndpoint
        {
            Route = route,
            AssetFile = Path.GetFullPath(assetFile),
            EndpointProperties = addLabel
                ? [new StaticWebAssetEndpointProperty { Name = "label", Value = "label-value" }]
                : [],
        }.ToTaskItem();
    }
}
