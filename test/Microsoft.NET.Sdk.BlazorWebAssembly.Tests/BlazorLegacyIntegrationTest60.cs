﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class BlazorLegacyIntegrationTest60 : BlazorWasmBaselineTests
    {
        public BlazorLegacyIntegrationTest60(ITestOutputHelper log) : base(log, GenerateBaselines)
        {
        }

        [CoreMSBuildOnlyFact(Skip = "The Runtime pack resolves to 8.0 instead of 9.0")]
        public void Build60Hosted_Works()
        {
            // Arrange
            var testAsset = "BlazorWasmHosted60";
            var targetFramework = "net6.0";
            var testInstance = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(testInstance, "Server");
            build.WithWorkingDirectory(testInstance.TestRoot);
            build.Execute("/bl")
                .Should()
                .Pass();

            var clientBuildOutputDirectory = Path.Combine(testInstance.Path, "Client", "bin", "Debug", targetFramework);

            new FileInfo(Path.Combine(clientBuildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json")).Should().Exist();
            new FileInfo(Path.Combine(clientBuildOutputDirectory, "wwwroot", "_framework", "blazor.webassembly.js")).Should().Exist();
            new FileInfo(Path.Combine(clientBuildOutputDirectory, "wwwroot", "_framework", "dotnet.wasm")).Should().Exist();
            new FileInfo(Path.Combine(clientBuildOutputDirectory, "wwwroot", "_framework", "dotnet.timezones.blat")).Should().Exist();
            new FileInfo(Path.Combine(clientBuildOutputDirectory, "wwwroot", "_framework", "dotnet.wasm.gz")).Should().Exist();
            new FileInfo(Path.Combine(clientBuildOutputDirectory, "wwwroot", "_framework", $"{testAsset}.Client.dll")).Should().Exist();

            var serverBuildOutputDirectory = Path.Combine(testInstance.Path, "Server", "bin", "Debug", targetFramework);
            new FileInfo(Path.Combine(serverBuildOutputDirectory, $"{testAsset}.Server.dll")).Should().Exist();
            new FileInfo(Path.Combine(serverBuildOutputDirectory, $"{testAsset}.Client.dll")).Should().Exist();
            new FileInfo(Path.Combine(serverBuildOutputDirectory, $"{testAsset}.Shared.dll")).Should().Exist();
        }

        [CoreMSBuildOnlyFact(Skip = "The Runtime pack resolves to 8.0 instead of 9.0")]
        public void Publish60Hosted_Works()
        {
            // Arrange
            var testAsset = "BlazorWasmHosted60";
            var targetFramework = "net6.0";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var publish = new PublishCommand(ProjectDirectory, "Server");
            publish.WithWorkingDirectory(ProjectDirectory.TestRoot);
            publish.Execute("/bl")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("warning IL");

            var publishOutputDirectory = publish.GetOutputDirectory(targetFramework);

            publishOutputDirectory.Should().HaveFiles(new[]
            {
                $"{testAsset}.Client.dll",
                $"{testAsset}.Shared.dll",
                "wwwroot/index.html",
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.wasm",
                "wwwroot/_framework/System.Text.Json.dll",
                $"wwwroot/_framework/{testAsset}.Client.dll",
                $"wwwroot/_framework/{testAsset}.Shared.dll",
                "wwwroot/css/app.css",
                // Verify compression works
                "wwwroot/_framework/dotnet.wasm.br",
                $"wwwroot/_framework/{testAsset}.Client.dll.br",
                "wwwroot/_framework/System.Text.Json.dll.br"
            });

            var intermediateOutputPath = publish.GetIntermediateDirectory(targetFramework, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadPublishManifest());

            AssertPublishAssets(
                manifest,
                publishOutputDirectory.FullName,
                intermediateOutputPath);
        }
    }
}
