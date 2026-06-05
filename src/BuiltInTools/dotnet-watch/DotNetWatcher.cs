// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using Microsoft.Build.Graph;

namespace Microsoft.DotNet.Watch
{
    internal sealed class DotNetWatcher(DotNetWatchContext context, MSBuildFileSetFactory fileSetFactory) : Watcher(context, fileSetFactory)
    {
        public override async Task WatchAsync(CancellationToken shutdownCancellationToken)
        {
            var cancelledTaskSource = new TaskCompletionSource();
            shutdownCancellationToken.Register(state => ((TaskCompletionSource)state!).TrySetResult(),
                cancelledTaskSource);

            if (Context.EnvironmentOptions.SuppressMSBuildIncrementalism)
            {
                Context.Reporter.Verbose("MSBuild incremental optimizations suppressed.");
            }

            var environmentBuilder = EnvironmentVariablesBuilder.FromCurrentEnvironment();

            ChangedFile? changedFile = null;
            var buildEvaluator = new BuildEvaluator(Context, RootFileSetFactory);
            await using var browserConnector = new BrowserConnector(Context);

            for (var iteration = 0;;iteration++)
            {
                if (await buildEvaluator.EvaluateAsync(changedFile, shutdownCancellationToken) is not { } evaluationResult)
                {
                    Context.Reporter.Error("Failed to find a list of files to watch");
                    return;
                }

                StaticFileHandler? staticFileHandler;
                ProjectGraphNode? projectRootNode;
                if (evaluationResult.ProjectGraph != null)
                {
                    projectRootNode = evaluationResult.ProjectGraph.GraphRoots.Single();
                    var projectMap = new ProjectNodeMap(evaluationResult.ProjectGraph, Context.Reporter);
                    staticFileHandler = new StaticFileHandler(Context.Reporter, projectMap, browserConnector);
                }
                else
                {
                    Context.Reporter.Verbose("Unable to determine if this project is a webapp.");
                    projectRootNode = null;
                    staticFileHandler = null;
                }

                var processSpec = new ProcessSpec
                {
                    Executable = Context.EnvironmentOptions.MuxerPath,
                    WorkingDirectory = Context.EnvironmentOptions.WorkingDirectory,
                    Arguments = buildEvaluator.GetProcessArguments(iteration),
                    EnvironmentVariables =
                    {
                        [EnvironmentVariables.Names.DotnetWatch] = "1",
                        [EnvironmentVariables.Names.DotnetWatchIteration] = (iteration + 1).ToString(CultureInfo.InvariantCulture),
                    }
                };

                var browserRefreshServer = (projectRootNode != null)
                    ? await browserConnector.GetOrCreateBrowserRefreshServerAsync(projectRootNode, processSpec, environmentBuilder, Context.RootProjectOptions, shutdownCancellationToken)
                    : null;

                environmentBuilder.SetProcessEnvironmentVariables(processSpec);

                // Reset for next run
                buildEvaluator.RequiresRevaluation = false;

                if (shutdownCancellationToken.IsCancellationRequested)
                {
                    return;
                }

                using var currentRunCancellationSource = new CancellationTokenSource();
                using var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(shutdownCancellationToken, currentRunCancellationSource.Token);
                using var fileSetWatcher = new FileWatcher(Context.Reporter);

                fileSetWatcher.WatchContainingDirectories(evaluationResult.Files.Keys);

                var processTask = ProcessRunner.RunAsync(processSpec, Context.Reporter, isUserApplication: true, launchResult: null, combinedCancellationSource.Token);

                Task<ChangedFile?> fileSetTask;
                Task finishedTask;

                while (true)
                {
                    fileSetTask = fileSetWatcher.WaitForFileChangeAsync(evaluationResult.Files, startedWatching: null, combinedCancellationSource.Token);
                    finishedTask = await Task.WhenAny(processTask, fileSetTask, cancelledTaskSource.Task);

                    if (staticFileHandler != null && finishedTask == fileSetTask && fileSetTask.Result.HasValue)
                    {
                        if (await staticFileHandler.HandleFileChangesAsync([fileSetTask.Result.Value], combinedCancellationSource.Token))
                        {
                            // We're able to handle the file change event without doing a full-rebuild.
                            continue;
                        }
                    }

                    break;
                }

                // Regardless of the which task finished first, make sure everything is cancelled
                // and wait for dotnet to exit. We don't want orphan processes
                currentRunCancellationSource.Cancel();

                await Task.WhenAll(processTask, fileSetTask);

                if (finishedTask == cancelledTaskSource.Task || shutdownCancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (finishedTask == processTask)
                {
                    // Process exited. Redo evalulation
                    buildEvaluator.RequiresRevaluation = true;

                    // Now wait for a file to change before restarting process
                    changedFile = await fileSetWatcher.WaitForFileChangeAsync(
                        evaluationResult.Files,
                        startedWatching: () => Context.Reporter.Report(MessageDescriptor.WaitingForFileChangeBeforeRestarting),
                        shutdownCancellationToken);
                }
                else
                {
                    Debug.Assert(finishedTask == fileSetTask);
                    changedFile = fileSetTask.Result;
                    Debug.Assert(changedFile != null, "ChangedFile should only be null when cancelled");
                    Context.Reporter.Output($"File changed: {changedFile.Value.Item.FilePath}");
                }
            }
        }
    }
}
