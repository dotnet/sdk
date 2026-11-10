// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.NET.Sdk.BlazorWebAssembly.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Microsoft.NET.Sdk.BlazorWebAssembly.Tests.ServiceWorkerAssert;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.AoT.Tests
{
    [TestClass]
    public class WasmAoTPublishIntegrationTest : WasmPublishIntegrationTestBase
    {
        [TestMethod]
        public void ReadyToRun_Publish_HostedAppWithScopedCss_VisualStudio()
        {
            // Simulates publishing the same way VS does by setting BuildProjectReferences=false.
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAssetWithReadyToRun(testAppName, new[] { "blazorwasm" })
                .WithProjectChanges((p, doc) =>
                {
                    if (Path.GetFileName(p) == "blazorwasm.csproj")
                    {
                        var itemGroup = new XElement("PropertyGroup");
                        itemGroup.Add(new XElement("WasmFingerprintAssets", false));
                        doc.Root.Add(itemGroup);
                    }
                });

            File.WriteAllText(Path.Combine(testInstance.TestRoot, "blazorwasm", "App.razor.css"), "h1 { font-size: 16px; }");

            // ReadyToRun is a publish-time operation; publish the client first so this test verifies the CoreCLR compiler output.
            var blazorPublishCommand = CreatePublishCommand(testInstance, "blazorwasm");
            ExecuteCommand(blazorPublishCommand, "/p:UsingBrowserRuntimeWorkload=false", "/p:Configuration=Release").Should().Pass();

            var clientPublishDirectory = blazorPublishCommand.GetOutputDirectory(DefaultTfm, "Release");
            AssertReadyToRunAndNativeRelink(testInstance, clientPublishDirectory);

            // VS builds projects individually and then a publish with BuildDependencies=false, but building the main project is a close enough approximation for this test.
            var buildCommand = CreateBuildCommand(testInstance, "blazorwasm");
            ExecuteCommand(buildCommand, "/p:UsingBrowserRuntimeWorkload=false", "/p:BuildInsideVisualStudio=true", "/p:Configuration=Release").Should().Pass();

            // Publish
            var publishCommand = CreatePublishCommand(testInstance, "blazorhosted");
            ExecuteCommand(publishCommand, "/p:BuildProjectReferences=false", "/p:BuildInsideVisualStudio=true", "/p:Configuration=Release").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm, "Release");
            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot");

            // Make sure the main project exists
            new FileInfo(Path.Combine(publishDirectory.ToString(), "blazorhosted.dll")).Should().Exist();

            // Verification for https://github.com/dotnet/aspnetcore/issues/19926. Verify binaries for projects
            // referenced by the Hosted project appear in the publish directory
            publishDirectory.Should().HaveFiles(new[]
            {
                "RazorClassLibrary.dll",
                "blazorwasm.dll"
            });

            publishDirectory.Should().HaveFiles(new[]
            {
                $"wwwroot/_framework/{WasmBootConfigFileName}",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.native.wasm",
                "wwwroot/_framework/blazorwasm.wasm",
                "wwwroot/_framework/System.Text.Json.wasm"
            });

            // Verify project references appear as static web assets
            // Also verify project references to the server project appear in the publish output
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/RazorClassLibrary.wasm",
                "RazorClassLibrary.dll"
            });

            // Verify static assets are in the publish directory
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/index.html"
            });

            // Verify scoped css
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/blazorwasm.styles.css"
            });

            // Verify static web assets from referenced projects are copied.
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_content/RazorClassLibrary/wwwroot/exampleJsInterop.js",
                "wwwroot/_content/RazorClassLibrary/styles.css",
            });

            // Verify web.config
            publishDirectory.Should().HaveFiles(new[]
            {
                "web.config"
            });

            VerifyBootManifestHashes(testInstance, Path.Combine(publishDirectory.ToString(), "wwwroot"));

            // Verify compression works
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/dotnet.native.wasm.br",
                "wwwroot/_framework/blazorwasm.wasm.br",
                "wwwroot/_framework/RazorClassLibrary.wasm.br",
                "wwwroot/_framework/System.Text.Json.wasm.br"
            });

            VerifyBootManifestHashes(testInstance, blazorPublishDirectory);
            VerifyServiceWorkerFiles(testInstance, blazorPublishDirectory,
                serviceWorkerPath: Path.Combine("serviceworkers", "my-service-worker.js"),
                serviceWorkerContent: "// This is the production service worker",
                assetsManifestPath: "custom-service-worker-assets.js");
        }

        private TestAsset CreateAspNetSdkTestAssetWithReadyToRun(
            string testAsset,
            string[] projectsToReadyToRun,
            [CallerMemberName] string callerName = "")
        {
            return CreateAspNetSdkTestAsset(testAsset, callerName: callerName, identifier: "ReadyToRun")
                    .WithProjectChanges((project, document) =>
                    {
                        if (projectsToReadyToRun.Contains(Path.GetFileNameWithoutExtension(project)))
                        {
                            var propertyGroup = new XElement("PropertyGroup");
                            propertyGroup.Add(new XElement("PublishReadyToRun", "true"));
                            propertyGroup.Add(new XElement("WasmBuildNative", "true"));
                            document.Root.Add(propertyGroup);
                        }
                    });
        }

        private void AssertReadyToRunAndNativeRelink(TestAsset testInstance, DirectoryInfo publishDirectory)
        {
            var projectDirectory = Path.Combine(testInstance.TestRoot, "blazorwasm");
            var readyToRunImage = new FileInfo(Path.Combine(projectDirectory, "obj", "Release", DefaultTfm, "R2R", "System.Private.CoreLib.wasm"));
            readyToRunImage.Should().Exist();
            readyToRunImage.Length.Should().BeGreaterThan(0);

            var relinkedNative = Path.Combine(projectDirectory, "obj", "Release", DefaultTfm, "wasm", "for-publish", "dotnet.native.wasm");
            var publishedNative = Path.Combine(publishDirectory.FullName, "wwwroot", "_framework", "dotnet.native.wasm");
            var runtimePackNative = GetRuntimePackNativeFile(projectDirectory);

            File.Exists(relinkedNative).Should().BeTrue($"Expected the relinked native output at '{relinkedNative}'.");
            File.Exists(publishedNative).Should().BeTrue($"Expected the published native output at '{publishedNative}'.");
            File.ReadAllBytes(publishedNative).SequenceEqual(File.ReadAllBytes(relinkedNative)).Should().BeTrue("the publish output should use the relinked native bundle");
            File.ReadAllBytes(publishedNative).SequenceEqual(File.ReadAllBytes(runtimePackNative)).Should().BeFalse("the publish output should not copy the runtime-pack native bundle");
        }

        private static string GetRuntimePackNativeFile(string projectDirectory)
        {
            var assetsFile = Path.Combine(projectDirectory, "obj", "project.assets.json");
            using var assets = JsonDocument.Parse(File.ReadAllText(assetsFile));

            var runtimePackPath = assets.RootElement.GetProperty("libraries")
                .EnumerateObject()
                .Where(library => library.Name.StartsWith("Microsoft.NETCore.App.Runtime.CoreCLR.browser-wasm/", StringComparison.OrdinalIgnoreCase))
                .Select(library => library.Value.GetProperty("path").GetString())
                .FirstOrDefault();

            if (runtimePackPath is null)
            {
                throw new InvalidOperationException($"Could not find the CoreCLR browser runtime pack in '{assetsFile}'.");
            }

            var runtimePackNativeFile = assets.RootElement.GetProperty("packageFolders")
                .EnumerateObject()
                .Select(folder => Path.Combine(folder.Name, runtimePackPath, "runtimes", "browser-wasm", "native", "dotnet.native.wasm"))
                .FirstOrDefault(File.Exists);

            return runtimePackNativeFile
                ?? throw new FileNotFoundException($"Could not find dotnet.native.wasm for the runtime pack '{runtimePackPath}'.");
        }
    }
}
