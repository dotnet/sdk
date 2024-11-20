// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.Build.Graph;

namespace Microsoft.DotNet.Watch.UnitTests;

internal class TestRuntimeProcessLauncher(ProjectLauncher projectLauncher) : IRuntimeProcessLauncher
{
    public class Factory(Action<TestRuntimeProcessLauncher>? initialize = null) : IRuntimeProcessLauncherFactory
    {
        public IRuntimeProcessLauncher TryCreate(ProjectGraphNode projectNode, ProjectLauncher projectLauncher, IReadOnlyList<string> buildArguments)
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
}
