// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class BlazorWasmStaticWebAssetsIntegrationTest(ITestOutputHelper log) : BlazorWasmBaselineTests(log, GenerateBaselines)
    {
        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void StaticWebAssets_BuildMinimal_Works()
        {
            // Arrange
            // Minimal has no project references, service worker etc. This is pretty close to the project template.
            var testAsset = "BlazorWasmMinimal";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset)
                .WithProjectChanges((p, doc) =>
                {
                    var itemGroup = new XElement("PropertyGroup");
                    var fingerprintAssets = new XElement("WasmFingerprintAssets", false);
                    itemGroup.Add(fingerprintAssets);
                    doc.Root.Add(itemGroup);
                });

            File.WriteAllText(Path.Combine(ProjectDirectory.TestRoot, "App.razor.css"), "h1 { font-size: 16px; }");
            File.WriteAllText(Path.Combine(ProjectDirectory.TestRoot, "wwwroot", "appsettings.development.json"), "{}");

            var build = CreateBuildCommand(ProjectDirectory);

            var buildResult = ExecuteCommand(build);
            buildResult.Should().Pass();

            var outputPath = build.GetOutputDirectory(DefaultTfm).ToString();
            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "blazorwasm-minimal.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();

            AssertBuildAssets(
                manifest,
                outputPath,
                intermediateOutputPath);
        }

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void StaticWebAssets_PublishMinimal_Works()
        {
            // Arrange
            // Minimal has no project references, service worker etc. This is pretty close to the project template.
            var testAsset = "BlazorWasmMinimal";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset)
                .WithProjectChanges((p, doc) =>
                {
                    var itemGroup = new XElement("PropertyGroup");
                    var fingerprintAssets = new XElement("WasmFingerprintAssets", false);
                    itemGroup.Add(fingerprintAssets);
                    doc.Root.Add(itemGroup);
                });

            File.WriteAllText(Path.Combine(ProjectDirectory.TestRoot, "App.razor.css"), "h1 { font-size: 16px; }");
            File.WriteAllText(Path.Combine(ProjectDirectory.TestRoot, "wwwroot", "appsettings.development.json"), "{}");

            var publish = CreatePublishCommand(ProjectDirectory);
            var publishResult = ExecuteCommand(publish);
            publishResult.Should().Pass();

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

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void StaticWebAssets_Build_Hosted_Works()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAppName)
                .WithProjectChanges((p, doc) =>
                {
                    if (Path.GetFileName(p) == "blazorwasm.csproj")
                    {
                        var itemGroup = new XElement("PropertyGroup");
                        var fingerprintAssets = new XElement("WasmFingerprintAssets", false);
                        itemGroup.Add(fingerprintAssets);
                        doc.Root.Add(itemGroup);
                    }
                });

            var build = CreateBuildCommand(ProjectDirectory, "blazorhosted");
            var buildResult = ExecuteCommand(build);
            buildResult.Should().Pass();

            var outputPath = build.GetOutputDirectory(DefaultTfm).ToString();
            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "blazorhosted.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();

            AssertBuildAssets(
                manifest,
                outputPath,
                intermediateOutputPath);
        }

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void StaticWebAssets_Publish_Hosted_Works()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAppName)
                .WithProjectChanges((p, doc) =>
                {
                    if (Path.GetFileName(p) == "blazorwasm.csproj")
                    {
                        var itemGroup = new XElement("PropertyGroup");
                        var fingerprintAssets = new XElement("WasmFingerprintAssets", false);
                        itemGroup.Add(fingerprintAssets);
                        doc.Root.Add(itemGroup);
                    }
                });

            // Check that static web assets is correctly configured by setting up a css file to triger css isolation.
            // The list of publish files should not include bundle.scp.css and should include blazorwasm.styles.css
            File.WriteAllText(Path.Combine(ProjectDirectory.TestRoot, "blazorwasm", "App.razor.css"), "h1 { font-size: 16px; }");

            var publish = CreatePublishCommand(ProjectDirectory, "blazorhosted");
            var publishResult = ExecuteCommand(publish);
            publishResult.Should().Pass();

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

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void StaticWebAssets_Publish_DoesNotIncludeXmlDocumentationFiles_AsAssets()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAppName)
                .WithProjectChanges((p, doc) =>
                {
                    if (Path.GetFileName(p) == "blazorwasm.csproj")
                    {
                        var itemGroup = new XElement("PropertyGroup");
                        var fingerprintAssets = new XElement("WasmFingerprintAssets", false);
                        itemGroup.Add(fingerprintAssets);
                        doc.Root.Add(itemGroup);
                    }
                });

            // Check that static web assets is correctly configured by setting up a css file to triger css isolation.
            // The list of publish files should not include bundle.scp.css and should include blazorwasm.styles.css
            File.WriteAllText(Path.Combine(ProjectDirectory.TestRoot, "blazorwasm", "App.razor.css"), "h1 { font-size: 16px; }");

            var publish = CreatePublishCommand(ProjectDirectory, "blazorhosted");
            var publishResult = ExecuteCommand(publish, "/p:GenerateDocumentationFile=true");
            publishResult.Should().Pass();

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

        [Fact]
        public void StaticWebAssets_HostedApp_ReferencingNetStandardLibrary_Works()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAppName);

            ProjectDirectory.WithProjectChanges((project, document) =>
            {
                if (Path.GetFileNameWithoutExtension(project) == "blazorwasm")
                {
                    document.Descendants("TargetFramework").Single().ReplaceNodes("net5");
                }
                if (Path.GetFileNameWithoutExtension(project) == "RazorClassLibrary")
                {
                    document.Descendants("TargetFramework").Single().ReplaceNodes("netstandard2.1");
                    document.Descendants("PropertyGroup").First().Add(new XElement("RazorLangVersion", "3.0"));
                }
                if (Path.GetFileNameWithoutExtension(project) == "classlibrarywithsatelliteassemblies")
                {
                    document.Descendants("TargetFramework").Single().ReplaceNodes("netstandard2.1");
                    document.Descendants("PropertyGroup").First().Add(new XElement("RazorLangVersion", "3.0"));
                }
            });

            var build = CreateBuildCommand(ProjectDirectory, "blazorhosted");
            var buildResult = ExecuteCommand(build);
            buildResult.Should().Pass();

            var outputPath = build.GetOutputDirectory(DefaultTfm).ToString();
            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));

            AssertManifest(manifest, LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "blazorhosted.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();

            AssertBuildAssets(
                manifest,
                outputPath,
                intermediateOutputPath);
        }

        [Fact]
        public void StaticWebAssets_BackCompatibilityPublish_Hosted_Works()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAppName);

            ProjectDirectory.WithProjectChanges((project, document) =>
            {
                if (Path.GetFileNameWithoutExtension(project) == "blazorwasm")
                {
                    document.Descendants("TargetFramework").Single().ReplaceNodes("net5");
                }
                if (Path.GetFileNameWithoutExtension(project) == "RazorClassLibrary")
                {
                    document.Descendants("TargetFramework").Single().ReplaceNodes("netstandard2.1");
                    document.Descendants("PropertyGroup").First().Add(new XElement("RazorLangVersion", "3.0"));
                }
                if (Path.GetFileNameWithoutExtension(project) == "classlibrarywithsatelliteassemblies")
                {
                    document.Descendants("TargetFramework").Single().ReplaceNodes("netstandard2.1");
                    document.Descendants("PropertyGroup").First().Add(new XElement("RazorLangVersion", "3.0"));
                }
            });

            // Check that static web assets is correctly configured by setting up a css file to triger css isolation.
            // The list of publish files should not include bundle.scp.css and should include blazorwasm.styles.css
            File.WriteAllText(Path.Combine(ProjectDirectory.TestRoot, "blazorwasm", "App.razor.css"), "h1 { font-size: 16px; }");

            var publish = CreatePublishCommand(ProjectDirectory, "blazorhosted");
            var publishResult = ExecuteCommand(publish);
            publishResult.Should().Pass();

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
