// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.NET.Sdk.Razor.Tests;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class BlazorLegacyIntegrationTest60(ITestOutputHelper log)
        : IsolatedNuGetPackageFolderAspNetSdkBaselineTest(log, nameof(BlazorLegacyIntegrationTest60))
    {

        protected override string EmbeddedResourcePrefix => 
            string.Join('.', "Microsoft.NET.Sdk.BlazorWebAssembly.Tests", "StaticWebAssetsBaselines");

        protected override string ComputeBaselineFolder() =>
            Path.Combine(TestContext.GetRepoRoot() ?? AppContext.BaseDirectory, "test", "Microsoft.NET.Sdk.BlazorWebAssembly.Tests", "StaticWebAssetsBaselines");
            
        [CoreMSBuildOnlyFact]
        public void Build60Hosted_Works()
        {
            // Arrange
            var testAsset = "BlazorWasmHosted60";
            var targetFramework = "net6.0";
            var testInstance = CreateAspNetSdkTestAsset(testAsset);

            var build = CreateBuildCommand(testInstance, "Server");
            ExecuteCommand(build)
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

        [WindowsOnlyRequiresMSBuildVersionFact("17.13", Reason = "Needs System.Text.Json 8.0.5")] // https://github.com/dotnet/sdk/issues/44886
        [SkipOnPlatform(TestPlatforms.Linux | TestPlatforms.OSX, "https://github.com/dotnet/sdk/issues/42145")]
        public void Publish60Hosted_Works()
        {
            // Arrange
            var testAsset = "BlazorWasmHosted60";
            var targetFramework = "net6.0";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var publish = CreatePublishCommand(ProjectDirectory, "Server");
            ExecuteCommand(publish)
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
