﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Graph;

namespace Microsoft.DotNet.Watch;

internal abstract partial class HotReloadAppModel(ProjectGraphNode? agentInjectionProject)
{
    public abstract bool RequiresBrowserRefresh { get; }

    public abstract DeltaApplier? CreateDeltaApplier(BrowserRefreshServer? browserRefreshServer, IReporter processReporter);

    /// <summary>
    /// Returns true and the path to the client agent implementation binary if the application needs the agent to be injected.
    /// </summary>
    public bool TryGetStartupHookPath([NotNullWhen(true)] out string? path)
    {
        if (agentInjectionProject == null)
        {
            path = null;
            return false;
        }

        var hookTargetFramework = agentInjectionProject.GetTargetFramework() switch
        {
            // Note: Hot Reload is only supported on net6.0+
            "net6.0" or "net7.0" or "net8.0" or "net9.0" => "netstandard2.1",
            _ => "net10.0",
        };

        path = Path.Combine(AppContext.BaseDirectory, "hotreload", hookTargetFramework, "Microsoft.Extensions.DotNetDeltaApplier.dll");
        return true;
    }

    public static HotReloadAppModel InferFromProject(ProjectGraphNode projectNode, IReporter reporter)
    {
        if (projectNode.IsWebApp())
        {
            var queue = new Queue<ProjectGraphNode>();
            queue.Enqueue(projectNode);

            ProjectGraphNode? aspnetCoreProject = null;

            var visited = new HashSet<ProjectGraphNode>();

            while (queue.Count > 0)
            {
                var currentNode = queue.Dequeue();
                var projectCapability = currentNode.ProjectInstance.GetItems("ProjectCapability");

                foreach (var item in projectCapability)
                {
                    if (item.EvaluatedInclude == "AspNetCore")
                    {
                        aspnetCoreProject = currentNode;
                        break;
                    }

                    if (item.EvaluatedInclude == "WebAssembly")
                    {
                        // We saw a previous project that was AspNetCore. This must be a blazor hosted app.
                        if (aspnetCoreProject is not null && aspnetCoreProject.ProjectInstance != currentNode.ProjectInstance)
                        {
                            reporter.Verbose($"HotReloadProfile: BlazorHosted. {aspnetCoreProject.ProjectInstance.FullPath} references BlazorWebAssembly project {currentNode.ProjectInstance.FullPath}.", emoji: "🔥");
                            return new BlazorWebAssemblyHostedAppModel(clientProject: currentNode, serverProject: aspnetCoreProject);
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
        return new DefaultAppModel(projectNode);
    }
}
