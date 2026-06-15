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
            var packagePath = PackIdentityUILib("Pack_Nupkg");

            new FileInfo(packagePath).Should().Exist();

            using var archive = ZipFile.OpenRead(packagePath);
            var entryNames = archive.Entries.Select(e => e.FullName).ToList();

            var expectedPatterns = new[]
            {
                "staticwebassets/V4/css/site.css",
                "staticwebassets/V4/js/site.js",
                "staticwebassets/V5/css/site.css",
                "staticwebassets/V5/js/site.js",
                "build/IdentityUILib.PackageAssets.json",
                "build/Microsoft.AspNetCore.StaticWebAssets.targets",
                "build/StaticWebAssets.Groups.targets",
                "build/IdentityUILib.targets",
                "buildMultiTargeting/IdentityUILib.targets",
                "buildTransitive/IdentityUILib.targets",
            };

            foreach (var pattern in expectedPatterns)
            {
                entryNames.Should().Contain(
                    e => e.Replace('\\', '/').EndsWith(pattern, StringComparison.OrdinalIgnoreCase),
                    $"nupkg should contain entry matching '{pattern}'");
            }
        }

        [Fact]
        public void Pack_PropsFile_ContainsAssetGroups_Metadata()
        {
            var packagePath = PackIdentityUILib("Pack_Props");

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
            var manifest = BuildConsumer("Build_Default", "IdentityUIConsumer");

            // The library's StaticWebAssets.Groups.props defaults IdentityUIFrameworkVersion=V5,
            // so V5 is selected automatically and V4 is excluded.
            var v4Assets = manifest.Assets
                .Where(a => (a.AssetGroups ?? "").Contains("V4"))
                .ToList();

            var v5PrimaryAssets = manifest.Assets
                .Where(a => (a.AssetGroups ?? "").Contains("V5") && a.AssetRole == "Primary")
                .ToList();

            v4Assets.Should().BeEmpty("V4 grouped assets should be excluded when the default selects V5");
            v5PrimaryAssets.Should().HaveCountGreaterThan(1,
                "V5 should be the default group — at least css/site.css and js/site.js expected");
        }

        [Fact]
        public void Build_ConsumerV4_IncludesOnlyV4Assets()
        {
            var manifest = BuildConsumer("Build_V4", "IdentityUIConsumerV4");

            var includedAssets = manifest.Assets
                .Where(a => (a.AssetGroups ?? "").Contains("V4") && a.AssetRole == "Primary")
                .ToList();

            includedAssets.Should().HaveCountGreaterThan(1,
                "V4 assets should be included when consumer selects V4 — at least css/site.css and js/site.js");

            var excludedAssets = manifest.Assets
                .Where(a => (a.AssetGroups ?? "").Contains("V5"))
                .ToList();

            excludedAssets.Should().BeEmpty(
                "V5 assets should be excluded when consumer only selects V4");

            var includedAssetFiles = new HashSet<string>(includedAssets.Select(a => a.Identity));
            var includedEndpoints = manifest.Endpoints
                ?.Where(e => includedAssetFiles.Contains(e.AssetFile))
                .ToList();

            includedEndpoints.Should().NotBeNull().And.HaveCountGreaterThan(1,
                "endpoints should exist for V4 assets — at least one per asset");

            includedEndpoints.Should().AllSatisfy(e =>
                e.Route.Should().NotContain("V4/",
                    "file-only segment (~) should be excluded from endpoint routes"));
        }

        [Fact]
        public void Build_ConsumerV5_IncludesOnlyV5Assets()
        {
            var manifest = BuildConsumer("Build_V5", "IdentityUIConsumerV5");

            var includedAssets = manifest.Assets
                .Where(a => (a.AssetGroups ?? "").Contains("V5") && a.AssetRole == "Primary")
                .ToList();

            includedAssets.Should().HaveCountGreaterThan(1,
                "V5 assets should be included when consumer selects V5 — at least css/site.css and js/site.js");

            var excludedAssets = manifest.Assets
                .Where(a => (a.AssetGroups ?? "").Contains("V4"))
                .ToList();

            excludedAssets.Should().BeEmpty(
                "V4 assets should be excluded when consumer only selects V5");

            var includedAssetFiles = new HashSet<string>(includedAssets.Select(a => a.Identity));
            var includedEndpoints = manifest.Endpoints
                ?.Where(e => includedAssetFiles.Contains(e.AssetFile))
                .ToList();

            includedEndpoints.Should().NotBeNull().And.HaveCountGreaterThan(1,
                "endpoints should exist for V5 assets — at least one per asset");

            includedEndpoints.Should().AllSatisfy(e =>
                e.Route.Should().NotContain("V5/",
                    "file-only segment (~) should be excluded from endpoint routes"));
        }

        // Clear the cached package so NuGet re-extracts from the freshly-packed nupkg.
        private void ClearCachedPackage(string packageId)
        {
            var cached = Path.Combine(GetNuGetCachePath(), packageId);
            if (Directory.Exists(cached))
            {
                Directory.Delete(cached, recursive: true);
            }
        }

        private string PackIdentityUILib(string identifier)
        {
            var testAsset = "AssetGroupsSample";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset, identifier: identifier);

            var pack = CreatePackCommand(ProjectDirectory, "IdentityUILib");
            ExecuteCommand(pack).Should().Pass();

            return Path.Combine(
                ProjectDirectory.TestRoot,
                "TestPackages",
                "IdentityUILib.1.0.0.nupkg");
        }

        private StaticWebAssetsManifest BuildConsumer(string identifier, string consumerProject)
        {
            var testAsset = "AssetGroupsSample";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset, identifier: identifier);

            var pack = CreatePackCommand(ProjectDirectory, "IdentityUILib");
            ExecuteCommand(pack).Should().Pass();
            ClearCachedPackage("identityuilib");

            var restore = CreateRestoreCommand(ProjectDirectory, consumerProject);
            ExecuteCommand(restore).Should().Pass();

            var build = CreateBuildCommand(ProjectDirectory, consumerProject);
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var manifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(manifestPath).Should().Exist();

            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(manifestPath));
            manifest.Should().NotBeNull();
            return manifest;
        }
    }
}
