// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.IO.Compression;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests
{
    public class FrameworkAssetsIntegrationTest(ITestOutputHelper log)
        : IsolatedNuGetPackageFolderAspNetSdkBaselineTest(log, nameof(FrameworkAssetsIntegrationTest))
    {
        [Fact]
        public void Pack_PropsFile_ContainsFrameworkSourceType_ForMatchedAssets()
        {
            var testAsset = "FrameworkAssetsSample";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var pack = CreatePackCommand(ProjectDirectory, "FrameworkAssetsLib");
            ExecuteCommand(pack).Should().Pass();

            var packagePath = Path.Combine(
                ProjectDirectory.TestRoot,
                "TestPackages",
                "FrameworkAssetsLib.1.0.0.nupkg");

            new FileInfo(packagePath).Should().Exist();

            // Extract the props file from the nupkg and verify SourceType
            using var archive = ZipFile.OpenRead(packagePath);
            var propsEntry = archive.Entries.FirstOrDefault(
                e => e.FullName.Equals("build/Microsoft.AspNetCore.StaticWebAssets.props", StringComparison.OrdinalIgnoreCase));

            propsEntry.Should().NotBeNull("the nupkg should contain a StaticWebAssets.props file");

            using var stream = propsEntry.Open();
            using var reader = new StreamReader(stream);
            var propsContent = reader.ReadToEnd();

            // JS files should be marked as Framework
            propsContent.Should().Contain("<SourceType>Framework</SourceType>",
                "JS assets matching the FrameworkPattern should have SourceType=Framework");

            // CSS files should remain as Package
            propsContent.Should().Contain("<SourceType>Package</SourceType>",
                "CSS assets not matching the FrameworkPattern should have SourceType=Package");
        }

        [Fact]
        public void Pack_NupkgContains_ExpectedStaticWebAssets()
        {
            var testAsset = "FrameworkAssetsSample";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var pack = CreatePackCommand(ProjectDirectory, "FrameworkAssetsLib");
            var result = ExecuteCommand(pack);

            result.Should().Pass();

            var packagePath = Path.Combine(
                ProjectDirectory.TestRoot,
                "TestPackages",
                "FrameworkAssetsLib.1.0.0.nupkg");

            result.Should().NuPkgContainsPatterns(
                packagePath,
                filePatterns: new[]
                {
                    Path.Combine("staticwebassets", "js", "framework.js"),
                    Path.Combine("staticwebassets", "css", "site.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "FrameworkAssetsLib.props"),
                    Path.Combine("buildMultiTargeting", "FrameworkAssetsLib.props"),
                    Path.Combine("buildTransitive", "FrameworkAssetsLib.props"),
                });
        }

        [Fact]
        public void Build_Consumer_MaterializesFrameworkAssets()
        {
            var testAsset = "FrameworkAssetsSample";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            // Pack the library first
            var pack = CreatePackCommand(ProjectDirectory, "FrameworkAssetsLib");
            ExecuteCommand(pack).Should().Pass();

            // Restore and build the consumer
            var restore = CreateRestoreCommand(ProjectDirectory, "FrameworkAssetsConsumer");
            ExecuteCommand(restore).Should().Pass();

            var build = CreateBuildCommand(ProjectDirectory, "FrameworkAssetsConsumer");
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            // Verify the build manifest exists and contains our framework asset
            var manifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(manifestPath).Should().Exist();

            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(manifestPath));
            manifest.Should().NotBeNull();

            // The framework JS asset should be materialized (SourceType changed from Framework to Discovered)
            var frameworkAssets = manifest.Assets
                .Where(a => a.RelativePath.Contains("framework.js"))
                .ToList();

            frameworkAssets.Should().NotBeEmpty("framework.js should appear in the build manifest");

            // After materialization, the framework asset should have SourceType=Discovered
            // and be under the fx/ intermediate directory
            var materializedAsset = frameworkAssets
                .FirstOrDefault(a => a.Identity.Contains(Path.Combine("fx", "FrameworkAssetsLib")));

            materializedAsset.Should().NotBeNull(
                "framework.js should be materialized under the fx/FrameworkAssetsLib directory");
            materializedAsset.SourceType.Should().Be("Discovered");
            materializedAsset.AssetMode.Should().Be("CurrentProject");

            // The CSS asset should remain as a regular Package asset (not materialized)
            var cssAssets = manifest.Assets
                .Where(a => a.RelativePath.Contains("site.css"))
                .ToList();

            cssAssets.Should().NotBeEmpty("site.css should appear in the build manifest");
            cssAssets.Should().OnlyContain(a => a.SourceType == "Package",
                "CSS assets should remain as Package type since they don't match the FrameworkPattern");
        }

        [Fact]
        public void Build_Consumer_MaterializedFrameworkAsset_FileExistsOnDisk()
        {
            var testAsset = "FrameworkAssetsSample";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            // Pack the library first
            var pack = CreatePackCommand(ProjectDirectory, "FrameworkAssetsLib");
            ExecuteCommand(pack).Should().Pass();

            // Restore and build the consumer
            var restore = CreateRestoreCommand(ProjectDirectory, "FrameworkAssetsConsumer");
            ExecuteCommand(restore).Should().Pass();

            var build = CreateBuildCommand(ProjectDirectory, "FrameworkAssetsConsumer");
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            // The materialized file should exist on disk under the staticwebassets/fx directory
            var fxDir = Path.Combine(intermediateOutputPath, "staticwebassets", "fx", "FrameworkAssetsLib");
            var materializedFile = Directory.GetFiles(fxDir, "framework.js", SearchOption.AllDirectories);

            materializedFile.Should().HaveCount(1, "framework.js should be materialized exactly once");
            File.ReadAllText(materializedFile[0]).Should().NotBeEmpty();
        }

        [Fact]
        public void Build_Consumer_EndpointsRemapped_ForFrameworkAssets()
        {
            var testAsset = "FrameworkAssetsSample";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            // Pack the library first
            var pack = CreatePackCommand(ProjectDirectory, "FrameworkAssetsLib");
            ExecuteCommand(pack).Should().Pass();

            // Restore and build the consumer
            var restore = CreateRestoreCommand(ProjectDirectory, "FrameworkAssetsConsumer");
            ExecuteCommand(restore).Should().Pass();

            var build = CreateBuildCommand(ProjectDirectory, "FrameworkAssetsConsumer");
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            // Read the build manifest to check endpoints
            var manifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(manifestPath));

            // Check that the framework asset in the manifest has been remapped to the materialized path
            var frameworkAssets = manifest.Assets
                .Where(a => a.RelativePath.Contains("framework.js")
                    && a.Identity.Contains(Path.Combine("staticwebassets", "fx", "FrameworkAssetsLib")))
                .ToList();

            frameworkAssets.Should().NotBeEmpty(
                "the manifest should contain a materialized framework asset under staticwebassets/fx/");

            // Endpoints for the route should exist (some may be compressed variants)
            var fxEndpoints = manifest.Endpoints
                ?.Where(e => e.Route.Contains("framework.js"))
                .ToList();

            fxEndpoints.Should().NotBeNull().And.NotBeEmpty(
                "there should be at least one endpoint for framework.js");

            // At least one endpoint should reference the materialized asset (not all will — compressed endpoints point elsewhere)
            var endpointsPointingToMaterialized = fxEndpoints
                .Where(e => e.AssetFile.Contains(Path.Combine("staticwebassets", "fx", "FrameworkAssetsLib")))
                .ToList();

            endpointsPointingToMaterialized.Should().NotBeEmpty(
                "at least one endpoint for framework.js should point to the materialized file path under staticwebassets/fx/");
        }

        [Fact]
        public void Build_Consumer_IsIncremental()
        {
            var testAsset = "FrameworkAssetsSample";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            // Pack the library first
            var pack = CreatePackCommand(ProjectDirectory, "FrameworkAssetsLib");
            ExecuteCommand(pack).Should().Pass();

            // Restore once
            var restore = CreateRestoreCommand(ProjectDirectory, "FrameworkAssetsConsumer");
            ExecuteCommand(restore).Should().Pass();

            // First build
            var build1 = CreateBuildCommand(ProjectDirectory, "FrameworkAssetsConsumer");
            ExecuteCommand(build1).Should().Pass();

            var intermediateOutputPath = build1.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var fxDir = Path.Combine(intermediateOutputPath, "staticwebassets", "fx", "FrameworkAssetsLib");
            var materializedFile = Directory.GetFiles(fxDir, "framework.js", SearchOption.AllDirectories).Single();
            var firstWriteTime = File.GetLastWriteTimeUtc(materializedFile);

            // Second build — should be incremental (file not re-copied)
            var build2 = CreateBuildCommand(ProjectDirectory, "FrameworkAssetsConsumer");
            ExecuteCommand(build2).Should().Pass();

            var secondWriteTime = File.GetLastWriteTimeUtc(materializedFile);
            secondWriteTime.Should().Be(firstWriteTime,
                "framework asset should not be re-copied on incremental build");
        }
    }
}
