// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

namespace Microsoft.DotNet.Cli.CommandFactory;

public class CommandSpec(
    string path,
    string? args,
    IDictionary<string, string>? environmentVariables = null)
{
    public string Path { get; } = path;

    public string? Args { get; } = args;

    public IDictionary<string, string> EnvironmentVariables { get; } = environmentVariables ?? new Dictionary<string, string>();

    internal void AddEnvironmentVariablesFromProject(IProject project)
    {
        foreach (var environmentVariable in project.EnvironmentVariables)
        {
            EnvironmentVariables.Add(environmentVariable.Key, environmentVariable.Value);
        }
    }
}
