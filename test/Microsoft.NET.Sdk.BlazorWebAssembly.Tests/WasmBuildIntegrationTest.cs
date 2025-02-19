// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.NET.Sdk.WebAssembly;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class WasmBuildIntegrationTest(ITestOutputHelper log) : BlazorWasmBaselineTests(log, GenerateBaselines)
    {
        private static string customIcuFilename = "icudt_custom.dat";
        private static string fullIcuFilename = "icudt.dat";
        private static string[] icuShardFilenames = new string[] {
            "icudt_EFIGS.dat",
            "icudt_CJK.dat",
            "icudt_no_CJK.dat"
        };

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void BuildMinimal_Works()
        {
            // Arrange
            // Minimal has no project references, service worker etc. This is pretty close to the project template.
            var testAsset = "BlazorWasmMinimal";
            var testInstance = CreateAspNetSdkTestAsset(testAsset)
                .WithProjectChanges((p, doc) =>
                {
                        var itemGroup = new XElement("PropertyGroup");
                        var fingerprintAssets = new XElement("WasmFingerprintAssets", false);
                        itemGroup.Add(fingerprintAssets);
                        doc.Root.Add(itemGroup);
                });

            File.WriteAllText(Path.Combine(testInstance.TestRoot, "App.razor.css"), "h1 { font-size: 16px; }");

            var build = CreateBuildCommand(testInstance);
            ExecuteCommand(build)
                .Should()
                .Pass();

            var buildOutputDirectory = build.GetOutputDirectory(DefaultTfm).ToString();

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.webassembly.js")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.native.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.native.wasm.gz")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazorwasm-minimal.wasm")).Should().Exist();
        }

        [RequiresMSBuildVersionTheory("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        [InlineData("blazor")]
        [InlineData("blazor spaces")]
        public void Build_Works(string identifier)
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName, identifier: identifier)
                .WithProjectChanges((path, doc) =>
                {
                    var itemGroup = new XElement("PropertyGroup");
                    var fingerprintAssets = new XElement("WasmFingerprintAssets", false);
                    itemGroup.Add(fingerprintAssets);
                    doc.Root.Add(itemGroup);
                });

            var buildCommand = CreateBuildCommand(testInstance, "blazorwasm");
            ExecuteCommand(buildCommand)
                .Should().Pass();

            var buildOutputDirectory = buildCommand.GetOutputDirectory(DefaultTfm).ToString();

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.webassembly.js")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.native.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.native.wasm.gz")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazorwasm.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "System.Text.Json.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "System.Text.Json.wasm.gz")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "System.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "System.wasm.gz")).Should().Exist();

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazorwasm.pdb")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "RazorClassLibrary.pdb")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "RazorClassLibrary.wasm")).Should().Exist();
        }

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void Build_Works_WithLibraryUsingHintPath()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((project, document) =>
            {
                if (Path.GetFileNameWithoutExtension(project) == "blazorwasm")
                {
                    document.Root.Add(new XElement("PropertyGroup",
                        new XElement("WasmFingerprintAssets", false)));

                    var reference = document
                        .Descendants()
                        .Single(e =>
                            e.Name == "ProjectReference" &&
                            e.Attribute("Include").Value == @"..\razorclasslibrary\RazorClassLibrary.csproj");

                    reference.Name = "Reference";
                    reference.Add(new XElement(
                        "HintPath",
                        Path.Combine("..", "razorclasslibrary", "bin", "Debug", ToolsetInfo.CurrentTargetFramework, "RazorClassLibrary.dll")));
                }
            });

            var buildLibraryCommand = CreateBuildCommand(testInstance, "razorclasslibrary");
            ExecuteCommand(buildLibraryCommand)
                .Should().Pass();

            var buildCommand = CreateBuildCommand(testInstance, "blazorwasm");
            ExecuteCommand(buildCommand)
                .Should().Pass();

            var buildOutputDirectory = buildCommand.GetOutputDirectory(DefaultTfm).ToString();

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.webassembly.js")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.native.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.native.wasm.gz")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazorwasm.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "System.Text.Json.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "System.Text.Json.wasm.gz")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "System.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "System.wasm.gz")).Should().Exist();

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazorwasm.pdb")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "RazorClassLibrary.pdb")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "RazorClassLibrary.wasm")).Should().Exist();
        }

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void Build_InRelease_Works()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName)
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

            var buildCommand = CreateBuildCommand(testInstance, "blazorwasm");
            ExecuteCommand(buildCommand, "/p:Configuration=Release")
                .Should().Pass();

            var buildOutputDirectory = buildCommand.GetOutputDirectory(DefaultTfm, "Release").ToString();

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.webassembly.js")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.native.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.native.wasm.gz")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazorwasm.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "System.Text.Json.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "System.Text.Json.wasm.gz")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "System.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "System.wasm.gz")).Should().Exist();

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazorwasm.pdb")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "RazorClassLibrary.pdb")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "RazorClassLibrary.wasm")).Should().Exist();
        }

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void Build_ProducesBootJsonDataWithExpectedContent()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName)
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

            var wwwroot = Path.Combine(testInstance.TestRoot, "blazorwasm", "wwwroot");
            File.WriteAllText(Path.Combine(wwwroot, "appsettings.json"), "Default settings");
            File.WriteAllText(Path.Combine(wwwroot, "appsettings.development.json"), "Development settings");

            var buildCommand = CreateBuildCommand(testInstance, "blazorwasm");
            ExecuteCommand(buildCommand)
                .Should().Pass();

            var buildOutputDirectory = buildCommand.GetOutputDirectory(DefaultTfm).ToString();

            var bootJsonPath = Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json");
            var bootJsonData = ReadBootJsonData(bootJsonPath);

            bootJsonData.resources.wasmNative.Should().ContainKey("dotnet.native.wasm");

            var assemblies = bootJsonData.resources.assembly;
            assemblies.Should().ContainKey("blazorwasm.wasm");
            assemblies.Should().ContainKey("RazorClassLibrary.wasm");
            assemblies.Should().ContainKey("System.Text.Json.wasm");

            var pdb = bootJsonData.resources.pdb;
            pdb.Should().ContainKey("blazorwasm.pdb");
            pdb.Should().ContainKey("RazorClassLibrary.pdb");

            bootJsonData.resources.satelliteResources.Should().BeNull();

            bootJsonData.config.Should().Contain("../appsettings.json");
            bootJsonData.config.Should().Contain("../appsettings.development.json");
        }

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void Build_InRelease_ProducesBootJsonDataWithExpectedContent()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName)
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

            var wwwroot = Path.Combine(testInstance.TestRoot, "blazorwasm", "wwwroot");
            File.WriteAllText(Path.Combine(wwwroot, "appsettings.json"), "Default settings");
            File.WriteAllText(Path.Combine(wwwroot, "appsettings.development.json"), "Development settings");

            var buildCommand = CreateBuildCommand(testInstance, "blazorwasm");
            ExecuteCommand(buildCommand, "/p:Configuration=Release")
                .Should().Pass();

            var buildOutputDirectory = buildCommand.GetOutputDirectory(DefaultTfm, "Release").ToString();

            var bootJsonPath = Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json");
            var bootJsonData = ReadBootJsonData(bootJsonPath);

            bootJsonData.resources.wasmNative.Should().ContainKey("dotnet.native.wasm");

            var assemblies = bootJsonData.resources.assembly;
            assemblies.Should().ContainKey("blazorwasm.wasm");
            assemblies.Should().ContainKey("RazorClassLibrary.wasm");
            assemblies.Should().ContainKey("System.Text.Json.wasm");

            var pdb = bootJsonData.resources.pdb;
            pdb.Should().ContainKey("blazorwasm.pdb");
            pdb.Should().ContainKey("RazorClassLibrary.pdb");

            bootJsonData.resources.satelliteResources.Should().BeNull();
        }

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void Build_WithBlazorEnableTimeZoneSupportDisabled_DoesNotCopyTimeZoneInfo()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((project) =>
            {
                var ns = project.Root.Name.Namespace;
                var itemGroup = new XElement(ns + "PropertyGroup");
                itemGroup.Add(new XElement("BlazorEnableTimeZoneSupport", false));
                itemGroup.Add(new XElement("WasmFingerprintAssets", false));
                project.Root.Add(itemGroup);
            });

            var buildCommand = CreateBuildCommand(testInstance, "blazorwasm");
            ExecuteCommand(buildCommand)
                .Should().Pass();

            var buildOutputDirectory = buildCommand.GetOutputDirectory(DefaultTfm).ToString();

            var bootJsonPath = Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json");
            var bootJsonData = ReadBootJsonData(bootJsonPath);

            bootJsonData.resources.wasmNative.Should().ContainKey("dotnet.native.wasm");
            bootJsonData.resources.runtime.Should().BeNull();

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.native.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.timezones.blat")).Should().NotExist();
        }

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void Build_WithInvariantGlobalizationEnabled_DoesNotCopyGlobalizationData()
        {
            // Arrange
            var testAppName = "BlazorWasmMinimal";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((project) =>
            {
                var ns = project.Root.Name.Namespace;
                var itemGroup = new XElement(ns + "PropertyGroup");
                itemGroup.Add(new XElement("InvariantGlobalization", true));
                itemGroup.Add(new XElement("WasmFingerprintAssets", false));
                project.Root.Add(itemGroup);
            });

            var buildCommand = CreateBuildCommand(testInstance);
            ExecuteCommand(buildCommand)
                .Should().Pass();

            var buildOutputDirectory = buildCommand.GetOutputDirectory(DefaultTfm).ToString();

            var bootJsonPath = Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json");
            var bootJsonData = ReadBootJsonData(bootJsonPath);

            bootJsonData.globalizationMode.Should().Be("invariant");
            bootJsonData.resources.wasmNative.Should().ContainKey("dotnet.native.wasm");

            bootJsonData.resources.icu.Should().BeNull();

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.native.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", fullIcuFilename)).Should().NotExist();
            foreach (var shardFilename in icuShardFilenames)
            {
                new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", shardFilename)).Should().NotExist();
            }
        }

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void Publish_WithInvariantGlobalizationEnabled_DoesNotCopyGlobalizationData()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((project) =>
            {
                var ns = project.Root.Name.Namespace;
                var itemGroup = new XElement(ns + "PropertyGroup");
                itemGroup.Add(new XElement("InvariantGlobalization", true));
                itemGroup.Add(new XElement("WasmFingerprintAssets", false));
                project.Root.Add(itemGroup);
            });

            var publishCommand = CreatePublishCommand(testInstance, "blazorhosted");
            ExecuteCommand(publishCommand)
                .Should().Pass();

            var publishOutputDirectory = publishCommand.GetOutputDirectory(DefaultTfm).ToString();

            var bootJsonPath = Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "blazor.boot.json");
            var bootJsonData = ReadBootJsonData(bootJsonPath);

            bootJsonData.globalizationMode.Should().Be("invariant");
            bootJsonData.resources.wasmNative.Should().ContainKey("dotnet.native.wasm");

            bootJsonData.resources.icu.Should().BeNull();

            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", "dotnet.native.wasm")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", fullIcuFilename)).Should().NotExist();
            foreach (var shardFilename in icuShardFilenames)
            {
                new FileInfo(Path.Combine(publishOutputDirectory, "wwwroot", "_framework", shardFilename)).Should().NotExist();
            }
        }

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void Build_WithBlazorWebAssemblyLoadCustomGlobalizationData_SetsGlobalizationMode()
        {
            // Arrange
            var testAppName = "BlazorWasmMinimal";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((project) =>
            {
                var ns = project.Root.Name.Namespace;
                var itemGroup = new XElement(ns + "PropertyGroup");
                itemGroup.Add(new XElement("BlazorIcuDataFileName", customIcuFilename));
                itemGroup.Add(new XElement("WasmFingerprintAssets", false));
                project.Root.Add(itemGroup);
            });

            var buildCommand = CreateBuildCommand(testInstance);
            ExecuteCommand(buildCommand)
                .Should().Pass();

            var buildOutputDirectory = buildCommand.GetOutputDirectory(DefaultTfm).ToString();

            var bootJsonPath = Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json");
            var bootJsonData = ReadBootJsonData(bootJsonPath);

            bootJsonData.globalizationMode.Should().Be("custom");

            bootJsonData.resources.wasmNative.Should().ContainKey("dotnet.native.wasm");
            bootJsonData.resources.icu.Should().ContainKey(customIcuFilename);
            bootJsonData.resources.icu.Should().NotContainKey(fullIcuFilename);
            foreach (var shardFilename in icuShardFilenames)
            {
                bootJsonData.resources.icu.Should().NotContainKey(shardFilename);
            }

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.native.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", customIcuFilename)).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", fullIcuFilename)).Should().NotExist();
            foreach (var shardFilename in icuShardFilenames)
            {
                new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", shardFilename)).Should().NotExist();
            }
        }

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void Publish_WithBlazorWebAssemblyLoadCustomGlobalizationData_SetsGlobalizationMode()
        {
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((project) =>
            {
                var ns = project.Root.Name.Namespace;
                var itemGroup = new XElement(ns + "PropertyGroup");
                itemGroup.Add(new XElement("BlazorIcuDataFileName", customIcuFilename));
                itemGroup.Add(new XElement("WasmFingerprintAssets", false));
                project.Root.Add(itemGroup);
            });

            var publishCommand = CreatePublishCommand(testInstance, "blazorhosted");
            ExecuteCommand(publishCommand)
                .Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm).ToString();

            var bootJsonPath = Path.Combine(publishDirectory, "wwwroot", "_framework", "blazor.boot.json");
            var bootJsonData = ReadBootJsonData(bootJsonPath);

            bootJsonData.globalizationMode.Should().Be("custom");

            bootJsonData.resources.wasmNative.Should().ContainKey("dotnet.native.wasm");
            bootJsonData.resources.icu.Should().ContainKey(customIcuFilename);
            bootJsonData.resources.icu.Should().NotContainKey(fullIcuFilename);
            foreach (var shardFilename in icuShardFilenames)
            {
                bootJsonData.resources.icu.Should().NotContainKey(shardFilename);
            }

            new FileInfo(Path.Combine(publishDirectory, "wwwroot", "_framework", "dotnet.native.wasm")).Should().Exist();
            new FileInfo(Path.Combine(publishDirectory, "wwwroot", "_framework", customIcuFilename)).Should().Exist();
            new FileInfo(Path.Combine(publishDirectory, "wwwroot", "_framework", fullIcuFilename)).Should().NotExist();
            foreach (var shardFilename in icuShardFilenames)
            {
                new FileInfo(Path.Combine(publishDirectory, "wwwroot", "_framework", shardFilename)).Should().NotExist();
            }
        }

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void Build_WithBlazorWebAssemblyLoadAllGlobalizationData_SetsICUDataMode()
        {
            // Arrange
            var testAppName = "BlazorWasmMinimal";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((project) =>
            {
                var ns = project.Root.Name.Namespace;
                var itemGroup = new XElement(ns + "PropertyGroup");
                itemGroup.Add(new XElement("BlazorWebAssemblyLoadAllGlobalizationData", true));
                itemGroup.Add(new XElement("WasmFingerprintAssets", false));
                project.Root.Add(itemGroup);
            });

            var buildCommand = CreateBuildCommand(testInstance);
            ExecuteCommand(buildCommand)
                .Should().Pass();

            var buildOutputDirectory = buildCommand.GetOutputDirectory(DefaultTfm).ToString();

            var bootJsonPath = Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json");
            var bootJsonData = ReadBootJsonData(bootJsonPath);

            bootJsonData.globalizationMode.Should().Be("all");

            bootJsonData.resources.wasmNative.Should().ContainKey("dotnet.native.wasm");
            bootJsonData.resources.icu.Should().ContainKey(fullIcuFilename);
            foreach (var shardFilename in icuShardFilenames)
            {
                bootJsonData.resources.icu.Should().NotContainKey(shardFilename);
            }

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.native.wasm")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", fullIcuFilename)).Should().Exist();
            foreach (var shardFilename in icuShardFilenames)
            {
                new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", shardFilename)).Should().NotExist();
            }
        }

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void Publish_WithBlazorWebAssemblyLoadAllGlobalizationData_SetsGlobalizationMode()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((project) =>
            {
                var ns = project.Root.Name.Namespace;
                var itemGroup = new XElement(ns + "PropertyGroup");
                itemGroup.Add(new XElement("BlazorWebAssemblyLoadAllGlobalizationData", true));
                itemGroup.Add(new XElement("WasmFingerprintAssets", false));
                project.Root.Add(itemGroup);
            });

            var publishCommand = CreatePublishCommand(testInstance, "blazorhosted");
            ExecuteCommand(publishCommand)
                .Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(DefaultTfm).ToString();

            var bootJsonPath = Path.Combine(publishDirectory, "wwwroot", "_framework", "blazor.boot.json");
            var bootJsonData = ReadBootJsonData(bootJsonPath);

            bootJsonData.globalizationMode.Should().Be("all");

            bootJsonData.resources.wasmNative.Should().ContainKey("dotnet.native.wasm");
            bootJsonData.resources.icu.Should().ContainKey(fullIcuFilename);
            foreach (var shardFilename in icuShardFilenames)
            {
                bootJsonData.resources.icu.Should().NotContainKey(shardFilename);
            }

            new FileInfo(Path.Combine(publishDirectory, "wwwroot", "_framework", "dotnet.native.wasm")).Should().Exist();
            new FileInfo(Path.Combine(publishDirectory, "wwwroot", "_framework", fullIcuFilename)).Should().Exist();
            foreach (var shardFilename in icuShardFilenames)
            {
                new FileInfo(Path.Combine(publishDirectory, "wwwroot", "_framework", shardFilename)).Should().NotExist();
            }
        }

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void Build_Hosted_Works()
        {
            // Arrange
            var testAppName = "BlazorHosted";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            var buildCommand = CreateBuildCommand(testInstance, "blazorhosted");
            ExecuteCommand(buildCommand).Should().Pass();

            var buildOutputDirectory = buildCommand.GetOutputDirectory(DefaultTfm).ToString();

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "_bin", "blazorwasm.wasm")).Should().NotExist();
        }

        [Fact(Skip = "https://github.com/dotnet/runtime/issues/105399")]
        public void Build_SatelliteAssembliesAreCopiedToBuildOutput()
        {
            // Arrange
            var testAppName = "BlazorWasmWithLibrary";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAppName);

            ProjectDirectory.WithProjectChanges((path, project) =>
            {
                if (path.Contains("blazorwasm"))
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = new XElement(ns + "PropertyGroup");
                    propertyGroup.Add(new XElement("DefineConstants", @"$(DefineConstants);REFERENCE_classlibrarywithsatelliteassemblies"));
                    propertyGroup.Add(new XElement("WasmFingerprintAssets", false));
                    var itemGroup = new XElement(ns + "ItemGroup");
                    itemGroup.Add(new XElement("ProjectReference", new XAttribute("Include", @"..\classlibrarywithsatelliteassemblies\classlibrarywithsatelliteassemblies.csproj")));
                    project.Root.Add(propertyGroup);
                    project.Root.Add(itemGroup);
                }
            });

            var resxfileInProject = Path.Combine(ProjectDirectory.TestRoot, "blazorwasm", "Resources.ja.resx.txt");
            File.Move(resxfileInProject, Path.Combine(ProjectDirectory.TestRoot, "blazorwasm", "Resource.ja.resx"));

            var buildCommand = CreateBuildCommand(ProjectDirectory, "blazorwasm");
            ExecuteCommand(buildCommand).Should().Pass();

            var outputPath = buildCommand.GetOutputDirectory(DefaultTfm).ToString();
            var intermediateOutputPath = buildCommand.GetIntermediateDirectory(DefaultTfm).ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "blazorwasm.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();

            AssertBuildAssets(
                manifest,
                outputPath,
                intermediateOutputPath);

            new FileInfo(Path.Combine(outputPath, "wwwroot", "_framework", "blazorwasm.wasm")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "wwwroot", "_framework", "classlibrarywithsatelliteassemblies.wasm")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "wwwroot", "_framework", "Microsoft.CodeAnalysis.CSharp.wasm")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "wwwroot", "_framework", "fr", "Microsoft.CodeAnalysis.CSharp.resources.wasm")).Should().Exist();

            var bootJsonPath = new FileInfo(Path.Combine(outputPath, "wwwroot", "_framework", "blazor.boot.json"));
            bootJsonPath.Should().Contain("\"Microsoft.CodeAnalysis.CSharp.wasm\"");
            bootJsonPath.Should().Contain("\"fr\"");
            bootJsonPath.Should().Contain("\"Microsoft.CodeAnalysis.CSharp.resources.wasm\"");
        }

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void Build_WithCustomOutputPath_Works()
        {
            var testAppName = "BlazorWasmWithLibrary";
            var testInstance = CreateAspNetSdkTestAsset(testAppName);

            testInstance.WithProjectChanges((path, project) =>
            {
                if (path.Contains("blazorwasm"))
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = new XElement(ns + "PropertyGroup");
                    propertyGroup.Add(new XElement("BaseOutputPath", @"$(MSBuildThisFileDirectory)build\bin\"));
                    propertyGroup.Add(new XElement("BaseIntermediateOutputPath", @"$(MSBuildThisFileDirectory)build\obj\"));
                    project.Root.Add(propertyGroup);
                }
            });

            var buildCommand = CreateBuildCommand(testInstance, "blazorwasm");
            ExecuteCommand(buildCommand).Should().Pass();
        }

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void Build_WithTransitiveReference_Works()
        {
            // Regression test for https://github.com/dotnet/aspnetcore/issues/37574.
            var testInstance = CreateAspNetSdkTestAsset("BlazorWasmWithLibrary");

            var buildCommand = CreateBuildCommand(testInstance, "classlibrarywithsatelliteassemblies");
            ExecuteCommand(buildCommand).Should().Pass();
            var referenceAssemblyPath = new FileInfo(Path.Combine(
                buildCommand.GetOutputDirectory(DefaultTfm).ToString(),
                "classlibrarywithsatelliteassemblies.dll"));

            referenceAssemblyPath.Should().Exist();

            testInstance.WithProjectChanges((path, project) =>
            {
                if (path.Contains("blazorwasm.csproj"))
                {
                    var propertyGroup = new XElement(project.Root.Name.Namespace + "PropertyGroup");
                    propertyGroup.Add(new XElement("WasmFingerprintAssets", false));
                    project.Root.Add(propertyGroup);
                }

                if (path.Contains("razorclasslibrary"))
                {
                    var ns = project.Root.Name.Namespace;
                    // <ItemGroup>
                    //  <Reference Include="classlibrarywithsatelliteassemblies" HintPath="$Path\classlibrarywithsatelliteassemblies.wasm" />
                    // </ItemGroup>
                    var itemGroup = new XElement(ns + "ItemGroup",
                        new XElement(ns + "Reference",
                            new XAttribute("Include", "classlibrarywithsatelliteassemblies"),
                            new XAttribute("HintPath", referenceAssemblyPath)));

                    project.Root.Add(itemGroup);
                }
            });

            // Ensure a compile time reference exists between the project and the assembly added as a reference. This is required for
            // the assembly to be resolved by the "app" as part of RAR
            File.WriteAllText(Path.Combine(testInstance.Path, "razorclasslibrary", "TestReference.cs"),
@"
public class TestReference
{
    public void Method() => System.GC.KeepAlive(typeof(classlibrarywithsatelliteassemblies.Class1));
}");

            buildCommand = CreateBuildCommand(testInstance, "blazorwasm");
            ExecuteCommand(buildCommand).Should().Pass();

            // Assert
            var outputDirectory = buildCommand.GetOutputDirectory(DefaultTfm).ToString();
            var fileInWwwroot = new FileInfo(Path.Combine(outputDirectory, "wwwroot", "_framework", "classlibrarywithsatelliteassemblies.wasm"));
            fileInWwwroot.Should().Exist();
        }

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void Build_WithReference_Works()
        {
            // Regression test for https://github.com/dotnet/aspnetcore/issues/37574.
            var testInstance = CreateAspNetSdkTestAsset("BlazorWasmWithLibrary")
                .WithProjectChanges((path, project) =>
                {
                    if (path.Contains("blazorwasm.csproj"))
                    {
                        var ns = project.Root.Name.Namespace;
                        var propertyGroup = new XElement(ns + "PropertyGroup");
                        propertyGroup.Add(new XElement("WasmFingerprintAssets", false));
                        project.Root.Add(propertyGroup);
                    }
                });

            var buildCommand = CreateBuildCommand(testInstance, "classlibrarywithsatelliteassemblies");
            ExecuteCommand(buildCommand).Should().Pass();
            var referenceAssemblyPath = new FileInfo(Path.Combine(
                buildCommand.GetOutputDirectory(DefaultTfm).ToString(),
                "classlibrarywithsatelliteassemblies.dll"));

            referenceAssemblyPath.Should().Exist();

            testInstance.WithProjectChanges((path, project) =>
            {
                if (path.Contains("blazorwasm"))
                {
                    var ns = project.Root.Name.Namespace;
                    // <ItemGroup>
                    //  <Reference Include="classlibrarywithsatelliteassemblies" HintPath="$Path\classlibrarywithsatelliteassemblies.wasm" />
                    // </ItemGroup>
                    var itemGroup = new XElement(ns + "ItemGroup",
                        new XElement(ns + "Reference",
                            new XAttribute("Include", "classlibrarywithsatelliteassemblies"),
                            new XAttribute("HintPath", referenceAssemblyPath)));

                    project.Root.Add(itemGroup);

                    var propertyGroup = new XElement(ns + "PropertyGroup");
                    propertyGroup.Add(new XElement("WasmFingerprintAssets", false));
                    project.Root.Add(propertyGroup);
                }
            });

            // Ensure a compile time reference exists between the project and the assembly added as a reference. This is required for
            // the assembly to be resolved by the "app" as part of RAR
            File.WriteAllText(Path.Combine(testInstance.Path, "blazorwasm", "TestReference.cs"),
@"
public class TestReference
{
    public void Method() => System.GC.KeepAlive(typeof(classlibrarywithsatelliteassemblies.Class1));
}");

            buildCommand = CreateBuildCommand(testInstance, "blazorwasm");
            ExecuteCommand(buildCommand).Should().Pass();

            // Assert
            var outputDirectory = buildCommand.GetOutputDirectory(DefaultTfm).ToString();
            var fileInWwwroot = new FileInfo(Path.Combine(outputDirectory, "wwwroot", "_framework", "classlibrarywithsatelliteassemblies.wasm"));
            fileInWwwroot.Should().Exist();
        }

        [RequiresMSBuildVersionTheory("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        [InlineData(true)]
        [InlineData(false)]
        [InlineData(null)]
        public void Build_WithStartupMemoryCache(bool? value)
            => BuildWasmMinimalAndValidateBootConfig(new[] { ("BlazorWebAssemblyStartupMemoryCache", value?.ToString()) }, b => b.startupMemoryCache.Should().Be(value));

        [RequiresMSBuildVersionTheory("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        [InlineData(true)]
        [InlineData(false)]
        [InlineData(null)]
        public void Build_WithJiterpreter(bool? value)
            => BuildWasmMinimalAndValidateBootConfig(new[] { ("BlazorWebAssemblyJiterpreter", value?.ToString()) }, b =>
            {
                if (value != null)
                {
                    b.runtimeOptions.Should().NotBeNull();
                    b.runtimeOptions.Length.Should().Be(3);
                }
                else
                {
                    b.runtimeOptions.Should().BeNull();
                }
            });

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
        public void Build_WithJiterpreter_Advanced()
            => BuildWasmMinimalAndValidateBootConfig(new[] { ("BlazorWebAssemblyJiterpreter", "true"), ("BlazorWebAssemblyRuntimeOptions", "--no-jiterpreter-interp-entry-enabled") }, b =>
            {
                b.runtimeOptions.Should().NotBeNull();
                b.runtimeOptions.Length.Should().Be(3);
                b.runtimeOptions.Should().Contain("--no-jiterpreter-interp-entry-enabled");
                b.runtimeOptions.Should().NotContain("--jiterpreter-interp-entry-enabled");
                b.runtimeOptions.Should().Contain("--jiterpreter-traces-enabled");
                b.runtimeOptions.Should().Contain("--jiterpreter-jit-call-enabled");
            });

        private void BuildWasmMinimalAndValidateBootConfig((string name, string value)[] properties, Action<BootJsonData> validateBootConfig)
        {
            var testAppName = "BlazorWasmMinimal";
            var testInstance = CreateAspNetSdkTestAsset(testAppName, identifier: string.Join("-", properties.Select(p => p.name + p.value ?? "null")));

            foreach (var property in properties)
            {
                if (property.value != null)
                {
                    testInstance.WithProjectChanges((project) =>
                    {
                        var ns = project.Root.Name.Namespace;
                        var itemGroup = new XElement(ns + "PropertyGroup");
                        itemGroup.Add(new XElement(property.name, property.value));
                        project.Root.Add(itemGroup);
                    });
                }
            }

            var buildCommand = CreateBuildCommand(testInstance);
            ExecuteCommand(buildCommand)
                .Should().Pass();

            var buildOutputDirectory = buildCommand.GetOutputDirectory(DefaultTfm).ToString();

            var bootJsonPath = Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json");
            var bootJsonData = ReadBootJsonData(bootJsonPath);

            validateBootConfig(bootJsonData);
        }

        private static BootJsonData ReadBootJsonData(string path)
        {
            return JsonSerializer.Deserialize<BootJsonData>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
    }
}
