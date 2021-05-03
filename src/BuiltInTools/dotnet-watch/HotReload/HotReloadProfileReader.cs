// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Graph;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    public static class HotReloadProfileReader
    {
        public static HotReloadProfile InferHotReloadProfile(ProjectGraph projectGraph, IReporter reporter)
        {
            var queue = new Queue<ProjectGraphNode>(projectGraph.EntryPointNodes);

            var seenAspNetCoreProject = false;

            while (queue.Count > 0)
            {
                var next = queue.Dequeue();
                var projectCapability = next.ProjectInstance.GetItems("ProjectCapability");

                foreach (var item in projectCapability)
                {
                    if (item.EvaluatedInclude == "AspNetCore")
                    {
                        seenAspNetCoreProject = true;
                    }
                    else if (item.EvaluatedInclude == "WebAssembly")
                    {
                        if (seenAspNetCoreProject)
                        {
                            reporter.Verbose("HotReloadProfile: BlazorHosted.");
                            return HotReloadProfile.BlazorHosted;
                        }

                        reporter.Verbose("HotReloadProfile: BlazorWebAssembly.");
                        return HotReloadProfile.BlazorWebAssembly;
                    }
                }

                foreach (var project in next.ProjectReferences)
                {
                    queue.Enqueue(project);
                }
            }

            reporter.Verbose("HotReloadProfile: Default.");
            return HotReloadProfile.Default;
        }
    }
}
