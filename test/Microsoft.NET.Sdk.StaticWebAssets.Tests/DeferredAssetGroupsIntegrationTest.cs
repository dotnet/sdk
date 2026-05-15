// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests
{
    public class DeferredAssetGroupsIntegrationTest(ITestOutputHelper log) : AspNetSdkTest(log)
    {
        [Fact]
        public void Build_DeferredGroupEnabled_IncludesGroupedAssetAndEndpoints()
        {
            var intermediateOutputPath = BuildWithDeferredGroup(enableBlazorGroup: "enabled");
            var manifest = LoadBuildManifest(intermediateOutputPath);
            var endpoints = LoadEndpointsManifest(intermediateOutputPath);

            // Primary asset should be present
            var primaryAsset = manifest.Assets
                .Where(a => a.RelativePath == "deferred.blazor.js" && a.AssetRole == "Primary")
                .ToList();
            primaryAsset.Should().ContainSingle("deferred.blazor.js primary asset should be present when BlazorGroup=enabled");

            // Compressed alternatives should be present
            var compressedAssets = manifest.Assets
                .Where(a => a.RelativePath.StartsWith("deferred.blazor.js.") && a.AssetRole == "Alternative" && a.AssetTraitName == "Content-Encoding")
                .ToList();
            compressedAssets.Should().NotBeEmpty("compressed alternatives (gzip/brotli) should exist for deferred.blazor.js");

            // Uncompressed endpoint (no selector)
            var primaryEndpoints = endpoints
                .Where(e => e.Route.EndsWith("deferred.blazor.js") && e.Selectors.Length == 0)
                .ToList();
            primaryEndpoints.Should().ContainSingle("an uncompressed endpoint should exist for deferred.blazor.js");

            // Compressed endpoints (with Content-Encoding selector on the same route)
            var compressedEndpoints = endpoints
                .Where(e => e.Route.EndsWith("deferred.blazor.js") && e.Selectors.Length == 1 && e.Selectors[0].Name == "Content-Encoding")
                .ToList();
            compressedEndpoints.Should().NotBeEmpty("compressed endpoints with Content-Encoding selector should exist for deferred.blazor.js");

            // Direct .gz/.br route endpoints
            var directCompressedEndpoints = endpoints
                .Where(e => e.Route.EndsWith("deferred.blazor.js.gz") || e.Route.EndsWith("deferred.blazor.js.br"))
                .ToList();
            directCompressedEndpoints.Should().NotBeEmpty("direct .gz/.br route endpoints should exist for deferred.blazor.js");

            // Existing non-grouped assets should be unaffected
            var existingEndpoints = endpoints
                .Where(e => e.Route.Contains("project-transitive-dep.js"))
                .ToList();
            existingEndpoints.Should().NotBeEmpty("endpoints for existing non-grouped assets should be unaffected");
        }

        [Fact]
        public void Build_DeferredGroupDisabled_ExcludesGroupedAssetAndEndpoints()
        {
            var intermediateOutputPath = BuildWithDeferredGroup(enableBlazorGroup: "disabled");
            var manifest = LoadBuildManifest(intermediateOutputPath);
            var endpoints = LoadEndpointsManifest(intermediateOutputPath);

            // Build manifest retains all assets (unfiltered) — the primary asset is still there
            var primaryAsset = manifest.Assets
                .Where(a => a.RelativePath == "deferred.blazor.js" && a.AssetRole == "Primary")
                .ToList();
            primaryAsset.Should().ContainSingle("build manifest retains all variants; deferred.blazor.js should still be present");

            // But all deferred.blazor.js endpoints should be excluded from the endpoints manifest
            var deferredEndpoints = endpoints
                .Where(e => e.Route.Contains("deferred.blazor.js"))
                .ToList();
            deferredEndpoints.Should().BeEmpty("no endpoints should exist for deferred.blazor.js when BlazorGroup=disabled");

            // That includes direct .gz/.br routes
            var directCompressedEndpoints = endpoints
                .Where(e => e.Route.EndsWith("deferred.blazor.js.gz") || e.Route.EndsWith("deferred.blazor.js.br"))
                .ToList();
            directCompressedEndpoints.Should().BeEmpty("no compressed route endpoints should exist for excluded deferred.blazor.js");

            // Existing non-grouped assets should be unaffected
            var existingEndpoints = endpoints
                .Where(e => e.Route.Contains("project-transitive-dep.js"))
                .ToList();
            existingEndpoints.Should().NotBeEmpty("endpoints for existing non-grouped assets should be unaffected");
        }

        private string BuildWithDeferredGroup(string enableBlazorGroup)
        {
            var testAsset = "RazorAppWithP2PReference";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, identifier: enableBlazorGroup)
                .WithProjectChanges((path, document) =>
                {
                    if (Path.GetFileName(path) == "ClassLibrary.csproj")
                    {
                        document.Root.Add(
                            new XElement("ItemGroup",
                                new XElement("StaticWebAssetGroupDefinition",
                                    new XAttribute("Include", "BlazorGroup"),
                                    new XAttribute("Value", "enabled"),
                                    new XAttribute("Order", "0"),
                                    new XAttribute("SourceId", "ClassLibrary"),
                                    new XAttribute("IncludePattern", "deferred.blazor.js"))));
                        document.Root.Add(
                            new XElement("Import",
                                new XAttribute("Project", "StaticWebAssets.Groups.targets")));
                    }

                    if (Path.GetFileName(path) == "AppWithP2PReference.csproj")
                    {
                        document.Root.Add(
                            new XElement("Import",
                                new XAttribute("Project", @"..\ClassLibrary\StaticWebAssets.Groups.targets")));
                    }
                });

            var classLibDir = Path.Combine(projectDirectory.TestRoot, "ClassLibrary");

            File.WriteAllText(
                Path.Combine(classLibDir, "wwwroot", "deferred.blazor.js"),
                "console.log('deferred blazor');");

            File.WriteAllText(
                Path.Combine(classLibDir, "StaticWebAssets.Groups.targets"),
                $"""
                <Project>
                  <PropertyGroup>
                    <EnableBlazorGroup Condition="'$(EnableBlazorGroup)' == ''">{enableBlazorGroup}</EnableBlazorGroup>
                    <FilterDeferredStaticWebAssetGroupsDependsOn>
                      $(FilterDeferredStaticWebAssetGroupsDependsOn);
                      ResolveDeferredBlazorGroup
                    </FilterDeferredStaticWebAssetGroupsDependsOn>
                  </PropertyGroup>
                  <ItemGroup>
                    <StaticWebAssetGroup Include="BlazorGroup" SourceId="ClassLibrary" Deferred="true" />
                  </ItemGroup>
                  <Target Name="ResolveDeferredBlazorGroup">
                    <ItemGroup>
                      <StaticWebAssetGroup Remove="BlazorGroup" />
                      <StaticWebAssetGroup Include="BlazorGroup" Value="$(EnableBlazorGroup)" SourceId="ClassLibrary" />
                    </ItemGroup>
                  </Target>
                </Project>
                """);

            var build = CreateBuildCommand(projectDirectory, "AppWithP2PReference");
            ExecuteCommand(build).Should().Pass();

            return build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
        }

        private static StaticWebAssetsManifest LoadBuildManifest(string intermediateOutputPath)
        {
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            return StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
        }

        private static StaticWebAssetEndpoint[] LoadEndpointsManifest(string intermediateOutputPath)
        {
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.endpoints.json");
            var manifest = JsonSerializer.Deserialize<StaticWebAssetEndpointsManifest>(
                File.ReadAllBytes(path),
                StaticWebAssetsJsonSerializerContext.Default.StaticWebAssetEndpointsManifest);
            return manifest?.Endpoints ?? [];
        }
    }
}
