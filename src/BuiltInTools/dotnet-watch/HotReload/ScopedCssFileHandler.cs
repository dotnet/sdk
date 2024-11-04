// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using Microsoft.Build.Graph;

namespace Microsoft.DotNet.Watch
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

            var logger = reporter.IsVerbose ? new[] { new Build.Logging.ConsoleLogger() } : null;

            var tasks = projectsToRefresh.Select(async projectNode =>
            {
                if (!projectNode.ProjectInstance.DeepCopy().Build(BuildTargetName, logger))
                {
                    return false;
                }

                if (browserConnector.TryGetRefreshServer(projectNode, out var browserRefreshServer))
                {
                    await HandleBrowserRefresh(browserRefreshServer, projectNode.ProjectInstance.FullPath, cancellationToken);
                }

                return true;
            });

            var results = await Task.WhenAll(tasks).WaitAsync(cancellationToken);

            if (hasApplicableFiles)
            {
                var successfulCount = results.Sum(r => r ? 1 : 0);

                if (successfulCount == results.Length)
                {
                    reporter.Output("Hot reload of scoped css succeeded.", emoji: "🔥");
                }
                else if (successfulCount > 0)
                {
                    reporter.Output($"Hot reload of scoped css partially succeeded: {successfulCount} project(s) out of {results.Length} were updated.", emoji: "🔥");
                }
                else
                {
                    reporter.Output("Hot reload of scoped css failed.", emoji: "🔥");
                }
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
            await browserRefreshServer.SendJsonMessageAsync(message, cancellationToken);
        }

        private readonly struct UpdateStaticFileMessage
        {
            public string Type => "UpdateStaticFile";

            public string Path { get; init; }
        }
    }
}
