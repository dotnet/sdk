// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks;

[assembly:CollectionBehavior(DisableTestParallelization = true)]

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class LegacyStaticWebAssetsV1IntegrationTest(ITestOutputHelper log)
        : IsolatedNuGetPackageFolderAspNetSdkBaselineTest(log, nameof(LegacyStaticWebAssetsV1IntegrationTest))
    {
        [Fact]
        public void PublishProjectWithReferences_WorksWithStaticWebAssetsV1ClassLibraries()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset)
                .WithProjectChanges((project, document) =>
                {
                    if (Path.GetFileName(project) == "AnotherClassLib.csproj")
                    {
                        document.Descendants("TargetFramework").Single().ReplaceNodes("netstandard2.1");
                        document.Descendants("FrameworkReference").Single().Remove();
                        document.Descendants("PropertyGroup").First().Add(new XElement("RazorLangVersion", "3.0"));
                    }
                    if (Path.GetFileName(project) == "ClassLibrary.csproj")
                    {
                        document.Descendants("TargetFramework").Single().ReplaceNodes("netstandard2.0");
                        document.Descendants("FrameworkReference").Single().Remove();
                        document.Descendants("PropertyGroup").First().Add(new XElement("RazorLangVersion", "3.0"));
                    }
                });

            // We are deleting Views and Components because we are only interested in the static web assets behavior for this test
            // and this makes it easier to validate the test.
            Directory.Delete(Path.Combine(ProjectDirectory.TestRoot, "AnotherClassLib", "Views"), recursive: true);
            Directory.Delete(Path.Combine(ProjectDirectory.TestRoot, "ClassLibrary", "Views"), recursive: true);
            Directory.Delete(Path.Combine(ProjectDirectory.TestRoot, "ClassLibrary", "Components"), recursive: true);

            EnsureLocalPackagesExists();

            var restore = CreateRestoreCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            ExecuteCommand(restore).Should().Pass();

            var publish = CreatePublishCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            ExecuteCommand(publish).Should().Pass();

            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var publishPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            AssertManifest(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path)),
                LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(publishPath, "AppWithPackageAndP2PReference.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().NotExist();

            // GenerateStaticWebAssetsPublishManifest should generate the publish manifest file.
            var intermediatePublishManifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath));
            AssertManifest(
                publishManifest,
                LoadPublishManifest());

            AssertPublishAssets(
                publishManifest,
                publishPath,
                intermediateOutputPath);
        }

        [Fact]
        public void BuildProjectWithReferences_WorksWithStaticWebAssetsV1ClassLibraries()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset)
                .WithProjectChanges((project, document) =>
                {
                    if (Path.GetFileName(project) == "AnotherClassLib.csproj")
                    {
                        document.Descendants("TargetFramework").Single().ReplaceNodes("netstandard2.1");
                        document.Descendants("FrameworkReference").Single().Remove();
                        document.Descendants("PropertyGroup").First().Add(new XElement("RazorLangVersion", "3.0"));
                    }
                    if (Path.GetFileName(project) == "ClassLibrary.csproj")
                    {
                        document.Descendants("TargetFramework").Single().ReplaceNodes("netstandard2.0");
                        document.Descendants("FrameworkReference").Single().Remove();
                        document.Descendants("PropertyGroup").First().Add(new XElement("RazorLangVersion", "3.0"));
                    }
                });

            // We are deleting Views and Components because we are only interested in the static web assets behavior for this test
            // and this makes it easier to validate the test.
            Directory.Delete(Path.Combine(ProjectDirectory.TestRoot, "AnotherClassLib", "Views"), recursive: true);
            Directory.Delete(Path.Combine(ProjectDirectory.TestRoot, "ClassLibrary", "Views"), recursive: true);
            Directory.Delete(Path.Combine(ProjectDirectory.TestRoot, "ClassLibrary", "Components"), recursive: true);

            EnsureLocalPackagesExists();

            var restore = CreateRestoreCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            ExecuteCommand(restore).Should().Pass();

            var build = CreateBuildCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));
            AssertManifest(
                manifest,
                LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "AppWithPackageAndP2PReference.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();

            AssertBuildAssets(
                manifest,
                outputPath,
                intermediateOutputPath);
        }
    }
}
