// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

// Test parallelization is disabled assembly-wide via
// [assembly:CollectionBehavior(DisableTestParallelization = true)] in
// LegacyStaticWebAssetsV1IntegrationTest.cs, which already isolates the
// process-CWD mutation this test performs.
public class ComputeEndpointsForReferenceStaticWebAssetsMultiThreadingTest
{
    [Fact]
    public void ProducesCorrectEndpointsWhenTaskEnvironmentProjectDirectoryDiffersFromProcessCurrentDirectory()
    {
        // Scope of this test: verify that ComputeEndpointsForReferenceStaticWebAssets
        // runs correctly under MSBuild's multithreaded execution model, where each task
        // instance receives a per-task TaskEnvironment whose ProjectDirectory may differ
        // from Environment.CurrentDirectory.
        //
        // The task threads its TaskEnvironment through StaticWebAsset.ToAssetDictionary so
        // that asset-side path normalization (ContentRoot, RelatedAsset) is resolved against
        // TaskEnvironment.ProjectDirectory rather than the process CWD. To force the env-
        // driven branch in StaticWebAsset.Normalize(env) to actually run, the asset item is
        // constructed with *relative* ContentRoot metadata.
        //
        // The deep semantics of NormalizeContentRootPath / Normalize / FromTaskItem under
        // env != Fallback are covered by StaticWebAssetTaskEnvironmentTests. This test is
        // the end-to-end smoke check that proves the task itself supports the MT contract
        // (accepts a non-Fallback TaskEnvironment, runs without crashing under CWD ≠
        // ProjectDirectory, and produces correct base-prefixed endpoint output).
        //
        // Layout (the two roots are intentionally placed in different subtrees so a
        // relative "wwwroot" resolves to *different* absolute paths depending on which
        // root it is resolved against):
        //   <testRoot>/project/         <-- TaskEnvironment.ProjectDirectory
        //   <testRoot>/decoy/spawn/     <-- process CWD (the "decoy")
        var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(ComputeEndpointsForReferenceStaticWebAssetsMultiThreadingTest), Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(testRoot, "project");
        var spawnDir = Path.Combine(testRoot, "decoy", "spawn");
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(spawnDir);

        const string relativeContentRoot = "wwwroot";

        // Sanity: resolving the relative ContentRoot against the two roots produces
        // distinct absolute paths. This is what makes the test setup meaningful — if the
        // two were equal, a regression that uses the wrong root would not be detectable.
        var projectAbsoluteContentRoot = Path.GetFullPath(Path.Combine(projectDir, relativeContentRoot));
        var spawnAbsoluteContentRoot = Path.GetFullPath(Path.Combine(spawnDir, relativeContentRoot));
        projectAbsoluteContentRoot.Should().NotBe(spawnAbsoluteContentRoot,
            "the test setup must place project and decoy in different parents so a relative path resolves differently against each");

        // Real MSBuild emits asset Identity and endpoint AssetFile as absolute paths via
        // %(FullPath). Identity is taken verbatim from item.ItemSpec (not env-absolutized),
        // so we pre-absolutize it against the project directory — matching production
        // usage. This keeps the asset/endpoint dictionary lookup deterministic and isolates
        // the env-driven code path to ContentRoot/RelatedAsset normalization.
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
        // Build the StaticWebAsset directly with relative ContentRoot metadata and
        // INTENTIONALLY skip Normalize() — leaving ContentRoot relative on the wire is
        // what forces the task's StaticWebAsset.ToAssetDictionary(env) call to perform
        // the env-driven normalization rather than receiving an already-absolute
        // ContentRoot from the test fixture.
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
            // Pre-populated so ApplyDefaults does not call ResolveFile against a
            // non-existent disk file under the decoy CWD.
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
