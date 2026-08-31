// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

extern alias MSTestFramework;

using System.Collections.Immutable;
using Microsoft.DotNet.FileBasedPrograms;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class ProjectGraphFactoryTests
{
    public TestContext TestContext { get; set; } = null!;
    private DualOutputHelper? _output;
    private DualOutputHelper Output => _output ??= new(new TestContextOutputHelper(TestContext));
    private TestAssetsManager? _testAssetManager;
    private TestAssetsManager TestAssetManager => _testAssetManager ??= new(Output);
    private readonly TestLogger _testLogger = new();

    [TestMethod]
    public void RegularProject()
    {
        var testAsset = TestAssetManager.CopyTestAsset("WatchNoDepsApp")
            .WithSource();

        var projectPath = Path.Combine(testAsset.Path, "WatchNoDepsApp.csproj");

        var projectRepr = new ProjectRepresentation(projectPath, entryPointFilePath: null);
        var factory = new ProjectGraphFactory([projectRepr], buildProperties: [], _testLogger, TestOptions.GlobalOptions, TestOptions.GetEnvironmentOptions(asset: testAsset));

        var graph = factory.TryLoadProjectGraph(projectGraphRequired: true, virtualProjectTargetFramework: null, CancellationToken.None);
        Assert.IsNotNull(graph);

        var root = graph.Graph.GraphRoots.Single();
        Assert.AreEqual(projectPath, root.ProjectInstance.FullPath);
    }

    [TestMethod]
    public void VirtualProject()
    {
        var dir = TestAssetManager.CreateTestDirectory().Path;

        var entryPointFilePath = Path.Combine(dir, "App.cs");
        File.WriteAllText(entryPointFilePath, """
            Console.WriteLine(1);
            """);

        var projectRepr = new ProjectRepresentation(projectPath: null, entryPointFilePath);
        var factory = new ProjectGraphFactory([projectRepr], buildProperties: [], _testLogger, TestOptions.GlobalOptions, TestOptions.GetEnvironmentOptions());

        var graph = factory.TryLoadProjectGraph(projectGraphRequired: true, virtualProjectTargetFramework: null, CancellationToken.None);
        Assert.IsNotNull(graph);

        var root = graph.Graph.GraphRoots.Single();
        Assert.AreEqual(VirtualProjectBuilder.GetVirtualProjectPath(entryPointFilePath), root.ProjectInstance.FullPath);
    }

    [TestMethod]
    public void VirtualProject_Error()
    {
        var dir = TestAssetManager.CreateTestDirectory().Path;

        var entryPointFilePath = Path.Combine(dir, "App.cs");
        File.WriteAllText(entryPointFilePath, """
            #:project NonExistent.csproj
            """);

        var projectRepr = new ProjectRepresentation(projectPath: null, entryPointFilePath);
        var factory = new ProjectGraphFactory([projectRepr], buildProperties: [], _testLogger, TestOptions.GlobalOptions, TestOptions.GetEnvironmentOptions());

        var graph = factory.TryLoadProjectGraph(projectGraphRequired: true, virtualProjectTargetFramework: null, CancellationToken.None);
        Assert.IsNull(graph);

        var message = string.Format(FileBasedProgramsResources.InvalidProjectDirective,
            string.Format(FileBasedProgramsResources.CouldNotFindProjectOrDirectory, Path.Combine(dir, "NonExistent.csproj")));

        AssertEx.SequenceEqual(
        [
            $"[Error] {entryPointFilePath}(1): {message}",
            "[Debug] Failed to load project graph."
        ], _testLogger.GetAndClearMessages());
    }

    [TestMethod]
    public void VirtualProject_ProjectDirective()
    {
        var testAsset = TestAssetManager.CopyTestAsset("WatchNoDepsApp")
            .WithSource();

        var projectPath = Path.Combine(testAsset.Path, "WatchNoDepsApp.csproj");
        var scriptsDir = Path.Combine(testAsset.Path, ".scripts");
        Directory.CreateDirectory(scriptsDir);

        var entryPointFilePath = Path.Combine(scriptsDir, "Script.cs");

        File.WriteAllText(entryPointFilePath, """
            #:project ..\WatchNoDepsApp.csproj
            """);

        var projectRepr = new ProjectRepresentation(projectPath: null, entryPointFilePath);
        var factory = new ProjectGraphFactory([projectRepr], buildProperties: [], _testLogger, TestOptions.GlobalOptions, TestOptions.GetEnvironmentOptions(asset: testAsset));

        var graph = factory.TryLoadProjectGraph(projectGraphRequired: true, virtualProjectTargetFramework: null, CancellationToken.None);
        Assert.IsNotNull(graph);

        AssertEx.SequenceEqual(
            [projectPath, VirtualProjectBuilder.GetVirtualProjectPath(entryPointFilePath)],
            graph.Graph.ProjectNodesTopologicallySorted.Select(p => p.ProjectInstance.FullPath));
    }

    [TestMethod]
    public void ProjectsWithGlobalProperties()
    {
        var testAsset = TestAssetManager.CopyTestAsset("ProjectsWithGlobalProperties")
            .WithSource();

        var appProjectPath = Path.Combine(testAsset.Path, "App", "App.csproj");
        var libraryProjectPath = Path.Combine(testAsset.Path, "Library", "Library.csproj");

        var projectRepr = new ProjectRepresentation(projectPath: appProjectPath, entryPointFilePath: null);
        var buildProperties = new[] { KeyValuePair.Create("BuildProp", "1")}.ToImmutableDictionary();

        var factory = new ProjectGraphFactory([projectRepr], buildProperties, _testLogger, TestOptions.GlobalOptions, TestOptions.GetEnvironmentOptions(asset: testAsset));

        var graph = factory.TryLoadProjectGraph(projectGraphRequired: true, virtualProjectTargetFramework: null, CancellationToken.None);
        Assert.IsNotNull(graph);

        var messages = _testLogger.GetAndClearMessages();
        Assert.IsTrue(messages.Any(m =>
            m.Contains($"[Warning] Ignoring project instance '{libraryProjectPath}'", StringComparison.Ordinal) &&
            m.Contains("X=One", StringComparison.Ordinal) &&
            m.Contains("X=Two", StringComparison.Ordinal)));

        var map = graph.GetProjectInstanceMap(deepCopy: false);

        var actual = map.Select(entry => $"{entry.Key.ProjectPath} TFM={entry.Key.TargetFramework} X={(entry.Value.GlobalProperties.TryGetValue(\"X\", out var x) ? x : \"\")}").ToArray();
        Assert.Contains($"{appProjectPath} TFM={ToolsetInfo.CurrentTargetFramework} X=", actual);
        Assert.IsTrue(
            actual.Contains($"{libraryProjectPath} TFM={ToolsetInfo.CurrentTargetFramework} X=One") ||
            actual.Contains($"{libraryProjectPath} TFM={ToolsetInfo.CurrentTargetFramework} X=Two"));
    }
}
