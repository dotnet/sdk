// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

public class StaticWebAssetsBaselineComparer
{
    private static readonly string BaselineGenerationInstructions =
    @"If the difference in baselines is expected, please re-generate the baselines.
Start by ensuring you're dogfooding the SDK from the current branch (dotnet --version should be '*.0.0-dev').
    If you're not on the dogfood sdk, from the root of the repository run:
        1. dotnet clean
        2. .\restore.cmd or ./restore.sh
        3. .\build.cmd ./build.sh
        4. .\eng\dogfood.cmd or . ./eng/dogfood.sh

Then, using the dogfood SDK run the .\src\RazorSdk\update-test-baselines.ps1 script.";

    public static StaticWebAssetsBaselineComparer Instance { get; } = new();

    internal void AssertManifest(StaticWebAssetsManifest expected, StaticWebAssetsManifest actual)
    {
        //Many of the properties in the manifest contain full paths, to avoid flakiness on the tests, we don't compare the full paths.
        actual.Version.Should().Be(expected.Version);
        actual.Source.Should().Be(expected.Source);
        actual.BasePath.Should().Be(expected.BasePath);
        actual.Mode.Should().Be(expected.Mode);
        actual.ManifestType.Should().Be(expected.ManifestType);

        actual.ReferencedProjectsConfiguration.Should().HaveSameCount(expected.ReferencedProjectsConfiguration);

        // Relax the check for project reference configuration items see
        // https://github.com/dotnet/sdk/pull/27381#issuecomment-1228764471
        // for details.
        //manifest.ReferencedProjectsConfiguration.OrderBy(cm => cm.Identity)
        //    .Should()
        //    .BeEquivalentTo(expected.ReferencedProjectsConfiguration.OrderBy(cm => cm.Identity));

        actual.DiscoveryPatterns.OrderBy(dp => dp.Name).Should().BeEquivalentTo(expected.DiscoveryPatterns.OrderBy(dp => dp.Name));

        var actualAssets = actual.Assets
            .OrderBy(a => a.BasePath)
            .ThenBy(a => a.RelativePath)
            .ThenBy(a => a.AssetKind)
            .GroupBy(a => GetAssetGroup(a))
            .ToDictionary(a => a.Key, a => a.Order().ToArray());

        var duplicateAssets = actual.Assets
            .GroupBy(a => a)
            .ToDictionary(a => a.Key, a => a.Order().ToArray());

        var foundDuplicateAssetss = duplicateAssets.Where(a => a.Value.Length > 1).ToArray();
        duplicateAssets.Where(a => a.Value.Length > 1).Should().BeEmpty($@"no duplicate assets should exist. But found:
    {string.Join($"{Environment.NewLine}    ", foundDuplicateAssetss.Select(a => @$"{a.Key.Identity} - {a.Value.Length}"))}{Environment.NewLine}");

        var expectedAssets = expected.Assets
            .OrderBy(a => a.BasePath)
            .ThenBy(a => a.RelativePath)
            .ThenBy(a => a.AssetKind)
            .GroupBy(a => GetAssetGroup(a))
            .ToDictionary(a => a.Key, a => a.Order().ToArray());

        var actualAssetsByIdentity = actual.Assets.GroupBy(a => a.Identity).ToDictionary(a => a.Key, a => a.Order().ToArray());
        foreach (var asset in actual.Assets)
        {
            if (!string.IsNullOrEmpty(asset.RelatedAsset))
            {
                actualAssetsByIdentity.Should().ContainKey(asset.RelatedAsset);
            }
        }

        foreach (var (group, actualAssetsGroup) in actualAssets)
        {
            var expectedAssetsGroup = expectedAssets[group];
            CompareAssetGroup(group, actualAssetsGroup, expectedAssetsGroup);
        }

        var actualEndpoints = actual.Endpoints
            .OrderBy(a => a.Route)
            .ThenBy(a => a.AssetFile)
            .GroupBy(a => GetEndpointGroup(a))
            .ToDictionary(a => a.Key, a => a.Order().ToArray());

        SortEndpointProperties(actualEndpoints);

        var duplicateEndpoints = actual.Endpoints
            .GroupBy(a => a)
            .ToDictionary(a => a.Key, a => a.Order().ToArray());

        var foundDuplicateEndpoints = duplicateEndpoints.Where(a => DuplicatesExist(a)).ToArray();

        duplicateEndpoints.Where(a => DuplicatesExist(a)).Should().BeEmpty($@"no duplicate endpoints should exist. But found:
    {string.Join($"{Environment.NewLine}    ", foundDuplicateEndpoints.Select(a => @$"{a.Key.Route} - {a.Key.AssetFile} - {a.Key.Selectors.Length} - {a.Value.Length}"))}{Environment.NewLine}");

        foreach (var endpoint in actual.Endpoints)
        {
            actualAssetsByIdentity.Should().ContainKey(endpoint.AssetFile);
        }

        var expectedEndpoints = expected.Endpoints
            .OrderBy(a => a.Route)
            .ThenBy(a => a.AssetFile)
            .GroupBy(a => GetEndpointGroup(a))
            .ToDictionary(a => a.Key, a => a.Order().ToArray());

        SortEndpointProperties(expectedEndpoints);

        foreach (var (group, actualEndpointsGroup) in actualEndpoints)
        {
            var expectedEndpointsGroup = expectedEndpoints[group];
            CompareEndpointGroup(group, actualEndpointsGroup, expectedEndpointsGroup);
        }

        static bool DuplicatesExist(KeyValuePair<StaticWebAssetEndpoint, StaticWebAssetEndpoint[]> a)
        {
            var endpoint = a.Key;
            if (endpoint.Route.EndsWith(".gz") || endpoint.Route.EndsWith(".br") || endpoint.Selectors.Length == 1)
            {
                // This is not exact, but there are situations in which our templatization process is not biyective and Build and Publish assets defined during build for
                // the same asset end up having the same endpoint. To avoid issues with this, we relax the check to support finding more than one.
                return a.Value.Length > 2;
            }
            else
            {
                return a.Value.Length > 1;
            }
        }
    }

    private static void SortEndpointProperties(Dictionary<string, StaticWebAssetEndpoint[]> endpoints)
    {
        foreach (var endpointGroup in endpoints.Values)
        {
            foreach (var endpoint in endpointGroup)
            {
                Array.Sort(endpoint.Selectors, (a, b) => (a.Name, a.Value).CompareTo((b.Name, b.Value)));
                Array.Sort(endpoint.ResponseHeaders, (a, b) => (a.Name, a.Value).CompareTo((b.Name, b.Value)));
                Array.Sort(endpoint.EndpointProperties, (a, b) => (a.Name, a.Value).CompareTo((b.Name, b.Value)));
            }
        }
    }

    protected virtual void CompareAssetGroup(string group, StaticWebAsset[] manifestAssets, StaticWebAsset[] expectedAssets)
    {
        var comparisonMode = CompareAssetCounts(group, manifestAssets, expectedAssets);
        Array.Sort(manifestAssets, (a, b) => a.Identity.CompareTo(b.Identity));
        Array.Sort(expectedAssets, (a, b) => a.Identity.CompareTo(b.Identity));

        // Otherwise, do a property level comparison of all assets
        switch (comparisonMode)
        {
            case GroupComparisonMode.Exact:
                break;
            case GroupComparisonMode.AllowAdditionalAssets:
                break;
            default:
                break;
        }

        var differences = new List<string>();
        var assetDifferences = new List<string>();
        var groupLength = Math.Min(manifestAssets.Length, expectedAssets.Length);
        for (var i = 0; i < groupLength; i++)
        {
            var manifestAsset = manifestAssets[i];
            var expectedAsset = expectedAssets[i];

            ComputeAssetDifferences(assetDifferences, manifestAsset, expectedAsset);

            if (assetDifferences.Any())
            {
                differences.Add(@$"
==================================================

For {expectedAsset.Identity}:

{string.Join(Environment.NewLine, assetDifferences)}

==================================================");
            }

            assetDifferences.Clear();
        }

        differences.Should().BeEmpty(
            @$" the generated manifest should match the expected baseline.

{BaselineGenerationInstructions}

");
    }

    private GroupComparisonMode CompareAssetCounts(string group, StaticWebAsset[] manifestAssets, StaticWebAsset[] expectedAssets)
    {
        var comparisonMode = GetGroupComparisonMode(group);

        // If there's a mismatch in the number of assets, just print the strict difference in the asset `Identity`
        switch (comparisonMode)
        {
            case GroupComparisonMode.Exact:
                if (manifestAssets.Length != expectedAssets.Length)
                {
                    ThrowAssetCountMismatchError(manifestAssets, expectedAssets);
                }
                break;
            case GroupComparisonMode.AllowAdditionalAssets:
                if (expectedAssets.Except(manifestAssets).Any())
                {
                    ThrowAssetCountMismatchError(manifestAssets, expectedAssets);
                }
                break;
            default:
                break;
        }

        return comparisonMode;

        static void ThrowAssetCountMismatchError(StaticWebAsset[] manifestAssets, StaticWebAsset[] expectedAssets)
        {
            var missingAssets = expectedAssets.Except(manifestAssets);
            var unexpectedAssets = manifestAssets.Except(expectedAssets);

            var differences = new List<string>();

            if (missingAssets.Any())
            {
                differences.Add($@"The following expected assets weren't found in the manifest:
    {string.Join($"{Environment.NewLine}\t", missingAssets.Select(a => a.Identity))}");
            }

            if (unexpectedAssets.Any())
            {
                differences.Add($@"The following additional unexpected assets were found in the manifest:
    {string.Join($"{Environment.NewLine}\t", unexpectedAssets.Select(a => a.Identity))}");
            }

            throw new Exception($@"{string.Join(Environment.NewLine, differences)}

{BaselineGenerationInstructions}");
        }
    }

    protected virtual GroupComparisonMode GetGroupComparisonMode(string group)
    {
        return GroupComparisonMode.Exact;
    }

    private static void ComputeAssetDifferences(List<string> assetDifferences, StaticWebAsset manifestAsset, StaticWebAsset expectedAsset)
    {
        if (manifestAsset.Identity != expectedAsset.Identity)
        {
            assetDifferences.Add($"Expected manifest Identity of {expectedAsset.Identity} but found {manifestAsset.Identity}.");
        }
        if (manifestAsset.SourceType != expectedAsset.SourceType)
        {
            assetDifferences.Add($"Expected manifest SourceType of {expectedAsset.SourceType} but found {manifestAsset.SourceType}.");
        }
        if (manifestAsset.SourceId != expectedAsset.SourceId)
        {
            assetDifferences.Add($"Expected manifest SourceId of {expectedAsset.SourceId} but found {manifestAsset.SourceId}.");
        }
        if (manifestAsset.ContentRoot != expectedAsset.ContentRoot)
        {
            assetDifferences.Add($"Expected manifest ContentRoot of {expectedAsset.ContentRoot} but found {manifestAsset.ContentRoot}.");
        }
        if (manifestAsset.BasePath != expectedAsset.BasePath)
        {
            assetDifferences.Add($"Expected manifest BasePath of {expectedAsset.BasePath} but found {manifestAsset.BasePath}.");
        }
        if (manifestAsset.RelativePath != expectedAsset.RelativePath)
        {
            assetDifferences.Add($"Expected manifest RelativePath of {expectedAsset.RelativePath} but found {manifestAsset.RelativePath}.");
        }
        if (manifestAsset.AssetKind != expectedAsset.AssetKind)
        {
            assetDifferences.Add($"Expected manifest AssetKind of {expectedAsset.AssetKind} but found {manifestAsset.AssetKind}.");
        }
        if (manifestAsset.AssetMode != expectedAsset.AssetMode)
        {
            assetDifferences.Add($"Expected manifest AssetMode of {expectedAsset.AssetMode} but found {manifestAsset.AssetMode}.");
        }
        if (manifestAsset.AssetRole != expectedAsset.AssetRole)
        {
            assetDifferences.Add($"Expected manifest AssetRole of {expectedAsset.AssetRole} but found {manifestAsset.AssetRole}.");
        }
        if (manifestAsset.RelatedAsset != expectedAsset.RelatedAsset)
        {
            assetDifferences.Add($"Expected manifest RelatedAsset of {expectedAsset.RelatedAsset} but found {manifestAsset.RelatedAsset}.");
        }
        if (manifestAsset.AssetTraitName != expectedAsset.AssetTraitName)
        {
            assetDifferences.Add($"Expected manifest AssetTraitName of {expectedAsset.AssetTraitName} but found {manifestAsset.AssetTraitName}.");
        }
        if (manifestAsset.AssetTraitValue != expectedAsset.AssetTraitValue)
        {
            assetDifferences.Add($"Expected manifest AssetTraitValue of {expectedAsset.AssetTraitValue} but found {manifestAsset.AssetTraitValue}.");
        }
        if (manifestAsset.CopyToOutputDirectory != expectedAsset.CopyToOutputDirectory)
        {
            assetDifferences.Add($"Expected manifest CopyToOutputDirectory of {expectedAsset.CopyToOutputDirectory} but found {manifestAsset.CopyToOutputDirectory}.");
        }
        if (manifestAsset.CopyToPublishDirectory != expectedAsset.CopyToPublishDirectory)
        {
            assetDifferences.Add($"Expected manifest CopyToPublishDirectory of {expectedAsset.CopyToPublishDirectory} but found {manifestAsset.CopyToPublishDirectory}.");
        }
        if (manifestAsset.OriginalItemSpec != expectedAsset.OriginalItemSpec)
        {
            assetDifferences.Add($"Expected manifest OriginalItemSpec of {expectedAsset.OriginalItemSpec} but found {manifestAsset.OriginalItemSpec}.");
        }
    }

    protected virtual string GetAssetGroup(StaticWebAsset asset)
    {
        return Path.GetExtension(asset.Identity.TrimEnd(']'));
    }

    protected virtual void CompareEndpointGroup(string group, StaticWebAssetEndpoint[] manifestAssets, StaticWebAssetEndpoint[] expectedAssets)
    {
        var comparisonMode = CompareEndpointCounts(group, manifestAssets, expectedAssets);
        Array.Sort(manifestAssets);
        Array.Sort(expectedAssets);

        // Otherwise, do a property level comparison of all assets
        switch (comparisonMode)
        {
            case GroupComparisonMode.Exact:
                break;
            case GroupComparisonMode.AllowAdditionalAssets:
                break;
            default:
                break;
        }

        var differences = new List<string>();
        var assetDifferences = new List<string>();
        var groupLength = Math.Min(manifestAssets.Length, expectedAssets.Length);
        for (var i = 0; i < groupLength; i++)
        {
            var manifestAsset = manifestAssets[i];
            var expectedAsset = expectedAssets[i];

            ComputeEndpointDifferences(assetDifferences, manifestAsset, expectedAsset);

            if (assetDifferences.Any())
            {
                differences.Add(@$"
==================================================

For {expectedAsset.AssetFile}:

{string.Join(Environment.NewLine, assetDifferences)}

==================================================");
            }

            assetDifferences.Clear();
        }

        differences.Should().BeEmpty(
            @$" the generated manifest should match the expected baseline.

{BaselineGenerationInstructions}

");
    }

    private GroupComparisonMode CompareEndpointCounts(string group, StaticWebAssetEndpoint[] manifestEndpoints, StaticWebAssetEndpoint[] expectedEndpoints)
    {
        var comparisonMode = GetGroupComparisonMode(group);

        // If there's a mismatch in the number of assets, just print the strict difference in the asset `Identity`
        switch (comparisonMode)
        {
            case GroupComparisonMode.Exact:
                if (manifestEndpoints.Length != expectedEndpoints.Length)
                {
                    ThrowEndpointCountMismatchError(group, manifestEndpoints, expectedEndpoints);
                }
                break;
            case GroupComparisonMode.AllowAdditionalAssets:
                if (expectedEndpoints.Except(manifestEndpoints).Any())
                {
                    ThrowEndpointCountMismatchError(group, manifestEndpoints, expectedEndpoints);
                }
                break;
            default:
                break;
        }

        return comparisonMode;

        static void ThrowEndpointCountMismatchError(string group, StaticWebAssetEndpoint[] manifestEndpoints, StaticWebAssetEndpoint[] expectedEndpoints)
        {
            var missingEndpoints = expectedEndpoints.Except(manifestEndpoints);
            var unexpectedEndpoints = manifestEndpoints.Except(expectedEndpoints);

            var differences = new List<string>
            {
                $"Expected group '{group}' to have '{expectedEndpoints.Length}' endpoints but found '{manifestEndpoints.Length}'.",
                "Expected Endpoints:",
                string.Join($"{Environment.NewLine}\t", expectedEndpoints.Select(a => $"{a.Route} - {a.Selectors.Length} - {a.AssetFile}")),
                "Actual Endpoints:",
                string.Join($"{Environment.NewLine}\t", manifestEndpoints.Select(a => $"{a.Route} - {a.Selectors.Length} - {a.AssetFile}"))
            };

            if (missingEndpoints.Any())
            {
                differences.Add($@"The following expected assets weren't found in the manifest:
    {string.Join($"{Environment.NewLine}\t", missingEndpoints.Select(a => $"{a.Route} - {a.AssetFile}"))}");
            }

            if (unexpectedEndpoints.Any())
            {
                differences.Add($@"The following additional unexpected assets were found in the manifest:
    {string.Join($"{Environment.NewLine}\t", unexpectedEndpoints.Select(a => $"{a.Route} - {a.AssetFile}"))}");
            }

            throw new Exception($@"{string.Join(Environment.NewLine, differences)}

{BaselineGenerationInstructions}");
        }
    }

    protected virtual GroupComparisonMode GetAssetGroupComparisonMode(string group)
    {
        return GroupComparisonMode.Exact;
    }

    private static void ComputeEndpointDifferences(List<string> assetDifferences, StaticWebAssetEndpoint manifestAsset, StaticWebAssetEndpoint expectedAsset)
    {
        if (manifestAsset.Route != expectedAsset.Route)
        {
            assetDifferences.Add($"Expected manifest Identity of {expectedAsset.Route} but found {manifestAsset.Route}.");
        }
        if (manifestAsset.AssetFile != expectedAsset.AssetFile)
        {
            assetDifferences.Add($"Expected manifest SourceType of {expectedAsset.AssetFile} but found {manifestAsset.AssetFile}.");
        }

        ComputeSelectorDifferences(assetDifferences, manifestAsset.Selectors, expectedAsset.Selectors);
        ComputeResponseHeaderDifferences(assetDifferences, manifestAsset.ResponseHeaders, expectedAsset.ResponseHeaders);
    }

    private static void ComputeResponseHeaderDifferences(
        List<string> assetDifferences,
        StaticWebAssetEndpointResponseHeader[] manifestResponseHeaders,
        StaticWebAssetEndpointResponseHeader[] expectedResponseHeaders)
    {
        if (manifestResponseHeaders.Length != expectedResponseHeaders.Length)
        {
            assetDifferences.Add($"Expected manifest to have {expectedResponseHeaders.Length} response headers but found {manifestResponseHeaders.Length}.");
        }

        var manifest = new HashSet<StaticWebAssetEndpointResponseHeader>(manifestResponseHeaders);
        var differences = new HashSet<StaticWebAssetEndpointResponseHeader>(manifestResponseHeaders);
        var expected = new HashSet<StaticWebAssetEndpointResponseHeader>(expectedResponseHeaders);
        differences.SymmetricExceptWith(expected);

        foreach (var difference in differences)
        {
            if (!manifest.Contains(difference))
            {
                assetDifferences.Add($"Expected manifest to have response header '{difference.Name}={difference.Value}' but it was not found.");
            }
            else
            {
                assetDifferences.Add($"Found unexpected response header '{difference.Name}={difference.Value}'.");
            }
        }
    }

    private static void ComputeSelectorDifferences(
        List<string> assetDifferences,
        StaticWebAssetEndpointSelector[] manifestSelectors,
        StaticWebAssetEndpointSelector[] expectedSelectors)
    {
        if (manifestSelectors.Length != expectedSelectors.Length)
        {
            assetDifferences.Add($"Expected manifest to have {expectedSelectors.Length} selectors but found {manifestSelectors.Length}.");
        }

        var manifest = new HashSet<StaticWebAssetEndpointSelector>(manifestSelectors);
        var differences = new HashSet<StaticWebAssetEndpointSelector>(manifestSelectors);
        var expected = new HashSet<StaticWebAssetEndpointSelector>(expectedSelectors);
        differences.SymmetricExceptWith(expected);

        foreach (var difference in differences)
        {
            if (!manifest.Contains(difference))
            {
                assetDifferences.Add($"Expected manifest to have selector '{difference.Name}={difference.Value};q={difference.Quality}' but it was not found.");
            }
            else
            {
                assetDifferences.Add($"Found unexpected selector '{difference.Name}={difference.Value};q={difference.Quality}'.");
            }
        }
    }

    protected virtual string GetEndpointGroup(StaticWebAssetEndpoint asset)
    {
        return Path.GetExtension(asset.AssetFile.TrimEnd(']'));
    }
}

public enum GroupComparisonMode
{
    // We require the same number of assets in a group for the baseline and the template.
    Exact,

    // We won't fail when we check against the baseline if additional assets are present for a group.
    AllowAdditionalAssets
}
