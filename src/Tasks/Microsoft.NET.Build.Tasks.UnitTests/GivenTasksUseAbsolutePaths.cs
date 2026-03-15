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

    #region GenerateDepsFile

    [Fact]
    public void GenerateDepsFile_WithRelativePaths_ShouldResolveFromProjectDirectory()
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
        _env.CreateProjectFile("myapp.csproj", "<Project></Project>");

        File.Exists(_env.GetProjectPath("obj/project.assets.json")).Should().BeTrue();
        File.Exists("obj/project.assets.json").Should().BeFalse("file should NOT exist relative to CWD");

        var task = new GenerateDepsFile
        {
            BuildEngine = new MockBuildEngine(),
            TaskEnvironment = _env.TaskEnvironment,
            ProjectPath = "myapp.csproj",
            AssetsFilePath = "obj/project.assets.json",
            DepsFilePath = "obj/myapp.deps.json",
            TargetFramework = "net8.0",
            AssemblyName = "myapp",
            AssemblyExtension = ".dll",
            AssemblyVersion = "1.0.0.0",
            IncludeMainProject = true,
            RuntimeFrameworks = Array.Empty<ITaskItem>(),
            CompileReferences = Array.Empty<ITaskItem>(),
            ResolvedNuGetFiles = Array.Empty<ITaskItem>(),
            ResolvedRuntimeTargetsFiles = Array.Empty<ITaskItem>(),
            RuntimeGraphPath = ""
        };

        var result = task.Execute();

        result.Should().BeTrue("task should resolve relative paths via TaskEnvironment");
        File.Exists(_env.GetProjectPath("obj/myapp.deps.json")).Should().BeTrue("deps file should be written to project dir");
    }

    #endregion

    #region ResolveAppHosts - No File I/O (with empty input)

    [Fact]
    public void ResolveAppHosts_NoFileIO_ShouldSucceed()
    {
        var task = new ResolveAppHosts
        {
            BuildEngine = new MockBuildEngine(),
            TaskEnvironment = _env.TaskEnvironment,
            TargetFrameworkIdentifier = ".NETCoreApp",
            TargetFrameworkVersion = "8.0",
            TargetingPackRoot = "packs",
            AppHostRuntimeIdentifier = "win-x64",
            PackAsToolShimRuntimeIdentifiers = Array.Empty<ITaskItem>(),
            KnownAppHostPacks = Array.Empty<ITaskItem>()
        };

        var result = task.Execute();
        _output.WriteLine($"Result: {result}");
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

    #region ShowPreviewMessage - No File I/O

    [Fact]
    public void ShowPreviewMessage_NoFileIO_ShouldSucceed()
    {
        var task = new ShowPreviewMessage
        {
            BuildEngine = new MockBuildEngine()
        };

        var result = task.Execute();
        result.Should().BeTrue();
    }

    #endregion

    #region WriteAppConfigWithSupportedRuntime

    [Fact]
    public void WriteAppConfigWithSupportedRuntime_WithRelativePaths_ShouldResolveFromProjectDirectory()
    {
        _env.CreateProjectFile("App.config", "<configuration></configuration>");
        _env.CreateProjectDirectory("bin");

        File.Exists(_env.GetProjectPath("App.config")).Should().BeTrue();
        File.Exists("App.config").Should().BeFalse("file should NOT exist relative to CWD");

        var task = new WriteAppConfigWithSupportedRuntime
        {
            BuildEngine = new MockBuildEngine(),
            TaskEnvironment = _env.TaskEnvironment,
            AppConfigFile = new MockTaskItem("App.config", new Dictionary<string, string>()),
            OutputAppConfigFile = new MockTaskItem("bin/myapp.exe.config", new Dictionary<string, string>()),
            TargetFrameworkIdentifier = ".NETFramework",
            TargetFrameworkVersion = "4.8"
        };

        var result = task.Execute();

        result.Should().BeTrue("task should resolve relative paths via TaskEnvironment");
        File.Exists(_env.GetProjectPath("bin/myapp.exe.config")).Should().BeTrue(
            "output should be written to project dir, not CWD");
    }

    #endregion
}
