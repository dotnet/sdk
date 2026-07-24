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
    public void RudeEditHelpLink_PointsToFSharpDiagnosticReference()
    {
        var field = typeof(FSharpHotReloadService).GetField(
            "RudeEditHelpLink",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        Assert.AreEqual(
            "https://github.com/dotnet/fsharp/blob/main/docs/hot-reload-rude-edits.md",
            field.GetRawConstantValue());
    }

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
    public void GetChangedFSharpProjects_MatchesDependencyByProjectDirectoryFallback()
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
            "GetChangedFSharpProjects",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        var result = (ImmutableArray<ProjectInstanceId>)method.Invoke(service, [changedFiles, runningProjects])!;

        Assert.HasCount(1, result);
        Assert.AreEqual(projectId, result[0]);
    }

    [TestMethod]
    public void GetChangedFSharpProjects_MatchesCommandLineDependencyOutsideProjectDirectory()
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
            "GetChangedFSharpProjects",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        var result = (ImmutableArray<ProjectInstanceId>)method.Invoke(service, [changedFiles, runningProjects])!;

        Assert.HasCount(1, result);
        Assert.AreEqual(projectId, result[0]);
    }

    [TestMethod]
    public void GetChangedFSharpProjects_IgnoresEditorTempFiles()
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
            "GetChangedFSharpProjects",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        var result = (ImmutableArray<ProjectInstanceId>)method.Invoke(service, [changedFiles, runningProjects])!;

        Assert.IsTrue(result.IsEmpty);
    }

    [TestMethod]
    public void GetChangedFSharpProjects_ReturnsEveryAffectedProjectInBatch()
    {
        var root = Path.Combine(Path.GetTempPath(), "dotnet-watch-fsharp-tests-batch");
        var appPath = Path.Combine(root, "App", "App.fsproj");
        var libPath = Path.Combine(root, "Lib", "Lib.fsproj");
        var appId = new ProjectInstanceId(appPath, "net10.0");
        var libId = new ProjectInstanceId(libPath, "net10.0");

        var projects = ImmutableDictionary<ProjectInstanceId, FSharpProjectInfo>.Empty
            .Add(appId, new FSharpProjectInfo(appId, appPath, "net10.0", Path.ChangeExtension(appPath, ".dll"), "fsc.dll", []))
            .Add(libId, new FSharpProjectInfo(libId, libPath, "net10.0", Path.ChangeExtension(libPath, ".dll"), "fsc.dll", []));

        var service = new FSharpHotReloadService(NullLogger.Instance);
        typeof(FSharpHotReloadService).GetField("_projects", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(service, projects);

        var changedFiles = new List<ChangedFile>
        {
            new(new FileItem { FilePath = Path.Combine(root, "App", "Program.fs"), ContainingProjectPaths = [appPath] }, ChangeKind.Update),
            new(new FileItem { FilePath = Path.Combine(root, "Lib", "Lib.fs"), ContainingProjectPaths = [libPath] }, ChangeKind.Update),
        };
        var runningProjects = ImmutableDictionary<string, ImmutableArray<RunningProject>>.Empty
            .Add(appPath, [])
            .Add(libPath, []);

        var method = typeof(FSharpHotReloadService).GetMethod(
            "GetChangedFSharpProjects",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var result = (ImmutableArray<ProjectInstanceId>)method.Invoke(service, [changedFiles, runningProjects])!;

        Assert.AreSequenceEqual(new[] { appId, libId }, result);
    }

    [TestMethod]
    public async Task TryEmitUpdatesAsync_BridgeUnavailableReportsReferencedProjectForRestart()
    {
        var root = Path.Combine(Path.GetTempPath(), "dotnet-watch-fsharp-tests-missing-bridge");
        var appPath = Path.Combine(root, "App", "App.fsproj");
        var libPath = Path.Combine(root, "Lib", "Lib.fsproj");
        var libId = new ProjectInstanceId(libPath, "net10.0");
        var libInfo = new FSharpProjectInfo(
            libId,
            libPath,
            "net10.0",
            Path.Combine(root, "Lib", "bin", "Debug", "net10.0", "Lib.dll"),
            Path.Combine(root, "missing", "FSharp.Compiler.Service.dll"),
            []);

        var service = new FSharpHotReloadService(NullLogger.Instance);
        typeof(FSharpHotReloadService).GetField("_projects", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(service, ImmutableDictionary<ProjectInstanceId, FSharpProjectInfo>.Empty.Add(libId, libInfo));

        var changedFile = new ChangedFile(
            new FileItem
            {
                FilePath = Path.Combine(root, "Lib", "Lib.fs"),
                ContainingProjectPaths = [libPath],
            },
            ChangeKind.Update);
        var runningProjects = ImmutableDictionary<string, ImmutableArray<RunningProject>>.Empty.Add(appPath, []);

        var result = await service.TryEmitUpdatesAsync([changedFile], runningProjects, CancellationToken.None);

        Assert.AreEqual(FSharpManagedUpdateStatus.RestartRequired, result.Status);
        Assert.IsTrue(result.Updates.IsEmpty);
        Assert.HasCount(1, result.Issues);
        Assert.AreEqual(libPath, result.Issues[0].ProjectPath);
    }

    [TestMethod]
    [DataRow("Program.fs", true)]
    [DataRow("payload.txt", true)]
    [DataRow("Project.fsproj", false)]
    [DataRow("Directory.Build.props", false)]
    public void IsSupportedChangedFile_LeavesProjectConfigurationOnRestartPath(string fileName, bool expected)
    {
        var method = typeof(FSharpHotReloadService).GetMethod(
            "IsSupportedChangedFile",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        Assert.AreEqual(expected, (bool)method.Invoke(null, [Path.Combine("/tmp", fileName)])!);
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
            Assert.IsTrue(result.Issues.IsEmpty);
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
