// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Execution;
using Microsoft.Build.Graph;

namespace Microsoft.DotNet.Watch;

internal abstract partial class HotReloadAppModel
{
    public abstract bool RequiresBrowserRefresh { get; }

    /// <summary>
    /// True to inject delta applier to the process.
    /// </summary>
    public abstract bool InjectDeltaApplier { get; }

    public abstract DeltaApplier? CreateDeltaApplier(BrowserRefreshServer? browserRefreshServer, IReporter processReporter);

    public static HotReloadAppModel InferFromProject(ProjectGraphNode projectNode, IReporter reporter)
    {
        if (projectNode.IsWebApp())
        {
            var queue = new Queue<ProjectGraphNode>();
            queue.Enqueue(projectNode);

            ProjectInstance? aspnetCoreProject = null;

            var visited = new HashSet<ProjectGraphNode>();

            while (queue.Count > 0)
            {
                var currentNode = queue.Dequeue();
                var projectCapability = currentNode.ProjectInstance.GetItems("ProjectCapability");

                foreach (var item in projectCapability)
                {
                    if (item.EvaluatedInclude == "AspNetCore")
                    {
                        aspnetCoreProject = currentNode.ProjectInstance;
                        break;
                    }

                    if (item.EvaluatedInclude == "WebAssembly")
                    {
                        // We saw a previous project that was AspNetCore. This must be a blazor hosted app.
                        if (aspnetCoreProject is not null && aspnetCoreProject != currentNode.ProjectInstance)
                        {
                            reporter.Verbose($"HotReloadProfile: BlazorHosted. {aspnetCoreProject.FullPath} references BlazorWebAssembly project {currentNode.ProjectInstance.FullPath}.", emoji: "🔥");
                            return new BlazorWebAssemblyHostedAppModel(clientProject: currentNode);
                        }

                        reporter.Verbose("HotReloadProfile: BlazorWebAssembly.", emoji: "🔥");
                        return new BlazorWebAssemblyAppModel(clientProject: currentNode);
                    }
                }

                foreach (var project in currentNode.ProjectReferences)
                {
                    if (visited.Add(project))
                    {
                        queue.Enqueue(project);
                    }
                }
            }
        }

        reporter.Verbose("HotReloadProfile: Default.", emoji: "🔥");
        return DefaultAppModel.Instance;
    }
}
