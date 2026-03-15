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
        // Clear the cached package so NuGet re-extracts from the freshly-packed nupkg.
        private void ClearCachedPackage(string packageId)
        {
            var cached = Path.Combine(GetNuGetCachePath(), packageId);
            if (Directory.Exists(cached))
            {
                Directory.Delete(cached, recursive: true);
            }
        }

        [Fact]
        public void Pack_NupkgContains_GroupedStaticWebAssets()
        {
            var testAsset = "AssetGroupsSample";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset, identifier: "Pack_Nupkg");

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
                    Path.Combine("build", "IdentityUILib.PackageAssets.json"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.targets"),
                    Path.Combine("build", "StaticWebAssets.Groups.targets"),
                    Path.Combine("build", "IdentityUILib.targets"),
                    Path.Combine("buildMultiTargeting", "IdentityUILib.targets"),
                    Path.Combine("buildTransitive", "IdentityUILib.targets"),
                });
        }

        [Fact]
        public void Pack_PropsFile_ContainsAssetGroups_Metadata()
        {
            var testAsset = "AssetGroupsSample";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset, identifier: "Pack_Props");

            var pack = CreatePackCommand(ProjectDirectory, "IdentityUILib");
            ExecuteCommand(pack).Should().Pass();

            var packagePath = Path.Combine(
                ProjectDirectory.TestRoot,
                "TestPackages",
                "IdentityUILib.1.0.0.nupkg");

            new FileInfo(packagePath).Should().Exist();

            using var archive = ZipFile.OpenRead(packagePath);
            var manifestEntry = archive.Entries.FirstOrDefault(
                e => e.FullName.Equals("build/IdentityUILib.PackageAssets.json", StringComparison.OrdinalIgnoreCase));

            manifestEntry.Should().NotBeNull("the nupkg should contain a PackageAssets.json manifest file");

            using var stream = manifestEntry.Open();
            using var reader = new StreamReader(stream);
            var manifestContent = reader.ReadToEnd();

            // V5 assets should carry AssetGroups metadata containing BootstrapVersion=V5
            manifestContent.Should().Contain("BootstrapVersion=V5",
                "V5 assets should have AssetGroups metadata with BootstrapVersion=V5");

            // V4 assets should carry AssetGroups metadata containing BootstrapVersion=V4
            manifestContent.Should().Contain("BootstrapVersion=V4",
                "V4 assets should have AssetGroups metadata with BootstrapVersion=V4");
        }

        [Fact]
        public void Build_ConsumerDefault_ExcludesGroupedAssets()
        {
            var testAsset = "AssetGroupsSample";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset, identifier: "Build_Default");

            // Pack the library first
            var pack = CreatePackCommand(ProjectDirectory, "IdentityUILib");
            ExecuteCommand(pack).Should().Pass();
            ClearCachedPackage("identityuilib");

            // Restore and build the default consumer (no explicit group override)
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

            // The library's StaticWebAssets.Groups.props defaults IdentityUIFrameworkVersion=V5,
            // so V5 is selected automatically and V4 is excluded.
            var v4Assets = manifest.Assets
                .Where(a => (a.AssetGroups ?? "").Contains("V4"))
                .ToList();

            var v5PrimaryAssets = manifest.Assets
                .Where(a => (a.AssetGroups ?? "").Contains("V5") && a.AssetRole == "Primary")
                .ToList();

            v4Assets.Should().BeEmpty("V4 grouped assets should be excluded when the default selects V5");
            v5PrimaryAssets.Should().NotBeEmpty("V5 should be the default group selected by the library's Groups.props");
        }

        [Fact]
        public void Build_ConsumerV4_IncludesOnlyV4Assets()
        {
            var testAsset = "AssetGroupsSample";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset, identifier: "Build_V4");

            // Pack the library first
            var pack = CreatePackCommand(ProjectDirectory, "IdentityUILib");
            ExecuteCommand(pack).Should().Pass();
            ClearCachedPackage("identityuilib");

            // Restore and build the V4 consumer
            var restore = CreateRestoreCommand(ProjectDirectory, "IdentityUIConsumerV4");
            ExecuteCommand(restore).Should().Pass();

            var build = CreateBuildCommand(ProjectDirectory, "IdentityUIConsumerV4");
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            var manifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(manifestPath).Should().Exist();

            var manifestContent = File.ReadAllText(manifestPath);

            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(manifestPath));
            manifest.Should().NotBeNull();

            var assetDump = string.Join("\n", (manifest.Assets ?? []).Select(a =>
                $"  Asset: Identity={a.Identity}, SourceId={a.SourceId}, AssetGroups={a.AssetGroups}, AssetRole={a.AssetRole}, RelativePath={a.RelativePath}, SourceType={a.SourceType}"));

            // V4 assets should be included since the consumer set IdentityUIFrameworkVersion=V4
            var v4Assets = manifest.Assets
                .Where(a => (a.AssetGroups ?? "").Contains("V4") && a.AssetRole == "Primary")
                .ToList();

            v4Assets.Should().NotBeEmpty($"V4 assets should be included when consumer sets IdentityUIFrameworkVersion=V4. All assets in manifest ({manifest.Assets?.Length ?? 0}):\n{assetDump}");

            // V5 assets should be excluded (consumer only selected V4)
            var v5Assets = manifest.Assets
                .Where(a => (a.AssetGroups ?? "").Contains("V5"))
                .ToList();

            v5Assets.Should().BeEmpty("V5 assets should be excluded when consumer only selects V4");

            // Endpoints should exist for the included V4 assets.
            // The group token is file-only (~), so V4/ does NOT appear in endpoint routes.
            var v4AssetFiles = new HashSet<string>(v4Assets.Select(a => a.Identity));
            var v4Endpoints = manifest.Endpoints
                ?.Where(e => v4AssetFiles.Contains(e.AssetFile))
                .ToList();

            v4Endpoints.Should().NotBeNull().And.NotBeEmpty(
                "endpoints should exist for V4 assets");
        }

        [Fact]
        public void Build_ConsumerV5_IncludesOnlyV5Assets()
        {
            var testAsset = "AssetGroupsSample";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset, identifier: "Build_V5");

            // Pack the library first
            var pack = CreatePackCommand(ProjectDirectory, "IdentityUILib");
            ExecuteCommand(pack).Should().Pass();
            ClearCachedPackage("identityuilib");

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

            // V5 assets should be included since the consumer set IdentityUIFrameworkVersion=V5
            var v5Assets = manifest.Assets
                .Where(a => (a.AssetGroups ?? "").Contains("V5") && a.AssetRole == "Primary")
                .ToList();

            v5Assets.Should().NotBeEmpty("V5 assets should be included when consumer sets IdentityUIFrameworkVersion=V5");

            // V4 assets should be excluded (consumer only selected V5)
            var v4Assets = manifest.Assets
                .Where(a => (a.AssetGroups ?? "").Contains("V4"))
                .ToList();

            v4Assets.Should().BeEmpty("V4 assets should be excluded when consumer only selects V5");

            // Endpoints should exist for the included V5 assets.
            // The group token is file-only (~), so V5/ does NOT appear in endpoint routes.
            var v5AssetFiles = new HashSet<string>(v5Assets.Select(a => a.Identity));
            var v5Endpoints = manifest.Endpoints
                ?.Where(e => v5AssetFiles.Contains(e.AssetFile))
                .ToList();

            v5Endpoints.Should().NotBeNull().And.NotBeEmpty(
                "endpoints should exist for V5 assets");
        }
    }
}
