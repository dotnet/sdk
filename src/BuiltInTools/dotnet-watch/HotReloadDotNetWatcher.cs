// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher
{
    internal sealed class HotReloadDotNetWatcher : Watcher
    {
        private readonly IConsole _console;
        private readonly IRuntimeProcessLauncherFactory? _runtimeProcessLauncherFactory;
        private readonly RestartPrompt? _rudeEditRestartPrompt;

        public HotReloadDotNetWatcher(DotNetWatchContext context, IConsole console, MSBuildFileSetFactory fileSetFactory, IRuntimeProcessLauncherFactory? runtimeProcessLauncherFactory)
            : base(context, fileSetFactory)
        {
            _console = console;
            _runtimeProcessLauncherFactory = runtimeProcessLauncherFactory;
            if (!context.Options.NonInteractive)
            {
                var consoleInput = new ConsoleInputReader(_console, context.Options.Quiet, context.EnvironmentOptions.SuppressEmojis);

                var noPrompt = EnvironmentVariables.RestartOnRudeEdit;
                if (noPrompt)
                {
                    context.Reporter.Verbose($"DOTNET_WATCH_RESTART_ON_RUDE_EDIT = 'true'. Will restart without prompt.");
                }

                _rudeEditRestartPrompt = new RestartPrompt(context.Reporter, consoleInput, noPrompt ? true : null);
            }
        }

        public override async Task WatchAsync(CancellationToken shutdownCancellationToken)
        {
            Debug.Assert(Context.ProjectGraph != null);

            CancellationTokenSource? forceRestartCancellationSource = null;
            var hotReloadEnabledMessage = "Hot reload enabled. For a list of supported edits, see https://aka.ms/dotnet/hot-reload.";

            if (!Context.Options.NonInteractive)
            {
                Context.Reporter.Output($"{hotReloadEnabledMessage}{Environment.NewLine}  {(Context.EnvironmentOptions.SuppressEmojis ? string.Empty : "💡")} Press \"Ctrl + R\" to restart.", emoji: "🔥");

                _console.KeyPressed += (key) =>
                {
                    var modifiers = ConsoleModifiers.Control;
                    if ((key.Modifiers & modifiers) == modifiers && key.Key == ConsoleKey.R && forceRestartCancellationSource is { } source)
                    {
                        // provide immediate feedback to the user:
                        Context.Reporter.Report(source.IsCancellationRequested ? MessageDescriptor.RestartInProgress : MessageDescriptor.RestartRequested);
                        source.Cancel();
                    }
                };
            }
            else
            {
                Context.Reporter.Output(hotReloadEnabledMessage, emoji: "🔥");
            }

            for (var iteration = 0; !shutdownCancellationToken.IsCancellationRequested; iteration++)
            {
                Interlocked.Exchange(ref forceRestartCancellationSource, new CancellationTokenSource())?.Dispose();

                using var rootProcessTerminationSource = new CancellationTokenSource();

                // This source will signal when the user cancels (either Ctrl+R or Ctrl+C) or when the root process terminates:
                using var iterationCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(shutdownCancellationToken, forceRestartCancellationSource.Token, rootProcessTerminationSource.Token);
                var iterationCancellationToken = iterationCancellationSource.Token;

                var waitForFileChangeBeforeRestarting = true;
                HotReloadFileSetWatcher? fileSetWatcher = null;
                EvaluationResult? evaluationResult = null;
                RunningProject? rootRunningProject = null;
                Task<ChangedFile[]?>? fileSetWatcherTask = null;

                try
                {
                    // Evaluate the target to find out the set of files to watch.
                    // In case the app fails to start due to build or other error we can wait for these files to change.
                    evaluationResult = await EvaluateRootProjectAsync(iterationCancellationToken);

                    await using var browserConnector = new BrowserConnector(Context);
                    var projectMap = new ProjectNodeMap(Context.ProjectGraph, Context.Reporter);
                    await using var compilationHandler = new CompilationHandler(Context.Reporter);
                    var staticFileHandler = new StaticFileHandler(Context.Reporter, projectMap, browserConnector);
                    var scopedCssFileHandler = new ScopedCssFileHandler(Context.Reporter, projectMap, browserConnector);
                    var projectLauncher = new ProjectLauncher(Context, projectMap, browserConnector, compilationHandler, iteration);

                    var rootProjectOptions = Context.RootProjectOptions;
                    var rootProjectNode = Context.ProjectGraph.GraphRoots.Single();

                    await using var runtimeProcessLauncher = _runtimeProcessLauncherFactory?.TryCreate(rootProjectNode, projectLauncher, rootProjectOptions.BuildProperties);
                    if (runtimeProcessLauncher != null)
                    {
                        var launcherEnvironment = await runtimeProcessLauncher.GetEnvironmentVariablesAsync(iterationCancellationToken);
                        rootProjectOptions = rootProjectOptions with
                        {
                            LaunchEnvironmentVariables = [.. rootProjectOptions.LaunchEnvironmentVariables, .. launcherEnvironment]
                        };
                    }

                    rootRunningProject = await projectLauncher.TryLaunchProcessAsync(rootProjectOptions, rootProcessTerminationSource, build: true, iterationCancellationToken);
                    if (rootRunningProject == null)
                    {
                        // error has been reported:
                        return;
                    }

                    // Cancel iteration as soon as the root process exits, so that we don't spent time loading solution, etc. when the process is already dead.
                    rootRunningProject.ProcessExitedSource.Token.Register(() => iterationCancellationSource.Cancel());

                    if (shutdownCancellationToken.IsCancellationRequested)
                    {
                        // Ctrl+C:
                        return;
                    }

                    try
                    {
                        await rootRunningProject.WaitForProcessRunningAsync(iterationCancellationToken);
                    }
                    catch (OperationCanceledException) when (rootRunningProject.ProcessExitedSource.Token.IsCancellationRequested)
                    {
                        // Process might have exited while we were trying to communicate with it.
                        // Cancel the iteration, but wait for a file change before starting a new one.
                        iterationCancellationSource.Cancel();
                        iterationCancellationSource.Token.ThrowIfCancellationRequested();
                    }

                    if (shutdownCancellationToken.IsCancellationRequested)
                    {
                        // Ctrl+C:
                        return;
                    }

                    var buildCompletionTime = DateTime.UtcNow;
                    await compilationHandler.Workspace.UpdateProjectConeAsync(RootFileSetFactory.RootProjectFile, iterationCancellationToken);

                    // Solution must be initialized after we load the solution but before we start watching for file changes to avoid race condition
                    // when the EnC session captures content of the file after the changes has already been made.
                    // The session must also start after the project is built, so that the EnC service can read document checksums from the PDB.
                    await compilationHandler.StartSessionAsync(iterationCancellationToken);

                    if (shutdownCancellationToken.IsCancellationRequested)
                    {
                        // Ctrl+C:
                        return;
                    }

                    fileSetWatcher = new HotReloadFileSetWatcher(evaluationResult.Files, buildCompletionTime, Context.Reporter, Context.EnvironmentOptions.TestFlags);

                    // Hot Reload loop - exits when the root process needs to be restarted.
                    while (true)
                    {
                        fileSetWatcherTask = fileSetWatcher.GetChangedFilesAsync(iterationCancellationToken);

                        var finishedTask = await Task.WhenAny(rootRunningProject.RunningProcess, fileSetWatcherTask).WaitAsync(iterationCancellationToken);
                        if (finishedTask == rootRunningProject.RunningProcess)
                        {
                            // Cancel the iteration, but wait for a file change before starting a new one.
                            iterationCancellationSource.Cancel();
                            break;
                        }

                        // File watcher returns null when canceled:
                        if (fileSetWatcherTask.Result is not { } changedFiles)
                        {
                            Debug.Assert(iterationCancellationToken.IsCancellationRequested);
                            waitForFileChangeBeforeRestarting = false;
                            break;
                        }

                        ReportFileChanges(changedFiles);

                        // When a new file is added we need to run design-time build to find out
                        // what kind of the file it is and which project(s) does it belong to (can be linked, web asset, etc.).
                        // We don't need to rebuild and restart the application though.
                        if (changedFiles.Any(f => f.Change is ChangeKind.Add))
                        {
                            Context.Reporter.Verbose("File addition triggered re-evaluation.");

                            evaluationResult = await EvaluateRootProjectAsync(iterationCancellationToken);

                            await compilationHandler.Workspace.UpdateProjectConeAsync(RootFileSetFactory.RootProjectFile, iterationCancellationToken);

                            if (shutdownCancellationToken.IsCancellationRequested)
                            {
                                // Ctrl+C:
                                return;
                            }

                            // update files in the change set with new evaluation info:
                            for (int i = 0; i < changedFiles.Length; i++)
                            {
                                if (evaluationResult.Files.TryGetValue(changedFiles[i].Item.FilePath, out var evaluatedFile))
                                {
                                    changedFiles[i] = changedFiles[i] with { Item = evaluatedFile };
                                }
                            }

                            ReportFileChanges(changedFiles);

                            fileSetWatcher = new HotReloadFileSetWatcher(evaluationResult.Files, buildCompletionTime, Context.Reporter, Context.EnvironmentOptions.TestFlags);
                        }
                        else
                        {
                            // update the workspace to reflect changes in the file content:
                            await compilationHandler.Workspace.UpdateFileContentAsync(changedFiles, iterationCancellationToken);
                        }

                        HotReloadEventSource.Log.HotReloadStart(HotReloadEventSource.StartType.Main);
                        var stopwatch = Stopwatch.StartNew();

                        HotReloadEventSource.Log.HotReloadStart(HotReloadEventSource.StartType.StaticHandler);
                        await staticFileHandler.HandleFileChangesAsync(changedFiles, iterationCancellationToken);
                        HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.StaticHandler);

                        HotReloadEventSource.Log.HotReloadStart(HotReloadEventSource.StartType.ScopedCssHandler);
                        await scopedCssFileHandler.HandleFileChangesAsync(changedFiles, iterationCancellationToken);
                        HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.ScopedCssHandler);

                        HotReloadEventSource.Log.HotReloadStart(HotReloadEventSource.StartType.CompilationHandler);

                        var (projectsToBeRebuilt, projectsToRestart) = await compilationHandler.HandleFileChangesAsync(restartPrompt: async (projects, cancellationToken) =>
                        {
                            if (_rudeEditRestartPrompt != null)
                            {
                                // stop before waiting for user input:
                                stopwatch.Stop();

                                string question;
                                if (runtimeProcessLauncher == null)
                                {
                                    question = "Do you want to restart your app?";
                                }
                                else
                                {
                                    Context.Reporter.Output("Affected projects:");

                                    foreach (var project in projects.OrderBy(p => p.Name))
                                    {
                                        Context.Reporter.Output("  " + project.Name);
                                    }

                                    question = "Do you want to restart these projects?";
                                }

                                if (!await _rudeEditRestartPrompt.WaitForRestartConfirmationAsync(question, cancellationToken))
                                {
                                    Context.Reporter.Output("Hot reload suspended. To continue hot reload, press \"Ctrl + R\".", emoji: "🔥");
                                    await Task.Delay(-1, cancellationToken);
                                }
                            }
                            else
                            {
                                Context.Reporter.Verbose("Restarting without prompt since dotnet-watch is running in non-interactive mode.");
                            }
                        }, iterationCancellationToken);
                        HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.CompilationHandler);

                        stopwatch.Stop();
                        Context.Reporter.Report(MessageDescriptor.HotReloadChangeHandled, stopwatch.ElapsedMilliseconds);

                        HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.Main);

                        // Terminate root process if it had rude edits or is non-reloadable.
                        if (projectsToRestart.SingleOrDefault(project => project.Options.IsRootProject) is { } rootProjectToRestart)
                        {
                            // Triggers rootRestartCancellationToken.
                            waitForFileChangeBeforeRestarting = false;
                            break;
                        }

                        if (projectsToRestart.Any())
                        {
                            // Restart all terminated child processes and wait until their build completes:
                            await Task.WhenAll(
                                projectsToRestart.Select(async runningProject =>
                                {
                                    var newRunningProject = await projectLauncher.LaunchProcessAsync(runningProject.Options, runningProject.ProjectNode, new CancellationTokenSource(), build: true, shutdownCancellationToken);
                                    await newRunningProject.WaitForProcessRunningAsync(shutdownCancellationToken);
                                }))
                                .WaitAsync(shutdownCancellationToken);

                            // Update build completion time, so that file changes caused by the rebuild do not affect our file watcher:
                            fileSetWatcher.UpdateBuildCompletionTime(DateTime.UtcNow);

                            // Restart session to capture new baseline that reflects the changes to the restarted projects.
                            await compilationHandler.RestartSessionAsync(projectsToBeRebuilt, iterationCancellationToken);
                        }
                    }
                }
                catch (OperationCanceledException) when (!shutdownCancellationToken.IsCancellationRequested)
                {
                    // start next iteration unless shutdown is requested
                }
                finally
                {
                    if (!rootProcessTerminationSource.IsCancellationRequested)
                    {
                        rootProcessTerminationSource.Cancel();
                    }

                    try
                    {
                        // Wait for the root process to exit. Child processes will be terminated upon CompilationHandler disposal.
                        await Task.WhenAll(new[] { rootRunningProject?.RunningProcess, fileSetWatcherTask }.Where(t => t != null)!);
                    }
                    catch (OperationCanceledException) when (!shutdownCancellationToken.IsCancellationRequested)
                    {
                        // nop
                    }
                    finally
                    {
                        fileSetWatcherTask = null;
                        rootRunningProject?.Dispose();

                        if (evaluationResult != null &&
                            waitForFileChangeBeforeRestarting &&
                            !shutdownCancellationToken.IsCancellationRequested &&
                            !forceRestartCancellationSource.IsCancellationRequested)
                        {
                            fileSetWatcher ??= new HotReloadFileSetWatcher(evaluationResult.Files, DateTime.MinValue, Context.Reporter, Context.EnvironmentOptions.TestFlags);
                            Context.Reporter.Report(MessageDescriptor.WaitingForFileChangeBeforeRestarting);

                            using var shutdownOrForcedRestartSource = CancellationTokenSource.CreateLinkedTokenSource(shutdownCancellationToken, forceRestartCancellationSource.Token);
                            await fileSetWatcher.GetChangedFilesAsync(shutdownOrForcedRestartSource.Token, forceWaitForNewUpdate: true);
                        }

                        fileSetWatcher?.Dispose();
                    }
                }
            }
        }

        private void ReportFileChanges(IReadOnlyList<ChangedFile> changedFiles)
        {
            Report(kind: ChangeKind.Add);
            Report(kind: ChangeKind.Update);
            Report(kind: ChangeKind.Delete);

            void Report(ChangeKind kind)
            {
                var items = changedFiles.Where(item => item.Change == kind).ToArray();
                if (items is not [])
                {
                    Context.Reporter.Output(GetMessage(items, kind));
                }
            }

            string GetMessage(IReadOnlyList<ChangedFile> items, ChangeKind kind)
                => items is [{Item: var item }]
                    ? GetSingularMessage(kind) + ": " + GetRelativeFilePath(item.FilePath)
                    : GetPluralMessage(kind) + ": " + string.Join(", ", items.Select(f => GetRelativeFilePath(f.Item.FilePath)));

            static string GetSingularMessage(ChangeKind kind)
                => kind switch
                {
                    ChangeKind.Update => "File updated",
                    ChangeKind.Add => "File added",
                    ChangeKind.Delete => "File deleted",
                    _ => throw new InvalidOperationException()
                };

            static string GetPluralMessage(ChangeKind kind)
                => kind switch
                {
                    ChangeKind.Update => "Files updated",
                    ChangeKind.Add => "Files added",
                    ChangeKind.Delete => "Files deleted",
                    _ => throw new InvalidOperationException()
                };
        }

        private async ValueTask<EvaluationResult> EvaluateRootProjectAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await RootFileSetFactory.TryCreateAsync(cancellationToken);
                if (result != null)
                {
                    return result;
                }

                Context.Reporter.Report(MessageDescriptor.FixBuildError);
                await FileWatcher.WaitForFileChangeAsync(RootFileSetFactory.RootProjectFile, Context.Reporter, cancellationToken);
            }
        }

        private string GetRelativeFilePath(string path)
        {
            var relativePath = path;
            var workingDirectory = Context.EnvironmentOptions.WorkingDirectory;
            if (path.StartsWith(workingDirectory, StringComparison.Ordinal) && path.Length > workingDirectory.Length)
            {
                relativePath = path.Substring(workingDirectory.Length);

                return $".{(relativePath.StartsWith(Path.DirectorySeparatorChar) ? string.Empty : Path.DirectorySeparatorChar)}{relativePath}";
            }

            return relativePath;
        }
    }
}
