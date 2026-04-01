// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.Watch.UnitTests;

public class CompilationHandlerTests(ITestOutputHelper output) : DotNetWatchTestBase(output)
{
    [Fact]
    public async Task ReferenceOutputAssembly_False()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchAppMultiProc")
            .WithSource();

        var workingDirectory = testAsset.Path;
        var hostDir = Path.Combine(testAsset.Path, "Host");
        var hostProject = Path.Combine(hostDir, "Host.csproj");
        var hostProjectRepr = new ProjectRepresentation(hostProject, entryPointFilePath: null);

        var cmdOptions = TestOptions.GetCommandLineOptions(["--project", hostProject]);
        var projectOptions = TestOptions.GetProjectOptions(cmdOptions);
        var environmentOptions = TestOptions.GetEnvironmentOptions(Environment.CurrentDirectory);

        var factory = new ProjectGraphFactory([hostProjectRepr], virtualProjectTargetFramework: null, buildProperties: [], NullLogger.Instance);
        var projectGraph = factory.TryLoadProjectGraph(projectGraphRequired: false, CancellationToken.None);
        Assert.NotNull(projectGraph);

        var processOutputReporter = new TestProcessOutputReporter();

        var handler = new RunningProjectsManager(new ProcessRunner(processCleanupTimeout: TimeSpan.Zero), NullLogger.Instance);
        var workspace = new ManagedCodeWorkspace(NullLogger.Instance, handler);

        await workspace.UpdateProjectGraphAsync(projectGraph.Graph, CancellationToken.None);

        // all projects are present
        AssertEx.SequenceEqual(["Host", "Lib2", "Lib", "A", "B"], workspace.CurrentSolution.Projects.Select(p => p.Name));

        // Host does not have project reference to A, B:
        AssertEx.SequenceEqual(["Lib2"],
            workspace.CurrentSolution.Projects.Single(p => p.Name == "Host").ProjectReferences
                .Select(r => workspace.CurrentSolution.GetProject(r.ProjectId)!.Name));
    }
}
