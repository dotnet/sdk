// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using Microsoft.Build.Graph;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch
{
    internal sealed class ScopedCssFileHandler(ILogger logger, ILogger buildLogger, ProjectNodeMap projectMap, BrowserConnector browserConnector, GlobalOptions options, EnvironmentOptions environmentOptions)
    {
        private const string BuildTargetName = TargetNames.GenerateComputedBuildStaticWebAssets;

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
                logger.LogDebug("Handling file change event for scoped css file {FilePath}.", file.FilePath);
                foreach (var containingProjectPath in file.ContainingProjectPaths)
                {
                    if (!projectMap.Map.TryGetValue(containingProjectPath, out var projectNodes))
                    {
                        // Shouldn't happen.
                        logger.LogWarning("Project '{Path}' not found in the project graph.", containingProjectPath);
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

            var buildReporter = new BuildReporter(buildLogger, options, environmentOptions);

            var buildTasks = projectsToRefresh.Select(projectNode => Task.Run(() =>
            {
                using var loggers = buildReporter.GetLoggers(projectNode.ProjectInstance.FullPath, BuildTargetName);

                // Deep copy so that we don't pollute the project graph:
                if (!projectNode.ProjectInstance.DeepCopy().Build(BuildTargetName, loggers))
                {
                    loggers.ReportOutput();
                    return null;
                }

                return projectNode;
            }));

            var buildResults = await Task.WhenAll(buildTasks).WaitAsync(cancellationToken);

            var browserRefreshTasks = buildResults.Where(p => p != null)!.GetTransitivelyReferencingProjects().Select(async projectNode =>
            {
                if (browserConnector.TryGetRefreshServer(projectNode, out var browserRefreshServer))
                {
                    // We'd like an accurate scoped css path, but this needs a lot of work to wire-up now.
                    // We'll handle this as part of https://github.com/dotnet/aspnetcore/issues/31217.
                    // For now, we'll make it look like some css file which would cause JS to update a
                    // single file if it's from the current project, or all locally hosted css files if it's a file from
                    // referenced project.
                    var relativeUrl = Path.GetFileNameWithoutExtension(projectNode.ProjectInstance.FullPath) + ".css";
                    await browserRefreshServer.UpdateStaticAssetsAsync([relativeUrl], cancellationToken);
                }
            });

            await Task.WhenAll(browserRefreshTasks).WaitAsync(cancellationToken);

            var successfulCount = buildResults.Sum(r => r != null ? 1 : 0);

            if (successfulCount == buildResults.Length)
            {
                logger.Log(MessageDescriptor.HotReloadOfScopedCssSucceeded);
            }
            else if (successfulCount > 0)
            {
                logger.Log(MessageDescriptor.HotReloadOfScopedCssPartiallySucceeded, successfulCount, buildResults.Length);
            }
            else
            {
                logger.Log(MessageDescriptor.HotReloadOfScopedCssFailed);
            }
        }
    }
}
