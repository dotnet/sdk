// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.DotNet.Watcher.Tools;

namespace Microsoft.DotNet.Watcher
{
    internal abstract class Watcher
    {
        public static async ValueTask UpdateBrowserAsync(int iteration, ProcessSpec processSpec, EnvironmentVariablesBuilder environmentBuilder, BrowserConnector browserConnector, ProjectInfo project, CancellationToken cancellationToken)
        {
            if (iteration == 0)
            {
                processSpec.OnOutput += browserConnector.GetBrowserLaunchTrigger(project, cancellationToken);
                var server = await browserConnector.StartRefreshServerAsync(cancellationToken);
                server?.SetEnvironmentVariables(environmentBuilder);
            }
            else if (browserConnector.RefreshServer != null)
            {
                // We've detected a change. Notify the browser.
                await browserConnector.RefreshServer.SendWaitMessageAsync(cancellationToken);
            }
        }
    }

    internal sealed class DotNetWatcher(DotNetWatchContext context, FileSetFactory fileSetFactory) : Watcher
    {
        private readonly ProcessRunner _processRunner = new(context.Reporter);
        private readonly StaticFileHandler _staticFileHandler = new(context.Reporter);

        public async Task WatchAsync(ProcessSpec processSpec, CancellationToken cancellationToken)
        {
            var cancelledTaskSource = new TaskCompletionSource();
            cancellationToken.Register(state => ((TaskCompletionSource)state!).TrySetResult(),
                cancelledTaskSource);

            processSpec.Executable = context.EnvironmentOptions.MuxerPath;
            var initialArguments = processSpec.Arguments?.ToArray() ?? [];

            if (context.EnvironmentOptions.SuppressMSBuildIncrementalism)
            {
                context.Reporter.Verbose("MSBuild incremental optimizations suppressed.");
            }

            var environmentBuilder = EnvironmentVariablesBuilder.FromCurrentEnvironment();

            FileItem? changedFile = null;
            var buildEvaluator = new BuildEvaluator(context, fileSetFactory);
            await using var browserConnector = new BrowserConnector(context);

            for (var iteration = 0;;iteration++)
            {
                // Reset arguments
                processSpec.Arguments = initialArguments;

                if (await buildEvaluator.EvaluateAsync(changedFile, cancellationToken) is not (var project, var fileSet))
                {
                    context.Reporter.Error("Failed to find a list of files to watch");
                    return;
                }

                buildEvaluator.UpdateProcessArguments(processSpec, iteration);

                await UpdateBrowserAsync(iteration, processSpec, environmentBuilder, browserConnector, project, cancellationToken);

                // Reset for next run
                buildEvaluator.RequiresRevaluation = false;

                if (iteration == 0)
                {
                    environmentBuilder.AddToEnvironment(processSpec.EnvironmentVariables);
                }

                processSpec.EnvironmentVariables[EnvironmentVariables.Names.DotnetWatchIteration] = (iteration + 1).ToString(CultureInfo.InvariantCulture);

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
                        await _staticFileHandler.TryHandleFileChange(browserConnector.RefreshServer, fileItem, combinedCancellationSource.Token))
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
                    buildEvaluator.RequiresRevaluation = true;
                    // Now wait for a file to change before restarting process
                    changedFile = await fileSetWatcher.GetChangedFileAsync(
                        () => context.Reporter.Warn("Waiting for a file to change before restarting dotnet...", emoji: "⏳"),
                        cancellationToken);
                }
                else
                {
                    Debug.Assert(finishedTask == fileSetTask);
                    changedFile = fileSetTask.Result;
                    Debug.Assert(changedFile != null, "ChangedFile should only be null when cancelled");
                    context.Reporter.Output($"File changed: {changedFile.Value.FilePath}");
                }
            }
        }
    }
}
