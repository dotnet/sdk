// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Graph;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal sealed class LoadedProjectGraph(ProjectGraph graph, ProjectCollection collection, ILogger logger)
{
    // full path of proj file to list of nodes representing all target frameworks of the project (excluding outer build nodes):
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ProjectGraphNode>> _innerBuildNodes = 
        graph.ProjectNodes.Where(n => n.ProjectInstance.GetTargetFramework() != "").GroupBy(n => n.ProjectInstance.FullPath).ToDictionary(
            keySelector: static g => g.Key,
            elementSelector: static g => (IReadOnlyList<ProjectGraphNode>)[.. g]);

    private readonly Lazy<IReadOnlySet<string>> _lazyBuildFiles = new(() =>
        graph.ProjectNodes.SelectMany(p => p.ProjectInstance.ImportPaths)
            .Concat(graph.ProjectNodes.Select(p => p.ProjectInstance.FullPath))
            .ToHashSet(PathUtilities.OSSpecificPathComparer));

    public ProjectGraph Graph => graph;
    public ILogger Logger => logger;
    public ProjectCollection ProjectCollection => collection;

    public IReadOnlySet<string> BuildFiles => _lazyBuildFiles.Value;

    public IReadOnlyList<ProjectGraphNode> GetProjectNodes(string projectPath)
    {
        if (_innerBuildNodes.TryGetValue(projectPath, out var nodes))
        {
            return nodes;
        }

        logger.LogError("Project '{ProjectPath}' not found in the project graph.", projectPath);
        return [];
    }

    public ProjectGraphNode? TryGetProjectNode(string projectPath, string? targetFramework)
    {
        var projectNodes = GetProjectNodes(projectPath);
        if (projectNodes is [])
        {
            return null;
        }

        if (targetFramework == null)
        {
            if (projectNodes.Count > 1)
            {
                logger.LogError("Project '{ProjectPath}' targets multiple frameworks. Specify which framework to run using '--framework'.", projectPath);
                return null;
            }

            return projectNodes[0];
        }

        ProjectGraphNode? candidate = null;
        foreach (var node in projectNodes)
        {
            if (node.ProjectInstance.GetTargetFramework() == targetFramework)
            {
                if (candidate != null)
                {
                    // shouldn't be possible:
                    logger.LogWarning("Project '{ProjectPath}' has multiple instances targeting {TargetFramework}.", projectPath, targetFramework);
                    return candidate;
                }

                candidate = node;
            }
        }

        if (candidate == null)
        {
            logger.LogError("Project '{ProjectPath}' doesn't have a target for {TargetFramework}.", projectPath, targetFramework);
        }

        return candidate;
    }
}
