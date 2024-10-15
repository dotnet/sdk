// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using Microsoft.Build.Graph;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal readonly struct ProjectNodeMap(ProjectGraph graph, IReporter reporter)
    {
        public readonly ProjectGraph Graph = graph;

        public readonly IReadOnlyDictionary<string, IReadOnlyList<ProjectGraphNode>> Map = 
            graph.ProjectNodes.GroupBy(n => n.ProjectInstance.FullPath).ToDictionary(
                keySelector: static g => g.Key,
                elementSelector: static g => (IReadOnlyList<ProjectGraphNode>)[.. g]);

        public IReadOnlyList<ProjectGraphNode> GetProjectNodes(string projectPath)
        {
            if (Map.TryGetValue(projectPath, out var rootProjectNodes))
            {
                return rootProjectNodes;
            }

            reporter.Error($"Project '{projectPath}' not found in the project graph.");
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
                    reporter.Error($"Project '{projectPath}' targets multiple frameworks. Specify which framework to run using '--framework'.");
                    return null;
                }

                return projectNodes[0];
            }

            ProjectGraphNode? candidate = null;
            foreach (var node in projectNodes)
            {
                if (node.ProjectInstance.GetPropertyValue("TargetFramework") == targetFramework)
                {
                    if (candidate != null)
                    {
                        // shouldn't be possible:
                        reporter.Warn($"Project '{projectPath}' has multiple instances targeting {targetFramework}.");
                        return candidate;
                    }

                    candidate = node;
                }
            }

            if (candidate == null)
            {
                reporter.Error($"Project '{projectPath}' doesn't have a target for {targetFramework}.");
            }

            return candidate;
        }
    }
}
