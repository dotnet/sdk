// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.Watch
{
    internal sealed partial class HotReloadDotNetWatcher : Watcher
    {
        private static readonly DateTime s_fileNotExistFileTime = DateTime.FromFileTime(0);

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
            CancellationTokenSource? forceRestartCancellationSource = null;
            var hotReloadEnabledMessage = "Hot reload enabled. For a list of supported edits, see https://aka.ms/dotnet/hot-reload.";

            if (!Context.Options.NonInteractive)
            {
                Context.Reporter.Output($"{hotReloadEnabledMessage}{Environment.NewLine}  {(Context.EnvironmentOptions.SuppressEmojis ? string.Empty : "💡")} Press \"Ctrl + R\" to restart.", emoji: "🔥");

                _console.KeyPressed += (key) =>
                {
                    if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.R && forceRestartCancellationSource is { } source)
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

            using var fileWatcher = new FileWatcher(Context.Reporter);

            for (var iteration = 0; !shutdownCancellationToken.IsCancellationRequested; iteration++)
            {
                Interlocked.Exchange(ref forceRestartCancellationSource, new CancellationTokenSource())?.Dispose();

                using var rootProcessTerminationSource = new CancellationTokenSource();

                // This source will signal when the user cancels (either Ctrl+R or Ctrl+C) or when the root process terminates:
                using var iterationCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(shutdownCancellationToken, forceRestartCancellationSource.Token, rootProcessTerminationSource.Token);
                var iterationCancellationToken = iterationCancellationSource.Token;

                var waitForFileChangeBeforeRestarting = true;
                EvaluationResult? evaluationResult = null;
                RunningProject? rootRunningProject = null;
                Task<ImmutableList<ChangedFile>>? fileWatcherTask = null;
                IRuntimeProcessLauncher? runtimeProcessLauncher = null;
                CompilationHandler? compilationHandler = null;
                Action<string, ChangeKind>? fileChangedCallback = null;

                try
                {
                    var rootProjectOptions = Context.RootProjectOptions;
                    var runtimeProcessLauncherFactory = _runtimeProcessLauncherFactory;

                    // Evaluate the target to find out the set of files to watch.
                    // In case the app fails to start due to build or other error we can wait for these files to change.
                    evaluationResult = await EvaluateRootProjectAsync(iterationCancellationToken);
                    Debug.Assert(evaluationResult.ProjectGraph != null);

                    var rootProject = evaluationResult.ProjectGraph.GraphRoots.Single();

                    // use normalized MSBuild path so that we can index into the ProjectGraph
                    rootProjectOptions = rootProjectOptions with { ProjectPath = rootProject.ProjectInstance.FullPath };

                    var rootProjectCapabilities = rootProject.GetCapabilities();
                    if (rootProjectCapabilities.Contains(AspireServiceFactory.AppHostProjectCapability))
                    {
                        runtimeProcessLauncherFactory ??= AspireServiceFactory.Instance;
                        Context.Reporter.Verbose("Using Aspire process launcher.");
                    }
                    
                    await using var browserConnector = new BrowserConnector(Context);
                    var projectMap = new ProjectNodeMap(evaluationResult.ProjectGraph, Context.Reporter);
                    compilationHandler = new CompilationHandler(Context.Reporter);
                    var staticFileHandler = new StaticFileHandler(Context.Reporter, projectMap, browserConnector);
                    var scopedCssFileHandler = new ScopedCssFileHandler(Context.Reporter, projectMap, browserConnector);
                    var projectLauncher = new ProjectLauncher(Context, projectMap, browserConnector, compilationHandler, iteration);

                    var rootProjectNode = evaluationResult.ProjectGraph.GraphRoots.Single();

                    runtimeProcessLauncher = runtimeProcessLauncherFactory?.TryCreate(rootProjectNode, projectLauncher, rootProjectOptions.BuildArguments);
                    if (runtimeProcessLauncher != null)
                    {
                        var launcherEnvironment = runtimeProcessLauncher.GetEnvironmentVariables();
                        rootProjectOptions = rootProjectOptions with
                        {
                            LaunchEnvironmentVariables = [.. rootProjectOptions.LaunchEnvironmentVariables, .. launcherEnvironment]
                        };
                    }

                    if (!await BuildProjectAsync(rootProjectOptions.ProjectPath, rootProjectOptions.BuildArguments, iterationCancellationToken))
                    {
                        // error has been reported:
                        continue;
                    }

                    rootRunningProject = await projectLauncher.TryLaunchProcessAsync(
                        rootProjectOptions,
                        rootProcessTerminationSource,
                        onOutput: null,
                        restartOperation: new RestartOperation(_ => throw new InvalidOperationException("Root project shouldn't be restarted")),
                        iterationCancellationToken);

                    if (rootRunningProject == null)
                    {
                        // error has been reported:
                        waitForFileChangeBeforeRestarting = false;
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

                    fileWatcher.WatchContainingDirectories(evaluationResult.Files.Keys);

                    var changedFilesAccumulator = ImmutableList<ChangedFile>.Empty;

                    void FileChangedCallback(string path, ChangeKind kind)
                    {
                        if (TryGetChangedFile(evaluationResult.Files, buildCompletionTime, path, kind) is { } changedFile)
                        {
                            ImmutableInterlocked.Update(ref changedFilesAccumulator, changedFiles => changedFiles.Add(changedFile));
                        }
                    }

                    fileChangedCallback = FileChangedCallback;
                    fileWatcher.OnFileChange += fileChangedCallback;
                    ReportWatchingForChanges();

                    // Hot Reload loop - exits when the root process needs to be restarted.
                    while (true)
                    {
                        try
                        {
                            // Use timeout to batch file changes. If the process doesn't exit within the given timespan we'll check
                            // for accumulated file changes. If there are any we attempt Hot Reload. Otherwise we come back here to wait again.
                            _ = await rootRunningProject.RunningProcess.WaitAsync(TimeSpan.FromMilliseconds(50), iterationCancellationToken);

                            // Process exited: cancel the iteration, but wait for a file change before starting a new one
                            waitForFileChangeBeforeRestarting = true;
                            iterationCancellationSource.Cancel();
                            break;
                        }
                        catch (TimeoutException)
                        {
                            // check for changed files
                        }
                        catch (OperationCanceledException)
                        {
                            Debug.Assert(iterationCancellationToken.IsCancellationRequested);
                            waitForFileChangeBeforeRestarting = false;
                            break;
                        }

                        var changedFiles = await CaptureChangedFilesSnapshot(rebuiltProjects: null);
                        if (changedFiles is [])
                        {
                            continue;
                        }

                        if (!rootProjectCapabilities.Contains("SupportsHotReload"))
                        {
                            Context.Reporter.Warn($"Project '{rootProject.GetDisplayName()}' does not support Hot Reload and must be rebuilt.");

                            // file change already detected
                            waitForFileChangeBeforeRestarting = false;
                            iterationCancellationSource.Cancel();
                            break;
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

                        var (projectsToRebuild, projectsToRestart) = await compilationHandler.HandleFileChangesAsync(restartPrompt: async (projectNames, cancellationToken) =>
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

                                    foreach (var projectName in projectNames.OrderBy(n => n))
                                    {
                                        Context.Reporter.Output("  " + projectName);
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

                                foreach (var projectName in projectNames)
                                {
                                    Context.Reporter.Verbose($"  Project to restart: '{projectName}'");
                                }
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

                        if (projectsToRebuild.Count > 0)
                        {
                            // Discard baselines before build.
                            compilationHandler.DiscardProjectBaselines(projectsToRebuild, iterationCancellationToken);

                            while (true)
                            {
                                iterationCancellationToken.ThrowIfCancellationRequested();

                                // pause accumulating file changes during build:
                                fileWatcher.OnFileChange -= fileChangedCallback;
                                try
                                {
                                    var buildResults = await Task.WhenAll(
                                        projectsToRebuild.Values.Select(projectPath => BuildProjectAsync(projectPath, rootProjectOptions.BuildArguments, iterationCancellationToken)));

                                    if (buildResults.All(success => success))
                                    {
                                        break;
                                    }
                                }
                                finally
                                {
                                    fileWatcher.OnFileChange += fileChangedCallback;
                                }

                                iterationCancellationToken.ThrowIfCancellationRequested();

                                _ = await fileWatcher.WaitForFileChangeAsync(
                                    startedWatching: () => Context.Reporter.Report(MessageDescriptor.FixBuildError),
                                    shutdownCancellationToken);
                            }

                            // Update build completion time, so that file changes caused by the rebuild do not affect our file watcher:
                            buildCompletionTime = DateTime.UtcNow;

                            // Changes made since last snapshot of the accumulator shouldn't be included in next Hot Reload update.
                            // Apply them to the workspace.
                            _ = await CaptureChangedFilesSnapshot(projectsToRebuild);

                            // Update project baselines to reflect changes to the restarted projects.
                            compilationHandler.UpdateProjectBaselines(projectsToRebuild, iterationCancellationToken);
                        }

                        if (projectsToRestart is not [])
                        {
                            await Task.WhenAll(
                                projectsToRestart.Select(async runningProject =>
                                {
                                    var newRunningProject = await runningProject.RestartOperation(shutdownCancellationToken);

                                    try
                                    {
                                        await newRunningProject.WaitForProcessRunningAsync(shutdownCancellationToken);
                                    }
                                    catch (OperationCanceledException) when (!shutdownCancellationToken.IsCancellationRequested)
                                    {
                                        // Process might have exited while we were trying to communicate with it.
                                    }
                                    finally
                                    {
                                        runningProject.Dispose();
                                    }
                                }))
                                .WaitAsync(shutdownCancellationToken);
                        }

                        async Task<ImmutableList<ChangedFile>> CaptureChangedFilesSnapshot(ImmutableDictionary<ProjectId, string>? rebuiltProjects)
                        {
                            var changedFiles = Interlocked.Exchange(ref changedFilesAccumulator, []);
                            if (changedFiles is [])
                            {
                                return [];
                            }

                            // When a new file is added we need to run design-time build to find out
                            // what kind of the file it is and which project(s) does it belong to (can be linked, web asset, etc.).
                            // We don't need to rebuild and restart the application though.
                            var hasAddedFile = changedFiles.Any(f => f.Change is ChangeKind.Add);

                            if (hasAddedFile)
                            {
                                Context.Reporter.Verbose("File addition triggered re-evaluation.");

                                evaluationResult = await EvaluateRootProjectAsync(iterationCancellationToken);

                                // additional directories may have been added:
                                fileWatcher.WatchContainingDirectories(evaluationResult.Files.Keys);

                                await compilationHandler.Workspace.UpdateProjectConeAsync(RootFileSetFactory.RootProjectFile, iterationCancellationToken);

                                if (shutdownCancellationToken.IsCancellationRequested)
                                {
                                    // Ctrl+C:
                                    return [];
                                }

                                // Update files in the change set with new evaluation info.
                                changedFiles = changedFiles
                                    .Select(f => evaluationResult.Files.TryGetValue(f.Item.FilePath, out var evaluatedFile) ? f with { Item = evaluatedFile } : f)
                                    .ToImmutableList();
                            }

                            if (rebuiltProjects != null)
                            {
                                // Filter changed files down to those contained in projects being rebuilt.
                                // File changes that affect projects that are not being rebuilt will stay in the accumulator
                                // and be included in the next Hot Reload change set.
                                var rebuiltProjectPaths = rebuiltProjects.Values.ToHashSet();

                                var newAccumulator = ImmutableList<ChangedFile>.Empty;
                                var newChangedFiles = ImmutableList<ChangedFile>.Empty;

                                foreach (var file in changedFiles)
                                {
                                    if (file.Item.ContainingProjectPaths.All(containingProjectPath => rebuiltProjectPaths.Contains(containingProjectPath)))
                                    {
                                        newChangedFiles = newChangedFiles.Add(file);
                                    }
                                    else
                                    {
                                        newAccumulator = newAccumulator.Add(file);
                                    }
                                }

                                changedFiles = newChangedFiles;
                                ImmutableInterlocked.Update(ref changedFilesAccumulator, accumulator => accumulator.AddRange(newAccumulator));
                            }

                            ReportFileChanges(changedFiles);

                            if (!hasAddedFile)
                            {
                                // update the workspace to reflect changes in the file content:
                                await compilationHandler.Workspace.UpdateFileContentAsync(changedFiles, iterationCancellationToken);
                            }

                            return changedFiles;
                        }
                    }
                }
                catch (OperationCanceledException) when (!shutdownCancellationToken.IsCancellationRequested)
                {
                    // start next iteration unless shutdown is requested
                }
                catch (Exception) when ((waitForFileChangeBeforeRestarting = false) == true)
                {
                    // unreachable
                    throw new InvalidOperationException();
                }
                finally
                {
                    // stop watching file changes:
                    if (fileChangedCallback != null)
                    {
                        fileWatcher.OnFileChange -= fileChangedCallback;
                    }

                    if (runtimeProcessLauncher != null)
                    {
                        // Request cleanup of all processes created by the launcher before we terminate the root process.
                        // Non-cancellable - can only be aborted by forced Ctrl+C, which immediately kills the dotnet-watch process.
                        await runtimeProcessLauncher.TerminateLaunchedProcessesAsync(CancellationToken.None);
                    }

                    if (compilationHandler != null)
                    {
                        // Non-cancellable - can only be aborted by forced Ctrl+C, which immediately kills the dotnet-watch process.
                        await compilationHandler.TerminateNonRootProcessesAndDispose(CancellationToken.None);
                    }

                    if (!rootProcessTerminationSource.IsCancellationRequested)
                    {
                        rootProcessTerminationSource.Cancel();
                    }

                    try
                    {
                        // Wait for the root process to exit.
                        await Task.WhenAll(new[] { (Task?)rootRunningProject?.RunningProcess, fileWatcherTask }.Where(t => t != null)!);
                    }
                    catch (OperationCanceledException) when (!shutdownCancellationToken.IsCancellationRequested)
                    {
                        // nop
                    }
                    finally
                    {
                        fileWatcherTask = null;

                        if (runtimeProcessLauncher != null)
                        {
                            await runtimeProcessLauncher.DisposeAsync();
                        }

                        rootRunningProject?.Dispose();

                        if (waitForFileChangeBeforeRestarting &&
                            !shutdownCancellationToken.IsCancellationRequested &&
                            !forceRestartCancellationSource.IsCancellationRequested)
                        {
                            using var shutdownOrForcedRestartSource = CancellationTokenSource.CreateLinkedTokenSource(shutdownCancellationToken, forceRestartCancellationSource.Token);
                            await WaitForFileChangeBeforeRestarting(fileWatcher, evaluationResult, shutdownOrForcedRestartSource.Token);
                        }
                    }
                }
            }
        }

        private async ValueTask WaitForFileChangeBeforeRestarting(FileWatcher fileWatcher, EvaluationResult? evaluationResult, CancellationToken cancellationToken)
        {
            if (evaluationResult != null)
            {
                if (!fileWatcher.WatchingDirectories)
                {
                    fileWatcher.WatchContainingDirectories(evaluationResult.Files.Keys);
                }

                _ = await fileWatcher.WaitForFileChangeAsync(
                    evaluationResult.Files,
                    startedWatching: () => Context.Reporter.Report(MessageDescriptor.WaitingForFileChangeBeforeRestarting),
                    cancellationToken);
            }
            else
            {
                // evaluation cancelled - watch for any changes in the directory containing the root project:
                fileWatcher.WatchContainingDirectories([RootFileSetFactory.RootProjectFile]);

                _ = await fileWatcher.WaitForFileChangeAsync(
                    startedWatching: () => Context.Reporter.Report(MessageDescriptor.WaitingForFileChangeBeforeRestarting),
                    cancellationToken);
            }
        }

        private ChangedFile? TryGetChangedFile(IReadOnlyDictionary<string, FileItem> fileSet, DateTime buildCompletionTime, string path, ChangeKind kind)
        {
            // only handle file changes:
            if (Directory.Exists(path))
            {
                return null;
            }

            if (kind != ChangeKind.Delete)
            {
                try
                {
                    // Do not report changes to files that happened during build:
                    var creationTime = File.GetCreationTimeUtc(path);
                    var writeTime = File.GetLastWriteTimeUtc(path);

                    if (creationTime == s_fileNotExistFileTime || writeTime == s_fileNotExistFileTime)
                    {
                        // file might have been deleted since we received the event
                        kind = ChangeKind.Delete;
                    }
                    else if (creationTime.Ticks < buildCompletionTime.Ticks && writeTime.Ticks < buildCompletionTime.Ticks)
                    {
                        Context.Reporter.Verbose(
                            $"Ignoring file change during build: {kind} '{path}' " +
                            $"(created {FormatTimestamp(creationTime)} and written {FormatTimestamp(writeTime)} before {FormatTimestamp(buildCompletionTime)}).");

                        return null;
                    }
                    else if (writeTime > creationTime)
                    {
                        Context.Reporter.Verbose($"File change: {kind} '{path}' (written {FormatTimestamp(writeTime)} after {FormatTimestamp(buildCompletionTime)}).");
                    }
                    else
                    {
                        Context.Reporter.Verbose($"File change: {kind} '{path}' (created {FormatTimestamp(creationTime)} after {FormatTimestamp(buildCompletionTime)}).");
                    }
                }
                catch (Exception e)
                {
                    Context.Reporter.Verbose($"Ignoring file '{path}' due to access error: {e.Message}.");
                    return null;
                }
            }

            if (kind == ChangeKind.Delete)
            {
                Context.Reporter.Verbose($"File '{path}' deleted after {FormatTimestamp(buildCompletionTime)}.");
            }

            if (fileSet.TryGetValue(path, out var fileItem))
            {
                // For some reason we are sometimes seeing Add events raised whan an existing file is updated:
                return new ChangedFile(fileItem, (kind == ChangeKind.Add) ? ChangeKind.Update : kind);
            }

            if (kind == ChangeKind.Add)
            {
                return new ChangedFile(new FileItem { FilePath = path, ContainingProjectPaths = [] }, kind);
            }

            Context.Reporter.Verbose($"Change ignored: {kind} '{path}'.");
            return null;
        }

        internal static string FormatTimestamp(DateTime time)
            => time.ToString("HH:mm:ss.fffffff");

        private void ReportWatchingForChanges()
        {
            var waitingForChanges = MessageDescriptor.WaitingForChanges;
            if (Context.EnvironmentOptions.TestFlags.HasFlag(TestFlags.ElevateWaitingForChangesMessageSeverity))
            {
                waitingForChanges = waitingForChanges with { Severity = MessageSeverity.Output };
            }

            Context.Reporter.Report(waitingForChanges);
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

                var result = await RootFileSetFactory.TryCreateAsync(requireProjectGraph: true, cancellationToken);
                if (result != null)
                {
                    Debug.Assert(result.ProjectGraph != null);
                    return result;
                }

                await FileWatcher.WaitForFileChangeAsync(
                    RootFileSetFactory.RootProjectFile,
                    Context.Reporter,
                    startedWatching: () => Context.Reporter.Report(MessageDescriptor.FixBuildError),
                    cancellationToken);
            }
        }

        private async Task<bool> BuildProjectAsync(string projectPath, IReadOnlyList<string> buildArguments, CancellationToken cancellationToken)
        {
            var buildOutput = new List<OutputLine>();

            var processSpec = new ProcessSpec
            {
                Executable = Context.EnvironmentOptions.MuxerPath,
                WorkingDirectory = Path.GetDirectoryName(projectPath)!,
                OnOutput = line =>
                {
                    lock (buildOutput)
                    {
                        buildOutput.Add(line);
                    }
                },
                // pass user-specified build arguments last to override defaults:
                Arguments = ["build", projectPath, "-consoleLoggerParameters:NoSummary;Verbosity=minimal", .. buildArguments]
            };

            Context.Reporter.Output($"Building '{projectPath}' ...");

            var exitCode = await ProcessRunner.RunAsync(processSpec, Context.Reporter, isUserApplication: false, launchResult: null, cancellationToken);
            BuildUtilities.ReportBuildOutput(Context.Reporter, buildOutput, verboseOutput: exitCode == 0);

            if (exitCode == 0)
            {
                Context.Reporter.Output("Build succeeded.");
            }

            return exitCode == 0;
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
