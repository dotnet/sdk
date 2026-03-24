// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Encodings.Web;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal sealed class HotReloadDotNetWatcher
{
    public const string ClientLogComponentName = $"{nameof(HotReloadDotNetWatcher)}:Client";
    public const string AgentLogComponentName = $"{nameof(HotReloadDotNetWatcher)}:Agent";

    private readonly IConsole _console;
    private readonly IRuntimeProcessLauncherFactory? _runtimeProcessLauncherFactory;
    private readonly RestartPrompt? _rudeEditRestartPrompt;
    private readonly TargetFrameworkSelectionPrompt? _targetFrameworkSelectionPrompt;

    private readonly DotNetWatchContext _context;
    private readonly ProjectGraphFactory _designTimeBuildGraphFactory;

    internal Task? Test_FileChangesCompletedTask { get; set; }

    public HotReloadDotNetWatcher(DotNetWatchContext context, IConsole console, IRuntimeProcessLauncherFactory? runtimeProcessLauncherFactory, TargetFrameworkSelectionPrompt? targetFrameworkSelectionPrompt)
    {
        _context = context;
        _console = console;
        _runtimeProcessLauncherFactory = runtimeProcessLauncherFactory;
        if (!context.Options.NonInteractive)
        {
            var consoleInput = new ConsoleInputReader(_console, context.Options.LogLevel, context.EnvironmentOptions.SuppressEmojis);

            var noPrompt = context.EnvironmentOptions.RestartOnRudeEdit;
            if (noPrompt)
            {
                context.Logger.LogDebug("DOTNET_WATCH_RESTART_ON_RUDE_EDIT = 'true'. Will restart without prompt.");
            }

            _rudeEditRestartPrompt = new RestartPrompt(context.Logger, consoleInput, noPrompt ? true : null);
            _targetFrameworkSelectionPrompt = targetFrameworkSelectionPrompt;
        }

        _designTimeBuildGraphFactory = new ProjectGraphFactory(
            context.RootProjects,
            context.MainProjectOptions?.TargetFramework,
            buildProperties: EvaluationResult.GetGlobalBuildProperties(
                context.BuildArguments,
                context.EnvironmentOptions),
            context.BuildLogger);
    }

    public async Task WatchAsync(CancellationToken shutdownCancellationToken)
    {
        CancellationTokenSource? forceRestartCancellationSource = null;

        _context.Logger.Log(MessageDescriptor.HotReloadEnabled);
        _context.Logger.Log(MessageDescriptor.PressCtrlRToRestart);

        _console.KeyPressed += (key) =>
        {
            if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.R && forceRestartCancellationSource is { } source)
            {
                // provide immediate feedback to the user:
                _context.Logger.Log(source.IsCancellationRequested ? MessageDescriptor.RestartInProgress : MessageDescriptor.RestartRequested);
                source.Cancel();
            }
        };

        using var fileWatcher = new FileWatcher(_context.Logger, _context.EnvironmentOptions);

        for (var iteration = 0; !shutdownCancellationToken.IsCancellationRequested; iteration++)
        {
            Interlocked.Exchange(ref forceRestartCancellationSource, new CancellationTokenSource())?.Dispose();

            // This source will signal when the user cancels (either Ctrl+R or Ctrl+C):
            using var iterationCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(shutdownCancellationToken, forceRestartCancellationSource.Token);
            var iterationCancellationToken = iterationCancellationSource.Token;

            var suppressWaitForFileChange = false;
            EvaluationResult? evaluationResult = null;
            RunningProject? mainRunningProject = null;
            IRuntimeProcessLauncher? runtimeProcessLauncher = null;
            CompilationHandler? compilationHandler = null;
            Action<ChangedPath>? fileChangedCallback = null;
            LoadedProjectGraph? projectGraph = null;
            BuildProjectsResult? rootProjectsBuildResult = null;

            try
            {
                rootProjectsBuildResult = await BuildProjectsAsync(
                    _context.RootProjects,
                    fileWatcher,
                    _context.MainProjectOptions,
                    frameworkSelector: _targetFrameworkSelectionPrompt != null ? _targetFrameworkSelectionPrompt.SelectAsync : null,
                    iterationCancellationToken);

                // Try load project graph and perform design-time build even if the build failed.
                // This allows us to watch the project and source files for changes that will trigger restart.

                projectGraph = rootProjectsBuildResult.ProjectGraph ?? TryLoadProjectGraph(iterationCancellationToken);
                if (projectGraph == null)
                {
                    continue;
                }

                fileWatcher.WatchFiles(projectGraph.BuildFiles);

                // Avoid restore since the build above already restored all root projects.
                evaluationResult = await TryEvaluateProjectGraphAsync(projectGraph, rootProjectsBuildResult.MainProjectTargetFramework, restore: false, iterationCancellationToken);
                if (evaluationResult == null)
                {
                    continue;
                }

                evaluationResult.ItemExclusions.Report(_context.Logger);
                evaluationResult.WatchFileItems(fileWatcher);

                if (!rootProjectsBuildResult.Success)
                {
                    continue;
                }

                compilationHandler = new CompilationHandler(_context);

                // The session must start after the project is built and design time build completes,
                // so that the EnC service can read document checksums from the PDB and the solution
                // can be initialized from the current state of the project instances in the graph.
                //
                // Starting the session hydrates the contents of solution documents from disk.
                // Session must be started before we start accepting file changes to avoid race condition
                // when the EnC session hydrates solution documents with their file content after the changes have already been observed.
                await compilationHandler.StartSessionAsync(evaluationResult.ProjectGraph.Graph, iterationCancellationToken);

                var projectLauncher = new ProjectLauncher(_context, projectGraph, compilationHandler, iteration);

                var runtimeProcessLauncherFactory = _runtimeProcessLauncherFactory;

                var mainProjectOptions = _context.MainProjectOptions;
                if (mainProjectOptions != null)
                {
                    mainProjectOptions = mainProjectOptions with { TargetFramework = rootProjectsBuildResult.MainProjectTargetFramework };

                    if (projectGraph.Graph.GraphRoots.Single()?.GetCapabilities().Contains(AspireServiceFactory.AppHostProjectCapability) == true)
                    {
                        runtimeProcessLauncherFactory ??= new AspireServiceFactory(mainProjectOptions);
                        _context.Logger.LogDebug("Using Aspire process launcher.");
                    }
                }

                if (runtimeProcessLauncherFactory != null)
                {
                    runtimeProcessLauncher = runtimeProcessLauncherFactory.Create(projectLauncher);
                    _context.Logger.Log(MessageDescriptor.RuntimeProcessLauncherCreatedNotification);
                }

                if (mainProjectOptions != null)
                {
                    if (runtimeProcessLauncher != null)
                    {
                        mainProjectOptions = mainProjectOptions with
                        {
                            LaunchEnvironmentVariables = [.. mainProjectOptions.LaunchEnvironmentVariables, .. runtimeProcessLauncher.GetEnvironmentVariables()]
                        };
                    }

                    mainRunningProject = await projectLauncher.TryLaunchProcessAsync(
                        mainProjectOptions,
                        onOutput: null,
                        onExit: (_, _) =>
                        {
                            iterationCancellationSource.Cancel();
                            return ValueTask.CompletedTask;
                        },
                        restartOperation: new RestartOperation(_ => default), // the process will automatically restart
                        iterationCancellationToken);

                    if (mainRunningProject == null)
                    {
                        // error has been reported:
                        return;
                    }

                    // Cancel iteration as soon as the main process exits, so that we don't spent time loading solution, etc. when the process is already dead.
                    mainRunningProject.Process.ExitedCancellationToken.Register(iterationCancellationSource.Cancel);
                }

                if (shutdownCancellationToken.IsCancellationRequested)
                {
                    // Ctrl+C:
                    return;
                }

                var changedFilesAccumulator = ImmutableList<ChangedPath>.Empty;

                void FileChangedCallback(ChangedPath change)
                {
                    if (AcceptChange(change, evaluationResult))
                    {
                        _context.Logger.LogDebug("File change: {Kind} '{Path}'.", change.Kind, change.Path);
                        ImmutableInterlocked.Update(ref changedFilesAccumulator, changedPaths => changedPaths.Add(change));
                    }
                }

                fileChangedCallback = FileChangedCallback;
                fileWatcher.OnFileChange += fileChangedCallback;
                _context.Logger.Log(MessageDescriptor.WaitingForChanges);

                if (Test_FileChangesCompletedTask != null)
                {
                    await Test_FileChangesCompletedTask;
                }

                // Hot Reload loop
                while (!iterationCancellationToken.IsCancellationRequested)
                {
                    ImmutableArray<ChangedFile> changedFiles;
                    do
                    {
                        // Use timeout to batch file changes. If the process doesn't exit within the given timespan we'll check
                        // for accumulated file changes. If there are any we attempt Hot Reload. Otherwise we come back here to wait again.
                        await Task.Delay(50, iterationCancellationToken);

                        // If the changes include addition/deletion wait a little bit more for possible matching deletion/addition.
                        // This eliminates reevaluations caused by teared add + delete of a temp file or a move of a file.
                        if (changedFilesAccumulator.Any(change => change.Kind is ChangeKind.Add or ChangeKind.Delete))
                        {
                            await Task.Delay(150, iterationCancellationToken);
                        }

                        changedFiles = await CaptureChangedFilesSnapshot(rebuiltProjects: []);
                    }
                    while (changedFiles is []);

                    var updates = new HotReloadProjectUpdatesBuilder();
                    var stopwatch = Stopwatch.StartNew();

                    await compilationHandler.GetStaticAssetUpdatesAsync(updates, changedFiles, evaluationResult, stopwatch, iterationCancellationToken);

                    await compilationHandler.GetManagedCodeUpdatesAsync(
                        updates,
                        restartPrompt: async (projectNames, cancellationToken) =>
                        {
                            // stop before waiting for user input:
                            stopwatch.Stop();
                            var result = await RestartPrompt(projectNames, runtimeProcessLauncher, cancellationToken);
                            stopwatch.Start();
                            return result;
                        },
                        autoRestart: _context.Options.NonInteractive || _rudeEditRestartPrompt?.AutoRestartPreference is true,
                        iterationCancellationToken);

                    // Terminate root process if it had rude edits or is non-reloadable.
                    if (updates.ProjectsToRestart.Any(static project => project.Options.IsMainProject))
                    {
                        Debug.Assert(mainRunningProject != null);
                        mainRunningProject.InitiateRestart();
                        break;
                    }

                    if (updates.ProjectsToRebuild is not [])
                    {
                        while (true)
                        {
                            iterationCancellationToken.ThrowIfCancellationRequested();

                            var result = await BuildProjectsAsync(
                                [.. updates.ProjectsToRebuild.Select(ProjectRepresentation.FromProjectOrEntryPointFilePath)],
                                fileWatcher,
                                mainProjectOptions,
                                frameworkSelector: null,
                                iterationCancellationToken);

                            if (result.Success)
                            {
                                break;
                            }

                            _ = await fileWatcher.WaitForFileChangeAsync(
                                change => AcceptChange(change, evaluationResult),
                                startedWatching: () => _context.Logger.Log(MessageDescriptor.FixBuildError),
                                shutdownCancellationToken);
                        }

                        // Changes made since last snapshot of the accumulator shouldn't be included in next Hot Reload update.
                        // Apply them to the workspace.
                        _ = await CaptureChangedFilesSnapshot(updates.ProjectsToRebuild);

                        _context.Logger.Log(MessageDescriptor.ProjectsRebuilt, updates.ProjectsToRebuild.Count);
                    }

                    // Deploy dependencies after rebuilding and before restarting.
                    if (updates.ProjectsToRedeploy is not [])
                    {
                        await DeployProjectDependenciesAsync(evaluationResult, updates.ProjectsToRedeploy, iterationCancellationToken);
                        _context.Logger.Log(MessageDescriptor.ProjectDependenciesDeployed, updates.ProjectsToRedeploy.Count);
                    }

                    // Apply updates only after dependencies have been deployed,
                    // so that updated code doesn't attempt to access the dependency before it has been deployed.
                    await compilationHandler.ApplyManagedCodeAndStaticAssetUpdatesAndRelaunchAsync(updates.ManagedCodeUpdates, updates.StaticAssetsToUpdate, changedFiles, evaluationResult.ProjectGraph, stopwatch, iterationCancellationToken);
                    if (updates.ProjectsToRestart is not [])
                    {
                        await compilationHandler.RestartPeripheralProjectsAsync(updates.ProjectsToRestart, shutdownCancellationToken);
                    }

                    async Task<ImmutableArray<ChangedFile>> CaptureChangedFilesSnapshot(IReadOnlyList<string> rebuiltProjects)
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
                            .ToList();

                        ReportFileChanges(changedFiles);

                        AnalyzeFileChanges(changedFiles, evaluationResult, out var evaluationRequired);

                        if (evaluationRequired)
                        {
                            // TODO: consider reloading/reevaluating only affected projects instead of the whole graph.

                            while (true)
                            {
                                iterationCancellationToken.ThrowIfCancellationRequested();

                                projectGraph = TryLoadProjectGraph(iterationCancellationToken);
                                if (projectGraph != null)
                                {
                                    fileWatcher.WatchFiles(projectGraph.BuildFiles);

                                    evaluationResult = await TryEvaluateProjectGraphAsync(projectGraph, mainProjectOptions?.TargetFramework, restore: true, iterationCancellationToken);
                                    if (evaluationResult != null)
                                    {
                                        break;
                                    }
                                }

                                _ = await fileWatcher.WaitForFileChangeAsync(
                                    acceptChange: AcceptChange,
                                    startedWatching: () => _context.Logger.Log(MessageDescriptor.FixBuildError),
                                    shutdownCancellationToken);
                            }

                            // additional files/directories may have been added:
                            evaluationResult.WatchFileItems(fileWatcher);

                            await compilationHandler.UpdateProjectGraphAsync(evaluationResult.ProjectGraph.Graph, iterationCancellationToken);

                            if (shutdownCancellationToken.IsCancellationRequested)
                            {
                                // Ctrl+C:
                                return [];
                            }

                            // Update files in the change set with new evaluation info.
                            for (var i = 0; i < changedFiles.Count; i++)
                            {
                                var file = changedFiles[i];
                                if (evaluationResult.Files.TryGetValue(file.Item.FilePath, out var evaluatedFile))
                                {
                                    changedFiles[i] = file with { Item = evaluatedFile };
                                }
                            }

                            _context.Logger.Log(MessageDescriptor.ReEvaluationCompleted);
                        }

                        if (rebuiltProjects is not [])
                        {
                            // Filter changed files down to those contained in projects being rebuilt.
                            // File changes that affect projects that are not being rebuilt will stay in the accumulator
                            // and be included in the next Hot Reload change set.
                            var rebuiltProjectPaths = rebuiltProjects.ToHashSet();

                            var newAccumulator = ImmutableList<ChangedPath>.Empty;
                            var newChangedFiles = new List<ChangedFile>();

                            foreach (var file in changedFiles)
                            {
                                if (file.Item.ContainingProjectPaths.All(rebuiltProjectPaths.Contains))
                                {
                                    newChangedFiles.Add(file);
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
                            // Update the workspace to reflect changes in the file content:.
                            // If the project was re-evaluated the Roslyn solution is already up to date.
                            await compilationHandler.UpdateFileContentAsync(changedFiles, iterationCancellationToken);
                        }

                        return [.. changedFiles];
                    }
                }
            }
            catch (OperationCanceledException) when (!shutdownCancellationToken.IsCancellationRequested)
            {
                // start next iteration unless shutdown is requested
            }
            catch (Exception) when (!(suppressWaitForFileChange = true))
            {
                // unreachable
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
                    // Dispose the launcher so that it won't start any new peripheral processes.
                    // Do this before terminating all processes, so that we don't leave any processes orphaned.
                    await runtimeProcessLauncher.DisposeAsync();
                }

                if (compilationHandler != null)
                {
                    // Non-cancellable - can only be aborted by forced Ctrl+C, which immediately kills the dotnet-watch process.
                    await compilationHandler.TerminatePeripheralProcessesAndDispose(CancellationToken.None);
                }

                if (mainRunningProject != null)
                {
                    await mainRunningProject.Process.TerminateAsync();
                }

                // Wait for file change
                // - if the process hasn't launched (e.g. build failed)
                // - if the process launched, has been terminated and is not being auto-restarted (rude edit),
                // unless Ctrl+R or Ctrl+C were pressed.
                if (shutdownCancellationToken.IsCancellationRequested)
                {
                    // no op
                }
                else if (forceRestartCancellationSource.IsCancellationRequested)
                {
                    _context.Logger.Log(MessageDescriptor.Restarting);
                }
                else if (mainRunningProject?.IsRestarting != true && !suppressWaitForFileChange)
                {
                    using var shutdownOrForcedRestartSource = CancellationTokenSource.CreateLinkedTokenSource(shutdownCancellationToken, forceRestartCancellationSource.Token);
                    await WaitForFileChangeBeforeRestarting(
                        fileWatcher,
                        evaluationResult,
                        projectGraph,
                        rootProjectsBuildResult?.Success == true,
                        shutdownOrForcedRestartSource.Token);
                }
            }
        }
    }

    private async Task<bool> RestartPrompt(IEnumerable<string> projectNames, IRuntimeProcessLauncher? runtimeProcessLauncher, CancellationToken cancellationToken)
    {
        if (_rudeEditRestartPrompt != null)
        {
            string question;
            if (runtimeProcessLauncher == null)
            {
                question = "Do you want to restart your app?";
            }
            else
            {
                _context.Logger.LogInformation("Affected projects:");

                foreach (var projectName in projectNames.Order())
                {
                    _context.Logger.LogInformation("  {ProjectName}", projectName);
                }

                question = "Do you want to restart these projects?";
            }

            return await _rudeEditRestartPrompt.WaitForRestartConfirmationAsync(question, cancellationToken);
        }

        _context.Logger.LogDebug("Restarting without prompt since dotnet-watch is running in non-interactive mode.");

        foreach (var projectName in projectNames)
        {
            _context.Logger.LogDebug("  Project to restart: '{ProjectName}'", projectName);
        }

        return true;
    }

    private void AnalyzeFileChanges(
        List<ChangedFile> changedFiles,
        EvaluationResult evaluationResult,
        out bool evaluationRequired)
    {
        // If any build file changed (project, props, targets) we need to re-evaluate the projects.
        // Currently we re-evaluate the whole project graph even if only a single project file changed.
        if (changedFiles.Select(f => f.Item.FilePath).FirstOrDefault(path => evaluationResult.ProjectGraph.BuildFiles.Contains(path) || MatchesBuildFile(path)) is { } firstBuildFilePath)
        {
            _context.Logger.Log(MessageDescriptor.ProjectChangeTriggeredReEvaluation, firstBuildFilePath);
            evaluationRequired = true;
            return;
        }

        for (var i = 0; i < changedFiles.Count; i++)
        {
            var changedFile = changedFiles[i];
            var filePath = changedFile.Item.FilePath;

            if (changedFile.Kind is ChangeKind.Add)
            {
                if (MatchesStaticWebAssetFilePattern(evaluationResult, filePath, out var staticWebAssetUrl))
                {
                    changedFiles[i] = changedFile with
                    {
                        Item = changedFile.Item with { StaticWebAssetRelativeUrl = staticWebAssetUrl }
                    };
                }
                else
                {
                    // TODO: https://github.com/dotnet/sdk/issues/52390
                    // Get patterns from evaluation that match Compile, AdditionalFile, AnalyzerConfigFile items.
                    // Avoid re-evaluating on addition of files that don't affect the project.

                    // project file or other file:
                    _context.Logger.Log(MessageDescriptor.FileAdditionTriggeredReEvaluation, filePath);
                    evaluationRequired = true;
                    return;
                }
            }
        }

        evaluationRequired = false;
    }

    /// <summary>
    /// True if the file path looks like a file that might be imported by MSBuild.
    /// </summary>
    private static bool MatchesBuildFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return extension.Equals(".props", PathUtilities.OSSpecificPathComparison)
            || extension.Equals(".targets", PathUtilities.OSSpecificPathComparison)
            || extension.EndsWith("proj", PathUtilities.OSSpecificPathComparison)
            || extension.Equals(".projitems", PathUtilities.OSSpecificPathComparison) // shared project items
            || string.Equals(Path.GetFileName(filePath), "global.json", PathUtilities.OSSpecificPathComparison);
    }

    /// <summary>
    /// Determines if the given file path is a static web asset file path based on
    /// the discovery patterns.
    /// </summary>
    private static bool MatchesStaticWebAssetFilePattern(EvaluationResult evaluationResult, string filePath, out string? staticWebAssetUrl)
    {
        staticWebAssetUrl = null;

        if (StaticWebAsset.IsScopedCssFile(filePath))
        {
            return true;
        }

        foreach (var (_, manifest) in evaluationResult.StaticWebAssetsManifests)
        {
            foreach (var pattern in manifest.DiscoveryPatterns)
            {
                var match = pattern.Glob.MatchInfo(filePath);
                if (match.IsMatch)
                {
                    var dirUrl = match.WildcardDirectoryPartMatchGroup.Replace(Path.DirectorySeparatorChar, '/');

                    Debug.Assert(!dirUrl.EndsWith('/'));
                    Debug.Assert(!pattern.BaseUrl.EndsWith('/'));

                    var url = UrlEncoder.Default.Encode(dirUrl + "/" + match.FilenamePartMatchGroup);
                    if (pattern.BaseUrl != "")
                    {
                        url = pattern.BaseUrl + "/" + url;
                    }

                    staticWebAssetUrl = url;
                    return true;
                }
            }
        }

        return false;
    }

    private async ValueTask DeployProjectDependenciesAsync(EvaluationResult evaluationResult, IEnumerable<string> projectPaths, CancellationToken cancellationToken)
    {
        const string TargetName = TargetNames.ReferenceCopyLocalPathsOutputGroup;

        var projectPathSet = projectPaths.ToImmutableHashSet(PathUtilities.OSSpecificPathComparer);

        var buildRequests = new List<BuildRequest<string>>();

        foreach (var (_, restoredProjectInstance) in evaluationResult.RestoredProjectInstances)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Avoid modification of the restored snapshot.
            var projectInstance = restoredProjectInstance.DeepCopy();

            var projectPath = projectInstance.FullPath;

            if (!projectPathSet.Contains(projectPath))
            {
                continue;
            }

            if (!projectInstance.Targets.ContainsKey(TargetName))
            {
                continue;
            }

            if (projectInstance.GetOutputDirectory() is not { } relativeOutputDir)
            {
                continue;
            }

            buildRequests.Add(BuildRequest.Create(projectInstance, [TargetName], relativeOutputDir));
        }

        var results = await evaluationResult.BuildManager.BuildAsync(
            buildRequests,
            onFailure: failedInstance =>
            {
                _context.Logger.LogDebug("[{ProjectName}] {TargetName} target failed", failedInstance.GetDisplayName(), TargetName);

                // continue build
                return true;
            },
            operationName: "DeployProjectDependencies",
            cancellationToken);

        var copyTasks = new List<Task>();

        foreach (var result in results)
        {
            if (!result.IsSuccess)
            {
                continue;
            }

            var relativeOutputDir = result.Data;
            var projectInstance = result.ProjectInstance;

            var projectPath = projectInstance.FullPath;

            var outputDir = Path.Combine(Path.GetDirectoryName(projectPath)!, relativeOutputDir);

            foreach (var item in result.TargetResults[TargetName].Items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sourcePath = item.ItemSpec;
                var targetPath = Path.Combine(outputDir, item.GetMetadata(MetadataNames.TargetPath));

                copyTasks.Add(Task.Run(() =>
                {
                    if (!File.Exists(targetPath))
                    {
                        _context.Logger.LogDebug("Deploying project dependency '{TargetPath}' from '{SourcePath}'", targetPath, sourcePath);

                        try
                        {
                            var directory = Path.GetDirectoryName(targetPath);
                            if (directory != null)
                            {
                                Directory.CreateDirectory(directory);
                            }

                            File.Copy(sourcePath, targetPath, overwrite: false);
                        }
                        catch (Exception e)
                        {
                            _context.Logger.LogDebug("Copy failed: {Message}", e.Message);
                        }
                    }
                }, cancellationToken));
            }
        }

        await Task.WhenAll(copyTasks);
    }

    private async ValueTask WaitForFileChangeBeforeRestarting(FileWatcher fileWatcher, EvaluationResult? evaluationResult, LoadedProjectGraph? projectGraph, bool buildSucceeded, CancellationToken cancellationToken)
    {
        var messageDescriptor = buildSucceeded ? MessageDescriptor.WaitingForFileChangeBeforeRestarting : MessageDescriptor.FixBuildError;

        if (evaluationResult != null)
        {
            _ = await fileWatcher.WaitForFileChangeAsync(
                evaluationResult.Files,
                startedWatching: () => _context.Logger.Log(messageDescriptor),
                cancellationToken);
        }
        else
        {
            // build failed or was cancelled - watch for any changes in the directory trees containing root projects or entry-point files:
            if (projectGraph == null)
            {
                fileWatcher.WatchContainingDirectories(_context.RootProjects.Select(p => p.ProjectOrEntryPointFilePath), includeSubdirectories: true);
            }

            _ = await fileWatcher.WaitForFileChangeAsync(
                acceptChange: AcceptChange,
                startedWatching: () => _context.Logger.Log(messageDescriptor),
                cancellationToken);
        }
    }

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
        if (evaluationResult.ProjectGraph.BuildFiles.Contains(path))
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
        return !evaluationResult.ItemExclusions.IsExcluded(path, kind, _context.Logger);
    }

    private bool AcceptChange(ChangedPath change)
    {
        var (path, kind) = change;

        if (Path.GetExtension(path) == ".binlog")
        {
            return false;
        }

        if (PathUtilities.GetContainingDirectories(path).FirstOrDefault(IsHiddenDirectory) is { } containingHiddenDir)
        {
            _context.Logger.Log(MessageDescriptor.IgnoringChangeInHiddenDirectory, containingHiddenDir, kind, path);
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
                _context.Logger.LogInformation(GetMessage(items, kind));
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

    private LoadedProjectGraph? TryLoadProjectGraph(CancellationToken cancellationToken)
        => _designTimeBuildGraphFactory.TryLoadProjectGraph(projectGraphRequired: true, cancellationToken);

    private ValueTask<EvaluationResult?> TryEvaluateProjectGraphAsync(LoadedProjectGraph projectGraph, string? mainProjectTargetFramework, bool restore, CancellationToken cancellationToken)
        => EvaluationResult.TryCreateAsync(
            projectGraph,
            _context.BuildLogger,
            _context.Options,
            _context.EnvironmentOptions,
            mainProjectTargetFramework,
            restore,
            cancellationToken);

    private enum BuildAction
    {
        RestoreOnly,
        RestoreAndBuild,
        BuildOnly,
    }

    // internal for testing
    internal sealed class BuildProjectsResult(string? mainProjectTargetFramework, LoadedProjectGraph? projectGraph, bool success)
    {
        public string? MainProjectTargetFramework { get; } = mainProjectTargetFramework;
        public LoadedProjectGraph? ProjectGraph { get; } = projectGraph;
        public bool Success { get; } = success;
    }

    // internal for testing
    internal async Task<BuildProjectsResult> BuildProjectsAsync(
        ImmutableArray<ProjectRepresentation> projects,
        FileWatcher fileWatcher,
        ProjectOptions? mainProjectOptions,
        Func<IReadOnlyList<string>, CancellationToken, ValueTask<string>>? frameworkSelector,
        CancellationToken cancellationToken)
    {
        Debug.Assert(projects.Any());

        LoadedProjectGraph? projectGraph = null;
        var targetFramework = mainProjectOptions?.TargetFramework;

        _context.Logger.Log(MessageDescriptor.BuildStartedNotification, projects);

        // pause accumulating file changes during build:
        fileWatcher.SuppressEvents = true;
        try
        {
            var success = await BuildWithFrameworkSelectionAsync();
            _context.Logger.Log(MessageDescriptor.BuildCompletedNotification, (projects, success));
            return new BuildProjectsResult(targetFramework, projectGraph, success);
        }
        finally
        {
            fileWatcher.SuppressEvents = false;
        }

        async ValueTask<bool> BuildWithFrameworkSelectionAsync()
        {
            if (mainProjectOptions == null ||
                frameworkSelector == null ||
                targetFramework != null ||
                !mainProjectOptions.Representation.IsProjectFile)
            {
                return await BuildAsync(BuildAction.RestoreAndBuild, targetFramework);
            }

            if (!await BuildAsync(BuildAction.RestoreOnly, targetFramework: null))
            {
                return false;
            }

            // load project graph after restore so that props and targets files from packages are imported:
            projectGraph = TryLoadProjectGraph(cancellationToken);
            if (projectGraph == null)
            {
                return false;
            }

            var rootProject = projectGraph.Graph.GraphRoots.Single().ProjectInstance;
            if (rootProject.GetTargetFramework() is var framework and not "")
            {
                targetFramework = framework;
            }
            else if (rootProject.GetTargetFrameworks() is var frameworks and not [])
            {
                targetFramework = await frameworkSelector(frameworks, cancellationToken);
            }
            else
            {
                _context.BuildLogger.LogError("Project '{Path}' does not specify a target framework.", rootProject.FullPath);
                return false;
            }

            return await BuildAsync(BuildAction.BuildOnly, targetFramework);
        }

        async Task<bool> BuildAsync(BuildAction action, string? targetFramework)
        {
            if (projects is [var singleProject])
            {
                return await BuildFileOrProjectOrSolutionAsync(singleProject.ProjectOrEntryPointFilePath, targetFramework, action, cancellationToken);
            }

            // TODO: workaround for https://github.com/dotnet/sdk/issues/51311

            var projectPaths = projects.Where(p => p.PhysicalPath != null).Select(p => p.PhysicalPath!).ToArray();

            if (projectPaths is [var singleProjectPath])
            {
                if (!await BuildFileOrProjectOrSolutionAsync(singleProjectPath, targetFramework, action, cancellationToken))
                {
                    return false;
                }
            }
            else if (projectPaths is not [])
            {
                var solutionFile = Path.Combine(Path.GetTempFileName() + ".slnx");
                var solutionElement = new XElement("Solution");

                foreach (var projectPath in projectPaths)
                {
                    solutionElement.Add(new XElement("Project", new XAttribute("Path", projectPath)));
                }

                var doc = new XDocument(solutionElement);
                doc.Save(solutionFile);

                try
                {
                    if (!await BuildFileOrProjectOrSolutionAsync(solutionFile, targetFramework, action, cancellationToken))
                    {
                        return false;
                    }
                }
                finally
                {
                    try
                    {
                        File.Delete(solutionFile);
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }

            // To maximize parallelism of building dependencies, build file-based projects after all physical projects:
            foreach (var file in projects.Where(p => p.EntryPointFilePath != null).Select(p => p.EntryPointFilePath!))
            {
                if (!await BuildFileOrProjectOrSolutionAsync(file, targetFramework, action, cancellationToken))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private async Task<bool> BuildFileOrProjectOrSolutionAsync(string path, string? targetFramework, BuildAction action, CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            action is BuildAction.RestoreOnly ? "restore" : "build",
            path
        };

        arguments.AddRange(_context.BuildArguments);

        if (action != BuildAction.RestoreOnly && targetFramework != null)
        {
            arguments.Add("--framework");
            arguments.Add(targetFramework);
        }

        if (action == BuildAction.BuildOnly)
        {
            arguments.Add("--no-restore");
        }
        else if (action == BuildAction.RestoreOnly)
        {
            arguments.Add("-consoleLoggerParameters:NoSummary");
        }

        List<OutputLine>? capturedOutput = _context.EnvironmentOptions.TestFlags != TestFlags.None ? [] : null;
        var processSpec = new ProcessSpec
        {
            Executable = _context.EnvironmentOptions.GetMuxerPath(),
            WorkingDirectory = Path.GetDirectoryName(path),
            IsUserApplication = false,

            // Capture output if running in a test environment.
            // If the output is not captured dotnet build will show live build progress.
            OnOutput = capturedOutput != null
                ? line =>
                {
                    lock (capturedOutput)
                    {
                        capturedOutput.Add(line);
                    }
                }
                : null,

            // pass user-specified build arguments last to override defaults:
            Arguments = arguments
        };

        _context.BuildLogger.Log(action == BuildAction.RestoreOnly ? MessageDescriptor.Restoring : MessageDescriptor.Building, path);

        var success = await _context.ProcessRunner.RunAsync(processSpec, _context.Logger, launchResult: null, cancellationToken) == 0;

        if (capturedOutput != null)
        {
            // To avoid multiple status messages, only log the status if the output of `dotnet build` is not being streamed to the console:
            _context.BuildLogger.Log(
                (action, success) switch
                {
                    (BuildAction.RestoreOnly, true) => MessageDescriptor.RestoreSucceeded,
                    (BuildAction.RestoreOnly, false) => MessageDescriptor.RestoreFailed,
                    (_, true) => MessageDescriptor.BuildSucceeded,
                    (_, false) => MessageDescriptor.BuildFailed,
                },
                path);

            BuildOutput.ReportBuildOutput(_context.BuildLogger, capturedOutput, success);
        }

        return success;
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
