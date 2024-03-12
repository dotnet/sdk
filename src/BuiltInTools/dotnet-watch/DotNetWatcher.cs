// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.DotNet.Watcher.Tools;

namespace Microsoft.DotNet.Watcher
{
    internal sealed class DotNetWatcher(DotNetWatchContext context, IFileSetFactory fileSetFactory, EnvironmentOptions environmentOptions) : IAsyncDisposable
    {
        private readonly ProcessRunner _processRunner = new(context.Reporter);
        private readonly StaticFileHandler _staticFileHandler = new(context.Reporter);

        private readonly ImmutableArray<IWatchFilter> _filters =
        [
            new MSBuildEvaluationFilter(context, fileSetFactory),
            new NoRestoreFilter(context),
            new LaunchBrowserFilter(context, environmentOptions),
            new BrowserRefreshFilter(context, environmentOptions),
        ];

        public async Task WatchAsync(WatchState state, CancellationToken cancellationToken)
        {
            var cancelledTaskSource = new TaskCompletionSource();
            cancellationToken.Register(state => ((TaskCompletionSource)state!).TrySetResult(),
                cancelledTaskSource);

            var processSpec = state.ProcessSpec;
            processSpec.Executable = environmentOptions.MuxerPath;
            var initialArguments = processSpec.Arguments?.ToArray() ?? [];

            if (context.SuppressMSBuildIncrementalism)
            {
                context.Reporter.Verbose("MSBuild incremental optimizations suppressed.");
            }

            while (true)
            {
                state.Iteration++;

                // Reset arguments
                processSpec.Arguments = initialArguments;

                for (var i = 0; i < _filters.Length; i++)
                {
                    await _filters[i].ProcessAsync(state, cancellationToken);
                }

                // Reset for next run
                state.RequiresMSBuildRevaluation = false;

                processSpec.EnvironmentVariables["DOTNET_WATCH_ITERATION"] = (state.Iteration + 1).ToString(CultureInfo.InvariantCulture);
                processSpec.EnvironmentVariables["DOTNET_LAUNCH_PROFILE"] = context.LaunchSettingsProfile.LaunchProfileName ?? string.Empty;

                var fileSet = state.FileSet;
                if (fileSet == null)
                {
                    context.Reporter.Error("Failed to find a list of files to watch");
                    return;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                using var currentRunCancellationSource = new CancellationTokenSource();
                using var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, currentRunCancellationSource.Token);
                using var fileSetWatcher = new FileSetWatcher(fileSet, context.Reporter);

                context.Reporter.Verbose($"Running {processSpec.ShortDisplayName()} with the following arguments: '{processSpec.GetArgumentsDisplay()}'");
                var processTask = _processRunner.RunAsync(processSpec, combinedCancellationSource.Token);

                context.Reporter.Output("Started", emoji: "🚀");

                Task<FileItem?> fileSetTask;
                Task finishedTask;

                while (true)
                {
                    fileSetTask = fileSetWatcher.GetChangedFileAsync(combinedCancellationSource.Token);
                    finishedTask = await Task.WhenAny(processTask, fileSetTask, cancelledTaskSource.Task);

                    if (finishedTask == fileSetTask &&
                        fileSetTask.Result is FileItem fileItem &&
                        await _staticFileHandler.TryHandleFileChange(state.BrowserRefreshServer, fileItem, combinedCancellationSource.Token))
                    {
                        // We're able to handle the file change event without doing a full-rebuild.
                    }
                    else
                    {
                        break;
                    }
                }

                // Regardless of the which task finished first, make sure everything is cancelled
                // and wait for dotnet to exit. We don't want orphan processes
                currentRunCancellationSource.Cancel();

                await Task.WhenAll(processTask, fileSetTask);

                if (processTask.Result != 0 && finishedTask == processTask && !cancellationToken.IsCancellationRequested)
                {
                    // Only show this error message if the process exited non-zero due to a normal process exit.
                    // Don't show this if dotnet-watch killed the inner process due to file change or CTRL+C by the user
                    context.Reporter.Error($"Exited with error code {processTask.Result}");
                }
                else
                {
                    context.Reporter.Output("Exited");
                }

                if (finishedTask == cancelledTaskSource.Task || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (finishedTask == processTask)
                {
                    // Process exited. Redo evalulation
                    state.RequiresMSBuildRevaluation = true;
                    // Now wait for a file to change before restarting process
                    state.ChangedFile = await fileSetWatcher.GetChangedFileAsync(
                        () => context.Reporter.Warn("Waiting for a file to change before restarting dotnet...", emoji: "⏳"),
                        cancellationToken);
                }
                else
                {
                    Debug.Assert(finishedTask == fileSetTask);
                    var changedFile = fileSetTask.Result;
                    state.ChangedFile = changedFile;
                    Debug.Assert(changedFile != null, "ChangedFile should only be null when cancelled");
                    context.Reporter.Output($"File changed: {changedFile.Value.FilePath}");
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var filter in _filters)
            {
                if (filter is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else if (filter is IDisposable diposable)
                {
                    diposable.Dispose();
                }

            }
        }
    }
}
