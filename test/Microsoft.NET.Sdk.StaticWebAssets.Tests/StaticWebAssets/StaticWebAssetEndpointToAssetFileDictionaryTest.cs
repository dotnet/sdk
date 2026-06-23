// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.TestFramework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class StaticWebAssetEndpointToAssetFileDictionaryTest
{
    // ToAssetFileDictionary keys are absolute filesystem paths (endpoint.AssetFile, typically
    // populated by upstream targets via MSBuild's %(FullPath) modifier). On Windows the
    // filesystem is case-insensitive, so two endpoints whose AssetFile values differ only in
    // casing must collide into a single dictionary entry — otherwise downstream lookups such
    // as ApplyCompressionNegotiation's `endpointsByAsset.TryGetValue(compressedAsset.Identity, ...)`
    // can silently miss when MSBuild round-trips path casing differently between producers
    // (e.g. ResolveProjectReferences vs DefineStaticWebAssets).
    //
    // This test is Windows-only because OSPath.PathComparer is OrdinalIgnoreCase only on
    // Windows; on Linux/macOS it is StringComparer.Ordinal (matching the case-sensitive
    // filesystem), which is observationally identical to the default Dictionary comparer.
    [PlatformSpecificFact(TestPlatforms.Windows)]
    public void GroupsEndpoints_ByPath_CaseInsensitively_OnWindows()
    {
        var upperCased = MakeEndpointItem("C:\\Repo\\WWWRoot\\site.css", "/_content/lib/site.css");
        var lowerCased = MakeEndpointItem("c:\\repo\\wwwroot\\site.css", "/_content/lib/site.css.gz");

        var result = StaticWebAssetEndpoint.ToAssetFileDictionary([upperCased, lowerCased]);

        result.Should().HaveCount(1,
            "two endpoints whose AssetFile values differ only in casing point to the same " +
            "file on Windows and must share a single dictionary entry");
        var only = result.Values.Single();
        only.Should().HaveCount(2,
            "both endpoints must be grouped under the case-insensitive key");
    }

    private static ITaskItem MakeEndpointItem(string assetFile, string route) =>
        new TaskItem(route, new Dictionary<string, string>
        {
            [nameof(StaticWebAssetEndpoint.AssetFile)] = assetFile,
            [nameof(StaticWebAssetEndpoint.Selectors)] = "",
            [nameof(StaticWebAssetEndpoint.ResponseHeaders)] = "",
            [nameof(StaticWebAssetEndpoint.EndpointProperties)] = "",
        });
}
