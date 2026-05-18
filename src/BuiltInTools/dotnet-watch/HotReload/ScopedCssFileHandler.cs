// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Collections;
using System.Diagnostics;
using Microsoft.Build.Graph;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed class ScopedCssFileHandler(IReporter reporter, ProjectNodeMap projectMap, BrowserConnector browserConnector)
    {
        private const string BuildTargetName = "GenerateComputedBuildStaticWebAssets";

        public async ValueTask HandleFileChangesAsync(IReadOnlyList<ChangedFile> files, CancellationToken cancellationToken)
        {
            var projectsToRefresh = new HashSet<ProjectGraphNode>();
            var hasApplicableFiles = false;

            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i].Item;

                if (!file.FilePath.EndsWith(".razor.css", StringComparison.Ordinal) &&
                    !file.FilePath.EndsWith(".cshtml.css", StringComparison.Ordinal))
                {
                    continue;
                }

                hasApplicableFiles = true;
                reporter.Verbose($"Handling file change event for scoped css file {file.FilePath}.");
                foreach (var containingProjectPath in file.ContainingProjectPaths)
                {
                    if (!projectMap.Map.TryGetValue(containingProjectPath, out var projectNodes))
                    {
                        // Shouldn't happen.
                        reporter.Warn($"Project '{containingProjectPath}' not found in the project graph.");
                        continue;
                    }

                    // Build and refresh each instance (TFM) of the project.
                    foreach (var projectNode in projectNodes)
                    {
                        // The outer build project instance (that specifies TargetFrameworks) won't have the target.
                        if (projectNode.ProjectInstance.Targets.ContainsKey(BuildTargetName))
                        {
                            projectsToRefresh.Add(projectNode);
                        }
                    }
                }
            }

            if (!hasApplicableFiles)
            {
                return;
            }

            var logger = reporter.IsVerbose ? new[] { new Build.Logging.ConsoleLogger(LoggerVerbosity.Minimal) } : null;

            var buildTasks = projectsToRefresh.Select(projectNode => Task.Run(() =>
            {
                try
                {
                    if (!projectNode.ProjectInstance.DeepCopy().Build(BuildTargetName, logger))
                    {
                        return null;
                    }
                }
                catch (Exception e)
                {
                    reporter.Error($"[{projectNode.GetDisplayName()}] Target {BuildTargetName} failed to build: {e}");
                    return null;
                }

                return projectNode;
            }));

            var buildResults = await Task.WhenAll(buildTasks).WaitAsync(cancellationToken);

            var browserRefreshTasks = buildResults.Where(p => p != null)!.GetTransitivelyReferencingProjects().Select(async projectNode =>
            {
                if (browserConnector.TryGetRefreshServer(projectNode, out var browserRefreshServer))
                {
                    reporter.Verbose($"[{projectNode.GetDisplayName()}] Refreshing browser.");
                    await HandleBrowserRefresh(browserRefreshServer, projectNode.ProjectInstance.FullPath, cancellationToken);
                }
                else
                {
                    reporter.Verbose($"[{projectNode.GetDisplayName()}] No refresh server.");
                }
            });

            await Task.WhenAll(browserRefreshTasks).WaitAsync(cancellationToken);

            var successfulCount = buildResults.Sum(r => r != null ? 1 : 0);

            if (successfulCount == buildResults.Length)
            {
                reporter.Output("Hot reload of scoped css succeeded.", emoji: "🔥");
            }
            else if (successfulCount > 0)
            {
                reporter.Output($"Hot reload of scoped css partially succeeded: {successfulCount} project(s) out of {buildResults.Length} were updated.", emoji: "🔥");
            }
            else
            {
                reporter.Output("Hot reload of scoped css failed.", emoji: "🔥");
            }
        }

        private static async Task HandleBrowserRefresh(BrowserRefreshServer browserRefreshServer, string containingProjectPath, CancellationToken cancellationToken)
        {
            // We'd like an accurate scoped css path, but this needs a lot of work to wire-up now.
            // We'll handle this as part of https://github.com/dotnet/aspnetcore/issues/31217.
            // For now, we'll make it look like some css file which would cause JS to update a
            // single file if it's from the current project, or all locally hosted css files if it's a file from
            // referenced project.
            var cssFilePath = Path.GetFileNameWithoutExtension(containingProjectPath) + ".css";
            var message = new UpdateStaticFileMessage { Path = cssFilePath };
            await browserRefreshServer.SendJsonSerlialized(message, cancellationToken);
        }

        private readonly struct UpdateStaticFileMessage
        {
            public string Type => "UpdateStaticFile";

            public string Path { get; init; }
        }
    }
}
