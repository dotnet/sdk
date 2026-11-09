// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Utilities;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

[TestClass]
public class StaticWebAssetsDesignTimeTest : AspNetSdkBaselineTest
{
#if DEBUG
    public const string Configuration = "Debug";
#else
    public const string Configuration = "Release";
#endif

    [TestMethod]
    public void StaticWebAssetFingerprintPattern_ItemsAreNotVisibleByDefault()
    {
        var testAsset = CreateAspNetSdkTestAsset("VanillaWasm", identifier: nameof(StaticWebAssetFingerprintPattern_ItemsAreNotVisibleByDefault))
            .WithProjectChanges(project =>
            {
                project.Root.Add(
                    new XElement("ItemGroup",
                        new XElement("StaticWebAssetFingerprintPattern",
                            new XAttribute("Include", "JS"),
                            new XAttribute("Pattern", "*.js"),
                            new XAttribute("Expression", "#[.{fingerprint}]!"))));
            });

        var msbuild = new MSBuildCommand(testAsset, string.Empty);
        var result = msbuild.Execute("-getItem:StaticWebAssetFingerprintPattern", "-nologo");

        result.Should().Pass();

        using var output = JsonDocument.Parse(result.StdOut);
        var fingerprintPattern = output.RootElement
            .GetProperty("Items")
            .GetProperty("StaticWebAssetFingerprintPattern")
            .EnumerateArray()
            .Single(item => item.GetProperty("Identity").GetString() == "JS");

        fingerprintPattern.GetProperty("Visible").GetString().Should().Be("false");
    }

    [TestMethod]
    public void ResolveWebAssemblyProjectReferences_ReturnsReferencedWebAssemblyProject()
    {
        var testAsset = CreateAspNetSdkTestAsset("BlazorHosted");
        var msbuild = new MSBuildCommand(
            testAsset,
            "ResolveWebAssemblyProjectReferences",
            "blazorhosted");
        msbuild.WithWorkingDirectory(testAsset.TestRoot);

        var result = msbuild.Execute("-getItem:WebAssemblyProjectReference", "-nologo");
        result.Should().Pass();

        using var output = JsonDocument.Parse(result.StdOut);
        var projectReferences = output.RootElement
            .GetProperty("Items")
            .GetProperty("WebAssemblyProjectReference")
            .EnumerateArray()
            .Select(item => item.GetProperty("Identity").GetString())
            .ToArray();

        projectReferences.Should().ContainSingle().Which.Should().Be(
            Path.GetFullPath(Path.Combine(testAsset.TestRoot, "blazorwasm", "blazorwasm.csproj")));
    }

    [TestMethod]
    [DataRow(false, true)]
    [DataRow(true, true)]
    [DataRow(false, false)]
    [DataRow(true, false)]
    public void CollectStaticWebAssetInputsDesignTime_TracksOnlyExistingServiceWorkerSources(bool outsideWebRoot, bool sourceExists)
    {
        var serviceWorkerPath = outsideWebRoot ? "custom-service-worker.js" : Path.Combine("wwwroot", "serviceworkers", "my-service-worker.js");
        var testAsset = CreateAspNetSdkTestAsset(outsideWebRoot ? "VanillaWasm" : "BlazorHosted", identifier: $"{outsideWebRoot}_{sourceExists}");
        if (outsideWebRoot)
        {
            testAsset = testAsset.WithProjectChanges(project =>
            {
                project.Root.Add(
                    new XElement("PropertyGroup",
                        new XElement("ServiceWorkerAssetsManifest", "service-worker-assets.js")),
                    new XElement("ItemGroup",
                        new XElement("ServiceWorker",
                            new XAttribute("Include", serviceWorkerPath),
                            new XAttribute("PublishedContent", Path.Combine("wwwroot", "service-worker.published.js"))),
                        new XElement("UpToDateCheckInput",
                            new XAttribute("Include", "Program.cs"),
                            new XAttribute("Set", "StaticWebAssets"))));
            });
        }

        var projectPath = outsideWebRoot ? testAsset.Path : Path.Combine(testAsset.Path, "blazorwasm");
        var sourcePath = Path.GetFullPath(Path.Combine(projectPath, serviceWorkerPath));
        var publishedSourcePath = outsideWebRoot
            ? Path.Combine(projectPath, "wwwroot", "service-worker.published.js")
            : Path.Combine(projectPath, "wwwroot", "serviceworkers", "my-prod-service-worker.js");
        if (outsideWebRoot)
        {
            File.WriteAllText(sourcePath, "// Development service worker");
            File.WriteAllText(publishedSourcePath, "// Published service worker");
        }

        if (!sourceExists)
        {
            File.Delete(sourcePath);
        }

        File.Exists(sourcePath).Should().Be(sourceExists);
        File.Exists(publishedSourcePath).Should().BeTrue();

        var msbuild = CreateMSBuildCommand(
            testAsset,
            outsideWebRoot ? string.Empty : "blazorwasm",
            "ResolveStaticWebAssetsConfiguration;ResolveProjectStaticWebAssets;CollectStaticWebAssetInputsDesignTime");
        var result = msbuild.Execute(
            "/p:DesignTimeBuild=true",
            "/p:BuildingInsideVisualStudio=true",
            "-getItem:UpToDateCheckInput",
            "-nologo");
        result.Should().Pass();

        using var output = JsonDocument.Parse(result.StdOut);
        var inputs = output.RootElement.GetProperty("Items").GetProperty("UpToDateCheckInput")
            .EnumerateArray().Select(item => item.GetProperty("FullPath").GetString()).ToArray();

        if (sourceExists)
        {
            inputs.Should().Contain(sourcePath);
        }
        else
        {
            inputs.Should().NotContain(sourcePath);
        }

        inputs.Should().Contain(outsideWebRoot
            ? Path.Combine(projectPath, "wwwroot", "main.js")
            : Path.Combine(projectPath, "wwwroot", "css", "app.css"));
        if (outsideWebRoot)
        {
            inputs.Should().Contain(Path.Combine(projectPath, "Program.cs"));
        }

        inputs.Should().NotContain(publishedSourcePath);
    }

    [TestMethod]
    public void CollectStaticWebAssetInputsDesignTime_WithoutServiceWorkerPreservesStaticAssets()
    {
        var testAsset = CreateAspNetSdkTestAsset("VanillaWasm");
        var msbuild = CreateMSBuildCommand(
            testAsset,
            string.Empty,
            "ResolveStaticWebAssetsConfiguration;ResolveProjectStaticWebAssets;CollectStaticWebAssetInputsDesignTime");
        var result = msbuild.Execute(
            "/p:DesignTimeBuild=true",
            "/p:BuildingInsideVisualStudio=true",
            "-getItem:UpToDateCheckInput",
            "-nologo");
        result.Should().Pass();

        using var output = JsonDocument.Parse(result.StdOut);
        var inputs = output.RootElement.GetProperty("Items").GetProperty("UpToDateCheckInput")
            .EnumerateArray().Select(item => item.GetProperty("FullPath").GetString()).ToArray();

        inputs.Should().Contain(Path.Combine(testAsset.Path, "wwwroot", "main.js"));
        inputs.Should().Contain(Path.Combine(testAsset.Path, "wwwroot", "index.html"));
        inputs.Should().NotContain(Path.Combine(testAsset.Path, "wwwroot", "service-worker.js"));
    }

    [TestMethod]
    public void CollectUpToDateCheckInputOutputsDesignTime_ReportsAddedFiles()
    {
        // Arrange
        var testAsset = "RazorAppWithP2PReference";
        ProjectDirectory = AddIntrospection(CreateAspNetSdkTestAsset(testAsset));

        var build = CreateBuildCommand(ProjectDirectory, "ClassLibrary");

        build.Execute("/p:DesignTimeBuild=true", "/p:BuildingInsideVisualStudio=true", "/bl:build.binlog").Should().Pass();

        File.WriteAllText(Path.Combine(ProjectDirectory.Path, "ClassLibrary", "wwwroot", "js", "file.js"), "New File");

        var msbuild = CreateMSBuildCommand(
            ProjectDirectory,
            "ClassLibrary",
            "ResolveStaticWebAssetsConfiguration;ResolveProjectStaticWebAssets;CollectStaticWebAssetInputsDesignTime;CollectStaticWebAssetOutputsDesignTime");

        msbuild.ExecuteWithoutRestore("/p:DesignTimeBuild=true", "/p:BuildingInsideVisualStudio=true", "/bl:design.binlog").Should().Pass();

        // Check the contents of the input and output files
        var inputFilePath = Path.Combine(build.GetIntermediateDirectory().FullName, "StaticWebAssetsUTDCInput.txt");
        new FileInfo(inputFilePath).Should().Exist();
        var inputFiles = File.ReadAllLines(inputFilePath);
        inputFiles.Should().HaveCount(3);
        inputFiles.Should().Contain(Path.Combine(ProjectDirectory.Path, "ClassLibrary", "wwwroot", "js", "file.js"));
        inputFiles.Should().Contain(Path.Combine(ProjectDirectory.Path, "ClassLibrary", "wwwroot", "js", "project-transitive-dep.js"));
        inputFiles.Should().Contain(Path.Combine(ProjectDirectory.Path, "ClassLibrary", "wwwroot", "js", "project-transitive-dep.v4.js"));

        var outputFilePath = Path.Combine(build.GetIntermediateDirectory().FullName, "StaticWebAssetsUTDCOutput.txt");
        new FileInfo(outputFilePath).Should().Exist();
        var outputFiles = File.ReadAllLines(outputFilePath);
        outputFiles.Should().ContainSingle();
        Path.GetFileName(outputFiles[0]).Should().Be("staticwebassets.build.json");
    }

    [TestMethod]
    public void CollectUpToDateCheckInputOutputsDesignTime_ReportsRemovedFiles_Once()
    {
        // Arrange
        var testAsset = "RazorAppWithP2PReference";
        ProjectDirectory = AddIntrospection(CreateAspNetSdkTestAsset(testAsset));

        var build = CreateBuildCommand(ProjectDirectory, "ClassLibrary");

        build.Execute("/p:DesignTimeBuild=true", "/p:BuildingInsideVisualStudio=true", "/bl:build.binlog").Should().Pass();

        File.Delete(Path.Combine(ProjectDirectory.Path, "ClassLibrary", "wwwroot", "js", "project-transitive-dep.js"));

        var msbuild = CreateMSBuildCommand(
            ProjectDirectory,
            "ClassLibrary",
            "ResolveStaticWebAssetsConfiguration;ResolveProjectStaticWebAssets;CollectStaticWebAssetInputsDesignTime;CollectStaticWebAssetOutputsDesignTime");

        msbuild.ExecuteWithoutRestore("/p:DesignTimeBuild=true", "/p:BuildingInsideVisualStudio=true", "/bl:design.binlog").Should().Pass();

        // Check the contents of the input and output files
        var inputFilePath = Path.Combine(build.GetIntermediateDirectory().FullName, "StaticWebAssetsUTDCInput.txt");
        new FileInfo(inputFilePath).Should().Exist();
        var inputFiles = File.ReadAllLines(inputFilePath);
        inputFiles.Should().HaveCount(2);
        inputFiles.Should().Contain(Path.Combine(build.GetIntermediateDirectory().FullName, "staticwebassets.removed.txt"));
        inputFiles.Should().Contain(Path.Combine(ProjectDirectory.Path, "ClassLibrary", "wwwroot", "js", "project-transitive-dep.v4.js"));

        var outputFilePath = Path.Combine(build.GetIntermediateDirectory().FullName, "StaticWebAssetsUTDCOutput.txt");
        new FileInfo(outputFilePath).Should().Exist();
        var outputFiles = File.ReadAllLines(outputFilePath);
        outputFiles.Should().ContainSingle();
        Path.GetFileName(outputFiles[0]).Should().Be("staticwebassets.build.json");
    }

    [TestMethod]
    public void CollectUpToDateCheckInputOutputsDesignTime_IncludesReferencedProjectsManifests()
    {
        // Arrange
        var testAsset = "RazorAppWithP2PReference";
        ProjectDirectory = AddIntrospection(CreateAspNetSdkTestAsset(testAsset));

        var build = CreateBuildCommand(ProjectDirectory, "AppWithP2PReference");

        build.Execute("/bl:build.binlog").Should().Pass();
        build.Execute("/p:DesignTimeBuild=true", "/p:BuildingInsideVisualStudio=true", "/bl:build.binlog").Should().Pass();

        var msbuild = CreateMSBuildCommand(
            ProjectDirectory,
            "AppWithP2PReference",
            "ResolveStaticWebAssetsConfiguration;ResolveProjectStaticWebAssets;CollectStaticWebAssetInputsDesignTime;CollectStaticWebAssetOutputsDesignTime");

        msbuild.ExecuteWithoutRestore("/p:DesignTimeBuild=true", "/p:BuildingInsideVisualStudio=true", "/bl:design.binlog").Should().Pass();

        // Check the contents of the input and output files
        var inputFilePath = Path.Combine(build.GetIntermediateDirectory().FullName, "StaticWebAssetsUTDCInput.txt");
        new FileInfo(inputFilePath).Should().Exist();
        var inputFiles = File.ReadAllLines(inputFilePath);
        inputFiles.Should().HaveCount(1);
        inputFiles.Should().Contain(Path.Combine(ProjectDirectory.Path, "ClassLibrary", "obj", "Debug", DefaultTfm, "staticwebassets.build.json"));

        var outputFilePath = Path.Combine(build.GetIntermediateDirectory().FullName, "StaticWebAssetsUTDCOutput.txt");
        new FileInfo(outputFilePath).Should().Exist();
        var outputFiles = File.ReadAllLines(outputFilePath);
        outputFiles.Should().ContainSingle();
        Path.GetFileName(outputFiles[0]).Should().Be("staticwebassets.build.json");
    }

    private static MSBuildCommand CreateMSBuildCommand(TestAsset testAsset, string relativeProjectPath, string targets)
    {
        return (MSBuildCommand)new MSBuildCommand(testAsset.Log, targets, testAsset.TestRoot, relativeProjectPath)
            .WithWorkingDirectory(testAsset.TestRoot);
    }

    private static TestAsset AddIntrospection(TestAsset testAsset)
    {
        return testAsset
            .WithProjectChanges((name, project) =>
            {
                project.Document.Root.LastNode.AddAfterSelf(
                    XElement.Parse("""
                        <Target Name="ReportInputOutputs" AfterTargets="CollectStaticWebAssetOutputsDesignTime">
                            <ItemGroup>
                                <_StaticWebAssetsUTDCInput Include="@(UpToDateCheckInput)" Condition="'%(UpToDateCheckInput.Set)' == 'StaticWebAssets'" />
                                <_StaticWebAssetsUTDCOutput Include="@(UpToDateCheckOutput)" Condition="'%(UpToDateCheckOutput.Set)' == 'StaticWebAssets'" />
                            </ItemGroup>

                            <WriteLinesToFile Condition="'@(_StaticWebAssetsUTDCInput)' != ''"
                                File="$(IntermediateOutputPath)\StaticWebAssetsUTDCInput.txt"
                                Lines="@(_StaticWebAssetsUTDCInput->'%(FullPath)')"
                                Overwrite="true"
                                Encoding="UTF-8"
                                />

                            <WriteLinesToFile Condition="'@(_StaticWebAssetsUTDCOutput)' != ''"
                                File="$(IntermediateOutputPath)\StaticWebAssetsUTDCOutput.txt"
                                Lines="@(_StaticWebAssetsUTDCOutput->'%(FullPath)')"
                                Overwrite="true"
                                Encoding="UTF-8"
                                />
                        </Target>
                        """
                        ));
            });
    }
}
