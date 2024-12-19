// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Build.Graph;

namespace Microsoft.DotNet.Watch
{
    internal sealed class StaticFileHandler(IReporter reporter, ProjectNodeMap projectMap, BrowserConnector browserConnector)
    {
        private static readonly JsonSerializerOptions s_jsonSerializerOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public async ValueTask<bool> HandleFileChangesAsync(IReadOnlyList<ChangedFile> files, CancellationToken cancellationToken)
        {
            var allFilesHandled = true;
            var refreshRequests = new Dictionary<BrowserRefreshServer, List<string>>();
            var projectsWithoutRefreshServer = new HashSet<ProjectGraphNode>();

            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i].Item;

                if (file.StaticWebAssetPath is null)
                {
                    allFilesHandled = false;
                    continue;
                }

                reporter.Verbose($"Handling file change event for static content {file.FilePath}.");

                foreach (var containingProjectPath in file.ContainingProjectPaths)
                {
                    if (!projectMap.Map.TryGetValue(containingProjectPath, out var projectNodes))
                    {
                        // Shouldn't happen.
                        reporter.Warn($"Project '{containingProjectPath}' not found in the project graph.");
                        return allFilesHandled;
                    }

                    foreach (var projectNode in projectNodes)
                    {
                        if (browserConnector.TryGetRefreshServer(projectNode, out var refreshServer))
                        {
                            if (!refreshRequests.TryGetValue(refreshServer, out var filesPerServer))
                            {
                                reporter.Verbose($"[{projectNode.GetDisplayName()}] Refreshing browser.");
                                refreshRequests.Add(refreshServer, filesPerServer = []);
                            }

                            filesPerServer.Add(file.StaticWebAssetPath);
                        }
                        else if (projectsWithoutRefreshServer.Add(projectNode))
                        {
                            reporter.Verbose($"[{projectNode.GetDisplayName()}] No refresh server.");
                        }
                    }
                }
            }

            if (refreshRequests.Count == 0)
            {
                return allFilesHandled;
            }

            var tasks = refreshRequests.Select(async request =>
            {
                // Serialize all requests sent to a single server:
                foreach (var path in request.Value)
                {
                    reporter.Verbose($"Sending static file update request for asset '{path}'.");
                    var message = JsonSerializer.SerializeToUtf8Bytes(new UpdateStaticFileMessage { Path = path }, s_jsonSerializerOptions);
                    await request.Key.SendAsync(message, cancellationToken);
                }
            });

            await Task.WhenAll(tasks).WaitAsync(cancellationToken);

            reporter.Output("Hot Reload of static files succeeded.", emoji: "🔥");

            return allFilesHandled;
        }

        private readonly struct UpdateStaticFileMessage
        {
            public string Type => "UpdateStaticFile";
            public string Path { get; init; }
        }
    }
}
