// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using Microsoft.NET.Sdk.BlazorWebAssembly;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.NET.Sdk.BlazorWebAssembly.Tests.ServiceWorkerAssert;
using ResourceHashesByNameDictionary = System.Collections.Generic.Dictionary<string, string>;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class WasmPublishIntegrationTest : SdkTest
    {
        public WasmPublishIntegrationTest(ITestOutputHelper log) : base(log) { }

        [Fact]
        public void Publish_WithDefaultSettings_Works()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorwasm"));
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory("net5.0");

            var expectedFiles = new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.wasm",
                "wwwroot/_framework/blazorwasm.dll",
                "wwwroot/_framework/System.Text.Json.dll",
                "wwwroot/_content/RazorClassLibrary/wwwroot/exampleJsInterop.js",
                "wwwroot/_content/RazorClassLibrary/styles.css",
                "wwwroot/index.html",
                "wwwroot/js/LinkedScript.js",
                "wwwroot/css/app.css",
                "web.config"
            };

            // Verify web.config
            var content = File.ReadAllText(Path.Combine(publishDirectory.ToString(), "web.config"));
            content.Should().Contain("<remove fileExtension=\".blat\" />");

            publishDirectory.Should().HaveFiles(expectedFiles);

            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot");
            var cssFile = new FileInfo(Path.Combine(blazorPublishDirectory, "css", "app.css"));
            cssFile.Should().Exist();
            cssFile.Should().Contain(".publish");

            new FileInfo(Path.Combine(publishDirectory.ToString(), "dist", "Fake-License.txt"));

            VerifyBootManifestHashes(testInstance, blazorPublishDirectory);
            VerifyServiceWorkerFiles(testInstance, blazorPublishDirectory,
                serviceWorkerPath: Path.Combine("serviceworkers", "my-service-worker.js"),
                serviceWorkerContent: "// This is the production service worker",
                assetsManifestPath: "custom-service-worker-assets.js");

            VerifyTypeGranularTrimming(blazorPublishDirectory);
        }

        [Fact]
        public void Publish_WithExistingWebConfig_Works()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var webConfigContents = "test webconfig contents";
            File.WriteAllText(Path.Combine(testInstance.TestRoot, "blazorwasm", "web.config"), webConfigContents);

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorwasm"));
            publishCommand.Execute("/p:Configuration=Release").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory("net5.0", "Release");

            // Verify web.config
            new FileInfo(Path.Combine(publishDirectory.ToString(), "..", "web.config")).Should().Exist();
            new FileInfo(Path.Combine(publishDirectory.ToString(), "..", "web.config")).Should().Contain(webConfigContents);
        }

        [Fact]
        public void Publish_WithNoBuild_Works()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var buildCommand = new BuildCommand(testInstance, "blazorwasm");
            buildCommand.Execute()
                .Should().Pass();

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorwasm"));
            publishCommand.Execute("/p:NoBuild=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory("net5.0");
            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot");

            var expectedFiles = new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.wasm",
                "wwwroot/_framework/blazorwasm.dll",
                "wwwroot/_framework/System.Text.Json.dll",
                "wwwroot/_content/RazorClassLibrary/wwwroot/exampleJsInterop.js",
                "wwwroot/_content/RazorClassLibrary/styles.css",
                "wwwroot/index.html",
                "wwwroot/js/LinkedScript.js",
                "wwwroot/css/app.css",
                "web.config"
            };

            publishDirectory.Should().HaveFiles(expectedFiles);

            VerifyBootManifestHashes(testInstance, blazorPublishDirectory);
            VerifyServiceWorkerFiles(testInstance, blazorPublishDirectory,
                serviceWorkerPath: Path.Combine("serviceworkers", "my-service-worker.js"),
                serviceWorkerContent: "// This is the production service worker",
                assetsManifestPath: "custom-service-worker-assets.js");

            VerifyCompression(testInstance, blazorPublishDirectory);
        }

        [Theory]
        [InlineData("different-path/")]
        [InlineData("/different-path/")]
        public void Publish_WithStaticWebBasePathWorks(string basePath)
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            testInstance.WithProjectChanges((path, project) =>
            {
                if (path.Contains("blazorwasm"))
                {
                    var ns = project.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "PropertyGroup");
                    itemGroup.Add(new XElement("StaticWebAssetBasePath", basePath));
                    project.Root.Add(itemGroup);
                }
            });

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorwasm"));
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory("net5.0");

            var expectedFiles = new[]
            {
                "wwwroot/different-path/_framework/blazor.boot.json",
                "wwwroot/different-path/_framework/blazor.webassembly.js",
                "wwwroot/different-path/_framework/dotnet.wasm",
                "wwwroot/different-path/_framework/blazorwasm.dll",
                "wwwroot/different-path/_framework/System.Text.Json.dll",
                "wwwroot/different-path/_content/RazorClassLibrary/wwwroot/exampleJsInterop.js",
                "wwwroot/different-path/_content/RazorClassLibrary/styles.css",
                "wwwroot/different-path/index.html",
                "wwwroot/different-path/js/LinkedScript.js",
                "wwwroot/different-path/css/app.css",
                "web.config"
            };

            publishDirectory.Should().HaveFiles(expectedFiles);

            // Verify nothing is published directly to the wwwroot directory
            new DirectoryInfo(Path.Combine(publishDirectory.ToString(), "wwwroot")).Should().HaveDirectory("different-path");

            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot", "different-path");

            // Verify web.config
            var content = File.ReadAllText(Path.Combine(publishDirectory.ToString(), "web.config"));
            content.Should().Contain("<remove fileExtension=\".blat\" />");


            VerifyBootManifestHashes(testInstance, blazorPublishDirectory);
            VerifyServiceWorkerFiles(testInstance,
                Path.Combine(publishDirectory.ToString(), "wwwroot"),
                serviceWorkerPath: Path.Combine("serviceworkers", "my-service-worker.js"),
                serviceWorkerContent: "// This is the production service worker",
                assetsManifestPath: "custom-service-worker-assets.js",
                staticWebAssetsBasePath: "different-path");
        }

        [Theory]
        [InlineData("different-path")]
        [InlineData("/different-path")]
        public void Publish_Hosted_WithStaticWebBasePathWorks(string basePath)
        {
            var testAppName = "BlazorHosted";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            testInstance.WithProjectChanges((path, project) =>
            {
                if (path.Contains("blazorwasm"))
                {
                    var ns = project.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "PropertyGroup");
                    itemGroup.Add(new XElement("StaticWebAssetBasePath", basePath));
                    project.Root.Add(itemGroup);
                }
            });

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorhosted"));
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory("net5.0");

            var expectedFiles = new[]
            {
                "wwwroot/different-path/_framework/blazor.boot.json",
                "wwwroot/different-path/_framework/blazor.webassembly.js",
                "wwwroot/different-path/_framework/dotnet.wasm",
                "wwwroot/different-path/_framework/dotnet.wasm.br",
                "wwwroot/different-path/_framework/dotnet.wasm.gz",
                "wwwroot/different-path/_framework/blazorwasm.dll",
                "wwwroot/different-path/_framework/blazorwasm.dll.gz",
                "wwwroot/different-path/_framework/System.Text.Json.dll",
                "wwwroot/different-path/_framework/System.Text.Json.dll.gz",
                "wwwroot/different-path/_framework/System.Text.Json.dll.br",
                "wwwroot/different-path/_framework/RazorClassLibrary.dll.gz",
                "wwwroot/different-path/_framework/RazorClassLibrary.dll.br",
                "wwwroot/_content/RazorClassLibrary/wwwroot/exampleJsInterop.js",
                "wwwroot/_content/RazorClassLibrary/styles.css",
                "wwwroot/different-path/index.html",
                "web.config"
            };

            publishDirectory.Should().HaveFiles(expectedFiles);

            // Verify nothing is published directly to the wwwroot directory
            new DirectoryInfo(Path.Combine(publishDirectory.ToString(), "wwwroot")).Should().HaveDirectory("different-path");

            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot", "different-path");

            VerifyBootManifestHashes(testInstance, blazorPublishDirectory);
        }

        private static void VerifyCompression(TestAsset testAsset, string blazorPublishDirectory)
        {
            var original = Path.Combine(blazorPublishDirectory, "_framework", "blazor.boot.json");
            var compressed = Path.Combine(blazorPublishDirectory, "_framework", "blazor.boot.json.br");
            using var brotliStream = new BrotliStream(File.OpenRead(compressed), CompressionMode.Decompress);
            using var textReader = new StreamReader(brotliStream);
            var uncompressedText = textReader.ReadToEnd();
            var originalText = File.ReadAllText(original);

            uncompressedText.Should().Be(originalText);
        }

        [Fact]
        public void Publish_SatelliteAssemblies_AreCopiedToBuildOutput()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName).WithSource();

            testInstance.WithProjectChanges((path, project) =>
            {
                if (path.Contains("blazorwasm"))
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = new XElement(ns + "PropertyGroup");
                    propertyGroup.Add(new XElement("DefineConstants", @"$(DefineConstants);REFERENCE_classlibrarywithsatelliteassemblies"));
                    var itemGroup = new XElement(ns + "ItemGroup");
                    itemGroup.Add(new XElement("ProjectReference", new XAttribute("Include", @"..\classlibrarywithsatelliteassemblies\classlibrarywithsatelliteassemblies.csproj")));
                    project.Root.Add(propertyGroup);
                    project.Root.Add(itemGroup);
                }
            });

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorwasm"));
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory("net5.0");
            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot");

            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/Microsoft.CodeAnalysis.CSharp.dll",
                "wwwroot/_framework/fr/Microsoft.CodeAnalysis.CSharp.resources.dll"
            });

            var bootJsonData = new FileInfo(Path.Combine(blazorPublishDirectory, "_framework", "blazor.boot.json"));
            bootJsonData.Should().Contain("\"Microsoft.CodeAnalysis.CSharp.dll\"");
            bootJsonData.Should().Contain("\"fr\\/Microsoft.CodeAnalysis.CSharp.resources.dll\"");

            VerifyBootManifestHashes(testInstance, blazorPublishDirectory);
        }

        [Fact]
        public void Publish_HostedApp_WithSatelliteAssemblies()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            testInstance.WithProjectChanges((path, project) =>
            {
                if (path.Contains("blazorwasm"))
                {
                    var ns = project.Root.Name.Namespace;
                    // Workaround for https://github.com/mono/linker/issues/1390
                    var propertyGroup = new XElement(ns + "PropertyGroup");

                    propertyGroup.Add(new XElement("PublishTrimmed", false));
                    propertyGroup.Add(new XElement("DefineConstants", @"$(DefineConstants);REFERENCE_classlibrarywithsatelliteassemblies"));
                    var itemGroup = new XElement(ns + "ItemGroup");
                    itemGroup.Add(new XElement("ProjectReference", new XAttribute("Include", @"..\classlibrarywithsatelliteassemblies\classlibrarywithsatelliteassemblies.csproj")));
                    project.Root.Add(propertyGroup);
                    project.Root.Add(itemGroup);
                }

            });

            var resxfileInProject = Path.Combine(testInstance.TestRoot, "blazorwasm", "Resources.ja.resx.txt");
            File.Move(resxfileInProject, Path.Combine(testInstance.TestRoot, "blazorwasm", "Resource.ja.resx"));

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorhosted"));
            publishCommand.Execute().Should().Pass();

            var publishOutputDirectory = publishCommand.GetOutputDirectory("net5.0");

            // Verification for https://github.com/dotnet/aspnetcore/issues/19926. Verify binaries for projects
            // referenced by the Hosted project appear in the publish directory
            publishOutputDirectory.Should().HaveFiles(new[]
            {
                "RazorClassLibrary.dll",
                "blazorwasm.dll"
            });

            var blazorPublishDirectory = Path.Combine(publishOutputDirectory.ToString(), "wwwroot");
            publishOutputDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.wasm",
                "wwwroot/_framework/blazorwasm.dll",
                "wwwroot/_framework/System.Text.Json.dll"
            });

            // Verify project references appear as static web assets
            publishOutputDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/RazorClassLibrary.dll",
                "RazorClassLibrary.dll"
            });

            // Verify static assets are in the publish directory
            publishOutputDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/index.html"
            });

            // Verify static web assets from referenced projects are copied.
            publishOutputDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_content/RazorClassLibrary/wwwroot/exampleJsInterop.js",
                "wwwroot/_content/RazorClassLibrary/styles.css",
            });

            // Verify web.config
            publishOutputDirectory.Should().HaveFiles(new[]
            {
                "web.config"
            });

            VerifyBootManifestHashes(testInstance, blazorPublishDirectory);

            // Verify compression works
            publishOutputDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/dotnet.wasm.br",
                "wwwroot/_framework/blazorwasm.dll.br",
                "wwwroot/_framework/RazorClassLibrary.dll.br",
                "wwwroot/_framework/System.Text.Json.dll.br"
            });

            publishOutputDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/dotnet.wasm.gz",
                "wwwroot/_framework/blazorwasm.dll.gz",
                "wwwroot/_framework/RazorClassLibrary.dll.gz",
                "wwwroot/_framework/System.Text.Json.dll.gz"
            });

            // Verify that Blazor bootJSON contains the right contents
            var bootJsonPath = Path.Combine(blazorPublishDirectory, "_framework", "blazor.boot.json");
            var bootJsonData = ReadBootJsonData(bootJsonPath);

            var runtime = bootJsonData.resources.runtime;
            runtime.Should().ContainKey("dotnet.wasm");

            var assemblies = bootJsonData.resources.assembly;
            assemblies.Should().ContainKey("blazorwasm.dll");
            assemblies.Should().ContainKey("RazorClassLibrary.dll");
            assemblies.Should().ContainKey("System.Text.Json.dll");

            VerifyServiceWorkerFiles(testInstance, blazorPublishDirectory,
                serviceWorkerPath: Path.Combine("serviceworkers", "my-service-worker.js"),
                serviceWorkerContent: "// This is the production service worker",
                assetsManifestPath: "custom-service-worker-assets.js");

            VerifyTypeGranularTrimming(blazorPublishDirectory);

            publishOutputDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/blazorwasm.dll",
                "wwwroot/_framework/classlibrarywithsatelliteassemblies.dll",
                "wwwroot/_framework/Microsoft.CodeAnalysis.CSharp.dll",
                "wwwroot/_framework/fr/Microsoft.CodeAnalysis.CSharp.resources.dll",
            });

            var bootJsonFile = new FileInfo(Path.Combine(blazorPublishDirectory, "_framework", "blazor.boot.json"));
            bootJsonFile.Should().Contain("\"Microsoft.CodeAnalysis.CSharp.dll\"");
            bootJsonFile.Should().Contain("\"fr\\/Microsoft.CodeAnalysis.CSharp.resources.dll\"");
        }

        [Fact]
        // Regression test for https://github.com/dotnet/aspnetcore/issues/18752
        public void Publish_HostedApp_WithoutTrimming_Works()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            testInstance.WithProjectChanges((path, project) =>
            {
                if (path.Contains("blazorwasm"))
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = new XElement(ns + "PropertyGroup");

                    propertyGroup.Add(new XElement("PublishTrimmed", false));
                    project.Root.Add(propertyGroup);
                }
            });

            // VS builds projects individually and then a publish with BuildDependencies=false, but building the main project is a close enough approximation for this test.
            var buildCommand = new BuildCommand(testInstance, "blazorwasm");
            buildCommand.Execute().Should().Pass();

            // Publish
            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorhosted"));
            publishCommand.Execute("/p:BuildDependencies=false").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory("net5.0");
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
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.wasm",
                "wwwroot/_framework/blazorwasm.dll",
                "wwwroot/_framework/System.Text.Json.dll"
            });

            // Verify project references appear as static web assets
            // Also verify project references to the server project appear in the publish output
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/RazorClassLibrary.dll",
                "RazorClassLibrary.dll"
            });

            // Verify static assets are in the publish directory
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/index.html"
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
                "wwwroot/_framework/dotnet.wasm.br",
                "wwwroot/_framework/blazorwasm.dll.br",
                "wwwroot/_framework/RazorClassLibrary.dll.br",
                "wwwroot/_framework/System.Text.Json.dll.br"
            });

            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/dotnet.wasm.gz",
                "wwwroot/_framework/blazorwasm.dll.gz",
                "wwwroot/_framework/RazorClassLibrary.dll.gz",
                "wwwroot/_framework/System.Text.Json.dll.gz"
            });

            VerifyServiceWorkerFiles(testInstance, Path.Combine(publishDirectory.ToString(), "wwwroot"),
                serviceWorkerPath: Path.Combine("serviceworkers", "my-service-worker.js"),
                serviceWorkerContent: "// This is the production service worker",
                assetsManifestPath: "custom-service-worker-assets.js");
        }

        [Fact]
        public void Publish_HostedApp_WithNoBuild_Works()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var buildCommand = new BuildCommand(testInstance, "blazorhosted");
            buildCommand.Execute().Should().Pass();

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorhosted"));
            publishCommand.Execute("/p:NoBuild=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory("net5.0");
            // Make sure the main project exists
            new FileInfo(Path.Combine(publishDirectory.ToString(), "blazorhosted.dll")).Should().Exist();

            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.wasm",
                "wwwroot/_framework/blazorwasm.dll",
                "wwwroot/_framework/System.Text.Json.dll"
            });

            // Verify static assets are in the publish directory
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/index.html"
            });

            // Verify static web assets from referenced projects are copied.
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_content/RazorClassLibrary/wwwroot/exampleJsInterop.js.br",
                "wwwroot/_content/RazorClassLibrary/styles.css.br",
            });

            // Verify web.config
            publishDirectory.Should().HaveFiles(new[]
            {
                "web.config"
            });

            VerifyBootManifestHashes(testInstance, Path.Combine(publishDirectory.ToString(), "wwwroot"));
            VerifyServiceWorkerFiles(testInstance, Path.Combine(publishDirectory.ToString(), "wwwroot"),
                serviceWorkerPath: Path.Combine("serviceworkers", "my-service-worker.js"),
                serviceWorkerContent: "// This is the production service worker",
                assetsManifestPath: "custom-service-worker-assets.js");
        }

        [Fact]
        public void Publish_HostedAppWithScopedCssAndSatelliteAssemblies_VisualStudio()
        {
            // Simulates publishing the same way VS does by setting BuildProjectReferences=false.
            var testAppName = "BlazorHosted";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            testInstance.WithProjectChanges((path, project) =>
            {
                if (path.Contains("blazorwasm"))
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = new XElement(ns + "PropertyGroup");
                    propertyGroup.Add(new XElement("DefineConstants", @"$(DefineConstants);REFERENCE_classlibrarywithsatelliteassemblies"));
                    var itemGroup = new XElement(ns + "ItemGroup");
                    itemGroup.Add(new XElement("ProjectReference", new XAttribute("Include", @"..\classlibrarywithsatelliteassemblies\classlibrarywithsatelliteassemblies.csproj")));
                    project.Root.Add(propertyGroup);
                    project.Root.Add(itemGroup);
                }
            });

            var resxfileInProject = Path.Combine(testInstance.TestRoot, "blazorwasm", "Resources.ja.resx.txt");
            File.Move(resxfileInProject, Path.Combine(testInstance.TestRoot, "blazorwasm", "Resource.ja.resx"));

            File.WriteAllText(Path.Combine(testInstance.TestRoot, "blazorwasm", "App.razor.css"), "h1 { font-size: 16px; }");

            // VS builds projects individually and then a publish with BuildDependencies=false, but building the main project is a close enough approximation for this test.
            var buildCommand = new BuildCommand(testInstance, "blazorwasm");
            buildCommand.Execute("/p:BuildInsideVisualStudio=true /p:Configuration=Release").Should().Pass();

            // Publish
            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorhosted"));
            publishCommand.Execute("/p:BuildProjectReferences=false /p:BuildInsideVisualStudio=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory("net5.0");
            var blazorPublishDirectory = Path.Combine(publishDirectory.ToString(), "wwwroot");

            // Make sure the main project exists
            new FileInfo(Path.Combine(publishDirectory.ToString(), "blazorhosted.dll")).Should().Exist();

            // Verification for satelitte assemblies
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/ja/blazorwasm.resources.dll",
                "wwwroot/_framework/fr/Microsoft.CodeAnalysis.CSharp.resources.dll"
            });

            var bootJsonData = new FileInfo(Path.Combine(blazorPublishDirectory, "_framework", "blazor.boot.json"));
            bootJsonData.Should().Contain("\"es-ES\\/classlibrarywithsatelliteassemblies.resources.dll\"");
            bootJsonData.Should().Contain("\"ja\\/blazorwasm.resources.dll\"");
            bootJsonData.Should().Contain("\"fr\\/Microsoft.CodeAnalysis.CSharp.resources.dll\"");

            // Verification for https://github.com/dotnet/aspnetcore/issues/19926. Verify binaries for projects
            // referenced by the Hosted project appear in the publish directory
            publishDirectory.Should().HaveFiles(new[]
            {
                "RazorClassLibrary.dll",
                "blazorwasm.dll"
            });

            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.wasm",
                "wwwroot/_framework/blazorwasm.dll",
                "wwwroot/_framework/System.Text.Json.dll"
            });

            // Verify project references appear as static web assets
            // Also verify project references to the server project appear in the publish output
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/RazorClassLibrary.dll",
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
                "wwwroot/_framework/dotnet.wasm.br",
                "wwwroot/_framework/blazorwasm.dll.br",
                "wwwroot/_framework/RazorClassLibrary.dll.br",
                "wwwroot/_framework/System.Text.Json.dll.br"
            });

            VerifyBootManifestHashes(testInstance, blazorPublishDirectory);
            VerifyServiceWorkerFiles(testInstance, blazorPublishDirectory,
                serviceWorkerPath: Path.Combine("serviceworkers", "my-service-worker.js"),
                serviceWorkerContent: "// This is the production service worker",
                assetsManifestPath: "custom-service-worker-assets.js");
        }

        [Fact]
        public void Publish_HostedApp_WithRidSpecifiedInCLI_Works()
        {
            // Arrange
            var testAppName = "BlazorHostedRID";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var publishCommand = new PublishCommand(Log, Path.Combine(testInstance.TestRoot, "blazorhosted"));
            publishCommand.Execute("/p:RuntimeIdentifier=linux-x64").Should().Pass();

            AssertRIDPublishOuput(publishCommand, testInstance);
        }

        private static void AssertRIDPublishOuput(PublishCommand command, TestAsset testInstance)
        {
            var publishDirectory = command.GetOutputDirectory("net5.0", "Debug", "linux-x64");

            // Make sure the main project exists
            publishDirectory.Should().HaveFiles(new[]
            {
                "libhostfxr.so" // Verify that we're doing a self-contained deployment
            });

            publishDirectory.Should().HaveFiles(new[]
            {
                "RazorClassLibrary.dll",
                "blazorwasm.dll",
            });

            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/blazor.boot.json",
                "wwwroot/_framework/blazor.webassembly.js",
                "wwwroot/_framework/dotnet.wasm",
                "wwwroot/_framework/blazorwasm.dll",
                "wwwroot/_framework/System.Text.Json.dll"
            });


            publishDirectory.Should().HaveFiles(new[]
            {
                // Verify project references appear as static web assets
                "wwwroot/_framework/RazorClassLibrary.dll",
                // Also verify project references to the server project appear in the publish output
                "RazorClassLibrary.dll",
            });

            // Verify static assets are in the publish directory
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/index.html.br"
            });

            // Verify static web assets from referenced projects are copied.
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_content/RazorClassLibrary/wwwroot/exampleJsInterop.js.br",
                "wwwroot/_content/RazorClassLibrary/styles.css.br",
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
                "wwwroot/_framework/dotnet.wasm.br",
                "wwwroot/_framework/blazorwasm.dll.br",
                "wwwroot/_framework/RazorClassLibrary.dll.br",
                "wwwroot/_framework/System.Text.Json.dll.br"
            });
            publishDirectory.Should().HaveFiles(new[]
            {
                "wwwroot/_framework/dotnet.wasm.gz",
                "wwwroot/_framework/blazorwasm.dll.gz",
                "wwwroot/_framework/RazorClassLibrary.dll.gz",
                "wwwroot/_framework/System.Text.Json.dll.gz"
            });

            VerifyServiceWorkerFiles(testInstance, Path.Combine(publishDirectory.ToString(), "wwwroot"),
                serviceWorkerPath: Path.Combine("serviceworkers", "my-service-worker.js"),
                serviceWorkerContent: "// This is the production service worker",
                assetsManifestPath: "custom-service-worker-assets.js");
        }

        [Fact]
        public void Publish_WithInvariantGlobalizationEnabled_DoesNotCopyGlobalizationData()
        {
            // Arrange
            var testAppName = "BlazorWasmMinimal";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            testInstance.WithProjectChanges((project) =>
            {
                var ns = project.Root.Name.Namespace;
                var itemGroup = new XElement(ns + "PropertyGroup");
                itemGroup.Add(new XElement("InvariantGlobalization", true));
                project.Root.Add(itemGroup);
            });

            var publishCommand = new PublishCommand(Log, testInstance.TestRoot);
            publishCommand.Execute().Should().Pass();

            var publishOutputDirectory = publishCommand.GetOutputDirectory("net5.0").ToString();

            var bootJsonPath = Path.Combine(publishOutputDirectory.ToString(), "wwwroot", "_framework", "blazor.boot.json");
            var bootJsonData = ReadBootJsonData(bootJsonPath);

            bootJsonData.icuDataMode.Should().Be(ICUDataMode.Invariant);
            var runtime = bootJsonData.resources.runtime;

            runtime.Should().ContainKey("dotnet.wasm");
            runtime.Should().NotContainKey("icudt.dat");
            runtime.Should().NotContainKey("icudt_EFIGS.dat");

            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "dotnet.wasm")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "icudt.dat")).Should().NotExist();
            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "icudt_CJK.dat")).Should().NotExist();
            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "icudt_EFIGS.dat")).Should().NotExist();
            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "icudt_no_CJK.dat")).Should().NotExist();
        }

        private static void VerifyBootManifestHashes(TestAsset testAsset, string blazorPublishDirectory)
        {
            var bootManifestResolvedPath = Path.Combine(blazorPublishDirectory, "_framework", "blazor.boot.json");
            var bootManifestJson = File.ReadAllText(bootManifestResolvedPath);
            var bootManifest = JsonSerializer.Deserialize<BootJsonData>(bootManifestJson);

            VerifyBootManifestHashes(testAsset, blazorPublishDirectory, bootManifest.resources.assembly);
            VerifyBootManifestHashes(testAsset, blazorPublishDirectory, bootManifest.resources.runtime);

            if (bootManifest.resources.pdb != null)
            {
                VerifyBootManifestHashes(testAsset, blazorPublishDirectory, bootManifest.resources.pdb);
            }

            if (bootManifest.resources.satelliteResources != null)
            {
                foreach (var resourcesForCulture in bootManifest.resources.satelliteResources.Values)
                {
                    VerifyBootManifestHashes(testAsset, blazorPublishDirectory, resourcesForCulture);
                }
            }

            static void VerifyBootManifestHashes(TestAsset testAsset, string blazorPublishDirectory, ResourceHashesByNameDictionary resources)
            {
                foreach (var (name, hash) in resources)
                {
                    var relativePath = Path.Combine(blazorPublishDirectory, "_framework", name);
                    new FileInfo(Path.Combine(testAsset.TestRoot, relativePath)).Should().HashEquals(ParseWebFormattedHash(hash));
                }
            }

            static string ParseWebFormattedHash(string webFormattedHash)
            {
                Assert.StartsWith("sha256-", webFormattedHash);
                return webFormattedHash.Substring(7);
            }
        }

        private void VerifyTypeGranularTrimming(string blazorPublishDirectory)
        {
            VerifyAssemblyHasTypes(Path.Combine(blazorPublishDirectory, "_framework", "Microsoft.AspNetCore.Components.dll"), new[] {
                    "Microsoft.AspNetCore.Components.RouteView",
                    "Microsoft.AspNetCore.Components.RouteData",
                    "Microsoft.AspNetCore.Components.CascadingParameterAttribute"
                });
        }

        private void VerifyAssemblyHasTypes(string assemblyPath, string[] expectedTypes)
        {
            new FileInfo(assemblyPath).Should().Exist();

            using (var file = File.OpenRead(assemblyPath))
            {
                using var peReader = new PEReader(file);
                var metadataReader = peReader.GetMetadataReader();
                var types = metadataReader.TypeDefinitions.Where(t => !t.IsNil).Select(t =>
                {
                    var type = metadataReader.GetTypeDefinition(t);
                    return metadataReader.GetString(type.Namespace) + "." + metadataReader.GetString(type.Name);
                }).ToArray();
                types.Should().Contain(expectedTypes);
            }
        }

        private static BootJsonData ReadBootJsonData(string path)
        {
            return JsonSerializer.Deserialize<BootJsonData>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
    }
}
