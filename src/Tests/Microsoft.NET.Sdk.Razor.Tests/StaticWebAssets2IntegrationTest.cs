// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class StaticWebAssets2IntegrationTest : AspNetSdkTest
    {
        public StaticWebAssets2IntegrationTest(ITestOutputHelper log) : base(log) {}

        private const string ExpectedManifest = "{\"Version\":1,\"Hash\":\"tbdHB6SAc1VTTSPP4cFRxuKQe/PkH5LwisFj2j3WJYY=\",\"Source\":\"ComponentApp\",\"BasePath\":\"_content/ComponentApp\",\"Mode\":\"Default\",\"ManifestType\":\"Build\",\"RelatedManifests\":[],\"DiscoveryPatterns\":[{\"Name\":\"ComponentApp\\\\wwwroot\",\"ContentRoot\":\"C:\\\\work\\\\dotnet-sdk2\\\\artifacts\\\\tmp\\\\Debug\\\\Build_Generat---BA866529\\\\wwwroot\\\\\",\"BasePath\":\"_content/ComponentApp\",\"Pattern\":\"wwwroot/**\"}],\"Assets\":[{\"Identity\":\"C:\\\\work\\\\dotnet-sdk2\\\\artifacts\\\\tmp\\\\Debug\\\\Build_Generat---BA866529\\\\projectbundle\\\\ComponentApp.bundle.scp.css\",\"SourceType\":\"Computed\",\"SourceId\":\"ComponentApp\",\"ContentRoot\":\"projectbundle\\\\\",\"BasePath\":\"_content/ComponentApp\",\"RelativePath\":\"ComponentApp.bundle.scp.css\",\"AssetKind\":\"All\",\"AssetMode\":\"All\",\"CopyToOutputDirectory\":\"\",\"CopyToPublishDirectory\":\"\"}]}";

        // Build Standalone project
        [Fact]
        public void Build_GeneratesJsonManifestAndCopiesItToOutputFolder()
        {
            var testAsset = "RazorComponentApp";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory);
            build.Execute("/bl").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "StaticWebAssets.build.json");
            new FileInfo(path).Should().Exist();
            File.ReadAllText(path).Should().Be(ExpectedManifest);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.json");
            new FileInfo(finalPath).Should().Exist();
            File.ReadAllText(finalPath).Should().Be(ExpectedManifest);
        }

        [Fact]
        public void Build_DoesNotUpdateManifest_WhenHasNotChanged()
        {
            var testAsset = "RazorComponentApp";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "StaticWebAssets.build.json");
            var originalObjFile = new FileInfo(path);
            originalObjFile.Should().Exist();
            var objManifestContents = File.ReadAllText(path);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.json");
            var originalFile = new FileInfo(finalPath);
            originalFile.Should().Exist();           
            var binManifestContents = File.ReadAllText(finalPath);
            
            binManifestContents.Should().Be(objManifestContents);

            var secondBuild = new BuildCommand(projectDirectory);
            secondBuild.Execute().Should().Pass();

            var secondPath = Path.Combine(intermediateOutputPath, "StaticWebAssets.build.json");
            var secondObjFile = new FileInfo(secondPath);
            secondObjFile.Should().Exist();
            var secondObjManifest = File.ReadAllText(secondPath);
            secondObjManifest.Should().Be(objManifestContents);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var secondFinalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.json");
            var secondFinalFile = new FileInfo(secondFinalPath);
            secondFinalFile.Should().Exist();
            var secondBinManifest = File.ReadAllText(secondFinalPath);
            secondBinManifest.Should().Be(binManifestContents);

            secondFinalFile.LastWriteTimeUtc.Should().Be(originalFile.LastWriteTimeUtc);
        }

        [Fact]
        public void Build_UpdatesManifest_WhenFilesChange()
        {
            var testAsset = "RazorComponentApp";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "StaticWebAssets.build.json");
            var originalObjFile = new FileInfo(path);
            originalObjFile.Should().Exist();
            var objManifestContents = File.ReadAllText(path);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.json");
            var originalFile = new FileInfo(finalPath);
            originalFile.Should().Exist();
            var binManifestContents = File.ReadAllText(finalPath);

            binManifestContents.Should().Be(objManifestContents);

            // Second build
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "index.html"), "some html");

            var secondBuild = new BuildCommand(projectDirectory);
            secondBuild.Execute().Should().Pass();

            var secondPath = Path.Combine(intermediateOutputPath, "StaticWebAssets.build.json");
            var secondObjFile = new FileInfo(secondPath);
            secondObjFile.Should().Exist();
            var secondObjManifest = File.ReadAllText(secondPath);
            secondObjManifest.Should().NotBe(objManifestContents);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var secondFinalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.json");
            var secondFinalFile = new FileInfo(secondFinalPath);
            secondFinalFile.Should().Exist();
            var secondBinManifest = File.ReadAllText(secondFinalPath);
            secondBinManifest.Should().NotBe(binManifestContents);
            secondBinManifest.Should().Be(secondObjManifest);

            secondObjFile.LastWriteTimeUtc.Should().Should().NotBe(originalObjFile.LastWriteTimeUtc);
            secondFinalFile.LastWriteTimeUtc.Should().Should().NotBe(originalFile.LastWriteTimeUtc);
        }

        // Project with references

        [Fact]
        public void BuildProjectWithReferences_GeneratesJsonManifestAndCopiesItToOutputFolder()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory, "AppWithPackageAndP2PReference");
            build.Execute("/bl").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "StaticWebAssets.build.json");
            new FileInfo(path).Should().Exist();
            //File.ReadAllText(path).Should().Be(ExpectedManifest);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.json");
            new FileInfo(finalPath).Should().Exist();
            //File.ReadAllText(finalPath).Should().Be(ExpectedManifest);
        }

        // Build no dependencies

        // Rebuild

        // Publish

        // Pack

        // Clean
    }
}
