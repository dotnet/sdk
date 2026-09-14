// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests;

internal class TestRuntimeProcessLauncher(ProjectLauncher projectLauncher) : IRuntimeProcessLauncher
{
    public class Factory(Action<TestRuntimeProcessLauncher>? initialize = null) : IRuntimeProcessLauncherFactory
    {
        public IRuntimeProcessLauncher Create(ProjectLauncher projectLauncher)
        {
            var service = new TestRuntimeProcessLauncher(projectLauncher);
            initialize?.Invoke(service);
            return service;
        }
    }

    public Func<IEnumerable<(string name, string value)>>? GetEnvironmentVariablesImpl;
    public Action? TerminateLaunchedProcessesImpl;

    public ProjectLauncher ProjectLauncher { get; } = projectLauncher;

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    public IEnumerable<(string name, string value)> GetEnvironmentVariables()
        => GetEnvironmentVariablesImpl?.Invoke() ?? [];

    public ValueTask TerminateLaunchedProcessesAsync(CancellationToken cancellationToken)
    {
        TerminateLaunchedProcessesImpl?.Invoke();
        return ValueTask.CompletedTask;
    }

    public async Task<RunningProject> Launch(string projectPath, string workingDirectory, CancellationToken cancellationToken)
    {
        var projectOptions = new ProjectOptions()
        {
            IsMainProject = false,
            Representation = new ProjectRepresentation(projectPath, entryPointFilePath: null),
            WorkingDirectory = workingDirectory,
            Command = "run",
            CommandArguments = ["--project", projectPath],
            LaunchEnvironmentVariables = [],
            LaunchProfileName = default,
        };

        RunningProject? runningProject = null;
        RestartOperation? startOp = null;
        startOp = new RestartOperation(async cancellationToken =>
        {
            Assert.NotNull(startOp);

            runningProject = await ProjectLauncher.TryLaunchProcessAsync(
                projectOptions,
                onOutput: null,
                onExit: null,
                restartOperation: startOp,
                cancellationToken);

            Assert.NotNull(runningProject);

            await runningProject.Clients.WaitForConnectionEstablishedAsync(cancellationToken);
        });

        await startOp(cancellationToken);

        Assert.NotNull(runningProject);
        return runningProject;
    }
}
