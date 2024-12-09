// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tests;

public class CompilationHandlerTests(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
{
    [Fact]
    public async Task ReferenceOutputAssembly_False()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchAppMultiProc")
            .WithSource();

        var workingDirectory = testAsset.Path;
        var hostDir = Path.Combine(testAsset.Path, "Host");
        var hostProject = Path.Combine(hostDir, "Host.csproj");

        var reporter = new TestReporter(Logger);
        var options = TestOptions.GetProjectOptions(["--project", hostProject]);

        var factory = new MSBuildFileSetFactory(
            rootProjectFile: options.ProjectPath,
            targetFramework: null,
            buildProperties: [],
            environmentOptions: new EnvironmentOptions(Environment.CurrentDirectory, "dotnet"),
            reporter);

        var projectGraph = factory.TryLoadProjectGraph(projectGraphRequired: false);
        var handler = new CompilationHandler(reporter);

        await handler.Workspace.UpdateProjectConeAsync(hostProject, CancellationToken.None);

        // all projects are present
        AssertEx.SequenceEqual(["Host", "Lib2", "Lib", "A", "B"], handler.Workspace.CurrentSolution.Projects.Select(p => p.Name));

        // Host does not have project reference to A, B:
        AssertEx.SequenceEqual(["Lib2"],
            handler.Workspace.CurrentSolution.Projects.Single(p => p.Name == "Host").ProjectReferences
                .Select(r => handler.Workspace.CurrentSolution.GetProject(r.ProjectId)!.Name));
    }
}
