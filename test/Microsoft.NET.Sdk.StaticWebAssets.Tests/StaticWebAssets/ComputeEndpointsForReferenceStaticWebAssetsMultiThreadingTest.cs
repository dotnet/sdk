// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

[DoNotParallelize]
[TestClass]

public class ComputeEndpointsForReferenceStaticWebAssetsMultiThreadingTest
{
    [TestMethod]
    public void ProducesCorrectEndpointsWhenTaskEnvironmentProjectDirectoryDiffersFromProcessCurrentDirectory()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(ComputeEndpointsForReferenceStaticWebAssetsMultiThreadingTest), Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(testRoot, "project");
        var spawnDir = Path.Combine(testRoot, "decoy", "spawn");
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(spawnDir);

        const string relativeContentRoot = "wwwroot";

        var projectAbsoluteContentRoot = Path.GetFullPath(Path.Combine(projectDir, relativeContentRoot));
        var spawnAbsoluteContentRoot = Path.GetFullPath(Path.Combine(spawnDir, relativeContentRoot));
        projectAbsoluteContentRoot.Should().NotBe(spawnAbsoluteContentRoot,
            "the test setup must place project and decoy in different parents so a relative path resolves differently against each");

        var assetIdentity = Path.Combine(projectAbsoluteContentRoot, "candidate.js");

        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(spawnDir);

            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeEndpointsForReferenceStaticWebAssets
            {
                BuildEngine = buildEngine.Object,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                Assets = new[] { CreateAssetItemWithRelativeContentRoot(assetIdentity, relativeContentRoot, basePath: "base") },
                CandidateEndpoints = new[] { CreateCandidateEndpoint(route: "candidate.js", assetFile: assetIdentity) }
            };

            var result = task.Execute();

            result.Should().BeTrue("the task must run to completion when TaskEnvironment.ProjectDirectory differs from the process CWD");
            errorMessages.Should().BeEmpty();
            task.Endpoints.Should().ContainSingle();

            // The route is re-rooted under the asset's BasePath — proves the endpoint
            // matched the asset in the dictionary (assets[endpoint.AssetFile] succeeded)
            // and the BasePath-application branch ran.
            task.Endpoints[0].ItemSpec.Should().Be("base/candidate.js");

            // AssetFile is passed through unchanged from the input endpoint.
            task.Endpoints[0].GetMetadata("AssetFile").Should().Be(assetIdentity);
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

    private static ITaskItem CreateAssetItemWithRelativeContentRoot(string identity, string relativeContentRoot, string basePath)
    {
        var asset = new StaticWebAsset
        {
            Identity = identity,
            SourceId = "MyPackage",
            SourceType = StaticWebAsset.SourceTypes.Discovered,
            ContentRoot = relativeContentRoot,
            BasePath = basePath,
            RelativePath = Path.GetFileName(identity),
            AssetKind = StaticWebAsset.AssetKinds.All,
            AssetMode = StaticWebAsset.AssetModes.All,
            AssetRole = StaticWebAsset.AssetRoles.Primary,
            RelatedAsset = "",
            AssetTraitName = "",
            AssetTraitValue = "",
            CopyToOutputDirectory = "",
            CopyToPublishDirectory = "",
            OriginalItemSpec = identity,
            Integrity = "integrity",
            Fingerprint = "fingerprint",
            LastWriteTime = DateTime.UtcNow,
            FileLength = 10,
        };

        return asset.ToTaskItem();
    }

    private static ITaskItem CreateCandidateEndpoint(string route, string assetFile)
    {
        return new StaticWebAssetEndpoint
        {
            Route = route,
            AssetFile = assetFile,
            EndpointProperties = [],
        }.ToTaskItem();
    }
}
