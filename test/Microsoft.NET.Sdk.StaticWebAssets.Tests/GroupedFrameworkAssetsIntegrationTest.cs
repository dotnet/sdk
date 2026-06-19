// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml.Linq;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests
{
    [TestClass]
    public class GroupedFrameworkAssetsIntegrationTest : IsolatedNuGetPackageFolderAspNetSdkBaselineTest
    {
        protected override string RestoreNugetPackagePath => nameof(GroupedFrameworkAssetsIntegrationTest);
        // Regression coverage for the blazor.webassembly.js 404. A package ships an asset that is both a
        // framework asset and a member of a group (as Microsoft.AspNetCore.Components.WebAssembly does for
        // blazor.webassembly.js). The scenario is Package -> Library -> App:
        //  * The group is opt-in via the IncludeGroupedFrameworkAssets property set by the library, so
        //    inclusion is conditional.
        //  * When the library enables the group, the framework asset materializes into the library under the
        //    library base path (_content/<Library>) with its AssetGroups cleared. If AssetGroups is not
        //    cleared during materialization, downstream endpoint generation skips the asset and it 404s.
        //  * The materialized framework asset is a current-project asset of the library, so the app (which
        //    does not enable the group) does not include it at the root.
        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void Build_PackageToLibraryToApp_GroupedFrameworkAsset_IsConditionalAndMaterializedIntoLibrary(bool includeGroupedFrameworkAssets)
        {
            ProjectDirectory = CreateAspNetSdkTestAsset("GroupedFrameworkAssetsSample", identifier: includeGroupedFrameworkAssets.ToString())
                .WithProjectChanges((path, document) =>
                {
                    if (Path.GetFileName(path) == "GroupedFrameworkLibrary.csproj")
                    {
                        // Only the library opts into the group, so the app does not re-include the asset.
                        var propertyGroup = document.Root.Descendants("TargetFramework").First().Parent;
                        propertyGroup.Add(
                            new XElement("IncludeGroupedFrameworkAssets", includeGroupedFrameworkAssets.ToString()));
                    }
                });

            var pack = CreatePackCommand(ProjectDirectory, "GroupedFrameworkPackage");
            ExecuteCommand(pack).Should().Pass();
            ClearCachedPackage("groupedframeworkpackage");

            var build = CreateBuildCommand(ProjectDirectory, "GroupedFrameworkApp");
            ExecuteCommand(build).Should().Pass();

            var libraryManifest = LoadBuildManifest(
                Path.Combine(ProjectDirectory.TestRoot, "GroupedFrameworkLibrary", "obj", "Debug", DefaultTfm));
            var appManifest = LoadBuildManifest(build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString());

            // The materialized asset lives under fx\<originalSourceId> (the originating framework
            // package), but is owned by the consuming library (SourceId, BasePath are remapped).
            var libraryMaterializedJs = libraryManifest.Assets
                .Where(a => a.RelativePath.Contains(".js")
                    && a.Identity.Contains(Path.Combine("fx", "GroupedFrameworkPackage")))
                .ToList();

            if (includeGroupedFrameworkAssets)
            {
                libraryMaterializedJs.Should().NotBeEmpty(
                    "the grouped JS framework asset should be materialized into the library when the group is enabled");

                foreach (var asset in libraryMaterializedJs)
                {
                    // Materialized into the library, under the library base path.
                    asset.SourceId.Should().Be("GroupedFrameworkLibrary",
                        $"materialized framework asset {asset.RelativePath} should belong to the library");
                    asset.BasePath.Should().Be("_content/GroupedFrameworkLibrary",
                        $"materialized framework asset {asset.RelativePath} should be under the library base path");

                    // AssetGroups must be cleared, otherwise endpoint generation skips the asset (the 404).
                    asset.AssetGroups.Should().BeNullOrEmpty(
                        $"materialized framework asset {asset.RelativePath} must have its AssetGroups cleared");
                }

                // The framework asset is a current-project asset of the library, so the app does not
                // include it at the root.
                appManifest.Assets
                    .Where(a => a.RelativePath.Contains("feature") && a.BasePath == "/")
                    .Should().BeEmpty("the app should not include the library's framework asset at the root");
            }
            else
            {
                // The group is not enabled, so the grouped framework asset is excluded entirely.
                libraryMaterializedJs.Should().BeEmpty(
                    "the grouped JS framework asset should be excluded when the group is not enabled");
            }
        }

        private static StaticWebAssetsManifest LoadBuildManifest(string intermediateOutputPath)
        {
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            manifest.Should().NotBeNull();
            return manifest;
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
    }
}
