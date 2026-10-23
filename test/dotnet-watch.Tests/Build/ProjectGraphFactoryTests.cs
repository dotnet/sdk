// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

extern alias MSTestFramework;

using System.Collections.Immutable;
using Microsoft.DotNet.FileBasedPrograms;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class ProjectGraphFactoryTests
{
    public TestContext TestContext { get; set; } = null!;
    private DualOutputHelper? _output;
    private DualOutputHelper Output => _output ??= new(new MSTestFramework::Microsoft.NET.TestFramework.TestContextOutputHelper(TestContext));
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
    public void AdditionalBuildProperties_ApplyToEachLoad()
    {
        var dir = TestAssetManager.CreateTestDirectory().Path;
        var projectPath = Path.Combine(dir, "Project.csproj");
        File.WriteAllText(projectPath, $"""
            <Project>
              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                <IntermediateOutputPath>obj\$(Configuration)\$(TargetFramework)\$(RuntimeIdentifier)\</IntermediateOutputPath>
              </PropertyGroup>
            </Project>
            """);

        var projectRepr = new ProjectRepresentation(projectPath, entryPointFilePath: null);
        var factory = new ProjectGraphFactory(
            [projectRepr],
            buildProperties: ImmutableDictionary<string, string>.Empty
                .Add("Configuration", "Debug")
                .Add("RuntimeIdentifier", "iossimulator-arm64"),
            _testLogger,
            TestOptions.GlobalOptions,
            TestOptions.GetEnvironmentOptions());
        var deviceProperties = ImmutableDictionary<string, string>.Empty
            .Add("Device", "test-iphone")
            .Add("RuntimeIdentifier", "ios-arm64");

        var graph = factory.TryLoadProjectGraph(
            projectGraphRequired: true, virtualProjectTargetFramework: null, CancellationToken.None, deviceProperties);
        Assert.IsNotNull(graph);
        var instance = graph.Graph.GraphRoots.Single().ProjectInstance;
        Assert.AreEqual("test-iphone", instance.GetPropertyValue("Device"));
        Assert.AreEqual("ios-arm64", instance.GetPropertyValue("RuntimeIdentifier"));
        Assert.AreEqual(
            Path.Combine(dir, "obj", "Debug", ToolsetInfo.CurrentTargetFramework, "ios-arm64"),
            Path.TrimEndingDirectorySeparator(instance.GetIntermediateOutputDirectory()!));

        var defaultGraph = factory.TryLoadProjectGraph(projectGraphRequired: true, virtualProjectTargetFramework: null, CancellationToken.None);
        Assert.IsNotNull(defaultGraph);
        var defaultInstance = defaultGraph.Graph.GraphRoots.Single().ProjectInstance;
        Assert.AreEqual("", defaultInstance.GetPropertyValue("Device"));
        Assert.AreEqual("iossimulator-arm64", defaultInstance.GetPropertyValue("RuntimeIdentifier"));
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
}
