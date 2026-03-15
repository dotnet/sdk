// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

public class StaticWebAssetsDesignTimeTest(ITestOutputHelper log) : AspNetSdkBaselineTest(log)
{
#if DEBUG
    public const string Configuration = "Debug";
#else
    public const string Configuration = "Release";
#endif

    [Fact]
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
        outputFiles.Should().HaveCount(2);
        outputFiles.Should().Contain(x => Path.GetFileName(x) == "staticwebassets.build.json");
        outputFiles.Should().Contain(x => Path.GetFileName(x) == "staticwebassets.build.endpoints.json");
    }

    [Fact]
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
        outputFiles.Should().HaveCount(2);
        outputFiles.Should().Contain(x => Path.GetFileName(x) == "staticwebassets.build.json");
        outputFiles.Should().Contain(x => Path.GetFileName(x) == "staticwebassets.build.endpoints.json");
    }

    [Fact]
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
            "ResolveStaticWebAssetsConfiguration;ResolveProjectStaticWebAssets;ResolveReferencedProjectsStaticWebAssetsConfiguration;CollectStaticWebAssetInputsDesignTime;CollectStaticWebAssetOutputsDesignTime");

        msbuild.ExecuteWithoutRestore("/p:DesignTimeBuild=true", "/p:BuildingInsideVisualStudio=true", "/bl:design.binlog").Should().Pass();

        // Check the contents of the input and output files
        var inputFilePath = Path.Combine(build.GetIntermediateDirectory().FullName, "StaticWebAssetsUTDCInput.txt");
        new FileInfo(inputFilePath).Should().Exist();
        var inputFiles = File.ReadAllLines(inputFilePath);
        inputFiles.Should().HaveCount(2);
        inputFiles.Should().Contain(Path.Combine(ProjectDirectory.Path, "ClassLibrary", "obj", "Debug", DefaultTfm, "staticwebassets.build.json"));
        inputFiles.Should().Contain(Path.Combine(ProjectDirectory.Path, "ClassLibrary", "obj", "Debug", DefaultTfm, "staticwebassets.build.endpoints.json"));

        var outputFilePath = Path.Combine(build.GetIntermediateDirectory().FullName, "StaticWebAssetsUTDCOutput.txt");
        new FileInfo(outputFilePath).Should().Exist();
        var outputFiles = File.ReadAllLines(outputFilePath);
        outputFiles.Should().HaveCount(2);
        outputFiles.Should().Contain(x => Path.GetFileName(x) == "staticwebassets.build.json");
        outputFiles.Should().Contain(x => Path.GetFileName(x) == "staticwebassets.build.endpoints.json");
    }

    [Fact]
    public void CollectUpToDateCheckInputOutputsDesignTime_IncludesReferencedProjectsManifests_OnInitialLoad()
    {
        // This test verifies the timing bug fix: referenced project manifest paths must be available
        // in the FUTDC inputs even before a full build has occurred (i.e., on initial VS load).
        // Arrange
        var testAsset = "RazorAppWithP2PReference";
        ProjectDirectory = AddIntrospection(CreateAspNetSdkTestAsset(testAsset));

        var build = CreateBuildCommand(ProjectDirectory, "AppWithP2PReference");

        var msbuild = CreateMSBuildCommand(
            ProjectDirectory,
            "AppWithP2PReference",
            "ResolveStaticWebAssetsConfiguration;ResolveProjectStaticWebAssets;ResolveReferencedProjectsStaticWebAssetsConfiguration;CollectStaticWebAssetInputsDesignTime;CollectStaticWebAssetOutputsDesignTime");

        // Run design-time targets WITHOUT a prior full build to simulate initial VS project load
        msbuild.ExecuteWithoutRestore("/p:DesignTimeBuild=true", "/p:BuildingInsideVisualStudio=true", "/bl:design.binlog").Should().Pass();

        // The referenced project manifest paths must be tracked as inputs, even without a prior build
        var inputFilePath = Path.Combine(build.GetIntermediateDirectory().FullName, "StaticWebAssetsUTDCInput.txt");
        new FileInfo(inputFilePath).Should().Exist();
        var inputFiles = File.ReadAllLines(inputFilePath);
        inputFiles.Should().HaveCount(2);
        inputFiles.Should().Contain(Path.Combine(ProjectDirectory.Path, "ClassLibrary", "obj", "Debug", DefaultTfm, "staticwebassets.build.json"));
        inputFiles.Should().Contain(Path.Combine(ProjectDirectory.Path, "ClassLibrary", "obj", "Debug", DefaultTfm, "staticwebassets.build.endpoints.json"));

        var outputFilePath = Path.Combine(build.GetIntermediateDirectory().FullName, "StaticWebAssetsUTDCOutput.txt");
        new FileInfo(outputFilePath).Should().Exist();
        var outputFiles = File.ReadAllLines(outputFilePath);
        outputFiles.Should().HaveCount(2);
        outputFiles.Should().Contain(x => Path.GetFileName(x) == "staticwebassets.build.json");
        outputFiles.Should().Contain(x => Path.GetFileName(x) == "staticwebassets.build.endpoints.json");
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
