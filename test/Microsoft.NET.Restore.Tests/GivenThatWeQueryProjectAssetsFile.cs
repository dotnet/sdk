// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

namespace Microsoft.NET.Restore.Tests;

[TestClass]
public class GivenThatWeQueryProjectAssetsFile : SdkTest
{
    [TestMethod]
    [CombinatorialData]
    public void The_assets_file_path_is_available_before_and_during_restore(
        bool multiTarget,
        [CombinatorialValues("default", "extensions", "restore-output")] string path)
    {
        var testProject = new TestProject
        {
            Name = "AssetsPath",
            TargetFrameworks = multiTarget
                ? $"{ToolsetInfo.CurrentTargetFramework};netstandard2.1"
                : ToolsetInfo.CurrentTargetFramework
        };

        if (path == "restore-output")
        {
            // RestoreOutputPath controls NuGet's output; ProjectAssetsFile controls the SDK's input.
            testProject.AdditionalProperties["RestoreOutputPath"] = "restore/../restore/";
            testProject.AdditionalProperties["ProjectAssetsFile"] = "$(RestoreOutputPath)project.assets.json";
        }

        var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: $"{multiTarget}_{path}");
        string projectDirectory = Path.Combine(testAsset.TestRoot, testProject.Name);
        string assetsDirectory = path switch
        {
            "extensions" => "custom",
            "restore-output" => "restore",
            _ => "obj"
        };
        string expectedAssetsFile = Path.Combine(projectDirectory, assetsDirectory, "project.assets.json");
        string[] pathArguments = path == "extensions" ? ["-p:MSBuildProjectExtensionsPath=custom/../custom/"] : [];
        string[] queryArguments =
        [
            "--getProperty:ProjectAssetsFile",
            "--getProperty:TargetFrameworks",
            "--getProperty:TargetFramework",
            "-m:1",
            "-nr:false",
            .. pathArguments
        ];

        Directory.Exists(Path.Combine(projectDirectory, assetsDirectory)).Should().BeFalse();
        Query("msbuild", queryArguments);
        File.Exists(expectedAssetsFile).Should().BeFalse();

        Query("restore", queryArguments);
        new FileInfo(expectedAssetsFile).Should().Exist();
        Query("restore", queryArguments);
        Query("msbuild", queryArguments);

        if (multiTarget)
        {
            Query("msbuild", [.. queryArguments, $"-p:TargetFramework={ToolsetInfo.CurrentTargetFramework}"]);
        }

        new DotnetCommand(Log, ["build", "--no-restore", "-m:1", "-nr:false", "-p:UseSharedCompilation=false", .. pathArguments])
            .WithWorkingDirectory(projectDirectory)
            .Execute()
            .Should().Pass();

        foreach (string framework in testProject.TargetFrameworks.Split(';'))
        {
            new FileInfo(Path.Combine(projectDirectory, "bin", "Debug", framework, $"{testProject.Name}.dll"))
                .Should().Exist();
        }

        void Query(string command, string[] arguments)
        {
            var result = new DotnetCommand(Log, [command, .. arguments])
                .WithWorkingDirectory(projectDirectory)
                .Execute();
            result.Should().Pass();

            using var document = JsonDocument.Parse(result.StdOut!);
            var properties = document.RootElement.GetProperty("Properties");
            properties.GetProperty("ProjectAssetsFile").GetString().Should().Be(expectedAssetsFile);
            properties.GetProperty("TargetFrameworks").GetString().Should().Be(multiTarget ? testProject.TargetFrameworks : "");
            properties.GetProperty("TargetFramework").GetString().Should().Be(
                !multiTarget || arguments.Any(a => a.StartsWith("-p:TargetFramework=", StringComparison.Ordinal))
                    ? ToolsetInfo.CurrentTargetFramework
                    : "");
        }
    }

    [TestMethod]
    [CombinatorialData]
    public void An_explicit_assets_file_path_takes_precedence(bool multiTarget, bool globalProperty)
    {
        const string relativeAssetsFile = "custom/../custom/renamed.assets.json";
        var testProject = new TestProject
        {
            Name = "ExplicitAssetsPath",
            TargetFrameworks = multiTarget
                ? $"{ToolsetInfo.CurrentTargetFramework};netstandard2.1"
                : ToolsetInfo.CurrentTargetFramework
        };
        if (!globalProperty)
        {
            testProject.AdditionalProperties["ProjectAssetsFile"] = relativeAssetsFile;
        }

        var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: $"{multiTarget}_{globalProperty}");
        string projectDirectory = Path.Combine(testAsset.TestRoot, testProject.Name);
        string[] arguments = globalProperty ? [$"-p:ProjectAssetsFile={relativeAssetsFile}"] : [];
        var result = new DotnetCommand(Log, ["msbuild", "--getProperty:ProjectAssetsFile", .. arguments])
            .WithWorkingDirectory(projectDirectory)
            .Execute();
        result.Should().Pass();

        // Global properties cannot be reassigned, including by the SDK's normalization.
        result.StdOut!.Trim().Should().Be(globalProperty
            ? relativeAssetsFile
            : Path.Combine(projectDirectory, "custom", "renamed.assets.json"));
    }

    [TestMethod]
    [CombinatorialData]
    public void Imports_before_sdk_targets_can_set_the_assets_file_path(
        bool multiTarget,
        [CombinatorialValues("before-sdk", "directory-targets", "generated-props", "generated-targets")] string import)
    {
        var testProject = new TestProject
        {
            Name = "ImportedAssetsPath",
            TargetFrameworks = multiTarget
                ? $"{ToolsetInfo.CurrentTargetFramework};netstandard2.1"
                : ToolsetInfo.CurrentTargetFramework
        };
        bool generatedImport = import is "generated-props" or "generated-targets";
        if (!generatedImport)
        {
            string property = import == "before-sdk" ? "BeforeMicrosoftNETSdkTargets" : "DirectoryBuildTargetsPath";
            testProject.AdditionalProperties[property] = "$(MSBuildProjectDirectory)/Override.targets";
        }

        var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: $"{multiTarget}_{import}");
        string projectDirectory = Path.Combine(testAsset.TestRoot, testProject.Name);
        string importFile = Path.Combine(projectDirectory, "Override.targets");
        if (generatedImport)
        {
            Directory.CreateDirectory(Path.Combine(projectDirectory, "obj"));
            string extension = import == "generated-props" ? "props" : "targets";
            importFile = Path.Combine(projectDirectory, "obj", $"{testProject.Name}.csproj.nuget.g.{extension}");
        }

        File.WriteAllText(importFile, """
            <Project>
              <PropertyGroup>
                <AssetsBeforeOverride>$(ProjectAssetsFile)</AssetsBeforeOverride>
                <ProjectAssetsFile Condition="'$(ProjectAssetsFile)' == ''">custom/../custom/imported.assets.json</ProjectAssetsFile>
              </PropertyGroup>
            </Project>
            """);

        var result = new DotnetCommand(Log, "msbuild", "--getProperty:ProjectAssetsFile,AssetsBeforeOverride")
            .WithWorkingDirectory(projectDirectory)
            .Execute();
        result.Should().Pass();

        using var document = JsonDocument.Parse(result.StdOut!);
        var properties = document.RootElement.GetProperty("Properties");
        properties.GetProperty("AssetsBeforeOverride").GetString().Should().BeEmpty();
        properties.GetProperty("ProjectAssetsFile").GetString().Should()
            .Be(Path.Combine(projectDirectory, "custom", "imported.assets.json"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Importing_common_targets_does_not_apply_package_dependency_defaults(bool packageReferences)
    {
        string projectDirectory = TestAssetsManager.CreateTestDirectory(identifier: packageReferences.ToString()).Path;
        string dependencyTargets = packageReferences
            ? "Microsoft.PackageDependencyResolution.targets"
            : "Microsoft.PackageDependencyResolutionStubs.targets";
        File.WriteAllText(Path.Combine(projectDirectory, "Assets.proj"), $$"""
            <Project>
              <PropertyGroup>
                <Language>C++</Language>
                <MSBuildProjectExtensionsPath>$(MSBuildProjectDirectory)/custom/</MSBuildProjectExtensionsPath>
                <IntermediateOutputPath>obj/</IntermediateOutputPath>
              </PropertyGroup>
              <Import Project="$(MSBuildSDKsPath)/Microsoft.NET.Sdk/targets/Microsoft.NET.Sdk.Common.targets" />
              <PropertyGroup>
                <AssetsAfterCommon>$(ProjectAssetsFile)</AssetsAfterCommon>
              </PropertyGroup>
              <Import Project="$(MSBuildSDKsPath)/Microsoft.NET.Sdk/targets/{{dependencyTargets}}" />
            </Project>
            """);

        var result = new DotnetCommand(Log, "msbuild", "Assets.proj", "--getProperty:ProjectAssetsFile,AssetsAfterCommon")
            .WithWorkingDirectory(projectDirectory)
            .Execute();
        result.Should().Pass();

        using var document = JsonDocument.Parse(result.StdOut!);
        var properties = document.RootElement.GetProperty("Properties");
        properties.GetProperty("AssetsAfterCommon").GetString().Should().BeEmpty();
        properties.GetProperty("ProjectAssetsFile").GetString().Should().Be(packageReferences
            ? Path.Combine(projectDirectory, "custom", "project.assets.json")
            : "");
    }
}
