// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Xml.Linq;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests
{
    public class FrameworkAssetsP2PIntegrationTest(ITestOutputHelper log) : AspNetSdkTest(log)
    {
        [Fact]
        public void Build_Consumer_MaterializesFrameworkAssetsFromProjectReference()
        {
            var intermediateOutputPath = BuildConsumerWithFrameworkPattern();
            var manifest = LoadBuildManifest(intermediateOutputPath);

            // The JS assets matching the FrameworkPattern should be materialized under fx/
            var materializedAssets = manifest.Assets
                .Where(a => a.RelativePath.Contains(".js")
                    && a.Identity.Contains(Path.Combine("fx", "ClassLibrary")))
                .ToList();

            materializedAssets.Should().NotBeEmpty(
                "JS assets matching FrameworkPattern should be materialized under the fx/ directory");

            foreach (var asset in materializedAssets)
            {
                // SourceType should be Discovered (changed from Framework during materialization)
                asset.SourceType.Should().Be("Discovered",
                    $"materialized framework asset {asset.RelativePath} should have SourceType=Discovered");

                // SourceId should be updated to the consuming project's PackageId
                asset.SourceId.Should().Be("AppWithP2PReference",
                    $"materialized framework asset {asset.RelativePath} should have SourceId updated to the consumer");

                // BasePath should be the consumer's base path ("/" for a web app)
                asset.BasePath.Should().Be("/",
                    $"materialized framework asset {asset.RelativePath} should have BasePath updated to the consumer");

                // AssetMode should be CurrentProject
                asset.AssetMode.Should().Be("CurrentProject",
                    $"materialized framework asset {asset.RelativePath} should have AssetMode=CurrentProject");
            }
        }

        [Fact]
        public void Build_Consumer_NonMatchingAssetsRemainUnchanged()
        {
            var intermediateOutputPath = BuildConsumerWithFrameworkPattern();
            var manifest = LoadBuildManifest(intermediateOutputPath);

            // CSS assets from ClassLibrary should remain as Project type (they don't match **/*.js)
            var cssAssets = manifest.Assets
                .Where(a => a.RelativePath.Contains(".css") && a.SourceId == "ClassLibrary")
                .ToList();

            cssAssets.Should().NotBeEmpty("CSS assets from ClassLibrary should be present");
            cssAssets.Should().OnlyContain(a => a.SourceType == "Project",
                "CSS assets not matching FrameworkPattern should remain as Project type");
        }

        [Fact]
        public void Build_Consumer_MaterializedFrameworkAssetFilesExistOnDisk()
        {
            var intermediateOutputPath = BuildConsumerWithFrameworkPattern();

            var fxDir = Path.Combine(intermediateOutputPath, "staticwebassets", "fx", "ClassLibrary");
            Directory.Exists(fxDir).Should().BeTrue("the fx/ClassLibrary directory should be created");

            var materializedFiles = Directory.GetFiles(fxDir, "*.js", SearchOption.AllDirectories);
            materializedFiles.Should().NotBeEmpty("JS framework assets should be copied to the fx/ directory");
        }

        [Fact]
        public void Build_Consumer_EndpointsExistForMaterializedFrameworkAssets()
        {
            var intermediateOutputPath = BuildConsumerWithFrameworkPattern();
            var manifest = LoadBuildManifest(intermediateOutputPath);

            var materializedAssets = manifest.Assets
                .Where(a => a.RelativePath.Contains(".js")
                    && a.Identity.Contains(Path.Combine("fx", "ClassLibrary")))
                .ToList();

            materializedAssets.Should().NotBeEmpty();

            // Each materialized asset should have at least one endpoint referencing it
            foreach (var asset in materializedAssets)
            {
                var matchingEndpoints = manifest.Endpoints
                    ?.Where(e => string.Equals(e.AssetFile, asset.Identity, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                matchingEndpoints.Should().NotBeNullOrEmpty(
                    $"materialized framework asset {asset.RelativePath} should have at least one endpoint");

                // Endpoint routes should NOT contain the library's base path — they should
                // reflect the consumer's base path (which is "/" for a web app).
                foreach (var ep in matchingEndpoints)
                {
                    ep.Route.Should().NotContain("_content/ClassLibrary",
                        "endpoint route should not retain the library's base path after materialization");
                }
            }
        }

        private string BuildConsumerWithFrameworkPattern()
        {
            var projectDirectory = CreateAspNetSdkTestAsset("RazorAppWithP2PReference")
                .WithProjectChanges((path, document) =>
                {
                    if (Path.GetFileName(path) == "ClassLibrary.csproj")
                    {
                        // Add FrameworkPattern to mark all .js files as framework assets
                        var propertyGroup = document.Root.Descendants("TargetFramework").First().Parent;
                        propertyGroup.Add(
                            new XElement("StaticWebAssetFrameworkPattern", "**/*.js"));
                    }
                });

            var build = CreateBuildCommand(projectDirectory, "AppWithP2PReference");
            ExecuteCommand(build).Should().Pass();

            return build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
        }

        private static StaticWebAssetsManifest LoadBuildManifest(string intermediateOutputPath)
        {
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            return StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
        }
    }
}
