// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Reflection;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

public class TypeScriptIntegrationTest : IsolatedNuGetPackageFolderAspNetSdkBaselineTest
{
    public string TypeScriptMSBuildPackageVersion { get; }

    public TypeScriptIntegrationTest(ITestOutputHelper log) : base(log, nameof(TypeScriptIntegrationTest))
    {
        var testAssemblyMetadata = TestAssembly.GetCustomAttributes<AssemblyMetadataAttribute>();
        TypeScriptMSBuildPackageVersion = testAssemblyMetadata.SingleOrDefault(a => a.Key == "MicrosoftTypeScriptMSBuildPackageVersion")?.Value ?? "5.9.3";
    }

    [Fact]
    public void Build_RegistersTypeScriptOutputsAsStaticWebAssets()
    {
        var testAsset = "RazorClassLibrary";
        ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

        SetupTypeScriptProject(ProjectDirectory);

        var build = CreateBuildCommand(ProjectDirectory);
        ExecuteCommand(build).Should().Pass();

        var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

        // Verify the TypeScript manifest was created
        var manifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.typescript.files.txt");
        new FileInfo(manifestPath).Should().Exist();
        // Verify the static web assets manifest contains the TypeScript outputs
        var finalPath = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
        var buildManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(finalPath));

        buildManifest.Should().NotBeNull();
        buildManifest.Assets.Should().Contain(a => a.RelativePath.EndsWith("app.js"));
    }

    [Fact]
    public void Build_TypeScriptOutputsAreProperlyCompressed()
    {
        var testAsset = "RazorClassLibrary";
        ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

        SetupTypeScriptProject(ProjectDirectory);

        // Enable compression
        ProjectDirectory.WithProjectChanges(document =>
        {
            var propertyGroup = document.Root.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "PropertyGroup");
            propertyGroup?.Add(new XElement("CompressionEnabled", "true"));
        });

        var build = CreateBuildCommand(ProjectDirectory);
        ExecuteCommand(build).Should().Pass();

        var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

        // Verify compression was applied
        var finalPath = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
        var buildManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(finalPath));

        // Should have compressed versions
        buildManifest.Assets.Should().Contain(a => a.RelativePath.EndsWith("app.js.gz") || a.RelativePath.EndsWith("app.js.br"));
    }

    [Fact]
    public void Rebuild_SucceedsWithTypeScriptOutputs()
    {
        var testAsset = "RazorClassLibrary";
        ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

        SetupTypeScriptProject(ProjectDirectory);

        // First build
        var build = CreateBuildCommand(ProjectDirectory);
        ExecuteCommand(build).Should().Pass();

        // Rebuild (clean + build)
        var rebuild = CreateRebuildCommand(ProjectDirectory);
        var rebuildResult = ExecuteCommand(rebuild);
        rebuildResult.Should().Pass();

        var intermediateOutputPath = rebuild.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

        // Verify static web assets are still correctly registered after rebuild
        var finalPath = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
        var buildManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(finalPath));

        buildManifest.Should().NotBeNull();
        buildManifest.Assets.Should().Contain(a => a.RelativePath.EndsWith("app.js"));
    }

    [Fact]
    public async Task Build_IncrementalBuild_WorksCorrectly()
    {
        var testAsset = "RazorClassLibrary";
        ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

        SetupTypeScriptProject(ProjectDirectory);

        // First build
        var build = CreateBuildCommand(ProjectDirectory);
        ExecuteCommand(build).Should().Pass();

        var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
        var manifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.typescript.files.txt");

        var firstBuildManifestTime = File.GetLastWriteTime(manifestPath);

        // Wait a bit and do incremental build
        await Task.Delay(100);

        build = CreateBuildCommand(ProjectDirectory);
        ExecuteCommand(build).Should().Pass();

        // Manifest should not have changed (WriteOnlyWhenDifferent)
        var secondBuildManifestTime = File.GetLastWriteTime(manifestPath);
        secondBuildManifestTime.Should().Be(firstBuildManifestTime);
    }

    [Fact]
    public void Build_ModifyTypeScriptFile_UpdatesStaticWebAssets()
    {
        var testAsset = "RazorClassLibrary";
        ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

        SetupTypeScriptProject(ProjectDirectory);

        // First build
        var build = CreateBuildCommand(ProjectDirectory);
        ExecuteCommand(build).Should().Pass();

        // Modify the TypeScript file
        var tsFilePath = Path.Combine(ProjectDirectory.TestRoot, "Scripts", "app.ts");
        File.WriteAllText(tsFilePath, "console.log('Modified!');");

        // Rebuild
        build = CreateBuildCommand(ProjectDirectory);
        ExecuteCommand(build).Should().Pass();

        var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

        // Verify the output was updated
        var jsFilePath = Path.Combine(ProjectDirectory.TestRoot, "wwwroot", "app.js");
        new FileInfo(jsFilePath).Should().Exist();
        File.ReadAllText(jsFilePath).Should().Contain("Modified");
    }

    [Fact]
    public void Publish_IncludesTypeScriptOutputs()
    {
        var testAsset = "RazorClassLibrary";
        ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

        SetupTypeScriptProject(ProjectDirectory);

        var publish = CreatePublishCommand(ProjectDirectory);
        ExecuteCommand(publish).Should().Pass();

        var intermediateOutputPath = publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

        // Verify publish manifest includes TypeScript outputs
        var finalPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
        var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(finalPath));

        publishManifest.Should().NotBeNull();
        publishManifest.Assets.Should().Contain(a => a.RelativePath.EndsWith("app.js"));
    }

    [Fact]
    public void Clean_ThenBuild_SucceedsWithTypeScriptOutputs()
    {
        var testAsset = "RazorClassLibrary";
        ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

        SetupTypeScriptProject(ProjectDirectory);

        // First build
        var build = CreateBuildCommand(ProjectDirectory);
        ExecuteCommand(build).Should().Pass();

        // Clean
        var clean = new CleanCommand(Log, ProjectDirectory.Path);
        clean.WithWorkingDirectory(ProjectDirectory.TestRoot);
        ExecuteCommand(clean).Should().Pass();

        // Build again
        build = CreateBuildCommand(ProjectDirectory);
        var result = ExecuteCommand(build);
        result.Should().Pass();

        var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

        // Verify static web assets are correctly registered
        var finalPath = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
        var buildManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(finalPath));

        buildManifest.Should().NotBeNull();
        buildManifest.Assets.Should().Contain(a => a.RelativePath.EndsWith("app.js"));
    }

    [Fact]
    public void Build_TypeScriptDisabled_DoesNotRegisterAssets()
    {
        var testAsset = "RazorClassLibrary";
        ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

        SetupTypeScriptProject(ProjectDirectory, enableTypeScript: false);

        var build = CreateBuildCommand(ProjectDirectory);
        ExecuteCommand(build).Should().Pass();

        var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();

        // TypeScript manifest should not exist when disabled
        var manifestPath = Path.Combine(intermediateOutputPath, "staticwebassets.typescript.files.txt");
        new FileInfo(manifestPath).Should().NotExist();
    }

    /// <summary>
    /// Sets up a test project with TypeScript configuration using the
    /// Microsoft.TypeScript.MSBuild NuGet package.
    /// </summary>
    private void SetupTypeScriptProject(TestAsset projectDirectory, bool enableTypeScript = true)
    {
        // Create Scripts folder and TypeScript file
        var scriptsDir = Path.Combine(projectDirectory.TestRoot, "Scripts");
        Directory.CreateDirectory(scriptsDir);
        File.WriteAllText(Path.Combine(scriptsDir, "app.ts"), "console.log('Hello from TypeScript!');");

        // Create tsconfig.json that outputs to wwwroot
        var tsconfig = @"{
  ""compilerOptions"": {
    ""target"": ""ES2020"",
    ""module"": ""ES2020"",
    ""outDir"": ""wwwroot"",
    ""rootDir"": ""Scripts"",
    ""strict"": true,
    ""sourceMap"": true
  },
  ""include"": [""Scripts/**/*""]
}";
        File.WriteAllText(Path.Combine(projectDirectory.TestRoot, "tsconfig.json"), tsconfig);

        // Ensure wwwroot exists
        Directory.CreateDirectory(Path.Combine(projectDirectory.TestRoot, "wwwroot"));

        // Modify project to add the TypeScript MSBuild package
        projectDirectory.WithProjectChanges(document =>
        {
            var root = document.Root;
            var ns = root.Name.Namespace;

            // Add PropertyGroup with TypeScript output directory
            var propertyGroup = root.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "PropertyGroup");

            if (propertyGroup != null)
            {
                propertyGroup.Add(new XElement(ns + "TypeScriptOutDir", "wwwroot"));
            }

            if (enableTypeScript)
            {
                // Add Microsoft.TypeScript.MSBuild package reference
                var itemGroup = new XElement(ns + "ItemGroup",
                    new XElement(ns + "PackageReference",
                        new XAttribute("Include", "Microsoft.TypeScript.MSBuild"),
                        new XAttribute("Version", TypeScriptMSBuildPackageVersion),
                        new XElement(ns + "PrivateAssets", "all"),
                        new XElement(ns + "IncludeAssets", "runtime; build; native; contentfiles; analyzers; buildtransitive")
                    )
                );
                root.Add(itemGroup);
            }
        });
    }
}
