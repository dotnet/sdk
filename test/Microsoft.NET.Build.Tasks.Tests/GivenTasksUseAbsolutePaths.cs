// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;

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

    #region Infrastructure Verification Tests

    [Fact]
    public void TestEnvironment_ProjectAndSpawnDirectories_AreDifferent()
    {
        _env.ProjectDirectory.Should().NotBe(_env.SpawnDirectory);
        Directory.Exists(_env.ProjectDirectory).Should().BeTrue();
        Directory.Exists(_env.SpawnDirectory).Should().BeTrue();

        _output.WriteLine($"Project directory: {_env.ProjectDirectory}");
        _output.WriteLine($"Spawn directory: {_env.SpawnDirectory}");
    }

    [Fact]
    public void TestEnvironment_DemonstratesPathDifference()
    {
        var projectFile = _env.CreateProjectFile("test.txt", "content");

        var correctPath = _env.GetProjectPath("test.txt");
        var incorrectPath = _env.GetIncorrectPath("test.txt");

        correctPath.Should().NotBe(incorrectPath);
        File.Exists(correctPath).Should().BeTrue("file was created in project directory");
        File.Exists(incorrectPath).Should().BeFalse("file should not exist in spawn directory");

        _output.WriteLine($"Correct path (in project): {correctPath}");
        _output.WriteLine($"Incorrect path (in spawn): {incorrectPath}");
    }

    #endregion

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

    #region GenerateRuntimeConfigurationFiles

    [Fact]
    public void GenerateRuntimeConfigurationFiles_WithRelativePaths_ShouldResolveFromProjectDirectory()
    {
        _env.CreateProjectDirectory("obj");

        Directory.Exists(_env.GetProjectPath("obj")).Should().BeTrue();
        Directory.Exists("obj").Should().BeFalse("obj should NOT exist relative to CWD");

        var task = new GenerateRuntimeConfigurationFiles
        {
            BuildEngine = new MockBuildEngine(),
            TaskEnvironment = _env.TaskEnvironment,
            RuntimeConfigPath = "obj/myapp.runtimeconfig.json",
            TargetFrameworkMoniker = ".NETCoreApp,Version=v8.0",
            RuntimeFrameworks = Array.Empty<ITaskItem>(),
            RollForward = "LatestMinor",
            UserRuntimeConfig = "",
            HostConfigurationOptions = Array.Empty<ITaskItem>(),
            AdditionalProbingPaths = Array.Empty<ITaskItem>(),
            IsSelfContained = false,
            WriteAdditionalProbingPathsToMainConfig = false,
            WriteIncludedFrameworks = false,
            AlwaysIncludeCoreFramework = false
        };

        var result = task.Execute();

        result.Should().BeTrue("task should resolve relative paths via TaskEnvironment");
        File.Exists(_env.GetProjectPath("obj/myapp.runtimeconfig.json")).Should().BeTrue(
            "runtimeconfig should be written to project dir, not CWD");
    }

    #endregion

    #region GenerateToolsSettingsFile

    [Fact]
    public void GenerateToolsSettingsFile_WithRelativePaths_ShouldResolveFromProjectDirectory()
    {
        _env.CreateProjectDirectory("obj");

        Directory.Exists(_env.GetProjectPath("obj")).Should().BeTrue();
        Directory.Exists("obj").Should().BeFalse("obj should NOT exist relative to CWD");

        var task = new GenerateToolsSettingsFile
        {
            BuildEngine = new MockBuildEngine(),
            TaskEnvironment = _env.TaskEnvironment,
            EntryPointRelativePath = "myapp.dll",
            CommandName = "mytool",
            ToolsSettingsFilePath = "obj/DotnetToolSettings.xml"
        };

        var result = task.Execute();

        result.Should().BeTrue("task should resolve relative paths via TaskEnvironment");
        File.Exists(_env.GetProjectPath("obj/DotnetToolSettings.xml")).Should().BeTrue(
            "settings file should be written to project dir, not CWD");
    }

    #endregion

    #region GetAssemblyAttributes

    [Fact]
    public void GetAssemblyAttributes_WithRelativePaths_ShouldResolveFromProjectDirectory()
    {
        _env.CreateProjectDirectory("obj");
        _env.CreateProjectFile("obj/AssemblyInfo.cs.template", "// template content");

        File.Exists(_env.GetProjectPath("obj/AssemblyInfo.cs.template")).Should().BeTrue();
        File.Exists("obj/AssemblyInfo.cs.template").Should().BeFalse("file should NOT exist relative to CWD");

        var task = new GetAssemblyAttributes
        {
            BuildEngine = new MockBuildEngine(),
            TaskEnvironment = _env.TaskEnvironment,
            PathToTemplateFile = "obj/AssemblyInfo.cs.template"
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

    #region ResolvePackageDependencies

    [Fact]
    public void ResolvePackageDependencies_WithRelativePaths_ShouldResolveFromProjectDirectory()
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

        var task = new ResolvePackageDependencies
        {
            BuildEngine = new MockBuildEngine(),
            TaskEnvironment = _env.TaskEnvironment,
            ProjectAssetsFile = "obj/project.assets.json",
            ProjectPath = "myapp.csproj"
        };

        var result = task.Execute();

        result.Should().BeTrue("task should resolve relative paths via TaskEnvironment");
    }

    #endregion

    #region Demonstration: Absolute vs Relative Paths

    [Fact]
    public void AbsolutePath_AlwaysPointsToCorrectFile()
    {
        var content = "test content";
        var absolutePath = _env.CreateProjectFile("data/input.txt", content);

        Path.IsPathRooted(absolutePath).Should().BeTrue();

        var readContent = File.ReadAllText(absolutePath);
        readContent.Should().Be(content);

        _output.WriteLine($"Absolute path: {absolutePath}");
        _output.WriteLine($"Successfully read file content: {readContent}");
    }

    [Fact]
    public void MockTaskEnvironment_ResolvesRelativeToProjectDirectory()
    {
        var content = "test content";
        _env.CreateProjectFile("subdir/file.txt", content);

        var resolvedPath = _env.TaskEnvironment.GetAbsolutePath("subdir/file.txt");

        var expectedPath = _env.GetProjectPath("subdir/file.txt");
        ((string)resolvedPath).Replace('/', Path.DirectorySeparatorChar)
            .Should().Be(expectedPath.Replace('/', Path.DirectorySeparatorChar));

        File.Exists(resolvedPath).Should().BeTrue();

        _output.WriteLine($"TaskEnvironment resolved: {resolvedPath}");
    }

    #endregion
}
