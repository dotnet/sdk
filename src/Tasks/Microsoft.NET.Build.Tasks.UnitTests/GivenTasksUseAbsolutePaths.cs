// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tasks.UnitTests;

/// <summary>
/// Tests that verify MSBuild tasks handle file paths correctly in multi-node scenarios.
///
/// When MSBuild runs in parallel mode, tasks may be spawned on different nodes, each with
/// a potentially different working directory. Tasks that use relative paths resolved from
/// the current working directory will fail in these scenarios.
///
/// These tests create files in a "project" directory, then verify task behavior by
/// passing RELATIVE paths and expecting tasks to resolve them via TaskEnvironment.
/// </summary>
public class GivenTasksUseAbsolutePaths : IDisposable
{
    private readonly TaskTestEnvironment _env;
    private readonly ITestOutputHelper _output;

    public GivenTasksUseAbsolutePaths(ITestOutputHelper output)
    {
        _env = new TaskTestEnvironment();
        _output = output;
    }

    public void Dispose()
    {
        _env.Dispose();
    }

    #region AllowEmptyTelemetry - No File I/O

    [Fact]
    public void AllowEmptyTelemetry_NoFileIO_ShouldSucceed()
    {
        var task = new AllowEmptyTelemetry
        {
            BuildEngine = new MockBuildEngine()
        };

        var result = task.Execute();
        result.Should().BeTrue();
    }

    #endregion

    #region CheckForTargetInAssetsFile

    [Fact]
    public void CheckForTargetInAssetsFile_WithRelativePaths_ShouldResolveFromProjectDirectory()
    {
        var assetsContent = @"{
            ""version"": 3,
            ""targets"": { "".NETCoreApp,Version=v8.0"": {} },
            ""libraries"": {},
            ""projectFileDependencyGroups"": { "".NETCoreApp,Version=v8.0"": [] },
            ""project"": { ""version"": ""1.0.0"", ""frameworks"": { ""net8.0"": {} } }
        }";
        _env.CreateProjectDirectory("obj");
        _env.CreateProjectFile("obj/project.assets.json", assetsContent);

        var correctPath = _env.GetProjectPath("obj/project.assets.json");
        File.Exists(correctPath).Should().BeTrue("file should exist in project directory");
        File.Exists("obj/project.assets.json").Should().BeFalse("file should NOT exist relative to CWD");

        var task = new CheckForTargetInAssetsFile
        {
            BuildEngine = new MockBuildEngine(),
            TaskEnvironment = _env.TaskEnvironment,
            AssetsFilePath = "obj/project.assets.json",
            TargetFramework = "net8.0"
        };

        var result = task.Execute();

        result.Should().BeTrue("task should resolve relative paths via TaskEnvironment");
    }

    #endregion

    #region ResolvePackageAssets

    [Fact]
    public void ResolvePackageAssets_WithRelativePaths_ShouldResolveFromProjectDirectory()
    {
        var assetsContent = @"{
            ""version"": 3,
            ""targets"": { "".NETCoreApp,Version=v8.0"": {} },
            ""libraries"": {},
            ""projectFileDependencyGroups"": { "".NETCoreApp,Version=v8.0"": [] },
            ""project"": { ""version"": ""1.0.0"", ""frameworks"": { ""net8.0"": {} } }
        }";

        _env.CreateProjectDirectory("obj");
        _env.CreateProjectFile("obj/project.assets.json", assetsContent);

        var correctAbsolutePath = _env.GetProjectPath("obj/project.assets.json");
        File.Exists(correctAbsolutePath).Should().BeTrue("file should exist in project directory");

        const string relativePath = "obj/project.assets.json";

        var task = new ResolvePackageAssets
        {
            BuildEngine = new MockBuildEngine(),
            TaskEnvironment = _env.TaskEnvironment,
            ProjectAssetsCacheFile = "obj/project.assets.cache",
            ProjectAssetsFile = relativePath,
            ProjectPath = "myapp.csproj",
            TargetFramework = "net8.0",
            RuntimeIdentifier = "",
            DisablePackageAssetsCache = true,
            DotNetAppHostExecutableNameWithoutExtension = "apphost",
            DefaultImplicitPackages = ""
        };

        _output.WriteLine($"Current directory: {Environment.CurrentDirectory}");
        _output.WriteLine($"Project directory: {_env.ProjectDirectory}");

        var result = task.Execute();

        result.Should().BeTrue("task should succeed when TaskEnvironment correctly resolves relative paths");
    }

    #endregion
}
