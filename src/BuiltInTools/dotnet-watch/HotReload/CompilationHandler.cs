// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Build.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.ExternalAccess.Watch.Api;

namespace Microsoft.DotNet.Watch
{
    internal sealed class CompilationHandler : IDisposable
    {
        public readonly IncrementalMSBuildWorkspace Workspace;
        private readonly IReporter _reporter;
        private readonly WatchHotReloadService _hotReloadService;
        private readonly ProcessRunner _processRunner;

        /// <summary>
        /// Lock to synchronize:
        /// <see cref="_runningProjects"/>
        /// <see cref="_previousUpdates"/>
        /// <see cref="_currentAggregateCapabilities"/>
        /// </summary>
        private readonly object _runningProjectsAndUpdatesGuard = new();

        /// <summary>
        /// Projects that have been launched and to which we apply changes. 
        /// </summary>
        private ImmutableDictionary<string, ImmutableArray<RunningProject>> _runningProjects = ImmutableDictionary<string, ImmutableArray<RunningProject>>.Empty;

        /// <summary>
        /// All updates that were attempted. Includes updates whose application failed.
        /// </summary>
        private ImmutableList<WatchHotReloadService.Update> _previousUpdates = [];

        private bool _isDisposed;

        static CompilationHandler()
        {
            WatchHotReloadService.RequireCommit = true;
        }

        public CompilationHandler(IReporter reporter, ProcessRunner processRunner)
        {
            _reporter = reporter;
            _processRunner = processRunner;
            Workspace = new IncrementalMSBuildWorkspace(reporter);
            _hotReloadService = new WatchHotReloadService(Workspace.CurrentSolution.Services, () => ValueTask.FromResult(GetAggregateCapabilities()));
        }

        public void Dispose()
        {
            _isDisposed = true;
            Workspace?.Dispose();
        }

        public async ValueTask TerminateNonRootProcessesAndDispose(CancellationToken cancellationToken)
        {
            _reporter.Verbose("Disposing remaining child processes.");

            var projectsToDispose = await TerminateNonRootProcessesAsync(projectPaths: null, cancellationToken);

            foreach (var project in projectsToDispose)
            {
                project.Dispose();
            }

            Dispose();
        }

        private void DiscardPreviousUpdates(ImmutableArray<ProjectId> projectsToBeRebuilt)
        {
            // Remove previous updates to all modules that were affected by rude edits.
            // All running projects that statically reference these modules have been terminated.
            // If we missed any project that dynamically references one of these modules its rebuild will fail.
            // At this point there is thus no process that these modules loaded and any process created in future
            // that will load their rebuilt versions.

            lock (_runningProjectsAndUpdatesGuard)
            {
                _previousUpdates = _previousUpdates.RemoveAll(update => projectsToBeRebuilt.Contains(update.ProjectId));
            }
        }
        public async ValueTask StartSessionAsync(CancellationToken cancellationToken)
        {
            _reporter.Report(MessageDescriptor.HotReloadSessionStarting);

            await _hotReloadService.StartSessionAsync(Workspace.CurrentSolution, cancellationToken);

            _reporter.Report(MessageDescriptor.HotReloadSessionStarted);
        }

        public async Task<RunningProject?> TrackRunningProjectAsync(
            ProjectGraphNode projectNode,
            ProjectOptions projectOptions,
            HotReloadAppModel appModel,
            string namedPipeName,
            BrowserRefreshServer? browserRefreshServer,
            ProcessSpec processSpec,
            RestartOperation restartOperation,
            IReporter processReporter,
            CancellationTokenSource processTerminationSource,
            CancellationToken cancellationToken)
        {
            var projectPath = projectNode.ProjectInstance.FullPath;

            var deltaApplier = appModel.CreateDeltaApplier(browserRefreshServer, processReporter);
            if (deltaApplier == null)
            {
                // error already reported
                return null;
            }

            var processExitedSource = new CancellationTokenSource();
            var processCommunicationCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(processExitedSource.Token, cancellationToken);

            // Dispose these objects on failure:
            using var disposables = new Disposables([deltaApplier, processExitedSource, processCommunicationCancellationSource]);

            // It is important to first create the named pipe connection (delta applier is the server)
            // and then start the process (named pipe client). Otherwise, the connection would fail.
            deltaApplier.CreateConnection(namedPipeName, processCommunicationCancellationSource.Token);

            processSpec.OnExit += (_, _) =>
            {
                processExitedSource.Cancel();
                return ValueTask.CompletedTask;
            };

            var launchResult = new ProcessLaunchResult();
            var runningProcess = _processRunner.RunAsync(processSpec, processReporter, isUserApplication: true, launchResult, processTerminationSource.Token);
            if (launchResult.ProcessId == null)
            {
                // error already reported
                return null;
            }

            // Wait for agent to create the name pipe and send capabilities over.
            // the agent blocks the app execution until initial updates are applied (if any).
            var capabilities = await deltaApplier.GetApplyUpdateCapabilitiesAsync(processCommunicationCancellationSource.Token);

            var runningProject = new RunningProject(
                projectNode,
                projectOptions,
                deltaApplier,
                processReporter,
                browserRefreshServer,
                runningProcess,
                launchResult.ProcessId.Value,
                processExitedSource: processExitedSource,
                processTerminationSource: processTerminationSource,
                restartOperation: restartOperation,
                disposables: [processCommunicationCancellationSource],
                capabilities);

            // ownership transferred to running project:
            disposables.Items.Clear();
            disposables.Items.Add(runningProject);

            var appliedUpdateCount = 0;
            while (true)
            {
                // Observe updates that need to be applied to the new process
                // and apply them before adding it to running processes.
                // Do not block on udpates being made to other processes to avoid delaying the new process being up-to-date.
                var updatesToApply = _previousUpdates.Skip(appliedUpdateCount).ToImmutableArray();
                if (updatesToApply.Any())
                {
                    _ = await deltaApplier.ApplyManagedCodeUpdates(updatesToApply, processCommunicationCancellationSource.Token);
                }

                appliedUpdateCount += updatesToApply.Length;

                lock (_runningProjectsAndUpdatesGuard)
                {
                    ObjectDisposedException.ThrowIf(_isDisposed, this);

                    // More updates might have come in while we have been applying updates.
                    // If so, continue updating.
                    if (_previousUpdates.Count > appliedUpdateCount)
                    {
                        continue;
                    }

                    // Only add the running process after it has been up-to-date.
                    // This will prevent new updates being applied before we have applied all the previous updates.
                    if (!_runningProjects.TryGetValue(projectPath, out var projectInstances))
                    {
                        projectInstances = [];
                    }

                    _runningProjects = _runningProjects.SetItem(projectPath, projectInstances.Add(runningProject));

                    // ownership transferred to _runningProjects
                    disposables.Items.Clear();
                    break;
                }
            }

            // Notifies the agent that it can unblock the execution of the process:
            await deltaApplier.InitialUpdatesApplied(cancellationToken);

            // If non-empty solution is loaded into the workspace (a Hot Reload session is active):
            if (Workspace.CurrentSolution is { ProjectIds: not [] } currentSolution)
            {
                // Preparing the compilation is a perf optimization. We can skip it if the session hasn't been started yet. 
                PrepareCompilations(currentSolution, projectPath, cancellationToken);
            }

            return runningProject;
        }

        private ImmutableArray<string> GetAggregateCapabilities()
        {
            var capabilities = _runningProjects
                .SelectMany(p => p.Value)
                .SelectMany(p => p.Capabilities)
                .Distinct(StringComparer.Ordinal)
                .ToImmutableArray();

            _reporter.Verbose($"Hot reload capabilities: {string.Join(" ", capabilities)}.", emoji: "🔥");
            return capabilities;
        }

        private static void PrepareCompilations(Solution solution, string projectPath, CancellationToken cancellationToken)
        {
            // Warm up the compilation. This would help make the deltas for first edit appear much more quickly
            foreach (var project in solution.Projects)
            {
                if (project.FilePath == projectPath)
                {
                    // fire and forget:
                    _ = project.GetCompilationAsync(cancellationToken);
                }
            }
        }

        public async ValueTask<(ImmutableDictionary<ProjectId, string> projectsToRebuild, ImmutableArray<RunningProject> terminatedProjects)> HandleManagedCodeChangesAsync(
            bool autoRestart,
            Func<IEnumerable<string>, CancellationToken, Task<bool>> restartPrompt,
            CancellationToken cancellationToken)
        {
            var currentSolution = Workspace.CurrentSolution;
            var runningProjects = _runningProjects;

            var runningProjectInfos =
               (from project in currentSolution.Projects
                let runningProject = GetCorrespondingRunningProject(project, runningProjects)
                where runningProject != null
                let autoRestartProject = autoRestart || runningProject.ProjectNode.IsAutoRestartEnabled()
                select (project.Id, info: new WatchHotReloadService.RunningProjectInfo() { RestartWhenChangesHaveNoEffect = autoRestartProject }))
                .ToImmutableDictionary(e => e.Id, e => e.info);

            var updates = await _hotReloadService.GetUpdatesAsync(currentSolution, runningProjectInfos, cancellationToken);

            await DisplayResultsAsync(updates, runningProjectInfos, cancellationToken);

            if (updates.Status is WatchHotReloadService.Status.NoChangesToApply or WatchHotReloadService.Status.Blocked)
            {
                // If Hot Reload is blocked (due to compilation error) we ignore the current
                // changes and await the next file change.

                // Note: CommitUpdate/DiscardUpdate is not expected to be called.
                return ([], []);
            }

            var projectsToPromptForRestart =
                (from projectId in updates.ProjectsToRestart.Keys
                 where !runningProjectInfos[projectId].RestartWhenChangesHaveNoEffect // equivallent to auto-restart
                 select currentSolution.GetProject(projectId)!.Name).ToList();

            if (projectsToPromptForRestart.Any() &&
                !await restartPrompt.Invoke(projectsToPromptForRestart, cancellationToken))
            {
                _hotReloadService.DiscardUpdate();

                _reporter.Output("Hot reload suspended. To continue hot reload, press \"Ctrl + R\".", emoji: "🔥");
                await Task.Delay(-1, cancellationToken);

                return ([], []);
            }

            if (!updates.ProjectUpdates.IsEmpty)
            {
                ImmutableDictionary<string, ImmutableArray<RunningProject>> projectsToUpdate;
                lock (_runningProjectsAndUpdatesGuard)
                {
                    // Adding the updates makes sure that all new processes receive them before they are added to running processes.
                    _previousUpdates = _previousUpdates.AddRange(updates.ProjectUpdates);

                    // Capture the set of processes that do not have the currently calculated deltas yet.
                    projectsToUpdate = _runningProjects;
                }

                // Apply changes to all running projects, even if they do not have a static project dependency on any project that changed.
                // The process may load any of the binaries using MEF or some other runtime dependency loader.

                await ForEachProjectAsync(projectsToUpdate, async (runningProject, cancellationToken) =>
                {
                    try
                    {
                        using var processCommunicationCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(runningProject.ProcessExitedSource.Token, cancellationToken);
                        var applySucceded = await runningProject.DeltaApplier.ApplyManagedCodeUpdates(updates.ProjectUpdates, processCommunicationCancellationSource.Token) != ApplyStatus.Failed;
                        if (applySucceded)
                        {
                            runningProject.Reporter.Report(MessageDescriptor.HotReloadSucceeded);
                            if (runningProject.BrowserRefreshServer is { } server)
                            {
                                runningProject.Reporter.Verbose("Refreshing browser.");
                                await server.RefreshBrowserAsync(cancellationToken);
                            }
                        }
                    }
                    catch (OperationCanceledException) when (runningProject.ProcessExitedSource.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        runningProject.Reporter.Verbose("Hot reload canceled because the process exited.", emoji: "🔥");
                    }
                }, cancellationToken);
            }

            // Note: Releases locked project baseline readers, so we can rebuild any projects that need rebuilding.
            _hotReloadService.CommitUpdate();

            DiscardPreviousUpdates(updates.ProjectsToRebuild);

            var projectsToRebuild = updates.ProjectsToRebuild.ToImmutableDictionary(keySelector: id => id, elementSelector: id => currentSolution.GetProject(id)!.FilePath!);

            // Terminate all tracked processes that need to be restarted,
            // except for the root process, which will terminate later on.
            var terminatedProjects = updates.ProjectsToRestart.IsEmpty
                ? []
                : await TerminateNonRootProcessesAsync(updates.ProjectsToRestart.Select(e => currentSolution.GetProject(e.Key)!.FilePath!), cancellationToken);

            return (projectsToRebuild, terminatedProjects);
        }

        private static RunningProject? GetCorrespondingRunningProject(Project project, ImmutableDictionary<string, ImmutableArray<RunningProject>> runningProjects)
        {
            if (project.FilePath == null || !runningProjects.TryGetValue(project.FilePath, out var projectsWithPath))
            {
                return null;
            }

            // msbuild workspace doesn't set TFM if the project is not multi-targeted
            var tfm = WatchHotReloadService.GetTargetFramework(project);
            if (tfm == null)
            {
                return projectsWithPath[0];
            }

            return projectsWithPath.SingleOrDefault(p => string.Equals(p.ProjectNode.GetTargetFramework(), tfm, StringComparison.OrdinalIgnoreCase));
        }

        private async ValueTask DisplayResultsAsync(WatchHotReloadService.Updates2 updates, ImmutableDictionary<ProjectId, WatchHotReloadService.RunningProjectInfo> runningProjectInfos, CancellationToken cancellationToken)
        {
            switch (updates.Status)
            {
                case WatchHotReloadService.Status.ReadyToApply:
                    break;

                case WatchHotReloadService.Status.NoChangesToApply:
                    _reporter.Report(MessageDescriptor.NoCSharpChangesToApply);
                    break;

                case WatchHotReloadService.Status.Blocked:
                    _reporter.Output("Unable to apply hot reload due to compilation errors.");
                    break;

                default:
                    throw new InvalidOperationException();
            }

            if (!updates.ProjectsToRestart.IsEmpty)
            {
                _reporter.Output("Restart is needed to apply the changes.");
            }

            var diagnosticsToDisplayInApp = new List<string>();

            // Display errors first, then warnings:
            ReportCompilationDiagnostics(DiagnosticSeverity.Error);
            ReportCompilationDiagnostics(DiagnosticSeverity.Warning);
            ReportRudeEdits();

            // report or clear diagnostics in the browser UI
            await ForEachProjectAsync(
                _runningProjects,
                (project, cancellationToken) => project.BrowserRefreshServer?.ReportCompilationErrorsInBrowserAsync([.. diagnosticsToDisplayInApp], cancellationToken).AsTask() ?? Task.CompletedTask,
                cancellationToken);

            void ReportCompilationDiagnostics(DiagnosticSeverity severity)
            {
                foreach (var diagnostic in updates.CompilationDiagnostics)
                {
                    if (diagnostic.Id == "CS8002")
                    {
                        // TODO: This is not a useful warning. Compiler shouldn't be reporting this on .NET/
                        // Referenced assembly '...' does not have a strong name"
                        continue;
                    }

                    // TODO: https://github.com/dotnet/roslyn/pull/79018
                    // shouldn't be included in compilation diagnostics
                    if (diagnostic.Id == "ENC0118")
                    {
                        // warning ENC0118: Changing 'top-level code' might not have any effect until the application is restarted
                        continue;
                    }

                    if (diagnostic.DefaultSeverity != severity)
                    {
                        continue;
                    }

                    ReportDiagnostic(diagnostic, GetMessageDescriptor(diagnostic, verbose: false));
                }
            }

            void ReportRudeEdits()
            {
                // Rude edits in projects that caused restart of a project that can be restarted automatically
                // will be reported only as verbose output.
                var projectsRestartedDueToRudeEdits = updates.ProjectsToRestart
                    .Where(e => IsAutoRestartEnabled(e.Key))
                    .SelectMany(e => e.Value)
                    .ToHashSet();

                // Project with rude edit that doesn't impact running project is only listed in ProjectsToRebuild.
                // Such projects are always auto-rebuilt whether or not there is any project to be restarted that needs a confirmation.
                var projectsRebuiltDueToRudeEdits = updates.ProjectsToRebuild
                    .Where(p => !updates.ProjectsToRestart.ContainsKey(p))
                    .ToHashSet();

                foreach (var (projectId, diagnostics) in updates.RudeEdits)
                {
                    foreach (var diagnostic in diagnostics)
                    {
                        var prefix =
                            projectsRestartedDueToRudeEdits.Contains(projectId) ? "[auto-restart] " :
                            projectsRebuiltDueToRudeEdits.Contains(projectId) ? "[auto-rebuild] " :
                            "";

                        var descriptor = GetMessageDescriptor(diagnostic, verbose: prefix != "");
                        ReportDiagnostic(diagnostic, descriptor, prefix);
                    }
                }
            }

            bool IsAutoRestartEnabled(ProjectId id)
                => runningProjectInfos.TryGetValue(id, out var info) && info.RestartWhenChangesHaveNoEffect;

            void ReportDiagnostic(Diagnostic diagnostic, MessageDescriptor descriptor, string prefix = "")
            {
                var display = CSharpDiagnosticFormatter.Instance.Format(diagnostic);
                _reporter.Report(descriptor, prefix, [display]);

                if (descriptor.TryGetMessage(prefix, [display], out var message))
                {
                    diagnosticsToDisplayInApp.Add(message);
                }
            }

            // Use the default severity of the diagnostic as it conveys impact on Hot Reload
            // (ignore warnings as errors and other severity configuration).
            static MessageDescriptor GetMessageDescriptor(Diagnostic diagnostic, bool verbose)
            {
                if (verbose)
                {
                    return MessageDescriptor.ApplyUpdate_Verbose;
                }

                if (diagnostic.Id == "ENC0118")
                {
                    // Changing '<entry-point>' might not have any effect until the application is restarted.
                    return MessageDescriptor.ApplyUpdate_ChangingEntryPoint;
                }

                return diagnostic.DefaultSeverity switch
                {
                    DiagnosticSeverity.Error => MessageDescriptor.ApplyUpdate_Error,
                    DiagnosticSeverity.Warning => MessageDescriptor.ApplyUpdate_Warning,
                    _ => MessageDescriptor.ApplyUpdate_Verbose,
                };
            }
        }

        public async ValueTask<bool> HandleStaticAssetChangesAsync(IReadOnlyList<ChangedFile> files, ProjectNodeMap projectMap, CancellationToken cancellationToken)
        {
            var allFilesHandled = true;

            var updates = new Dictionary<RunningProject, List<(string filePath, string relativeUrl, ProjectGraphNode containingProject)>>();

            foreach (var changedFile in files)
            {
                var file = changedFile.Item;

                if (file.StaticWebAssetPath is null)
                {
                    allFilesHandled = false;
                    continue;
                }

                foreach (var containingProjectPath in file.ContainingProjectPaths)
                {
                    if (!projectMap.Map.TryGetValue(containingProjectPath, out var containingProjectNodes))
                    {
                        // Shouldn't happen.
                        _reporter.Warn($"Project '{containingProjectPath}' not found in the project graph.");
                        continue;
                    }

                    foreach (var containingProjectNode in containingProjectNodes)
                    {
                        foreach (var referencingProjectNode in new[] { containingProjectNode }.GetTransitivelyReferencingProjects())
                        {
                            if (TryGetRunningProject(referencingProjectNode.ProjectInstance.FullPath, out var runningProjects))
                            {
                                foreach (var runningProject in runningProjects)
                                {
                                    if (!updates.TryGetValue(runningProject, out var updatesPerRunningProject))
                                    {
                                        updates.Add(runningProject, updatesPerRunningProject = []);
                                    }

                                    updatesPerRunningProject.Add((file.FilePath, file.StaticWebAssetPath, containingProjectNode));
                                }
                            }
                        }
                    }
                }
            }

            if (updates.Count == 0)
            {
                return allFilesHandled;
            }

            var tasks = updates.Select(async entry =>
            {
                var (runningProject, assets) = entry;

                if (runningProject.BrowserRefreshServer != null)
                {
                    await runningProject.BrowserRefreshServer.UpdateStaticAssetsAsync(assets.Select(a => a.relativeUrl), cancellationToken);
                }
                else
                {
                    var updates = new List<StaticAssetUpdate>();

                    foreach (var (filePath, relativeUrl, containingProject) in assets)
                    {
                        byte[] content;
                        try
                        {
                            content = await File.ReadAllBytesAsync(filePath, cancellationToken);
                        }
                        catch (Exception e)
                        {
                            _reporter.Error(e.Message);
                            continue;
                        }

                        updates.Add(new StaticAssetUpdate(
                            relativePath: relativeUrl,
                            assemblyName: containingProject.GetAssemblyName(),
                            content: content,
                            isApplicationProject: containingProject == runningProject.ProjectNode));

                        _reporter.Verbose($"Sending static file update request for asset '{relativeUrl}'.");
                    }

                    await runningProject.DeltaApplier.ApplyStaticAssetUpdates([.. updates], cancellationToken);
                }
            });

            await Task.WhenAll(tasks).WaitAsync(cancellationToken);

            _reporter.Output("Hot reload of static files succeeded.", emoji: "🔥");

            return allFilesHandled;
        }

        /// <summary>
        /// Terminates all processes launched for projects with <paramref name="projectPaths"/>,
        /// or all running non-root project processes if <paramref name="projectPaths"/> is null.
        /// 
        /// Removes corresponding entries from <see cref="_runningProjects"/>.
        /// 
        /// Does not terminate the root project.
        /// </summary>
        internal async ValueTask<ImmutableArray<RunningProject>> TerminateNonRootProcessesAsync(
            IEnumerable<string>? projectPaths, CancellationToken cancellationToken)
        {
            ImmutableArray<RunningProject> projectsToRestart = [];

            UpdateRunningProjects(runningProjectsByPath =>
            {
                if (projectPaths == null)
                {
                    projectsToRestart = _runningProjects.SelectMany(entry => entry.Value).Where(p => !p.Options.IsRootProject).ToImmutableArray();
                    return _runningProjects.Clear();
                }

                projectsToRestart = projectPaths.SelectMany(path => _runningProjects.TryGetValue(path, out var array) ? array : []).ToImmutableArray();
                return runningProjectsByPath.RemoveRange(projectPaths);
            });

            // Do not terminate root process at this time - it would signal the cancellation token we are currently using.
            // The process will be restarted later on.
            var projectsToTerminate = projectsToRestart.Where(p => !p.Options.IsRootProject);

            // wait for all processes to exit to release their resources, so we can rebuild:
            _ = await TerminateRunningProjects(projectsToTerminate, cancellationToken);

            return projectsToRestart;
        }

        /// <summary>
        /// Terminates process of the given <paramref name="project"/>.
        /// Removes corresponding entries from <see cref="_runningProjects"/>.
        ///
        /// Should not be called with the root project.
        /// </summary>
        /// <returns>Exit code of the terminated process.</returns>
        internal async ValueTask<int> TerminateNonRootProcessAsync(RunningProject project, CancellationToken cancellationToken)
        {
            Debug.Assert(!project.Options.IsRootProject);

            var projectPath = project.ProjectNode.ProjectInstance.FullPath;

            UpdateRunningProjects(runningProjectsByPath =>
            {
                if (!runningProjectsByPath.TryGetValue(projectPath, out var runningProjects) ||
                    runningProjects.Remove(project) is var updatedRunningProjects && runningProjects == updatedRunningProjects)
                {
                    _reporter.Verbose($"Ignoring an attempt to terminate process {project.ProcessId} of project '{projectPath}' that has no associated running processes.");
                    return runningProjectsByPath;
                }

                return updatedRunningProjects is []
                    ? runningProjectsByPath.Remove(projectPath)
                    : runningProjectsByPath.SetItem(projectPath, updatedRunningProjects);
            });

            // wait for all processes to exit to release their resources:
            return (await TerminateRunningProjects([project], cancellationToken)).Single();
        }

        private void UpdateRunningProjects(Func<ImmutableDictionary<string, ImmutableArray<RunningProject>>, ImmutableDictionary<string, ImmutableArray<RunningProject>>> updater)
        {
            lock (_runningProjectsAndUpdatesGuard)
            {
                _runningProjects = updater(_runningProjects);
            }
        }

        public bool TryGetRunningProject(string projectPath, out ImmutableArray<RunningProject> projects)
        {
            lock (_runningProjectsAndUpdatesGuard)
            {
                return _runningProjects.TryGetValue(projectPath, out projects);
            }
        }

        private async ValueTask<IReadOnlyList<int>> TerminateRunningProjects(IEnumerable<RunningProject> projects, CancellationToken cancellationToken)
        {
            // wait for all tasks to complete:
            return await Task.WhenAll(projects.Select(p => p.TerminateAsync().AsTask())).WaitAsync(cancellationToken);
        }

        private static Task ForEachProjectAsync(ImmutableDictionary<string, ImmutableArray<RunningProject>> projects, Func<RunningProject, CancellationToken, Task> action, CancellationToken cancellationToken)
            => Task.WhenAll(projects.SelectMany(entry => entry.Value).Select(project => action(project, cancellationToken))).WaitAsync(cancellationToken);
    }
}
