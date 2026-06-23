// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class FSharpHotReloadServiceTests
{
    [TestMethod]
    [DataRow("/tmp/Program.fs", false)]
    [DataRow("/tmp/Program.fsi", false)]
    [DataRow("/tmp/Project.fsproj", false)]
    [DataRow("/tmp/.Program.fs.swp", false)]
    [DataRow("/tmp/payload.txt", true)]
    [DataRow("/tmp/view.xaml", true)]
    public void IsManagedDependencyCandidatePath_ClassifiesProjectAndTempFiles(string path, bool expected)
    {
        var method = typeof(FSharpHotReloadService).GetMethod(
            "IsManagedDependencyCandidatePath",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        var raw = method.Invoke(null, [path]);
        Assert.IsInstanceOfType(raw, typeof(bool));
        Assert.AreEqual(expected, (bool)raw!);
    }

    [TestMethod]
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

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType(result, typeof(ProjectInstanceId));
        Assert.AreEqual(projectId, (ProjectInstanceId)result!);
    }

    [TestMethod]
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

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType(result, typeof(ProjectInstanceId));
        Assert.AreEqual(projectId, (ProjectInstanceId)result!);
    }

    [TestMethod]
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

        Assert.IsNull(result);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("false")]
    public async Task KillSwitch_DisablesEntireBridge(string killSwitchValue)
    {
        var originalValue = Environment.GetEnvironmentVariable("DOTNET_WATCH_FSHARP_HOTRELOAD");
        try
        {
            Environment.SetEnvironmentVariable("DOTNET_WATCH_FSHARP_HOTRELOAD", killSwitchValue);

            var projectDirectory = Path.Combine(Path.GetTempPath(), "dotnet-watch-fsharp-tests-killswitch");
            var projectPath = Path.Combine(projectDirectory, "TestApp.fsproj");
            var targetPath = Path.Combine(projectDirectory, "bin", "Debug", "net10.0", "TestApp.dll");
            var compilerPath = Path.Combine(projectDirectory, "fsc.dll");

            var projectId = new ProjectInstanceId(projectPath, "net10.0");
            var projectInfo = new FSharpProjectInfo(projectId, projectPath, "net10.0", targetPath, compilerPath, []);

            // The kill switch is read once at construction.
            var service = new FSharpHotReloadService(NullLogger.Instance);

            // Simulate a discovered F# project so that, were the bridge enabled, the change below
            // would match the running project and produce a result carrying the project path.
            var projectsField = typeof(FSharpHotReloadService).GetField("_projects", BindingFlags.Instance | BindingFlags.NonPublic)!;
            projectsField.SetValue(service, ImmutableDictionary<ProjectInstanceId, FSharpProjectInfo>.Empty.Add(projectId, projectInfo));

            var changedFile = new ChangedFile(
                new FileItem
                {
                    FilePath = Path.Combine(projectDirectory, "Program.fs"),
                    ContainingProjectPaths = [projectPath]
                },
                ChangeKind.Update);

            var runningProjects = ImmutableDictionary<string, ImmutableArray<RunningProject>>.Empty.Add(projectPath, []);

            await service.StartSessionAsync(CancellationToken.None);
            var result = await service.TryEmitUpdatesAsync([changedFile], runningProjects, CancellationToken.None);

            // Disabled: the service behaves as if no F# projects exist.
            Assert.AreEqual(FSharpManagedUpdateStatus.NoChanges, result.Status);
            Assert.IsTrue(result.Updates.IsEmpty);
            Assert.IsNull(result.ProjectPath);
            Assert.IsFalse(service.OwnsChangedFile(changedFile));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_WATCH_FSHARP_HOTRELOAD", originalValue);
        }
    }

    [TestMethod]
    public void TryGetCommandLineDependencyPath_ParsesResourceLogicalNameSuffix()
    {
        var method = typeof(FSharpHotReloadService).GetMethod(
            "TryGetCommandLineDependencyPath",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        var projectDirectory = Path.Combine(Path.GetTempPath(), "dotnet-watch-fsharp-tests-parse");
        var expectedPath = Path.GetFullPath(Path.Combine(projectDirectory, "Views", "MainPage.xaml"));
        var arguments = new object?[] { "--resource:Views/MainPage.xaml,MainPage.xaml", projectDirectory, null };

        var raw = method.Invoke(null, arguments);
        Assert.IsInstanceOfType(raw, typeof(bool));
        Assert.IsTrue((bool)raw!);

        Assert.IsInstanceOfType(arguments[2], typeof(string));
        Assert.AreEqual(expectedPath, (string)arguments[2]!);
    }
}
