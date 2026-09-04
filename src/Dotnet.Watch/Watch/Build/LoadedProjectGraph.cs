// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal sealed class LoadedProjectGraph(ProjectGraph graph, ProjectCollection collection, ILogger logger, GlobalOptions globalOptions, EnvironmentOptions environmentOptions)
{
    // full path of proj file to list of nodes representing all target frameworks of the project (excluding outer build nodes):
    private readonly ImmutableDictionary<string, ImmutableList<ProjectGraphNode>> _innerBuildNodes = CreateProjectNodeMap(graph, logger);

    private readonly Lazy<IReadOnlySet<string>> _lazyBuildFiles = new(() =>
        graph.ProjectNodes.SelectMany(p => p.ProjectInstance.ImportPaths)
            .Concat(graph.ProjectNodes.Select(p => p.ProjectInstance.FullPath))
            .ToHashSet(PathUtilities.OSSpecificPathComparer));

    public readonly ProjectBuildManager BuildManager =
        new(collection, new BuildReporter(logger, globalOptions, environmentOptions));

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

    public ProjectGraphNode GetProjectNode(ProjectInstanceId projectId)
        => _innerBuildNodes[projectId.ProjectPath].Single(n => n.ProjectInstance.GetTargetFramework() == projectId.TargetFramework);

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

        var node = projectNodes.SingleOrDefault(n => n.ProjectInstance.GetTargetFramework() == targetFramework);
        if (node == null)
        {
            logger.LogError("Project '{ProjectPath}' doesn't have a target for {TargetFramework}.", projectPath, targetFramework);
        }

        return node;
    }

    /// <summary>
    /// Returns a map of project nodes with <see cref="PropertyNames.TargetFramework"/> set.
    /// Skips nodes that don't have <see cref="PropertyNames.TargetFramework"/> set (e.g. outer build nodes) or
    /// are not unique based on path and <see cref="PropertyNames.TargetFramework"/>.
    /// </summary>
    /// <remarks>
    /// Although it is valid to have project nodes in the graph that only differ in global properties other than <see cref="PropertyNames.TargetFramework"/>,
    /// it is not generally supported in tooling.
    /// </remarks>
    private static ImmutableDictionary<string, ImmutableList<ProjectGraphNode>> CreateProjectNodeMap(ProjectGraph graph, ILogger logger)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, ImmutableList<ProjectGraphNode>>();

        foreach (var node in graph.ProjectNodes)
        {
            var projectId = node.ProjectInstance.GetId();
            if (projectId.TargetFramework is "")
            {
                // skip outer build nodes
                continue;
            }

            if (!builder.TryGetValue(projectId.ProjectPath, out var existingNodes))
            {
                existingNodes = [];
            }
            else if (existingNodes.FirstOrDefault(p => p.ProjectInstance.GetTargetFramework() == projectId.TargetFramework) is { } existingNode)
            {
                ReportGlobalPropertiesWarning(logger, node, existingNode);
                continue;
            }

            builder[projectId.ProjectPath] = existingNodes.Add(node);
        }

        return builder.ToImmutable();
    }

    private static void ReportGlobalPropertiesWarning(ILogger logger, ProjectGraphNode node, ProjectGraphNode existingNode)
    {
        var propertiesDiff = node.ProjectInstance.GlobalProperties.Union(existingNode.ProjectInstance.GlobalProperties)
            .Except(node.ProjectInstance.GlobalProperties.Intersect(existingNode.ProjectInstance.GlobalProperties))
            .Select(entry => entry.Key)
            .Distinct();

        logger.LogWarning("Ignoring project instance '{ProjectPath}' ({Value1}) because another one already exists and only differs in the values of global properties: {Values2}",
            node.ProjectInstance.FullPath,
            DisplayProperties(node),
            DisplayProperties(existingNode));

        string DisplayProperties(ProjectGraphNode node)
            => string.Join(",", propertiesDiff.Select(propertyName =>
                $"{propertyName}={(node.ProjectInstance.GlobalProperties.TryGetValue(propertyName, out var value) ? value : "<unset>")}"));

    public IReadOnlyDictionary<ProjectInstanceId, ProjectInstance> GetProjectInstanceMap(bool deepCopy)
        => _innerBuildNodes.SelectMany(entry => entry.Value).ToImmutableDictionary(
                keySelector: node => node.ProjectInstance.GetId(),
                elementSelector: node => deepCopy ? node.ProjectInstance.DeepCopy() : node.ProjectInstance);
}
