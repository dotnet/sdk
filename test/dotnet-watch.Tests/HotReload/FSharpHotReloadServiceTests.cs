// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.Watch.UnitTests;

public class FSharpHotReloadServiceTests
{
    [Theory]
    [InlineData("/tmp/Program.fs", false)]
    [InlineData("/tmp/Program.fsi", false)]
    [InlineData("/tmp/Project.fsproj", false)]
    [InlineData("/tmp/.Program.fs.swp", false)]
    [InlineData("/tmp/payload.txt", true)]
    [InlineData("/tmp/view.xaml", true)]
    public void IsManagedDependencyCandidatePath_ClassifiesProjectAndTempFiles(string path, bool expected)
    {
        var method = typeof(FSharpHotReloadService).GetMethod(
            "IsManagedDependencyCandidatePath",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        var result = Assert.IsType<bool>(method.Invoke(null, [path]));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryGetChangedRunningFSharpProject_MatchesDependencyByProjectDirectoryFallback()
    {
        var projectDirectory = Path.Combine(Path.GetTempPath(), "dotnet-watch-fsharp-tests");
        var projectPath = Path.Combine(projectDirectory, "TestApp.fsproj");
        var targetPath = Path.Combine(projectDirectory, "bin", "Debug", "net10.0", "TestApp.dll");
        var compilerPath = Path.Combine(projectDirectory, "fsc.dll");

        var projectId = new ProjectInstanceId(projectPath, "net10.0");
        var projectInfo = new FSharpProjectInfo(projectId, projectPath, "net10.0", targetPath, compilerPath, []);

        var service = new FSharpHotReloadService(NullLogger.Instance);
        var projectsField = typeof(FSharpHotReloadService).GetField("_projects", BindingFlags.Instance | BindingFlags.NonPublic)!;
        projectsField.SetValue(service, ImmutableDictionary<ProjectInstanceId, FSharpProjectInfo>.Empty.Add(projectId, projectInfo));

        var changedFiles = new List<ChangedFile>
        {
            new(
                new FileItem
                {
                    FilePath = Path.Combine(projectDirectory, "payload.txt"),
                    ContainingProjectPaths = []
                },
                ChangeKind.Update)
        };

        var runningProjects = ImmutableDictionary<string, ImmutableArray<RunningProject>>.Empty.Add(projectPath, []);

        var method = typeof(FSharpHotReloadService).GetMethod(
            "TryGetChangedRunningFSharpProject",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        var result = method.Invoke(service, [changedFiles, runningProjects]);

        Assert.NotNull(result);
        Assert.Equal(projectId, Assert.IsType<ProjectInstanceId>(result));
    }

    [Fact]
    public void TryGetChangedRunningFSharpProject_MatchesCommandLineDependencyOutsideProjectDirectory()
    {
        var projectDirectory = Path.Combine(Path.GetTempPath(), "dotnet-watch-fsharp-tests-cmdline");
        var projectPath = Path.Combine(projectDirectory, "TestApp.fsproj");
        var targetPath = Path.Combine(projectDirectory, "bin", "Debug", "net10.0", "TestApp.dll");
        var compilerPath = Path.Combine(projectDirectory, "fsc.dll");

        var externalDependency = Path.Combine(Path.GetTempPath(), "dotnet-watch-fsharp-external", "MainPage.xaml");

        var projectId = new ProjectInstanceId(projectPath, "net10.0");
        var projectInfo =
            new FSharpProjectInfo(
                projectId,
                projectPath,
                "net10.0",
                targetPath,
                compilerPath,
                [$"--resource:{externalDependency},MainPage.xaml"]);

        var service = new FSharpHotReloadService(NullLogger.Instance);
        var projectsField = typeof(FSharpHotReloadService).GetField("_projects", BindingFlags.Instance | BindingFlags.NonPublic)!;
        projectsField.SetValue(service, ImmutableDictionary<ProjectInstanceId, FSharpProjectInfo>.Empty.Add(projectId, projectInfo));

        var changedFiles = new List<ChangedFile>
        {
            new(
                new FileItem
                {
                    FilePath = externalDependency,
                    ContainingProjectPaths = []
                },
                ChangeKind.Update)
        };

        var runningProjects = ImmutableDictionary<string, ImmutableArray<RunningProject>>.Empty.Add(projectPath, []);

        var method = typeof(FSharpHotReloadService).GetMethod(
            "TryGetChangedRunningFSharpProject",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        var result = method.Invoke(service, [changedFiles, runningProjects]);

        Assert.NotNull(result);
        Assert.Equal(projectId, Assert.IsType<ProjectInstanceId>(result));
    }

    [Fact]
    public void TryGetChangedRunningFSharpProject_IgnoresEditorTempFiles()
    {
        var projectDirectory = Path.Combine(Path.GetTempPath(), "dotnet-watch-fsharp-tests-temp");
        var projectPath = Path.Combine(projectDirectory, "TestApp.fsproj");
        var targetPath = Path.Combine(projectDirectory, "bin", "Debug", "net10.0", "TestApp.dll");
        var compilerPath = Path.Combine(projectDirectory, "fsc.dll");

        var projectId = new ProjectInstanceId(projectPath, "net10.0");
        var projectInfo = new FSharpProjectInfo(projectId, projectPath, "net10.0", targetPath, compilerPath, []);

        var service = new FSharpHotReloadService(NullLogger.Instance);
        var projectsField = typeof(FSharpHotReloadService).GetField("_projects", BindingFlags.Instance | BindingFlags.NonPublic)!;
        projectsField.SetValue(service, ImmutableDictionary<ProjectInstanceId, FSharpProjectInfo>.Empty.Add(projectId, projectInfo));

        var changedFiles = new List<ChangedFile>
        {
            new(
                new FileItem
                {
                    FilePath = Path.Combine(projectDirectory, ".Program.fs.swp"),
                    ContainingProjectPaths = []
                },
                ChangeKind.Update)
        };

        var runningProjects = ImmutableDictionary<string, ImmutableArray<RunningProject>>.Empty.Add(projectPath, []);

        var method = typeof(FSharpHotReloadService).GetMethod(
            "TryGetChangedRunningFSharpProject",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        var result = method.Invoke(service, [changedFiles, runningProjects]);

        Assert.Null(result);
    }

    [Fact]
    public void TryGetCommandLineDependencyPath_ParsesResourceLogicalNameSuffix()
    {
        var method = typeof(FSharpHotReloadService).GetMethod(
            "TryGetCommandLineDependencyPath",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        var projectDirectory = Path.Combine(Path.GetTempPath(), "dotnet-watch-fsharp-tests-parse");
        var expectedPath = Path.GetFullPath(Path.Combine(projectDirectory, "Views", "MainPage.xaml"));
        var arguments = new object?[] { "--resource:Views/MainPage.xaml,MainPage.xaml", projectDirectory, null };

        var parsed = Assert.IsType<bool>(method.Invoke(null, arguments));

        Assert.True(parsed);
        Assert.Equal(expectedPath, Assert.IsType<string>(arguments[2]));
    }
}
