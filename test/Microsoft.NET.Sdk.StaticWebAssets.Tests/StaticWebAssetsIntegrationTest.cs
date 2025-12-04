// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Reflection;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests
{
    public class StaticWebAssetsIntegrationTest : AspNetSdkBaselineTest
    {
        public StaticWebAssetsIntegrationTest(ITestOutputHelper log) : base(log, GenerateBaselines) { }

        // Build Standalone project
        [Fact]
        public void Build_GeneratesJsonManifestAndCopiesItToOutputFolder()
        {
            var expectedManifest = LoadBuildManifest();
            var testAsset = "RazorComponentApp";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = CreateBuildCommand(ProjectDirectory);
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, expectedManifest);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();

            var manifest1 = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));
            AssertManifest(manifest1, expectedManifest);
            AssertBuildAssets(manifest1, outputPath, intermediateOutputPath);
        }

        [Fact]
        public void Build_Can_DisableAssetCaching()
        {
            var expectedManifest = LoadBuildManifest();
            var testAsset = "RazorComponentApp";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = CreateBuildCommand(ProjectDirectory);
            ExecuteCommand(build, "/p:StaticWebAssetsCacheDefineStaticWebAssetsEnabled=false").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, expectedManifest);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();

            // The caches shouldn't exist.
            // Manifest
            new FileInfo(Path.Combine(intermediateOutputPath, "rpswa.dswa.cache.json")).Should().NotExist();
            // Compressed assets
            new FileInfo(Path.Combine(intermediateOutputPath, "rbcswa.dswa.cache.json")).Should().NotExist();
            // Initializers
            new FileInfo(Path.Combine(intermediateOutputPath, "rjimswa.dswa.cache.json")).Should().NotExist();
            // JS Modules
            new FileInfo(Path.Combine(intermediateOutputPath, "rjsmcshtml.dswa.cache.json")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "rjsmrazor.dswa.cache.json")).Should().NotExist();

            var manifest1 = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));
            AssertManifest(manifest1, expectedManifest);
            AssertBuildAssets(manifest1, outputPath, intermediateOutputPath);
        }

        [Fact]
        public void Build_DoesNotUpdateManifest_WhenHasNotChanged()
        {
            var testAsset = "RazorComponentApp";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = CreateBuildCommand(ProjectDirectory);
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            var originalObjFile = new FileInfo(path);
            originalObjFile.Should().Exist();
            var objManifestContents = File.ReadAllText(Path.Combine(intermediateOutputPath, "staticwebassets.build.json"));
            AssertManifest(
                StaticWebAssetsManifest.FromJsonString(objManifestContents),
                LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.runtime.json");
            var originalFile = new FileInfo(finalPath);
            originalFile.Should().Exist();
            var binManifestContents = File.ReadAllText(finalPath);

            var secondBuild = CreateBuildCommand(ProjectDirectory);
            secondBuild.Execute().Should().Pass();

            var secondPath = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            var secondObjFile = new FileInfo(secondPath);
            secondObjFile.Should().Exist();
            var secondObjManifest = File.ReadAllText(secondPath);
            secondObjManifest.Should().Be(objManifestContents);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var secondFinalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.runtime.json");
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
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = CreateBuildCommand(ProjectDirectory);
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            var originalObjFile = new FileInfo(path);
            originalObjFile.Should().Exist();
            var objManifestContents = File.ReadAllText(Path.Combine(intermediateOutputPath, "staticwebassets.build.json"));
            var firstManifest = StaticWebAssetsManifest.FromJsonString(objManifestContents);
            AssertManifest(firstManifest, LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.runtime.json");
            var originalFile = new FileInfo(finalPath);
            originalFile.Should().Exist();
            var binManifestContents = File.ReadAllText(finalPath);

            AssertBuildAssets(
                firstManifest,
                outputPath,
                intermediateOutputPath);

            // Second build
            Directory.CreateDirectory(Path.Combine(ProjectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(ProjectDirectory.Path, "wwwroot", "index.html"), "some html");

            var secondBuild = CreateBuildCommand(ProjectDirectory);
            secondBuild.Execute().Should().Pass();

            var secondPath = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            var secondObjFile = new FileInfo(secondPath);
            secondObjFile.Should().Exist();
            var secondObjManifest = File.ReadAllText(secondPath);
            var secondManifest = StaticWebAssetsManifest.FromJsonString(secondObjManifest);
            AssertManifest(
                secondManifest,
                LoadBuildManifest("Updated"),
                "Updated");

            secondObjManifest.Should().NotBe(objManifestContents);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var secondFinalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.runtime.json");
            var secondFinalFile = new FileInfo(secondFinalPath);
            secondFinalFile.Should().Exist();
            var secondBinManifest = File.ReadAllText(secondFinalPath);
            secondBinManifest.Should().NotBe(binManifestContents);

            secondObjFile.LastWriteTimeUtc.Should().NotBe(originalObjFile.LastWriteTimeUtc);
            secondFinalFile.LastWriteTimeUtc.Should().NotBe(originalFile.LastWriteTimeUtc);

            AssertBuildAssets(
                secondManifest,
                outputPath,
                intermediateOutputPath,
                "Updated");
        }

        // Rebuild
        [Fact]
        public void Rebuild_RegeneratesJsonManifestAndCopiesItToOutputFolder()
        {
            var testAsset = "RazorComponentApp";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = CreateBuildCommand(ProjectDirectory);
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            var originalObjFile = new FileInfo(path);
            originalObjFile.Should().Exist();
            var objManifestContents = File.ReadAllText(Path.Combine(intermediateOutputPath, "staticwebassets.build.json"));
            AssertManifest(StaticWebAssetsManifest.FromJsonString(objManifestContents), LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.runtime.json");
            var originalFile = new FileInfo(finalPath);
            originalFile.Should().Exist();
            var binManifestContents = File.ReadAllText(finalPath);

            // rebuild build
            var rebuild = CreateRebuildCommand(ProjectDirectory);
            ExecuteCommand(rebuild).Should().Pass();

            var secondPath = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            var secondObjFile = new FileInfo(secondPath);
            secondObjFile.Should().Exist();
            var secondObjManifestContents = File.ReadAllText(secondPath);
            var secondManifest = StaticWebAssetsManifest.FromJsonString(secondObjManifestContents);
            AssertManifest(
                secondManifest,
                LoadBuildManifest("Rebuild"),
                "Rebuild");

            // This is no longer true because the manifests include the timestamp for the last modified
            // time of the file, etc.
            //secondObjManifestContents.Should().Be(objManifestContents);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var secondFinalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.runtime.json");
            var secondFinalFile = new FileInfo(secondFinalPath);
            secondFinalFile.Should().Exist();
            var secondBinManifest = File.ReadAllText(secondFinalPath);
            secondBinManifest.Should().Be(binManifestContents);

            secondObjFile.LastWriteTimeUtc.Should().NotBe(originalObjFile.LastWriteTimeUtc);
            secondFinalFile.LastWriteTimeUtc.Should().NotBe(originalFile.LastWriteTimeUtc);

            AssertBuildAssets(
                secondManifest,
                outputPath,
                intermediateOutputPath,
                "Rebuild");
        }

        // Publish
        [Fact]
        public void Publish_GeneratesPublishJsonManifestAndCopiesPublishAssets()
        {
            var testAsset = "RazorComponentApp";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var publish = CreatePublishCommand(ProjectDirectory);
            ExecuteCommand(publish).Should().Pass();

            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var publishPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the build manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(publishPath, "ComponentApp.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().NotExist();

            // GenerateStaticWebAssetsManifest should generate the publish manifest file.
            var intermediatePublishManifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath));
            AssertManifest(publishManifest, LoadPublishManifest());

            AssertPublishAssets(
                publishManifest,
                publishPath,
                intermediateOutputPath);
        }

        [Fact]
        public void Publish_PublishSingleFile_GeneratesPublishJsonManifestAndCopiesPublishAssets()
        {
            var expectedManifest = LoadBuildManifest();
            var testAsset = "RazorComponentApp";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var publish = CreatePublishCommand(ProjectDirectory);
            ExecuteCommand(publish, "/p:PublishSingleFile=true", $"/p:RuntimeIdentifier={RuntimeInformation.RuntimeIdentifier}").Should().Pass();

            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug", RuntimeInformation.RuntimeIdentifier).ToString();
            var publishPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the build manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, expectedManifest, runtimeIdentifier: RuntimeInformation.RuntimeIdentifier);

            // GenerateStaticWebAssetsManifest should not copy the file to the output folder.
            var finalPath = Path.Combine(publishPath, "ComponentApp.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().NotExist();

            // GenerateStaticWebAssetsManifest should generate the publish manifest file.
            var intermediatePublishManifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath));
            AssertManifest(
                publishManifest,
                LoadPublishManifest(),
                runtimeIdentifier: RuntimeInformation.RuntimeIdentifier);

            AssertPublishAssets(
                publishManifest,
                publishPath,
                intermediateOutputPath);
        }

        [Fact]
        public void Publish_NoBuild_GeneratesPublishJsonManifestAndCopiesPublishAssets()
        {
            var expectedManifest = LoadBuildManifest();
            var testAsset = "RazorComponentApp";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = CreateBuildCommand(ProjectDirectory);
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var publishPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            var objManifestFile = new FileInfo(path);
            objManifestFile.Should().Exist();
            var objManifestFileTimeStamp = objManifestFile.LastWriteTimeUtc;

            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, expectedManifest);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(publishPath, "ComponentApp.staticwebassets.runtime.json");
            var binManifestFile = new FileInfo(finalPath);
            binManifestFile.Should().Exist();
            var binManifestTimeStamp = binManifestFile.LastWriteTimeUtc;

            var finalManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));
            AssertManifest(finalManifest, expectedManifest);

            // Publish no build

            var publish = CreatePublishCommand(ProjectDirectory);
            ExecuteCommand(publish, "/p:NoBuild=true").Should().Pass();

            var secondObjTimeStamp = new FileInfo(path).LastWriteTimeUtc;

            secondObjTimeStamp.Should().Be(objManifestFileTimeStamp);

            var seconbObjManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(seconbObjManifest, expectedManifest);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var seconBinManifestPath = Path.Combine(publishPath, "ComponentApp.staticwebassets.runtime.json");
            var secondBinManifestFile = new FileInfo(seconBinManifestPath);
            secondBinManifestFile.Should().Exist();

            secondBinManifestFile.LastWriteTimeUtc.Should().Be(binManifestTimeStamp);

            var secondBinManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));
            AssertManifest(secondBinManifest, expectedManifest);

            // GenerateStaticWebAssetsManifest should generate the publish manifest file.
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
        public void Build_DeployOnBuild_GeneratesPublishJsonManifestAndCopiesPublishAssets()
        {
            var expectedManifest = LoadBuildManifest();
            var testAsset = "RazorComponentApp";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = CreateBuildCommand(ProjectDirectory);
            build.Execute("/p:DeployOnBuild=true").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the build manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();

            // GenerateStaticWebAssetsManifest should generate the publish manifest file.
            var intermediatePublishManifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath));
            AssertManifest(publishManifest, LoadPublishManifest());

            AssertPublishAssets(
                publishManifest,
                Path.Combine(outputPath, "publish"),
                intermediateOutputPath);
        }

        // Clean
        [Fact]
        public void Clean_RemovesManifestFrom_BuildAndIntermediateOutput()
        {
            var expectedManifest = LoadBuildManifest();
            var testAsset = "RazorComponentApp";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = CreateBuildCommand(ProjectDirectory);
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, expectedManifest);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();
            var finalManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));
            AssertManifest(finalManifest, expectedManifest);

            var clean = new CleanCommand(Log, ProjectDirectory.Path);
            clean.Execute().Should().Pass();

            // Obj folder manifest does not exist
            new FileInfo(path).Should().NotExist();

            // Bin folder manifest does not exist
            new FileInfo(finalPath).Should().NotExist();
        }

        [Fact]
        public void Publish_WithExternalProjectReference_UpdatesAssets()
        {
            var testAsset = "RazorAppWithP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset)
                .WithProjectChanges((name, project) =>
                {
                    if (Path.GetFileName(name).Equals("ClassLibrary.csproj", StringComparison.Ordinal))
                    {
                        var sdkAttribute = project.Root.Attribute("Sdk");
                        if (sdkAttribute == null)
                        {
                            sdkAttribute = new XAttribute("Sdk", "Microsoft.NET.Sdk");
                            project.Root.Add(sdkAttribute);
                        }
                        else
                        {
                            sdkAttribute.Value = "Microsoft.NET.Sdk";
                        }
                        project.Root.AddFirst(new XElement("Import", new XAttribute("Project", "ExternalStaticAssets.targets")));

                        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Microsoft.NET.Sdk.StaticWebAssets.Tests.content.ExternalStaticAssets.targets");
                        using var destination = File.OpenWrite(Path.Combine(Path.GetDirectoryName(name), "ExternalStaticAssets.targets"));
                        stream.CopyTo(destination);
                    }
                });

            var publish = CreatePublishCommand(ProjectDirectory, "AppWithP2PReference");
            ExecuteCommand(publish).Should().Pass();

            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var publishPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the build manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(publishPath, "ComponentApp.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().NotExist();

            // GenerateStaticWebAssetsManifest should generate the publish manifest file.
            var intermediatePublishManifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath));
            AssertManifest(publishManifest, LoadPublishManifest());

            AssertPublishAssets(
                publishManifest,
                publishPath,
                intermediateOutputPath);
        }

        [Fact]
        public void Build_WithExternalProjectReference_UpdatesAssets()
        {
            var testAsset = "RazorAppWithP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset)
                .WithProjectChanges((name, project) =>
                {
                    if (Path.GetFileName(name).Equals("ClassLibrary.csproj", StringComparison.Ordinal))
                    {
                        var sdkAttribute = project.Root.Attribute("Sdk");
                        if (sdkAttribute == null)
                        {
                            sdkAttribute = new XAttribute("Sdk", "Microsoft.NET.Sdk");
                            project.Root.Add(sdkAttribute);
                        }
                        else
                        {
                            sdkAttribute.Value = "Microsoft.NET.Sdk";
                        }
                        project.Root.AddFirst(new XElement("Import", new XAttribute("Project", "ExternalStaticAssets.targets")));

                        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Microsoft.NET.Sdk.StaticWebAssets.Tests.content.ExternalStaticAssets.targets");
                        using var destination = File.OpenWrite(Path.Combine(Path.GetDirectoryName(name), "ExternalStaticAssets.targets"));
                        stream.CopyTo(destination);
                    }

                    if (Path.GetFileName(name).Equals("AppWithP2PReference.csproj", StringComparison.Ordinal))
                    {
                        project.Root.AddFirst(new XElement("ItemGroup",
                            new XElement(
                                "StaticWebAssetFingerprintInferenceExpression",
                                new XAttribute("Include", "Version"),
                                new XAttribute("Pattern", ".*(?<fingerprint>v\\d{1})\\.js$"))));
                    }
                });

            var build = CreateBuildCommand(ProjectDirectory, "AppWithP2PReference");
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var buildPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the build manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, LoadBuildManifest());

            AssertBuildAssets(
                manifest,
                buildPath,
                intermediateOutputPath);
        }

        [Fact]
        public void Build_DoesNotFailToCompress_TwoAssetsWith_TheSameContent()
        {
            var expectedManifest = LoadBuildManifest();
            var testAsset = "RazorComponentApp";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset)
                .WithProjectChanges(document =>
                {
                    document.Root.AddFirst(new XElement("ItemGroup",
                        new XElement("Content",
                            new XAttribute("Update", "wwwroot\\file.build.txt"),
                            new XAttribute("TargetPath", "wwwroot\\file.txt"),
                            new XAttribute("CopyToPublishDirectory", "Never")),
                        new XElement("Content",
                            new XAttribute("Update", "wwwroot\\file.publish.txt"),
                            new XAttribute("TargetPath", "wwwroot\\file.txt"),
                            new XAttribute("CopyToOutputDirectory", "Never"))));
                });

            Directory.CreateDirectory(Path.Combine(ProjectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(ProjectDirectory.Path, "wwwroot", "file.build.txt"), "file1");
            File.WriteAllText(Path.Combine(ProjectDirectory.Path, "wwwroot", "file.publish.txt"), "file1");

            var build = CreateBuildCommand(ProjectDirectory);
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(manifest, expectedManifest);

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();

            var manifest1 = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));
            AssertManifest(manifest1, expectedManifest);
            AssertBuildAssets(manifest1, outputPath, intermediateOutputPath);
        }

        [Fact]
        public void Build_AdditionalStaticWebAssetsBasePath_IncludesExternalContentRoots()
        {
            var testAsset = "RazorComponentApp";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            // Create an additional folder outside wwwroot to serve as additional content root
            var additionalContentRoot = Path.Combine(ProjectDirectory.Path, "AdditionalAssets");
            Directory.CreateDirectory(additionalContentRoot);
            File.WriteAllText(Path.Combine(additionalContentRoot, "extra.js"), "console.log('extra');");
            File.WriteAllText(Path.Combine(additionalContentRoot, "extra.css"), ".extra { color: red; }");

            // Create a subdirectory with more assets
            var subDir = Path.Combine(additionalContentRoot, "sub");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(subDir, "nested.txt"), "nested content");

            var build = CreateBuildCommand(ProjectDirectory);

            // Pass the AdditionalStaticWebAssetsBasePath property with format: content-root,base-path
            // Quote the entire value to prevent MSBuild from interpreting the comma as a separator
            var additionalAssetsProperty = $"\"{additionalContentRoot},_content/AdditionalAssets\"";
            ExecuteCommand(build, $"/p:AdditionalStaticWebAssetsBasePath={additionalAssetsProperty}").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // Verify the manifest file exists
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();

            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));

            // Verify the additional assets are in the manifest (only count Primary assets, not compressed alternatives)
            var additionalAssets = manifest.Assets.Where(a =>
                a.AssetRole == "Primary" &&
                (a.RelativePath.Contains("extra.js") ||
                a.RelativePath.Contains("extra.css") ||
                a.RelativePath.Contains("nested.txt"))).ToArray();

            additionalAssets.Should().HaveCount(3, "All additional assets should be included in the manifest");

            // Verify the base path is correct
            foreach (var asset in additionalAssets)
            {
                asset.BasePath.Should().Be("_content/AdditionalAssets");
            }

            // Verify discovery patterns include the additional content root
            var discoveryPattern = manifest.DiscoveryPatterns.FirstOrDefault(p =>
                p.BasePath == "_content/AdditionalAssets");
            discoveryPattern.Should().NotBeNull("Discovery pattern for additional content root should exist");
        }

        [Fact]
        public void Build_AdditionalStaticWebAssetsBasePath_MultipleContentRoots()
        {
            var testAsset = "RazorComponentApp";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            // Create first additional folder
            var additionalContentRoot1 = Path.Combine(ProjectDirectory.Path, "AdditionalAssets1");
            Directory.CreateDirectory(additionalContentRoot1);
            File.WriteAllText(Path.Combine(additionalContentRoot1, "file1.js"), "console.log('file1');");

            // Create second additional folder
            var additionalContentRoot2 = Path.Combine(ProjectDirectory.Path, "AdditionalAssets2");
            Directory.CreateDirectory(additionalContentRoot2);
            File.WriteAllText(Path.Combine(additionalContentRoot2, "file2.js"), "console.log('file2');");

            var build = CreateBuildCommand(ProjectDirectory);

            // Pass multiple content roots separated by semicolons
            // Quote the entire value to prevent MSBuild from interpreting commas/semicolons as separators
            var additionalAssetsProperty = $"\"{additionalContentRoot1},_content/Assets1;{additionalContentRoot2},_content/Assets2\"";
            ExecuteCommand(build, $"/p:AdditionalStaticWebAssetsBasePath={additionalAssetsProperty}").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();

            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));

            // Verify assets from both content roots are in the manifest (only count Primary assets)
            var asset1 = manifest.Assets.FirstOrDefault(a => a.AssetRole == "Primary" && a.RelativePath.Contains("file1.js"));
            var asset2 = manifest.Assets.FirstOrDefault(a => a.AssetRole == "Primary" && a.RelativePath.Contains("file2.js"));

            asset1.Should().NotBeNull("Asset from first additional content root should exist");
            asset1!.BasePath.Should().Be("_content/Assets1");

            asset2.Should().NotBeNull("Asset from second additional content root should exist");
            asset2!.BasePath.Should().Be("_content/Assets2");

            // Verify discovery patterns for both content roots
            manifest.DiscoveryPatterns.Should().Contain(p => p.BasePath == "_content/Assets1");
            manifest.DiscoveryPatterns.Should().Contain(p => p.BasePath == "_content/Assets2");
        }

        [Fact]
        public void Publish_AdditionalStaticWebAssetsBasePath_IncludesExternalContentRoots()
        {
            var testAsset = "RazorComponentApp";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            // Create an additional folder outside wwwroot to serve as additional content root
            var additionalContentRoot = Path.Combine(ProjectDirectory.Path, "AdditionalAssets");
            Directory.CreateDirectory(additionalContentRoot);
            File.WriteAllText(Path.Combine(additionalContentRoot, "publish-extra.js"), "console.log('publish-extra');");

            var publish = CreatePublishCommand(ProjectDirectory);

            // Pass the AdditionalStaticWebAssetsBasePath property
            // Quote the entire value to prevent MSBuild from interpreting the comma as a separator
            var additionalAssetsProperty = $"\"{additionalContentRoot},_content/AdditionalAssets\"";
            ExecuteCommand(publish, $"/p:AdditionalStaticWebAssetsBasePath={additionalAssetsProperty}").Should().Pass();

            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var publishPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // Verify the build manifest file exists and includes the additional assets
            var buildPath = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(buildPath).Should().Exist();

            var buildManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(buildPath));

            // Verify the additional asset is in the build manifest (only count Primary assets)
            var additionalAsset = buildManifest.Assets.FirstOrDefault(a =>
                a.AssetRole == "Primary" && a.RelativePath.Contains("publish-extra.js"));

            additionalAsset.Should().NotBeNull("Additional asset should be included in the build manifest");
            additionalAsset!.BasePath.Should().Be("_content/AdditionalAssets");

            // Verify the publish manifest file exists
            var publishManifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(publishManifestPath).Should().Exist();

            var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(publishManifestPath));

            // Verify the additional asset is in the publish manifest
            var publishAsset = publishManifest.Assets.FirstOrDefault(a =>
                a.AssetRole == "Primary" && a.RelativePath.Contains("publish-extra.js"));

            publishAsset.Should().NotBeNull("Additional asset should be included in the publish manifest");
            publishAsset!.BasePath.Should().Be("_content/AdditionalAssets");
        }
    }

    public class StaticWebAssetsAppWithPackagesIntegrationTest(ITestOutputHelper log)
        : IsolatedNuGetPackageFolderAspNetSdkBaselineTest(log, nameof(StaticWebAssetsAppWithPackagesIntegrationTest))
    {
        [Fact]
        public void Build_Fails_WhenConflictingAssetsFoundBetweenAStaticWebAssetAndAFileInTheWebRootFolder()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            Directory.CreateDirectory(Path.Combine(ProjectDirectory.Path, "AppWithPackageAndP2PReference", "wwwroot", "_content", "ClassLibrary", "js"));
            File.WriteAllText(Path.Combine(ProjectDirectory.Path, "AppWithPackageAndP2PReference", "wwwroot", "_content", "ClassLibrary", "js", "project-transitive-dep.js"), "console.log('transitive-dep');");

            EnsureLocalPackagesExists();

            var restore = CreateRestoreCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            ExecuteCommand(restore).Should().Pass();

            var build = CreateBuildCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            ExecuteCommand(build).Should().Fail();
        }

        [Fact]
        public void BuildProjectWithReferences_DeployOnBuild_GeneratesPublishJsonManifestAndCopiesPublishAssets()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            EnsureLocalPackagesExists();

            var restore = CreateRestoreCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            ExecuteCommand(restore).Should().Pass();

            var build = CreateBuildCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            build.Execute("/p:DeployOnBuild=true").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();

            AssertManifest(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path)),
                LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "AppWithPackageAndP2PReference.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();

            // GenerateStaticWebAssetsManifest should generate the publish manifest file.
            var intermediatePublishManifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath));
            AssertManifest(publishManifest, LoadPublishManifest());

            AssertPublishAssets(
                publishManifest,
                Path.Combine(outputPath, "publish"),
                intermediateOutputPath);
        }

        [Fact]
        public void BuildProjectWithReferences_GeneratesJsonManifestAndCopiesItToOutputFolder()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

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
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
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

        [Fact]
        public void BuildProjectWithReferences_NoDependencies_GeneratesJsonManifestAndCopiesItToOutputFolder()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

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
            AssertManifest(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path)),
                LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "AppWithPackageAndP2PReference.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();
            var manifestContents = File.ReadAllText(finalPath);
            var initialManifest = StaticWebAssetsManifest.FromJsonString(File.ReadAllText(path));
            AssertManifest(
                initialManifest,
                LoadBuildManifest());

            // Second build
            var secondBuild = CreateBuildCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            ExecuteCommand(secondBuild,"/p:BuildProjectReferences=false").Should().Pass();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            new FileInfo(path).Should().Exist();
            var manifestNoDeps = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(
                manifestNoDeps,
                LoadBuildManifest("NoDependencies"),
                "NoDependencies");

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            new FileInfo(finalPath).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));
            AssertManifest(
                manifest,
                LoadBuildManifest("NoDependencies"),
                "NoDependencies");

            AssertBuildAssets(
                manifest,
                outputPath,
                intermediateOutputPath,
                "NoDependencies");

            // Check that the two manifests are the same
            manifestContents.Should().Be(File.ReadAllText(finalPath));
        }

        [Fact]
        public void PublishProjectWithReferences_GeneratesPublishJsonManifestAndCopiesPublishAssets()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

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
        public void PublishProjectWithReferences_PublishSingleFile_GeneratesPublishJsonManifestAndCopiesPublishAssets()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            EnsureLocalPackagesExists();

            var restore = CreateRestoreCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            ExecuteCommand(restore).Should().Pass();

            var publish = CreatePublishCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            ExecuteCommand(publish, "/p:PublishSingleFile=true", $"/p:RuntimeIdentifier={RuntimeInformation.RuntimeIdentifier}").Should().Pass();

            var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug", RuntimeInformation.RuntimeIdentifier).ToString();
            var publishPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            new FileInfo(path).Should().Exist();
            AssertManifest(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path)),
                LoadBuildManifest(),
                runtimeIdentifier: RuntimeInformation.RuntimeIdentifier);

            // GenerateStaticWebAssetsManifest should not copy the file to the output folder.
            var finalPath = Path.Combine(publishPath, "AppWithPackageAndP2PReference.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().NotExist();

            // GenerateStaticWebAssetsPublishManifest should generate the publish manifest file.
            var intermediatePublishManifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath));
            AssertManifest(publishManifest, LoadPublishManifest(), runtimeIdentifier: RuntimeInformation.RuntimeIdentifier);

            AssertPublishAssets(
                publishManifest,
                publishPath,
                intermediateOutputPath);
        }

        [Fact]
        public void PublishProjectWithReferences_NoBuild_GeneratesPublishJsonManifestAndCopiesPublishAssets()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            EnsureLocalPackagesExists();

            var restore = CreateRestoreCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            ExecuteCommand(restore).Should().Pass();

            var build = CreateBuildCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest file.
            var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
            var objManifestFile = new FileInfo(path);
            objManifestFile.Should().Exist();
            var objManifestFileTimeStamp = objManifestFile.LastWriteTimeUtc;

            AssertManifest(
                StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path)),
                LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "AppWithPackageAndP2PReference.staticwebassets.runtime.json");
            var binManifestFile = new FileInfo(finalPath);
            binManifestFile.Should().Exist();
            var binManifestTimeStamp = binManifestFile.LastWriteTimeUtc;

            AssertManifest(
                StaticWebAssetsManifest.FromJsonString(File.ReadAllText(path)),
                LoadBuildManifest());

            // Publish no build
            var publish = CreatePublishCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            var publishResult = ExecuteCommand(publish, "/p:NoBuild=true", "/p:ErrorOnDuplicatePublishOutputFiles=false");
            var publishPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            publishResult.Should().Pass();

            new FileInfo(path).LastWriteTimeUtc.Should().Be(objManifestFileTimeStamp);

            var seconbObjManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
            AssertManifest(seconbObjManifest, LoadBuildManifest());

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var seconBinManifestPath = Path.Combine(outputPath, "AppWithPackageAndP2PReference.staticwebassets.runtime.json");
            var secondBinManifestFile = new FileInfo(seconBinManifestPath);
            secondBinManifestFile.Should().Exist();

            secondBinManifestFile.LastWriteTimeUtc.Should().Be(binManifestTimeStamp);

            // GenerateStaticWebAssetsManifest should generate the publish manifest file.
            var intermediatePublishManifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(path).Should().Exist();
            var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(intermediatePublishManifestPath));
            AssertManifest(publishManifest, LoadPublishManifest());

            AssertPublishAssets(
                publishManifest,
                publishPath,
            intermediateOutputPath);
        }

        [Fact]
        public void PublishProjectWithReferences_AppendTargetFrameworkToOutputPathFalse_GeneratesPublishJsonManifestAndCopiesPublishAssets()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            EnsureLocalPackagesExists();

            var restore = CreateRestoreCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            ExecuteCommand(restore).Should().Pass();

            var publish = CreatePublishCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            ExecuteCommand(publish, "/p:AppendTargetFrameworkToOutputPath=false").Should().Pass();

            //  Hard code output paths here to account for AppendTargetFrameworkToOutputPath=false
            var intermediateOutputPath = Path.Combine(ProjectDirectory.Path, "AppWithPackageAndP2PReference", "obj", "Debug");
            var publishPath = Path.Combine(ProjectDirectory.Path, "AppWithPackageAndP2PReference", "bin", "Debug", "publish");

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
    }
}
