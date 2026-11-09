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
}
