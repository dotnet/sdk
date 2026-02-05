// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.FileBasedPrograms;

namespace Microsoft.DotNet.Watch.UnitTests;

public class ProjectGraphFactoryTests(ITestOutputHelper output)
{
    private readonly TestAssetsManager _testAssetManager = new(output);
    private readonly TestLogger _testLogger = new();

    [Fact]
    public void RegularProject()
    {
        var testAsset = _testAssetManager.CopyTestAsset("WatchNoDepsApp")
            .WithSource();

        var projectPath = Path.Combine(testAsset.Path, "WatchNoDepsApp.csproj");

        var projectRepr = new ProjectRepresentation(projectPath, entryPointFilePath: null);
        var factory = new ProjectGraphFactory(projectRepr, targetFramework: null, buildProperties: [], _testLogger);

        var graph = factory.TryLoadProjectGraph(projectGraphRequired: true, CancellationToken.None);
        Assert.NotNull(graph);

        var root = graph.Graph.GraphRoots.Single();
        Assert.Equal(projectPath, root.ProjectInstance.FullPath);
    }

    [Fact]
    public void VirtualProject()
    {
        var dir = _testAssetManager.CreateTestDirectory().Path;

        var entryPointFilePath = Path.Combine(dir, "App.cs");
        File.WriteAllText(entryPointFilePath, """
            Console.WriteLine(1);
            """);

        var projectRepr = new ProjectRepresentation(projectPath: null, entryPointFilePath);
        var factory = new ProjectGraphFactory(projectRepr, targetFramework: null, buildProperties: [], _testLogger);

        var graph = factory.TryLoadProjectGraph(projectGraphRequired: true, CancellationToken.None);
        Assert.NotNull(graph);

        var root = graph.Graph.GraphRoots.Single();
        Assert.Equal(Path.ChangeExtension(entryPointFilePath, ".csproj"), root.ProjectInstance.FullPath);
    }

    [Fact]
    public void VirtualProject_Error()
    {
        var dir = _testAssetManager.CreateTestDirectory().Path;

        var entryPointFilePath = Path.Combine(dir, "App.cs");
        File.WriteAllText(entryPointFilePath, """
            #:project NonExistent.csproj
            """);

        var projectRepr = new ProjectRepresentation(projectPath: null, entryPointFilePath);
        var factory = new ProjectGraphFactory(projectRepr, targetFramework: null, buildProperties: [], _testLogger);

        var graph = factory.TryLoadProjectGraph(projectGraphRequired: true, CancellationToken.None);
        Assert.Null(graph);

        var message = string.Format(FileBasedProgramsResources.InvalidProjectDirective,
            string.Format(FileBasedProgramsResources.CouldNotFindProjectOrDirectory, Path.Combine(dir, "NonExistent.csproj")));

        AssertEx.SequenceEqual(
        [
            $"[Error] {entryPointFilePath}(1): {message}",
            "[Debug] Failed to load project graph."
        ], _testLogger.GetAndClearMessages());
    }

    [Fact]
    public void VirtualProject_ProjectDirective()
    {
        var testAsset = _testAssetManager.CopyTestAsset("WatchNoDepsApp")
            .WithSource();

        var projectPath = Path.Combine(testAsset.Path, "WatchNoDepsApp.csproj");
        var scriptsDir = Path.Combine(testAsset.Path, ".scripts");
        Directory.CreateDirectory(scriptsDir);

        var entryPointFilePath = Path.Combine(scriptsDir, "Script.cs");

        File.WriteAllText(entryPointFilePath, """
            #:project ..\WatchNoDepsApp.csproj
            """);

        var projectRepr = new ProjectRepresentation(projectPath: null, entryPointFilePath);
        var factory = new ProjectGraphFactory(projectRepr, targetFramework: null, buildProperties: [], _testLogger);

        var graph = factory.TryLoadProjectGraph(projectGraphRequired: true, CancellationToken.None);
        Assert.NotNull(graph);

        AssertEx.SequenceEqual(
            [projectPath, Path.ChangeExtension(entryPointFilePath, ".csproj")],
            graph.Graph.ProjectNodesTopologicallySorted.Select(p => p.ProjectInstance.FullPath));
    }
}
