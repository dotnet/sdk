// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tests;

public class CompilationHandlerTests(ITestOutputHelper logger) : DotNetWatchTestBase(logger)
{
    [Fact]
    public async Task ReferenceOutputAssembly_False()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchAppAspire")
            .WithSource();

        var workingDirectory = testAsset.Path;
        var hostDir = Path.Combine(testAsset.Path, "Host");
        var hostProject = Path.Combine(hostDir, "Host.csproj");

        var reporter = new TestReporter(Logger);
        var options = TestOptions.GetProjectOptions(["--project", hostProject]);

        var projectGraph = Program.TryReadProject(options, reporter);
        var projectMap = new ProjectNodeMap(projectGraph, reporter);

        var handler = new CompilationHandler(reporter, projectMap);

        var projectsToBeRebuilt = handler.CurrentSolution.Projects.Where(p => p.Name == "Host").Select(p => p.Id).ToHashSet();
        await handler.RestartSessionAsync(projectsToBeRebuilt, CancellationToken.None);

        AssertEx.SequenceEqual(["Host", "Lib", "A", "B"], handler.CurrentSolution.Projects.Select(p => p.Name));
    }
}
