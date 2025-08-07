// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.Build.Graph;

namespace Microsoft.DotNet.Watcher.Tests;

internal class TestRuntimeProcessLauncher(ProjectLauncher projectLauncher) : IRuntimeProcessLauncher
{
    public class Factory(Action<TestRuntimeProcessLauncher>? initialize = null) : IRuntimeProcessLauncherFactory
    {
        public IRuntimeProcessLauncher TryCreate(ProjectGraphNode projectNode, ProjectLauncher projectLauncher, IReadOnlyList<(string name, string value)> buildProperties)
        {
            var service = new TestRuntimeProcessLauncher(projectLauncher);
            initialize?.Invoke(service);
            return service;
        }
    }

    public Func<IEnumerable<(string name, string value)>>? GetEnvironmentVariablesImpl;

    public ProjectLauncher ProjectLauncher { get; } = projectLauncher;

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    public ValueTask<IEnumerable<(string name, string value)>> GetEnvironmentVariablesAsync(CancellationToken cancelToken)
        => ValueTask.FromResult(GetEnvironmentVariablesImpl?.Invoke() ?? []);
}
