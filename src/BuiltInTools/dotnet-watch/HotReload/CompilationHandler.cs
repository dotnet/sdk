// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Build.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.EditAndContinue;
using Microsoft.CodeAnalysis.ExternalAccess.Watch.Api;

namespace Microsoft.DotNet.Watch
{
    internal sealed class CompilationHandler : IDisposable
    {
        public readonly IncrementalMSBuildWorkspace Workspace;

        private readonly IReporter _reporter;
        private readonly WatchHotReloadService _hotReloadService;

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

        /// <summary>
        /// Set of capabilities aggregated across the current set of <see cref="_runningProjects"/>.
        /// Default if not calculated yet.
        /// </summary>
        private ImmutableArray<string> _currentAggregateCapabilities;

        private bool _isDisposed;

        public CompilationHandler(IReporter reporter)
        {
            _reporter = reporter;
            Workspace = new IncrementalMSBuildWorkspace(reporter);
            _hotReloadService = new WatchHotReloadService(Workspace.CurrentSolution.Services, GetAggregateCapabilitiesAsync);
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

        public void DiscardProjectBaselines(ImmutableDictionary<ProjectId, string> projectsToBeRebuilt, CancellationToken cancellationToken)
        {
            // Remove previous updates to all modules that were affected by rude edits.
            // All running projects that statically reference these modules have been terminated.
            // If we missed any project that dynamically references one of these modules its rebuild will fail.
            // At this point there is thus no process that these modules loaded and any process created in future
            // that will load their rebuilt versions.

            lock (_runningProjectsAndUpdatesGuard)
            {
                _previousUpdates = _previousUpdates.RemoveAll(update => projectsToBeRebuilt.ContainsKey(update.ProjectId));
            }

            _hotReloadService.UpdateBaselines(Workspace.CurrentSolution, projectsToBeRebuilt.Keys.ToImmutableArray());
        }

        public void UpdateProjectBaselines(ImmutableDictionary<ProjectId, string> projectsToBeRebuilt, CancellationToken cancellationToken)
        {
            _hotReloadService.UpdateBaselines(Workspace.CurrentSolution, projectsToBeRebuilt.Keys.ToImmutableArray());
            _reporter.Report(MessageDescriptor.ProjectBaselinesUpdated);
        }

        public async ValueTask StartSessionAsync(CancellationToken cancellationToken)
        {
            _reporter.Report(MessageDescriptor.HotReloadSessionStarting);

            await _hotReloadService.StartSessionAsync(Workspace.CurrentSolution, cancellationToken);

            _reporter.Report(MessageDescriptor.HotReloadSessionStarted);
        }

        private static DeltaApplier CreateDeltaApplier(HotReloadProfile profile, Version? targetFramework, BrowserRefreshServer? browserRefreshServer, IReporter processReporter)
            => profile switch
            {
                HotReloadProfile.BlazorWebAssembly => new BlazorWebAssemblyDeltaApplier(processReporter, browserRefreshServer!, targetFramework),
                HotReloadProfile.BlazorHosted => new BlazorWebAssemblyHostedDeltaApplier(processReporter, browserRefreshServer!, targetFramework),
                _ => new DefaultDeltaApplier(processReporter),
            };

        public async Task<RunningProject?> TrackRunningProjectAsync(
            ProjectGraphNode projectNode,
            ProjectOptions projectOptions,
            HotReloadProfile profile,
            string namedPipeName,
            BrowserRefreshServer? browserRefreshServer,
            ProcessSpec processSpec,
            RestartOperation restartOperation,
            IReporter processReporter,
            CancellationTokenSource processTerminationSource,
            CancellationToken cancellationToken)
        {
            var projectPath = projectNode.ProjectInstance.FullPath;

            var targetFramework = projectNode.GetTargetFrameworkVersion();
            var deltaApplier = CreateDeltaApplier(profile, targetFramework, browserRefreshServer, processReporter);
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
            var runningProcess = ProcessRunner.RunAsync(processSpec, processReporter, isUserApplication: true, launchResult, processTerminationSource.Token);
            if (launchResult.ProcessId == null)
            {
                // error already reported
                return null;
            }

            var capabilityProvider = deltaApplier.GetApplyUpdateCapabilitiesAsync(processCommunicationCancellationSource.Token);
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
                capabilityProvider);

            // ownership transferred to running project:
            disposables.Items.Clear();
            disposables.Items.Add(runningProject);

            ImmutableArray<string> observedCapabilities = default;

            var appliedUpdateCount = 0;
            while (true)
            {
                // Observe updates that need to be applied to the new process
                // and apply them before adding it to running processes.
                // Do bot block on udpates being made to other processes to avoid delaying the new process being up-to-date.
                var updatesToApply = _previousUpdates.Skip(appliedUpdateCount).ToImmutableArray();
                if (updatesToApply.Any())
                {
                    _ = await deltaApplier.Apply(updatesToApply, processCommunicationCancellationSource.Token);
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

                    // reset capabilities:
                    observedCapabilities = _currentAggregateCapabilities;
                    _currentAggregateCapabilities = default;

                    // ownership transferred to _runningProjects
                    disposables.Items.Clear();
                    break;
                }
            }

            // If non-empty solution is loaded into the workspace (a Hot Reload session is active):
            if (Workspace.CurrentSolution is { ProjectIds: not [] } currentSolution)
            {
                // If capabilities have been observed by an edit session, restart the session. Next time EnC service needs
                // capabilities it calls GetAggregateCapabilitiesAsync which uses the set of projects assigned above to calculate them.
                if (!observedCapabilities.IsDefault)
                {
                    _hotReloadService.CapabilitiesChanged();
                }

                // Preparing the compilation is a perf optimization. We can skip it if the session hasn't been started yet. 
                PrepareCompilations(currentSolution, projectPath, cancellationToken);
            }

            return runningProject;
        }

        private async ValueTask<ImmutableArray<string>> GetAggregateCapabilitiesAsync()
        {
            var capabilities = _currentAggregateCapabilities;
            if (!capabilities.IsDefault)
            {
                return capabilities;
            }

            while (true)
            {
                var runningProjects = _runningProjects;
                var capabilitiesByProvider = await Task.WhenAll(runningProjects.SelectMany(p => p.Value).Select(p => p.CapabilityProvider));
                capabilities = capabilitiesByProvider.SelectMany(c => c).Distinct(StringComparer.Ordinal).ToImmutableArray();

                lock (_runningProjectsAndUpdatesGuard)
                {
                    if (runningProjects != _runningProjects)
                    {
                        // Another process has been launched while we were retrieving capabilities, query the providers again.
                        // The providers cache the result so we won't be calling into the respective processes again.
                        continue;
                    }

                    _currentAggregateCapabilities = capabilities;
                    break;
                }
            }

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

        public async ValueTask<(ImmutableDictionary<ProjectId, string> projectsToRebuild, ImmutableArray<RunningProject> terminatedProjects)> HandleFileChangesAsync(
            Func<IEnumerable<string>, CancellationToken, Task> restartPrompt,
            CancellationToken cancellationToken)
        {
            var currentSolution = Workspace.CurrentSolution;
            var runningProjects = _runningProjects;

            var runningProjectIds = currentSolution.Projects
                .Where(project => project.FilePath != null && runningProjects.ContainsKey(project.FilePath))
                .Select(project => project.Id)
                .ToImmutableHashSet();

            var updates = await _hotReloadService.GetUpdatesAsync(currentSolution, runningProjectIds, cancellationToken);
            var anyProcessNeedsRestart = !updates.ProjectIdsToRestart.IsEmpty;

            await DisplayResultsAsync(updates, cancellationToken);

            if (updates.Status is ModuleUpdateStatus.None or ModuleUpdateStatus.Blocked)
            {
                // If Hot Reload is blocked (due to compilation error) we ignore the current
                // changes and await the next file change.
                return (ImmutableDictionary<ProjectId, string>.Empty, []);
            }

            if (updates.Status == ModuleUpdateStatus.RestartRequired)
            {
                if (!anyProcessNeedsRestart)
                {
                    return (ImmutableDictionary<ProjectId, string>.Empty, []);
                }

                await restartPrompt.Invoke(updates.ProjectIdsToRestart.Select(id => currentSolution.GetProject(id)!.Name), cancellationToken);

                // Terminate all tracked processes that need to be restarted,
                // except for the root process, which will terminate later on.
                var terminatedProjects = await TerminateNonRootProcessesAsync(updates.ProjectIdsToRestart.Select(id => currentSolution.GetProject(id)!.FilePath!), cancellationToken);

                return (updates.ProjectIdsToRebuild.ToImmutableDictionary(keySelector: id => id, elementSelector: id => currentSolution.GetProject(id)!.FilePath!), terminatedProjects);
            }

            Debug.Assert(updates.Status == ModuleUpdateStatus.Ready);

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
                    var applySucceded = await runningProject.DeltaApplier.Apply(updates.ProjectUpdates, processCommunicationCancellationSource.Token) != ApplyStatus.Failed;
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

            return (ImmutableDictionary<ProjectId, string>.Empty, []);
        }

        private async ValueTask DisplayResultsAsync(WatchHotReloadService.Updates updates, CancellationToken cancellationToken)
        {
            var anyProcessNeedsRestart = !updates.ProjectIdsToRestart.IsEmpty;

            switch (updates.Status)
            {
                case ModuleUpdateStatus.None:
                    _reporter.Output("No C# changes to apply.");
                    break;

                case ModuleUpdateStatus.Ready:
                    break;

                case ModuleUpdateStatus.RestartRequired:
                    if (anyProcessNeedsRestart)
                    {
                        _reporter.Output("Unable to apply hot reload, restart is needed to apply the changes.");
                    }
                    else
                    {
                        _reporter.Verbose("Rude edits detected but do not affect any running process");
                    }

                    break;

                case ModuleUpdateStatus.Blocked:
                    _reporter.Output("Unable to apply hot reload due to compilation errors.");
                    break;

                default:
                    throw new InvalidOperationException();
            }

            // Diagnostics include syntactic errors, semantic warnings/errors and rude edit warnigns/errors for members being updated.

            var diagnosticsToDisplay = new List<string>();

            // Display errors first, then warnings:
            Display(MessageSeverity.Error);
            Display(MessageSeverity.Warning);

            void Display(MessageSeverity severity)
            {
                foreach (var diagnostic in updates.Diagnostics)
                {
                    MessageDescriptor descriptor;

                    if (diagnostic.Id == "ENC0118")
                    {
                        // Changing '<entry-point>' might not have any effect until the application is restarted.
                        descriptor = MessageDescriptor.ApplyUpdate_ChangingEntryPoint;
                    }
                    else if (diagnostic.Id == "ENC1005")
                    {
                        // TODO: This warning is overreported in cases when the solution contains projects that are not rebuilt (up-to-date)
                        // and a document is updated that is linked to such a project and another "active" project.
                        // E.g. multi-tfm projects where only one TFM is currently built/running.

                        // Warning: The current content of source file 'D:\Temp\App\Program.cs' does not match the built source.
                        // Any changes made to this file while debugging won't be applied until its content matches the built source.
                        descriptor = MessageDescriptor.ApplyUpdate_FileContentDoesNotMatchBuiltSource;
                    }
                    else if (diagnostic.Id == "CS8002")
                    {
                        // TODO: This is not a useful warning. Compiler shouldn't be reporting this on .NET/
                        // Referenced assembly '...' does not have a strong name"
                        continue;
                    }
                    else
                    {
                        // Use the default severity of the diagnostic as it conveys impact on Hot Reload
                        // (ignore warnings as errors and other severity configuration).
                        descriptor = diagnostic.DefaultSeverity switch
                        {
                            DiagnosticSeverity.Error => MessageDescriptor.ApplyUpdate_Error,
                            DiagnosticSeverity.Warning => MessageDescriptor.ApplyUpdate_Warning,
                            _ => MessageDescriptor.ApplyUpdate_Verbose,
                        };
                    }

                    if (descriptor.Severity != severity)
                    {
                        continue;
                    }

                    // Do not report rude edits as errors/warnings if no running process is affected.
                    if (!anyProcessNeedsRestart && diagnostic.Id is ['E', 'N', 'C', >= '0' and <= '9', ..])
                    {
                        descriptor = descriptor with { Severity = MessageSeverity.Verbose };
                    }

                    var display = CSharpDiagnosticFormatter.Instance.Format(diagnostic);
                    _reporter.Report(descriptor, display);

                    if (descriptor.TryGetMessage(prefix: null, [display], out var message))
                    {
                        diagnosticsToDisplay.Add(message);
                    }
                }
            }

            // report or clear diagnostics in the browser UI
            await ForEachProjectAsync(
                _runningProjects,
                (project, cancellationToken) => project.BrowserRefreshServer?.ReportCompilationErrorsInBrowserAsync(diagnosticsToDisplay.ToImmutableArray(), cancellationToken).AsTask() ?? Task.CompletedTask,
                cancellationToken);
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

                // reset capabilities:
                _currentAggregateCapabilities = default;
            }
        }

        private static async ValueTask<IReadOnlyList<int>> TerminateRunningProjects(IEnumerable<RunningProject> projects, CancellationToken cancellationToken)
        {
            // cancel first, this will cause the process tasks to complete:
            foreach (var project in projects)
            {
                project.ProcessTerminationSource.Cancel();
            }

            // wait for all tasks to complete:
            return await Task.WhenAll(projects.Select(p => p.RunningProcess)).WaitAsync(cancellationToken);
        }

        private static Task ForEachProjectAsync(ImmutableDictionary<string, ImmutableArray<RunningProject>> projects, Func<RunningProject, CancellationToken, Task> action, CancellationToken cancellationToken)
            => Task.WhenAll(projects.SelectMany(entry => entry.Value).Select(project => action(project, cancellationToken))).WaitAsync(cancellationToken);
    }
}
