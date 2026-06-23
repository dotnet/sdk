// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests
{
    [TestClass]
    public class DeferredAssetGroupsIntegrationTest : AspNetSdkTest
    {
        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
        public void Publish_DeferredGroupWithTwoVariantsOnSameRoute_KeepsSingleVariant()
        {
            // Two grouped variants (V4/V5) of a referenced project's asset collapse to the same route
            // (via the file-only '~' segment) and both are AssetKind=All. A deferred group resolves to a
            // single variant at build time. Publish must honor that decision: previously the deferred group
            // was never re-resolved at publish, so both variants survived and GenerateStaticWebAssetEndpointsManifest
            // threw 'Sequence contains more than one element'. See https://github.com/dotnet/sdk/issues/54940.
            var projectDirectory = CreateTwoVariantDeferredGroupProject(keepValue: "V5");

            var publish = CreatePublishCommand(projectDirectory, "AppWithP2PReference");
            ExecuteCommand(publish).Should().Pass();

            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            var publishManifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(publishManifestPath));

            // Exactly one primary asset should survive on the shared route, and it must be the V5 variant.
            var primaryAssets = publishManifest.Assets
                .Where(a => a.RelativePath.EndsWith("shared.js") && a.AssetRole == "Primary")
                .ToList();
            primaryAssets.Should().ContainSingle("only the winning (V5) variant should survive publish on the shared route");
            primaryAssets[0].AssetGroups.Should().Contain("V5");
            primaryAssets[0].AssetGroups.Should().NotContain("V4", "the V4 variant should have been excluded by the deferred group");

            var endpoints = LoadPublishEndpointsManifest(intermediateOutputPath);

            // Exactly one uncompressed endpoint on the collapsed route — the buggy path produced two.
            var uncompressedEndpoints = endpoints
                .Where(e => e.Route.EndsWith("shared.js") && e.Selectors.Length == 0)
                .ToList();
            uncompressedEndpoints.Should().ContainSingle("exactly one uncompressed endpoint should exist for the shared route");
        }

        [TestMethod]
        public void Publish_NoBuild_DeferredGroupWithTwoVariantsOnSameRoute_KeepsSingleVariant()
        {
            // This test covers the disk-manifest path (ReadStaticWebAssetsManifestFile →
            // LoadStaticWebAssetsBuildManifest) that is exercised when publishing with NoBuild=true.
            // After a regular build, the resolved groups are persisted in staticwebassets.build.json.
            // A subsequent NoBuild publish must re-apply those groups from the manifest on disk and
            // exclude the losing variant, preventing 'Sequence contains more than one element' in
            // GenerateStaticWebAssetEndpointsManifest. See https://github.com/dotnet/sdk/issues/54940.
            var projectDirectory = CreateTwoVariantDeferredGroupProject(keepValue: "V5");

            var build = CreateBuildCommand(projectDirectory, "AppWithP2PReference");
            ExecuteCommand(build).Should().Pass();

            var publish = CreatePublishCommand(projectDirectory, "AppWithP2PReference");
            ExecuteCommand(publish, "/p:NoBuild=true").Should().Pass();

            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            var publishManifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(publishManifestPath));

            // Exactly one primary asset should survive on the shared route, and it must be the V5 variant.
            var primaryAssets = publishManifest.Assets
                .Where(a => a.RelativePath.EndsWith("shared.js") && a.AssetRole == "Primary")
                .ToList();
            primaryAssets.Should().ContainSingle("only the winning (V5) variant should survive NoBuild publish on the shared route");
            primaryAssets[0].AssetGroups.Should().Contain("V5");
            primaryAssets[0].AssetGroups.Should().NotContain("V4", "the V4 variant should have been excluded by the deferred group");

            var endpoints = LoadPublishEndpointsManifest(intermediateOutputPath);

            // Exactly one uncompressed endpoint on the collapsed route — the buggy path produced two.
            var uncompressedEndpoints = endpoints
                .Where(e => e.Route.EndsWith("shared.js") && e.Selectors.Length == 0)
                .ToList();
            uncompressedEndpoints.Should().ContainSingle("exactly one uncompressed endpoint should exist for the shared route");
        }

        private TestAsset CreateTwoVariantDeferredGroupProject(string keepValue, [CallerMemberName] string callerName = "")
        {
            var testAsset = "RazorAppWithP2PReference";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, callerName, identifier: $"TwoVariant_{keepValue}")
                .WithProjectChanges((path, document) =>
                {
                    if (Path.GetFileName(path) == "ClassLibrary.csproj")
                    {
                        document.Root.Add(
                            new XElement("ItemGroup",
                                new XElement("StaticWebAssetGroupDefinition",
                                    new XAttribute("Include", "SharedVariants"),
                                    new XAttribute("Value", "V5"),
                                    new XAttribute("Order", "0"),
                                    new XAttribute("SourceId", "ClassLibrary"),
                                    new XAttribute("IncludePattern", "V5/**"),
                                    new XAttribute("RelativePathPattern", "V5/**"),
                                    new XAttribute("RelativePathPrefix", "#[{SharedVariants}]~/"),
                                    new XAttribute("ContentRootSuffix", "V5")),
                                new XElement("StaticWebAssetGroupDefinition",
                                    new XAttribute("Include", "SharedVariants"),
                                    new XAttribute("Value", "V4"),
                                    new XAttribute("Order", "1"),
                                    new XAttribute("SourceId", "ClassLibrary"),
                                    new XAttribute("IncludePattern", "V4/**"),
                                    new XAttribute("RelativePathPattern", "V4/**"),
                                    new XAttribute("RelativePathPrefix", "#[{SharedVariants}]~/"),
                                    new XAttribute("ContentRootSuffix", "V4"))));
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
            Directory.CreateDirectory(Path.Combine(classLibDir, "wwwroot", "V4"));
            Directory.CreateDirectory(Path.Combine(classLibDir, "wwwroot", "V5"));

            File.WriteAllText(Path.Combine(classLibDir, "wwwroot", "V4", "shared.js"), "console.log('v4 shared');");
            File.WriteAllText(Path.Combine(classLibDir, "wwwroot", "V5", "shared.js"), "console.log('v5 shared');");

            File.WriteAllText(
                Path.Combine(classLibDir, "StaticWebAssets.Groups.targets"),
                $$"""
                <Project>
                  <!-- SharedVariantsValue is the value the deferred 'SharedVariants' group resolves to.
                       ResolveSharedVariants (hooked into FilterDeferredStaticWebAssetGroupsDependsOn) flips
                       the group from Deferred to this concrete value so a single variant wins. -->
                  <PropertyGroup>
                    <SharedVariantsValue Condition="'$(SharedVariantsValue)' == ''">{{keepValue}}</SharedVariantsValue>
                    <FilterDeferredStaticWebAssetGroupsDependsOn>
                      $(FilterDeferredStaticWebAssetGroupsDependsOn);
                      ResolveSharedVariants
                    </FilterDeferredStaticWebAssetGroupsDependsOn>
                  </PropertyGroup>
                  <ItemGroup>
                    <StaticWebAssetGroup Include="SharedVariants" SourceId="ClassLibrary" Deferred="true" />
                  </ItemGroup>
                  <Target Name="ResolveSharedVariants">
                    <ItemGroup>
                      <StaticWebAssetGroup Remove="SharedVariants" />
                      <StaticWebAssetGroup Include="SharedVariants" Value="$(SharedVariantsValue)" SourceId="ClassLibrary" />
                    </ItemGroup>
                  </Target>
                </Project>
                """);

            return projectDirectory;
        }

        private static StaticWebAssetEndpoint[] LoadPublishEndpointsManifest(string intermediateOutputPath)
        {
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.publish.endpoints.json");
            var manifest = JsonSerializer.Deserialize<StaticWebAssetEndpointsManifest>(
                File.ReadAllBytes(path),
                StaticWebAssetsJsonSerializerContext.Default.StaticWebAssetEndpointsManifest);
            return manifest?.Endpoints ?? [];
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
