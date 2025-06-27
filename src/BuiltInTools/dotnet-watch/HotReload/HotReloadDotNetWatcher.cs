﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.Watch
{
    internal sealed class HotReloadDotNetWatcher
    {
        private readonly IConsole _console;
        private readonly IRuntimeProcessLauncherFactory? _runtimeProcessLauncherFactory;
        private readonly RestartPrompt? _rudeEditRestartPrompt;

        private readonly DotNetWatchContext _context;
        private readonly MSBuildFileSetFactory _fileSetFactory;

        public HotReloadDotNetWatcher(DotNetWatchContext context, IConsole console, IRuntimeProcessLauncherFactory? runtimeProcessLauncherFactory)
        {
            _context = context;
            _fileSetFactory = context.CreateMSBuildFileSetFactory();

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

        public async Task WatchAsync(CancellationToken shutdownCancellationToken)
        {
            CancellationTokenSource? forceRestartCancellationSource = null;
            var hotReloadEnabledMessage = "Hot reload enabled. For a list of supported edits, see https://aka.ms/dotnet/hot-reload.";

            if (!_context.Options.NonInteractive)
            {
                _context.Reporter.Output($"{hotReloadEnabledMessage}{Environment.NewLine}  {(_context.EnvironmentOptions.SuppressEmojis ? string.Empty : "💡")} Press \"Ctrl + R\" to restart.", emoji: "🔥");

                _console.KeyPressed += (key) =>
                {
                    if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.R && forceRestartCancellationSource is { } source)
                    {
                        // provide immediate feedback to the user:
                        _context.Reporter.Report(source.IsCancellationRequested ? MessageDescriptor.RestartInProgress : MessageDescriptor.RestartRequested);
                        source.Cancel();
                    }
                };
            }
            else
            {
                _context.Reporter.Output(hotReloadEnabledMessage, emoji: "🔥");
            }

            await using var browserConnector = new BrowserConnector(_context);
            using var fileWatcher = new FileWatcher(_context.Reporter);

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
                IRuntimeProcessLauncher? runtimeProcessLauncher = null;
                CompilationHandler? compilationHandler = null;
                Action<ChangedPath>? fileChangedCallback = null;

                try
                {
                    var rootProjectOptions = _context.RootProjectOptions;
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
                        _context.Reporter.Verbose("Using Aspire process launcher.");
                    }

                    var projectMap = new ProjectNodeMap(evaluationResult.ProjectGraph, _context.Reporter);
                    compilationHandler = new CompilationHandler(_context.Reporter, _context.ProcessRunner);
                    var scopedCssFileHandler = new ScopedCssFileHandler(_context.Reporter, projectMap, browserConnector);
                    var projectLauncher = new ProjectLauncher(_context, projectMap, browserConnector, compilationHandler, iteration);
                    evaluationResult.ItemExclusions.Report(_context.Reporter);

                    var rootProjectNode = evaluationResult.ProjectGraph.GraphRoots.Single();

                    runtimeProcessLauncher = runtimeProcessLauncherFactory?.TryCreate(rootProjectNode, projectLauncher, rootProjectOptions);
                    if (runtimeProcessLauncher != null)
                    {
                        var launcherEnvironment = runtimeProcessLauncher.GetEnvironmentVariables();
                        rootProjectOptions = rootProjectOptions with
                        {
                            LaunchEnvironmentVariables = [.. rootProjectOptions.LaunchEnvironmentVariables, .. launcherEnvironment]
                        };
                    }

                    var (buildSucceeded, buildOutput, _) = await BuildProjectAsync(rootProjectOptions.ProjectPath, rootProjectOptions.BuildArguments, iterationCancellationToken);
                    BuildOutput.ReportBuildOutput(_context.Reporter, buildOutput, buildSucceeded, projectDisplay: rootProjectOptions.ProjectPath);
                    if (!buildSucceeded)
                    {
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

                    await compilationHandler.Workspace.UpdateProjectConeAsync(_fileSetFactory.RootProjectFile, iterationCancellationToken);

                    // Solution must be initialized after we load the solution but before we start watching for file changes to avoid race condition
                    // when the EnC session captures content of the file after the changes has already been made.
                    // The session must also start after the project is built, so that the EnC service can read document checksums from the PDB.
                    await compilationHandler.StartSessionAsync(iterationCancellationToken);

                    if (shutdownCancellationToken.IsCancellationRequested)
                    {
                        // Ctrl+C:
                        return;
                    }

                    evaluationResult.WatchFiles(fileWatcher);

                    var changedFilesAccumulator = ImmutableList<ChangedPath>.Empty;

                    void FileChangedCallback(ChangedPath change)
                    {
                        if (AcceptChange(change, evaluationResult))
                        {
                            _context.Reporter.Verbose($"File change: {change.Kind} '{change.Path}'.");
                            ImmutableInterlocked.Update(ref changedFilesAccumulator, changedPaths => changedPaths.Add(change));
                        }
                    }

                    fileChangedCallback = FileChangedCallback;
                    fileWatcher.OnFileChange += fileChangedCallback;
                    ReportWatchingForChanges();

                    // Hot Reload loop - exits when the root process needs to be restarted.
                    bool extendTimeout = false;
                    while (true)
                    {
                        try
                        {
                            // Use timeout to batch file changes. If the process doesn't exit within the given timespan we'll check
                            // for accumulated file changes. If there are any we attempt Hot Reload. Otherwise we come back here to wait again.
                            _ = await rootRunningProject.RunningProcess.WaitAsync(TimeSpan.FromMilliseconds(extendTimeout ? 200 : 50), iterationCancellationToken);

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
                            // Ctrl+C, forced restart, or process exited.
                            Debug.Assert(iterationCancellationToken.IsCancellationRequested);

                            // Will wait for a file change if process exited.
                            waitForFileChangeBeforeRestarting = true;
                            break;
                        }

                        // If the changes include addition/deletion wait a little bit more for possible matching deletion/addition.
                        // This eliminates reevaluations caused by teared add + delete of a temp file or a move of a file.
                        if (!extendTimeout && changedFilesAccumulator.Any(change => change.Kind is ChangeKind.Add or ChangeKind.Delete))
                        {
                            extendTimeout = true;
                            continue;
                        }

                        extendTimeout = false;

                        var changedFiles = await CaptureChangedFilesSnapshot(rebuiltProjects: null);
                        if (changedFiles is [])
                        {
                            continue;
                        }

                        if (!rootProjectCapabilities.Contains("SupportsHotReload"))
                        {
                            _context.Reporter.Warn($"Project '{rootProject.GetDisplayName()}' does not support Hot Reload and must be rebuilt.");

                            // file change already detected
                            waitForFileChangeBeforeRestarting = false;
                            iterationCancellationSource.Cancel();
                            break;
                        }

                        HotReloadEventSource.Log.HotReloadStart(HotReloadEventSource.StartType.Main);
                        var stopwatch = Stopwatch.StartNew();

                        HotReloadEventSource.Log.HotReloadStart(HotReloadEventSource.StartType.StaticHandler);
                        await compilationHandler.HandleStaticAssetChangesAsync(changedFiles, projectMap, iterationCancellationToken);
                        HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.StaticHandler);

                        HotReloadEventSource.Log.HotReloadStart(HotReloadEventSource.StartType.ScopedCssHandler);
                        await scopedCssFileHandler.HandleFileChangesAsync(changedFiles, iterationCancellationToken);
                        HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.ScopedCssHandler);

                        HotReloadEventSource.Log.HotReloadStart(HotReloadEventSource.StartType.CompilationHandler);

                        var (projectsToRebuild, projectsToRestart) = await compilationHandler.HandleManagedCodeChangesAsync(
                            autoRestart: _context.Options.NonInteractive || _rudeEditRestartPrompt?.AutoRestartPreference is true,
                            restartPrompt: async (projectNames, cancellationToken) =>
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
                                        _context.Reporter.Output("Affected projects:");

                                        foreach (var projectName in projectNames.OrderBy(n => n))
                                        {
                                            _context.Reporter.Output("  " + projectName);
                                        }

                                        question = "Do you want to restart these projects?";
                                    }

                                    return await _rudeEditRestartPrompt.WaitForRestartConfirmationAsync(question, cancellationToken);
                                }

                                _context.Reporter.Verbose("Restarting without prompt since dotnet-watch is running in non-interactive mode.");

                                foreach (var projectName in projectNames)
                                {
                                    _context.Reporter.Verbose($"  Project to restart: '{projectName}'");
                                }

                                return true;
                            },
                            iterationCancellationToken);

                        HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.CompilationHandler);

                        stopwatch.Stop();

                        HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.Main);

                        // Terminate root process if it had rude edits or is non-reloadable.
                        if (projectsToRestart.SingleOrDefault(project => project.Options.IsRootProject) is { } rootProjectToRestart)
                        {
                            // Triggers rootRestartCancellationToken.
                            waitForFileChangeBeforeRestarting = false;
                            break;
                        }

                        if (!projectsToRebuild.IsEmpty)
                        {
                            while (true)
                            {
                                iterationCancellationToken.ThrowIfCancellationRequested();

                                // pause accumulating file changes during build:
                                fileWatcher.SuppressEvents = true;
                                try
                                {
                                    var buildResults = await Task.WhenAll(
                                        projectsToRebuild.Values.Select(projectPath => BuildProjectAsync(projectPath, rootProjectOptions.BuildArguments, iterationCancellationToken)));

                                    foreach (var (success, output, projectPath) in buildResults)
                                    {
                                        BuildOutput.ReportBuildOutput(_context.Reporter, output, success, projectPath);
                                    }

                                    if (buildResults.All(result => result.success))
                                    {
                                        break;
                                    }
                                }
                                finally
                                {
                                    fileWatcher.SuppressEvents = false;
                                }

                                iterationCancellationToken.ThrowIfCancellationRequested();

                                _ = await fileWatcher.WaitForFileChangeAsync(
                                    change => AcceptChange(change, evaluationResult),
                                    startedWatching: () => _context.Reporter.Report(MessageDescriptor.FixBuildError),
                                    shutdownCancellationToken);
                            }

                            // Changes made since last snapshot of the accumulator shouldn't be included in next Hot Reload update.
                            // Apply them to the workspace.
                            _ = await CaptureChangedFilesSnapshot(projectsToRebuild);

                            _context.Reporter.Report(MessageDescriptor.ProjectsRebuilt, projectsToRebuild.Count);
                        }

                        if (!projectsToRestart.IsEmpty)
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

                            _context.Reporter.Report(MessageDescriptor.ProjectsRestarted, projectsToRestart.Length);
                        }

                        _context.Reporter.Report(MessageDescriptor.HotReloadChangeHandled, stopwatch.ElapsedMilliseconds);

                        async Task<ImmutableList<ChangedFile>> CaptureChangedFilesSnapshot(ImmutableDictionary<ProjectId, string>? rebuiltProjects)
                        {
                            var changedPaths = Interlocked.Exchange(ref changedFilesAccumulator, []);
                            if (changedPaths is [])
                            {
                                return [];
                            }

                            // Note:
                            // It is possible that we could have received multiple changes for a file that should cancel each other (such as Delete + Add),
                            // but they end up split into two snapshots and we will interpret them as two separate Delete and Add changes that trigger
                            // two sets of Hot Reload updates. Hence the normalization is best effort as we can't predict future.

                            var changedFiles = NormalizePathChanges(changedPaths)
                                .Select(changedPath =>
                                {
                                    // On macOS may report Update followed by Add when a new file is created or just updated.
                                    // We normalize Update + Add to just Add and Update + Add + Delete to Update above.
                                    // To distinguish between an addition and an update we check if the file exists.

                                    if (evaluationResult.Files.TryGetValue(changedPath.Path, out var existingFileItem))
                                    {
                                        var changeKind = changedPath.Kind == ChangeKind.Add ? ChangeKind.Update : changedPath.Kind;

                                        return new ChangedFile(existingFileItem, changeKind);
                                    }

                                    // Do not assume the change is an addition, even if the file doesn't exist in the evaluation result.
                                    // The file could have been deleted and Add + Delete sequence could have been normalized to Update. 
                                    return new ChangedFile(
                                        new FileItem() { FilePath = changedPath.Path, ContainingProjectPaths = [] },
                                        changedPath.Kind);
                                })
                                .ToImmutableList();

                            ReportFileChanges(changedFiles);

                            // When a new file is added we need to run design-time build to find out
                            // what kind of the file it is and which project(s) does it belong to (can be linked, web asset, etc.).
                            // We also need to re-evaluate the project if any project files have been modified.
                            // We don't need to rebuild and restart the application though.
                            var fileAdded = changedFiles.Any(f => f.Kind is ChangeKind.Add);
                            var projectChanged = !fileAdded && changedFiles.Any(f => evaluationResult.BuildFiles.Contains(f.Item.FilePath));
                            var evaluationRequired = fileAdded || projectChanged;

                            if (evaluationRequired)
                            {
                                _context.Reporter.Report(fileAdded ? MessageDescriptor.FileAdditionTriggeredReEvaluation : MessageDescriptor.ProjectChangeTriggeredReEvaluation);

                                // TODO: consider re-evaluating only affected projects instead of the whole graph.
                                evaluationResult = await EvaluateRootProjectAsync(iterationCancellationToken);

                                // additional directories may have been added:
                                evaluationResult.WatchFiles(fileWatcher);

                                await compilationHandler.Workspace.UpdateProjectConeAsync(_fileSetFactory.RootProjectFile, iterationCancellationToken);

                                if (shutdownCancellationToken.IsCancellationRequested)
                                {
                                    // Ctrl+C:
                                    return [];
                                }

                                // Update files in the change set with new evaluation info.
                                changedFiles = [.. changedFiles
                                    .Select(f => evaluationResult.Files.TryGetValue(f.Item.FilePath, out var evaluatedFile) ? f with { Item = evaluatedFile } : f)];

                                _context.Reporter.Report(MessageDescriptor.ReEvaluationCompleted);
                            }

                            if (rebuiltProjects != null)
                            {
                                // Filter changed files down to those contained in projects being rebuilt.
                                // File changes that affect projects that are not being rebuilt will stay in the accumulator
                                // and be included in the next Hot Reload change set.
                                var rebuiltProjectPaths = rebuiltProjects.Values.ToHashSet();

                                var newAccumulator = ImmutableList<ChangedPath>.Empty;
                                var newChangedFiles = ImmutableList<ChangedFile>.Empty;

                                foreach (var file in changedFiles)
                                {
                                    if (file.Item.ContainingProjectPaths.All(containingProjectPath => rebuiltProjectPaths.Contains(containingProjectPath)))
                                    {
                                        newChangedFiles = newChangedFiles.Add(file);
                                    }
                                    else
                                    {
                                        newAccumulator = newAccumulator.Add(new ChangedPath(file.Item.FilePath, file.Kind));
                                    }
                                }

                                changedFiles = newChangedFiles;

                                ImmutableInterlocked.Update(ref changedFilesAccumulator, accumulator => accumulator.AddRange(newAccumulator));
                            }

                            if (!evaluationRequired)
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

                    if (rootRunningProject != null)
                    {
                        await rootRunningProject.TerminateAsync();
                    }

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

        private async ValueTask WaitForFileChangeBeforeRestarting(FileWatcher fileWatcher, EvaluationResult? evaluationResult, CancellationToken cancellationToken)
        {
            if (evaluationResult != null)
            {
                if (!fileWatcher.WatchingDirectories)
                {
                    evaluationResult.WatchFiles(fileWatcher);
                }

                _ = await fileWatcher.WaitForFileChangeAsync(
                    evaluationResult.Files,
                    startedWatching: () => _context.Reporter.Report(MessageDescriptor.WaitingForFileChangeBeforeRestarting),
                    cancellationToken);
            }
            else
            {
                // evaluation cancelled - watch for any changes in the directory tree containing the root project:
                fileWatcher.WatchContainingDirectories([_fileSetFactory.RootProjectFile], includeSubdirectories: true);

                _ = await fileWatcher.WaitForFileChangeAsync(
                    acceptChange: change => AcceptChange(change),
                    startedWatching: () => _context.Reporter.Report(MessageDescriptor.WaitingForFileChangeBeforeRestarting),
                    cancellationToken);
            }
        }

        private Predicate<ChangedPath> CreateChangeFilter(EvaluationResult evaluationResult)
            => new(change => AcceptChange(change, evaluationResult));

        private bool AcceptChange(ChangedPath change, EvaluationResult evaluationResult)
        {
            var (path, kind) = change;

            // Handle changes to files that are known to be project build inputs from its evaluation.
            // Compile items might be explicitly added by targets to directories that are excluded by default
            // (e.g. global usings in obj directory). Changes to these files should not be ignored.
            if (evaluationResult.Files.ContainsKey(path))
            {
                return true;
            }

            if (!AcceptChange(change))
            {
                return false;
            }

            // changes in *.*proj, *.props, *.targets:
            if (evaluationResult.BuildFiles.Contains(path))
            {
                return true;
            }

            // Ignore other changes that match DefaultItemExcludes glob if EnableDefaultItems is true,
            // otherwise changes under output and intermediate output directories.
            //
            // Unsupported scenario:
            // - msbuild target adds source files to intermediate output directory and Compile items
            //   based on the content of non-source file.
            //
            // On the other hand, changes to source files produced by source generators will be registered
            // since the changes to additional file will trigger workspace update, which will trigger the source generator.
            return !evaluationResult.ItemExclusions.IsExcluded(path, kind, _context.Reporter);
        }

        private bool AcceptChange(ChangedPath change)
        {
            var (path, kind) = change;

            if (PathUtilities.GetContainingDirectories(path).FirstOrDefault(IsHiddenDirectory) is { } containingHiddenDir)
            {
                _context.Reporter.Report(MessageDescriptor.IgnoringChangeInHiddenDirectory, containingHiddenDir, kind, path);
                return false;
            }

            return true;
        }

        // Directory name starts with '.' on Unix is considered hidden.
        // Apply the same convention on Windows as well (instead of checking for hidden attribute).
        // This is consistent with SDK rules for default item exclusions:
        // https://github.com/dotnet/sdk/blob/124be385f90f2c305dde2b817cb470e4d11d2d6b/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Sdk.DefaultItems.targets#L42
        private static bool IsHiddenDirectory(string dir)
            => Path.GetFileName(dir).StartsWith('.');

        internal static IEnumerable<ChangedPath> NormalizePathChanges(IEnumerable<ChangedPath> changes)
            => changes
                .GroupBy(keySelector: change => change.Path)
                .Select(group =>
                {
                    ChangedPath? lastUpdate = null;
                    ChangedPath? lastDelete = null;
                    ChangedPath? lastAdd = null;
                    ChangedPath? previous = null;

                    foreach (var item in group)
                    {
                        // eliminate repeated changes:
                        if (item.Kind == previous?.Kind)
                        {
                            continue;
                        }

                        previous = item;

                        if (item.Kind == ChangeKind.Add)
                        {
                            // eliminate delete-(update)*-add:
                            if (lastDelete.HasValue)
                            {
                                lastDelete = null;
                                lastAdd = null;
                                lastUpdate ??= item with { Kind = ChangeKind.Update };
                            }
                            else
                            {
                                lastAdd = item;
                            }
                        }
                        else if (item.Kind == ChangeKind.Delete)
                        {
                            // eliminate add-delete:
                            if (lastAdd.HasValue)
                            {
                                lastDelete = null;
                                lastAdd = null;
                            }
                            else
                            {
                                lastDelete = item;

                                // eliminate previous update:
                                lastUpdate = null;
                            }
                        }
                        else if (item.Kind == ChangeKind.Update)
                        {
                            // ignore updates after add:
                            if (!lastAdd.HasValue)
                            {
                                lastUpdate = item;
                            }
                        }
                        else
                        {
                            throw new InvalidOperationException($"Unexpected change kind: {item.Kind}");
                        }
                    }

                    return lastDelete ?? lastAdd ?? lastUpdate;
                })
                .Where(item => item != null)
                .Select(item => item!.Value);

        private void ReportWatchingForChanges()
        {
            var waitingForChanges = MessageDescriptor.WaitingForChanges;
            if (_context.EnvironmentOptions.TestFlags.HasFlag(TestFlags.ElevateWaitingForChangesMessageSeverity))
            {
                waitingForChanges = waitingForChanges with { Severity = MessageSeverity.Output };
            }

            _context.Reporter.Report(waitingForChanges);
        }

        private void ReportFileChanges(IReadOnlyList<ChangedFile> changedFiles)
        {
            Report(kind: ChangeKind.Add);
            Report(kind: ChangeKind.Update);
            Report(kind: ChangeKind.Delete);

            void Report(ChangeKind kind)
            {
                var items = changedFiles.Where(item => item.Kind == kind).ToArray();
                if (items is not [])
                {
                    _context.Reporter.Output(GetMessage(items, kind));
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

                var result = await _fileSetFactory.TryCreateAsync(requireProjectGraph: true, cancellationToken);
                if (result != null)
                {
                    Debug.Assert(result.ProjectGraph != null);
                    return result;
                }

                await FileWatcher.WaitForFileChangeAsync(
                    _fileSetFactory.RootProjectFile,
                    _context.Reporter,
                    startedWatching: () => _context.Reporter.Report(MessageDescriptor.FixBuildError),
                    cancellationToken);
            }
        }

        private async Task<(bool success, ImmutableArray<OutputLine> output, string projectPath)> BuildProjectAsync(
            string projectPath, IReadOnlyList<string> buildArguments, CancellationToken cancellationToken)
        {
            var buildOutput = new List<OutputLine>();

            var processSpec = new ProcessSpec
            {
                Executable = _context.EnvironmentOptions.MuxerPath,
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

            _context.Reporter.Output($"Building {projectPath} ...");

            var exitCode = await _context.ProcessRunner.RunAsync(processSpec, _context.Reporter, isUserApplication: false, launchResult: null, cancellationToken);
            return (exitCode == 0, buildOutput.ToImmutableArray(), projectPath);
        }

        private string GetRelativeFilePath(string path)
        {
            var relativePath = path;
            var workingDirectory = _context.EnvironmentOptions.WorkingDirectory;
            if (path.StartsWith(workingDirectory, StringComparison.Ordinal) && path.Length > workingDirectory.Length)
            {
                relativePath = path.Substring(workingDirectory.Length);

                return $".{(relativePath.StartsWith(Path.DirectorySeparatorChar) ? string.Empty : Path.DirectorySeparatorChar)}{relativePath}";
            }

            return relativePath;
        }
    }
}
