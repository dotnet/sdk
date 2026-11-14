// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;
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
            ExecuteCommand(blazorPublishCommand, "/p:Configuration=Release").Should().Pass();

            var clientPublishDirectory = blazorPublishCommand.GetOutputDirectory(DefaultTfm, "Release");
            AssertReadyToRunAndNativeRelink(testInstance, clientPublishDirectory);

            // VS builds projects individually and then a publish with BuildDependencies=false, but building the main project is a close enough approximation for this test.
            var buildCommand = CreateBuildCommand(testInstance, "blazorwasm");
            ExecuteCommand(buildCommand, "/p:BuildInsideVisualStudio=true", "/p:Configuration=Release").Should().Pass();

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

                            // Remove once the fix for https://github.com/dotnet/runtime/issues/136499 flows into the runtime packs.
                            document.Root.Add(new XElement("Target",
                                new XAttribute("Name", "PruneRuntimePackReadyToRunImagesForTest"),
                                new XAttribute("BeforeTargets", "_HandleFileConflictsForPublish"),
                                new XElement("ItemGroup",
                                    new XElement("_RuntimePackReadyToRunImages",
                                        new XAttribute("Include", "@(ResolvedRuntimePack->'%(PackageDirectory)/runtimes/browser-wasm/native/r2r/*.wasm')")),
                                    new XElement("ResolvedFileToPublish",
                                        new XAttribute("Remove", "@(_RuntimePackReadyToRunImages)")))));
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

        private string GetRuntimePackNativeFile(string projectDirectory)
        {
            // Workload and PackageDownload packs are not entries in project.assets.json's libraries.
            var command = new GetValuesCommand(Log, projectDirectory, DefaultTfm, "ResolvedRuntimePack", GetValuesCommand.ValueType.Item)
            {
                Configuration = "Release",
                DependsOnTargets = "ResolveFrameworkReferences",
                MetadataNames = new List<string> { "RuntimeIdentifier", "PackageDirectory" }
            };
            ExecuteCommand(command, "/p:Configuration=Release").Should().Pass();

            var runtimePacks = command.GetValuesWithMetadata()
                .Where(pack => pack.metadata["RuntimeIdentifier"] == "browser-wasm")
                .ToList();
            runtimePacks.Should().ContainSingle("the build should resolve one browser runtime pack");
            var runtimePackNativeFile = Path.Combine(runtimePacks.Single().metadata["PackageDirectory"],
                "runtimes", "browser-wasm", "native", "dotnet.native.wasm");
            new FileInfo(runtimePackNativeFile).Should().Exist();
            return runtimePackNativeFile;
        }
    }
}
