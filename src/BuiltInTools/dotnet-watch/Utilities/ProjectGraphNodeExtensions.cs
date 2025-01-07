// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Graph;
using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Watch;

internal static class ProjectGraphNodeExtensions
{
    public static string GetDisplayName(this ProjectGraphNode projectNode)
        => $"{Path.GetFileNameWithoutExtension(projectNode.ProjectInstance.FullPath)} ({projectNode.GetTargetFramework()})";

    public static string GetTargetFramework(this ProjectGraphNode projectNode)
        => projectNode.ProjectInstance.GetPropertyValue("TargetFramework");

    public static Version? GetTargetFrameworkVersion(this ProjectGraphNode projectNode)
        => EnvironmentVariableNames.TryParseTargetFrameworkVersion(projectNode.ProjectInstance.GetPropertyValue("TargetFrameworkVersion"));

    public static ImmutableArray<string> GetWebAssemblyCapabilities(this ProjectGraphNode projectNode)
        => [.. projectNode.ProjectInstance.GetPropertyValue("WebAssemblyHotReloadCapabilities").Split(';').Select(static c => c.Trim()).Where(static c => c != "")];

    public static bool IsTargetFrameworkVersionOrNewer(this ProjectGraphNode projectNode, Version minVersion)
        => GetTargetFrameworkVersion(projectNode) is { } version && version >= minVersion;

    public static bool IsNetCoreApp(string identifier)
        => string.Equals(identifier, ".NETCoreApp", StringComparison.OrdinalIgnoreCase);

    public static bool IsNetCoreApp(this ProjectGraphNode projectNode)
        => IsNetCoreApp(projectNode.ProjectInstance.GetPropertyValue("TargetFrameworkIdentifier"));

    public static bool IsNetCoreApp(this ProjectGraphNode projectNode, Version minVersion)
        => IsNetCoreApp(projectNode) && IsTargetFrameworkVersionOrNewer(projectNode, minVersion);

    public static string? GetOutputDirectory(this ProjectGraphNode projectNode)
        => projectNode.ProjectInstance.GetPropertyValue("TargetPath") is { Length: >0 } path ? Path.GetDirectoryName(Path.Combine(projectNode.ProjectInstance.Directory, path)) : null;

    public static string? GetIntermediateOutputDirectory(this ProjectGraphNode projectNode)
        => projectNode.ProjectInstance.GetPropertyValue("IntermediateOutputPath") is { Length: >0 } path ? Path.Combine(projectNode.ProjectInstance.Directory, path) : null;

    public static IEnumerable<string> GetCapabilities(this ProjectGraphNode projectNode)
        => projectNode.ProjectInstance.GetItems("ProjectCapability").Select(item => item.EvaluatedInclude);

    public static IEnumerable<ProjectGraphNode> GetTransitivelyReferencingProjects(this IEnumerable<ProjectGraphNode> projects)
    {
        var visited = new HashSet<ProjectGraphNode>();
        var queue = new Queue<ProjectGraphNode>();
        foreach (var project in projects)
        {
            queue.Enqueue(project);
        }

        while (queue.Count > 0)
        {
            var project = queue.Dequeue();
            if (visited.Add(project))
            {
                foreach (var referencingProject in project.ReferencingProjects)
                {
                    queue.Enqueue(referencingProject);
                }
            }
        }

        return visited;
    }
}
