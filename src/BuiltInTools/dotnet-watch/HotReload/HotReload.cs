// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using Microsoft.Build.Graph;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed class HotReload(IReporter reporter, ProjectGraph projectGraph, BrowserRefreshServer? browserRefreshServer) : IDisposable
    {
        private readonly StaticFileHandler _staticFileHandler = new(reporter);
        private readonly ScopedCssFileHandler _scopedCssFileHandler = new(reporter, browserRefreshServer);
        private readonly CompilationHandler _compilationHandler = new(reporter, projectGraph, browserRefreshServer);

        public void Dispose()
        {
            _compilationHandler.Dispose();
        }

        public Task InitializeAsync(ProjectInfo project, string namedPipeName, CancellationToken cancellationToken)
            => _compilationHandler.InitializeAsync(project, namedPipeName, cancellationToken);

        public async ValueTask<bool> TryHandleFileChange(DotNetWatchContext context, FileItem[] files, CancellationToken cancellationToken)
        {
            HotReloadEventSource.Log.HotReloadStart(HotReloadEventSource.StartType.Main);

            var fileHandlerResult = false;
            for (var i = files.Length - 1; i >= 0; i--)
            {
                var file = files[i];
                if (await _staticFileHandler.TryHandleFileChange(browserRefreshServer, file, cancellationToken) ||
                    await _scopedCssFileHandler.TryHandleFileChange(context, file, cancellationToken))
                {
                    fileHandlerResult = true;
                }
            }

            fileHandlerResult |= await _compilationHandler.TryHandleFileChange(context, files, cancellationToken);

            HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.Main);
            return fileHandlerResult;
        }
    }
}
