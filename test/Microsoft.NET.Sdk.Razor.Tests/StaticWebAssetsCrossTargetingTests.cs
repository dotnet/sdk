// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class StaticWebAssetsCrossTargetingTests(ITestOutputHelper log)
        : IsolatedNuGetPackageFolderAspNetSdkBaselineTest(log, nameof(StaticWebAssetsCrossTargetingTests))
    {
        // Build Standalone project
        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void Build_CrosstargetingTests_CanIncludeBrowserAssets()
        {
            var expectedManifest = LoadBuildManifest();
            var testAsset = "RazorComponentAppMultitarget";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            ProjectDirectory.WithProjectChanges(d =>
            {
                d.Root.Element("PropertyGroup").Add(
                    XElement.Parse("""<StaticWebAssetBasePath>/</StaticWebAssetBasePath>"""));

                d.Root.LastNode.AddBeforeSelf(
                    XElement.Parse("""
                        <ItemGroup>
                          <StaticWebAssetsEmbeddedConfiguration
                            Include="$(TargetFramework)"
                            Condition="'$([MSBuild]::GetTargetPlatformIdentifier($(TargetFramework)))' == '' And $([MSBuild]::VersionGreaterThanOrEquals(8.0, $([MSBuild]::GetTargetFrameworkVersion($(TargetFramework)))))">
                            <Platform>browser</Platform>
                          </StaticWebAssetsEmbeddedConfiguration>
                        </ItemGroup>
                        """));
            });

            var wwwroot = Directory.CreateDirectory(Path.Combine(ProjectDirectory.TestRoot, "wwwroot"));
            File.WriteAllText(Path.Combine(wwwroot.FullName, "test.js"), "console.log('hello')");

            var build = CreateBuildCommand(ProjectDirectory);
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, expectedManifest);
            AssertBuildAssets(manifest, outputPath, intermediateOutputPath);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "RazorComponentAppMultitarget.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();
        }

        [Fact]
        public void Publish_CrosstargetingTests_CanIncludeBrowserAssets()
        {
            var testAsset = "RazorComponentAppMultitarget";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            ProjectDirectory.WithProjectChanges(d =>
            {
                d.Root.Element("PropertyGroup").Add(
                    XElement.Parse("""<StaticWebAssetBasePath>/</StaticWebAssetBasePath>"""));

                d.Root.LastNode.AddBeforeSelf(
                    XElement.Parse("""
                        <ItemGroup>
                          <StaticWebAssetsEmbeddedConfiguration
                            Include="$(TargetFramework)"
                            Condition="'$([MSBuild]::GetTargetPlatformIdentifier($(TargetFramework)))' == '' And $([MSBuild]::VersionGreaterThanOrEquals(8.0, $([MSBuild]::GetTargetFrameworkVersion($(TargetFramework)))))">
                            <Platform>browser</Platform>
                          </StaticWebAssetsEmbeddedConfiguration>
                        </ItemGroup>
                        """));
            });

            var wwwroot = Directory.CreateDirectory(Path.Combine(ProjectDirectory.TestRoot, "wwwroot"));
            File.WriteAllText(Path.Combine(wwwroot.FullName, "test.js"), "console.log('hello')");

            var restore = CreateRestoreCommand(ProjectDirectory);
            ExecuteCommand(restore).Should().Pass();

            var publish = CreatePublishCommand(ProjectDirectory);
            ExecuteCommandWithoutRestore(publish, "/bl", "/p:TargetFramework=net10.0").Should().Pass();

            var publishPath = publish.GetOutputDirectory(DefaultTfm).ToString();
            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadPublishManifest());

            AssertPublishAssets(
                manifest,
                publishPath,
                intermediateOutputPath);
        }
    }
}
