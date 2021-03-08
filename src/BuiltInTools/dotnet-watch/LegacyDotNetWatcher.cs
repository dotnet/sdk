// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher
{
    public class LegacyDotNetWatcher : IAsyncDisposable
    {
        private readonly IReporter _reporter;
        private readonly ProcessRunner _processRunner;
        private readonly DotNetWatchOptions _dotnetWatchOptions;
        private readonly StaticFileHandler _staticFileHandler;
        private readonly IWatchFilter[] _filters;

        public LegacyDotNetWatcher(IReporter reporter, IFileSetFactory fileSetFactory, DotNetWatchOptions dotNetWatchOptions)
        {
            Ensure.NotNull(reporter, nameof(reporter));

            _reporter = reporter;
            _processRunner = new ProcessRunner(reporter);
            _dotnetWatchOptions = dotNetWatchOptions;
            _staticFileHandler = new StaticFileHandler(reporter);

            _filters = new IWatchFilter[]
            {
                new MSBuildEvaluationFilter(fileSetFactory),
                new NoRestoreFilter(),
                new LaunchBrowserFilter(dotNetWatchOptions),
            };
        }

        public async Task WatchAsync(DotNetWatchContext context, CancellationToken cancellationToken)
        {
            var cancelledTaskSource = new TaskCompletionSource();
            cancellationToken.Register(state => ((TaskCompletionSource)state).TrySetResult(),
                cancelledTaskSource);

            var processSpec = context.ProcessSpec;
            var initialArguments = processSpec.Arguments.ToArray();

            if (context.SuppressMSBuildIncrementalism)
            {
                _reporter.Verbose("MSBuild incremental optimizations suppressed.");
            }

            while (true)
            {
                context.Iteration++;

                // Reset arguments
                processSpec.Arguments = initialArguments;

                for (var i = 0; i < _filters.Length; i++)
                {
                    await _filters[i].ProcessAsync(context, cancellationToken);
                }

                // Reset for next run
                context.RequiresMSBuildRevaluation = false;

                processSpec.EnvironmentVariables["DOTNET_WATCH_ITERATION"] = (context.Iteration + 1).ToString(CultureInfo.InvariantCulture);

                var fileSet = context.FileSet;
                if (fileSet == null)
                {
                    _reporter.Error("Failed to find a list of files to watch");
                    return;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                using (var currentRunCancellationSource = new CancellationTokenSource())
                using (var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    currentRunCancellationSource.Token))
                using (var fileSetWatcher = new FileSetWatcher(fileSet, _reporter))
                {
                    var processTask = _processRunner.RunAsync(processSpec, combinedCancellationSource.Token);
                    var args = string.Join(" ", processSpec.Arguments);
                    _reporter.Verbose($"Running {processSpec.ShortDisplayName()} with the following arguments: {args}");

                    _reporter.Output("Started");

                    Task<FileItem?> fileSetTask;
                    Task finishedTask;

                    while (true)
                    {
                        fileSetTask = fileSetWatcher.GetChangedFileAsync(combinedCancellationSource.Token);
                        finishedTask = await Task.WhenAny(processTask, fileSetTask, cancelledTaskSource.Task);

                        if (finishedTask != fileSetTask || fileSetTask.Result is not FileItem fileItem)
                        {
                            // The app exited.
                            break;
                        }
                        else
                        {
                            if (await _staticFileHandler.TryHandleFileChange(context, fileItem, combinedCancellationSource.Token))
                            {
                                _reporter.Verbose($"Successfully handled changes to {fileItem.FilePath}.");
                            }
                            else
                            {
                                _reporter.Verbose($"Unable to handle changes to {fileItem.FilePath}. Rebuilding the app..");
                                break;
                            }
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
                        _reporter.Error($"Exited with error code {processTask.Result}");
                    }
                    else
                    {
                        _reporter.Output("Exited");
                    }

                    if (finishedTask == cancelledTaskSource.Task || cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (finishedTask == processTask)
                    {
                        // Process exited. Redo evaludation
                        context.RequiresMSBuildRevaluation = true;
                        // Now wait for a file to change before restarting process
                        context.ChangedFile = await fileSetWatcher.GetChangedFileAsync(cancellationToken, () => _reporter.Warn("Waiting for a file to change before restarting dotnet..."));
                    }
                    else
                    {
                        Debug.Assert(finishedTask == fileSetTask);
                        var changedFile = fileSetTask.Result;
                        context.ChangedFile = changedFile;
                        _reporter.Output($"File changed: {changedFile.Value.FilePath}");
                    }
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
