// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using static Microsoft.NET.Sdk.BlazorWebAssembly.Tests.ServiceWorkerAssert;


namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class WasmPwaManifestTests(ITestOutputHelper log) : AspNetSdkTest(log)
    {
        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void Build_ServiceWorkerAssetsManifest_Works()
        {
            // Arrange
            var expectedExtensions = new[] { ".pdb", ".js", ".wasm" };
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName)
                .WithProjectChanges((p, doc) =>
                {
                    if (Path.GetFileName(p) == "blazorwasm.csproj")
                    {
                        var itemGroup = new XElement("PropertyGroup");
                        var serviceWorkerAssetsManifest = new XElement("ServiceWorkerAssetsManifest", "service-worker-assets.js");
                        itemGroup.Add(new XElement("WasmFingerprintAssets", false));
                        itemGroup.Add(serviceWorkerAssetsManifest);
                        doc.Root.Add(itemGroup);
                    }
                });

            var buildCommand = CreateBuildCommand(testInstance, "blazorwasm");
            buildCommand.WithWorkingDirectory(testInstance.TestRoot);
            ExecuteCommand(buildCommand)
                .Should().Pass();

            var buildOutputDirectory = buildCommand.GetOutputDirectory(DefaultTfm).ToString();

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", WasmBootConfigFileName)).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.native.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazorwasm.wasm")).Should().Exist();

            var serviceWorkerAssetsManifest = Path.Combine(buildOutputDirectory, "wwwroot", "service-worker-assets.js");
            var manifestContents = ReadServiceWorkerAssetsManifest(serviceWorkerAssetsManifest);

            var entries = manifestContents.assets.Select(e => e.url).OrderBy(e => e).ToArray();
            entries.Should().Contain(e => expectedExtensions.Contains(Path.GetExtension(e)));

            VerifyServiceWorkerFiles(testInstance,
               Path.Combine(buildOutputDirectory, "wwwroot"),
               serviceWorkerPath: Path.Combine("serviceworkers", "my-service-worker.js"),
               serviceWorkerContent: "// This is the development service worker",
               assetsManifestPath: "service-worker-assets.js");
        }

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void Build_HostedAppWithServiceWorker_Works()
        {
            // Arrange
            var expectedExtensions = new[] { ".pdb", ".js", ".wasm" };
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var buildCommand = CreateBuildCommand(testInstance, "blazorhosted");
            ExecuteCommand(buildCommand)
                .Should().Pass();

            var buildOutputDirectory = OutputPathCalculator.FromProject(Path.Combine(testInstance.TestRoot, "blazorwasm")).GetOutputDirectory();

            var serviceWorkerAssetsManifest = Path.Combine(buildOutputDirectory, "wwwroot", "custom-service-worker-assets.js");
            var manifestContents = ReadServiceWorkerAssetsManifest(serviceWorkerAssetsManifest);

            var entries = manifestContents.assets.Select(e => e.url).OrderBy(e => e).ToArray();
            entries.Should().Contain(e => expectedExtensions.Contains(Path.GetExtension(e)));
        }

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void PublishWithPWA_ProducesAssets()
        {
            // Arrange
            var expectedExtensions = new[] { ".pdb", ".js", ".wasm" };
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var publishCommand = CreatePublishCommand(testInstance, "blazorwasm");
            ExecuteCommand(publishCommand).Should().Pass();

            var publishOutputDirectory = publishCommand.GetOutputDirectory(DefaultTfm).ToString();

            var serviceWorkerAssetsManifest = Path.Combine(publishOutputDirectory, "wwwroot", "custom-service-worker-assets.js");
            var manifestContents = ReadServiceWorkerAssetsManifest(serviceWorkerAssetsManifest);

            var entries = manifestContents.assets.Select(e => e.url).OrderBy(e => e).ToArray();
            entries.Should().Contain(e => expectedExtensions.Contains(Path.GetExtension(e)));

            var serviceWorkerFile = Path.Combine(publishOutputDirectory, "wwwroot", "serviceworkers", "my-service-worker.js");
            // Assert.FileContainsLine(result, serviceWorkerFile, "// This is the production service worker");
        }

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void PublishHostedWithPWA_ProducesAssets()
        {
            // Arrange
            var expectedExtensions = new[] { ".pdb", ".js", ".wasm" };
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var publishCommand = CreatePublishCommand(testInstance, "blazorhosted");
            ExecuteCommand(publishCommand).Should().Pass();

            var publishOutputDirectory = publishCommand.GetOutputDirectory(DefaultTfm).ToString();

            var serviceWorkerAssetsManifest = Path.Combine(publishOutputDirectory, "wwwroot", "custom-service-worker-assets.js");
            var manifestContents = ReadServiceWorkerAssetsManifest(serviceWorkerAssetsManifest);

            var entries = manifestContents.assets.Select(e => e.url).OrderBy(e => e).ToArray();
            entries.Should().Contain(e => expectedExtensions.Contains(Path.GetExtension(e)));

            var serviceWorkerFile = Path.Combine(publishOutputDirectory, "wwwroot", "serviceworkers", "my-service-worker.js");
            // Assert.FileContainsLine(result, serviceWorkerFile, "// This is the production service worker");
        }

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void Publish_UpdatesServiceWorkerVersionHash_WhenSourcesChange()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName)
                .WithProjectChanges((p, doc) =>
                {
                    if (Path.GetFileName(p) == "blazorwasm.csproj")
                    {
                        var itemGroup = new XElement("PropertyGroup");
                        var serviceWorkerAssetsManifest = new XElement("ServiceWorkerAssetsManifest", "service-worker-assets.js");
                        itemGroup.Add(serviceWorkerAssetsManifest);
                        doc.Root.Add(itemGroup);
                    }
                });

            var publishCommand = CreatePublishCommand(testInstance, "blazorwasm");
            ExecuteCommand(publishCommand).Should().Pass();

            var publishOutputDirectory = publishCommand.GetOutputDirectory(DefaultTfm).ToString();

            var serviceWorkerFile = Path.Combine(publishOutputDirectory, "wwwroot", "serviceworkers", "my-service-worker.js");
            var serviceWorkerAssetsManifest = Path.Combine(publishOutputDirectory, "wwwroot", "service-worker-assets.js");

            File.ReadAllText(serviceWorkerFile).Should().NotContain("/* Manifest version:");
            var capture = ReadServiceWorkerAssetsManifest(serviceWorkerAssetsManifest).version;
            capture.Should().NotBeNullOrEmpty();

            // Act
            var cssFile = Path.Combine(testInstance.TestRoot, "blazorwasm", "LinkToWebRoot", "css", "app.css");
            File.WriteAllText(cssFile, ".updated { }");

            // Assert
            publishCommand = CreatePublishCommand(testInstance, "blazorwasm");
            ExecuteCommand(publishCommand).Should().Pass();

            File.ReadAllText(serviceWorkerFile).Should().NotContain("/* Manifest version:");
            var updatedCapture = ReadServiceWorkerAssetsManifest(serviceWorkerAssetsManifest).version;
            updatedCapture.Should().NotBeNullOrEmpty();
            updatedCapture.Should().NotBe(capture);
        }

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void Publish_DeterministicAcrossBuilds_WhenNoSourcesChange()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName)
                .WithProjectChanges((p, doc) =>
                {
                    if (Path.GetFileName(p) == "blazorwasm.csproj")
                    {
                        var itemGroup = new XElement("PropertyGroup");
                        var serviceWorkerAssetsManifest = new XElement("ServiceWorkerAssetsManifest", "service-worker-assets.js");
                        itemGroup.Add(serviceWorkerAssetsManifest);
                        doc.Root.Add(itemGroup);
                    }
                });

            var publishCommand = CreatePublishCommand(testInstance, "blazorwasm");
            ExecuteCommand(publishCommand).Should().Pass();

            var publishOutputDirectory = publishCommand.GetOutputDirectory(DefaultTfm).ToString();

            var serviceWorkerFile = Path.Combine(publishOutputDirectory, "wwwroot", "serviceworkers", "my-service-worker.js");
            var serviceWorkerAssetsManifest = Path.Combine(publishOutputDirectory, "wwwroot", "service-worker-assets.js");

            File.ReadAllText(serviceWorkerFile).Should().NotContain("/* Manifest version:");
            var capture = ReadServiceWorkerAssetsManifest(serviceWorkerAssetsManifest).version;
            capture.Should().NotBeNullOrEmpty();

            // Act && Assert
            publishCommand = CreatePublishCommand(testInstance, "blazorwasm");
            ExecuteCommand(publishCommand).Should().Pass();

            File.ReadAllText(serviceWorkerFile).Should().NotContain("/* Manifest version:");
            var updatedCapture = ReadServiceWorkerAssetsManifest(serviceWorkerAssetsManifest).version;
            updatedCapture.Should().NotBeNullOrEmpty();
            updatedCapture.Should().Be(capture);
        }
    }
}
