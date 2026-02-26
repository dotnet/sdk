// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.IO.Compression;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests
{
    public class AssetGroupsIntegrationTest(ITestOutputHelper log)
        : IsolatedNuGetPackageFolderAspNetSdkBaselineTest(log, nameof(AssetGroupsIntegrationTest))
    {
        [Fact]
        public void Pack_NupkgContains_GroupedStaticWebAssets()
        {
            var testAsset = "AssetGroupsSample";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var pack = CreatePackCommand(ProjectDirectory, "IdentityUILib");
            var result = ExecuteCommand(pack);

            result.Should().Pass();

            var packagePath = Path.Combine(
                ProjectDirectory.TestRoot,
                "TestPackages",
                "IdentityUILib.1.0.0.nupkg");

            result.Should().NuPkgContainsPatterns(
                packagePath,
                filePatterns: new[]
                {
                    Path.Combine("staticwebassets", "V4", "css", "site.css"),
                    Path.Combine("staticwebassets", "V4", "js", "site.js"),
                    Path.Combine("staticwebassets", "V5", "css", "site.css"),
                    Path.Combine("staticwebassets", "V5", "js", "site.js"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "IdentityUILib.props"),
                    Path.Combine("buildMultiTargeting", "IdentityUILib.props"),
                    Path.Combine("buildTransitive", "IdentityUILib.props"),
                });
        }

        [Fact]
        public void Pack_PropsFile_ContainsAssetGroups_Metadata()
        {
            var testAsset = "AssetGroupsSample";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var pack = CreatePackCommand(ProjectDirectory, "IdentityUILib");
            ExecuteCommand(pack).Should().Pass();

            var packagePath = Path.Combine(
                ProjectDirectory.TestRoot,
                "TestPackages",
                "IdentityUILib.1.0.0.nupkg");

            new FileInfo(packagePath).Should().Exist();

            using var archive = ZipFile.OpenRead(packagePath);
            var propsEntry = archive.Entries.FirstOrDefault(
                e => e.FullName.Equals("build/Microsoft.AspNetCore.StaticWebAssets.props", StringComparison.OrdinalIgnoreCase));

            propsEntry.Should().NotBeNull("the nupkg should contain a StaticWebAssets.props file");

            using var stream = propsEntry.Open();
            using var reader = new StreamReader(stream);
            var propsContent = reader.ReadToEnd();

            // V5 assets should carry AssetGroups metadata containing BootstrapVersion=V5
            propsContent.Should().Contain("BootstrapVersion=V5",
                "V5 assets should have AssetGroups metadata with BootstrapVersion=V5");

            // V4 assets should carry AssetGroups metadata containing BootstrapVersion=V4
            propsContent.Should().Contain("BootstrapVersion=V4",
                "V4 assets should have AssetGroups metadata with BootstrapVersion=V4");
        }

        [Fact]
        public void Build_ConsumerDefault_ExcludesGroupedAssets()
        {
            var testAsset = "AssetGroupsSample";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            // Pack the library first
            var pack = CreatePackCommand(ProjectDirectory, "IdentityUILib");
            ExecuteCommand(pack).Should().Pass();

            // Restore and build the default consumer (no group declarations)
            var restore = CreateRestoreCommand(ProjectDirectory, "IdentityUIConsumer");
            ExecuteCommand(restore).Should().Pass();

            var build = CreateBuildCommand(ProjectDirectory, "IdentityUIConsumer");
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            // Verify the build manifest
            var manifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(manifestPath).Should().Exist();

            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(manifestPath));
            manifest.Should().NotBeNull();

            // Without any StaticWebAssetGroup declarations, all grouped assets should be excluded
            var v4Assets = manifest.Assets
                .Where(a => a.RelativePath.Contains("V4"))
                .ToList();

            var v5Assets = manifest.Assets
                .Where(a => a.RelativePath.Contains("V5"))
                .ToList();

            v4Assets.Should().BeEmpty("V4 grouped assets should be excluded when no group is declared by consumer");
            v5Assets.Should().BeEmpty("V5 grouped assets should be excluded when no group is declared by consumer");
        }

        [Fact]
        public void Build_ConsumerV4_IncludesOnlyV4Assets()
        {
            var testAsset = "AssetGroupsSample";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            // Pack the library first
            var pack = CreatePackCommand(ProjectDirectory, "IdentityUILib");
            ExecuteCommand(pack).Should().Pass();

            // Restore and build the V4 consumer
            var restore = CreateRestoreCommand(ProjectDirectory, "IdentityUIConsumerV4");
            ExecuteCommand(restore).Should().Pass();

            var build = CreateBuildCommand(ProjectDirectory, "IdentityUIConsumerV4");
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            var manifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(manifestPath).Should().Exist();

            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(manifestPath));
            manifest.Should().NotBeNull();

            // V4 assets should be included since the consumer declared BootstrapVersion=V4
            var v4Assets = manifest.Assets
                .Where(a => a.RelativePath.Contains("V4"))
                .ToList();

            v4Assets.Should().NotBeEmpty("V4 assets should be included when consumer declares BootstrapVersion=V4");

            // V5 assets should be excluded (consumer only selected V4)
            var v5Assets = manifest.Assets
                .Where(a => a.RelativePath.Contains("V5"))
                .ToList();

            v5Assets.Should().BeEmpty("V5 assets should be excluded when consumer only declares BootstrapVersion=V4");

            // V4 endpoints should exist
            var v4Endpoints = manifest.Endpoints
                ?.Where(e => e.Route.Contains("V4"))
                .ToList();

            v4Endpoints.Should().NotBeNull().And.NotBeEmpty(
                "endpoints should exist for V4 assets");
        }

        [Fact]
        public void Build_ConsumerV5_IncludesOnlyV5Assets()
        {
            var testAsset = "AssetGroupsSample";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            // Pack the library first
            var pack = CreatePackCommand(ProjectDirectory, "IdentityUILib");
            ExecuteCommand(pack).Should().Pass();

            // Restore and build the V5 consumer
            var restore = CreateRestoreCommand(ProjectDirectory, "IdentityUIConsumerV5");
            ExecuteCommand(restore).Should().Pass();

            var build = CreateBuildCommand(ProjectDirectory, "IdentityUIConsumerV5");
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            var manifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(manifestPath).Should().Exist();

            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(manifestPath));
            manifest.Should().NotBeNull();

            // V5 assets should be included since the consumer declared BootstrapVersion=V5
            var v5Assets = manifest.Assets
                .Where(a => a.RelativePath.Contains("V5"))
                .ToList();

            v5Assets.Should().NotBeEmpty("V5 assets should be included when consumer declares BootstrapVersion=V5");

            // V4 assets should be excluded (consumer only selected V5)
            var v4Assets = manifest.Assets
                .Where(a => a.RelativePath.Contains("V4"))
                .ToList();

            v4Assets.Should().BeEmpty("V4 assets should be excluded when consumer only declares BootstrapVersion=V5");

            // V5 endpoints should exist
            var v5Endpoints = manifest.Endpoints
                ?.Where(e => e.Route.Contains("V5"))
                .ToList();

            v5Endpoints.Should().NotBeNull().And.NotBeEmpty(
                "endpoints should exist for V5 assets");
        }
    }
}
