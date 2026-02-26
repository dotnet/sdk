// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
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

        var options = TestOptions.GetProjectOptions(["--project", hostProject]);
        var environmentOptions = TestOptions.GetEnvironmentOptions(Environment.CurrentDirectory, "dotnet");

        var factory = new ProjectGraphFactory(globalOptions: []);
        var projectGraph = factory.TryLoadProjectGraph(options.ProjectPath, NullLogger.Instance, projectGraphRequired: false, CancellationToken.None);

        var processOutputReporter = new TestProcessOutputReporter();

        var context = new DotNetWatchContext()
        {
            ProcessOutputReporter = processOutputReporter,
            Logger = NullLogger.Instance,
            BuildLogger = NullLogger.Instance,
            LoggerFactory = NullLoggerFactory.Instance,
            ProcessRunner = new ProcessRunner(processCleanupTimeout: TimeSpan.Zero),
            Options = new(),
            RootProjectOptions = TestOptions.ProjectOptions,
            EnvironmentOptions = environmentOptions,
            BrowserLauncher = new BrowserLauncher(NullLogger.Instance, processOutputReporter, environmentOptions),
            BrowserRefreshServerFactory = new BrowserRefreshServerFactory()
        };

        var handler = new CompilationHandler(context);

        await handler.Workspace.UpdateProjectConeAsync(hostProject, CancellationToken.None);

        // all projects are present
        AssertEx.SequenceEqual(["Host", "Lib2", "Lib", "A", "B"], handler.Workspace.CurrentSolution.Projects.Select(p => p.Name));

        // Host does not have project reference to A, B:
        AssertEx.SequenceEqual(["Lib2"],
            handler.Workspace.CurrentSolution.Projects.Single(p => p.Name == "Host").ProjectReferences
                .Select(r => handler.Workspace.CurrentSolution.GetProject(r.ProjectId)!.Name));
    }
}
