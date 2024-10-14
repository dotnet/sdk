// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Build.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.EditAndContinue;
using Microsoft.CodeAnalysis.ExternalAccess.Watch.Api;
using Microsoft.CodeAnalysis.Text;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed class CompilationHandler : IAsyncDisposable
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

        public async ValueTask DisposeAsync()
        {
            _isDisposed = true;

            Workspace?.Dispose();

            IEnumerable<RunningProject> projects;
            lock (_runningProjectsAndUpdatesGuard)
            {
                projects = _runningProjects.SelectMany(entry => entry.Value).Where(p => !p.Options.IsRootProject);
                _runningProjects = _runningProjects.Clear();
            }

            await TerminateAndDisposeRunningProjects(projects);
        }

        private static async ValueTask TerminateAndDisposeRunningProjects(IEnumerable<RunningProject> projects)
        {
            // cancel first, this will cause the process tasks to complete:
            foreach (var project in projects)
            {
                project.ProcessTerminationSource.Cancel();
            }

            // wait for all tasks to complete:
            await Task.WhenAll(projects.Select(p => p.RunningProcess)).WaitAsync(CancellationToken.None);

            // dispose only after all tasks have completed to prevent the tasks from accessing disposed resources:
            foreach (var project in projects)
            {
                project.Dispose();
            }
        }

        public ValueTask RestartSessionAsync(IReadOnlySet<ProjectId> projectsToBeRebuilt, CancellationToken cancellationToken)
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

            _hotReloadService.EndSession();
            _reporter.Report(MessageDescriptor.HotReloadSessionEnded);
            return StartSessionAsync(cancellationToken);
        }

        public async ValueTask StartSessionAsync(CancellationToken cancellationToken)
        {
            _reporter.Report(MessageDescriptor.HotReloadSessionStarting);

            await _hotReloadService.StartSessionAsync(Workspace.CurrentSolution, cancellationToken);

            _reporter.Report(MessageDescriptor.HotReloadSessionStarted);
        }

        private DeltaApplier CreateDeltaApplier(ProjectGraphNode projectNode, BrowserRefreshServer? browserRefreshServer, IReporter processReporter)
            => HotReloadProfileReader.InferHotReloadProfile(projectNode, _reporter) switch
            {
                HotReloadProfile.BlazorWebAssembly => new BlazorWebAssemblyDeltaApplier(processReporter, browserRefreshServer!, projectNode.GetTargetFrameworkVersion()),
                HotReloadProfile.BlazorHosted => new BlazorWebAssemblyHostedDeltaApplier(processReporter, browserRefreshServer!, projectNode.GetTargetFrameworkVersion()),
                _ => new DefaultDeltaApplier(processReporter),
            };

        public async Task<RunningProject> TrackRunningProjectAsync(
            ProjectGraphNode projectNode,
            ProjectOptions projectOptions,
            string namedPipeName,
            BrowserRefreshServer? browserRefreshServer,
            ProcessSpec processSpec,
            IReporter processReporter,
            CancellationTokenSource processTerminationSource,
            CancellationToken cancellationToken)
        {
            var projectPath = projectNode.ProjectInstance.FullPath;

            var deltaApplier = CreateDeltaApplier(projectNode, browserRefreshServer, processReporter);
            var processExitedSource = new CancellationTokenSource();
            var processCommunicationCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(processExitedSource.Token, cancellationToken);

            // Dispose these objects on failure:
            using var disposables = new Disposables([deltaApplier, processExitedSource, processCommunicationCancellationSource]);

            // It is important to first create the named pipe connection (delta applier is the server)
            // and then start the process (named pipe client). Otherwise, the connection would fail.
            deltaApplier.CreateConnection(namedPipeName, processCommunicationCancellationSource.Token);
            var runningProcess = ProcessRunner.RunAsync(processSpec, processReporter, isUserApplication: true, processExitedSource, processTerminationSource.Token);

            var capabilityProvider = deltaApplier.GetApplyUpdateCapabilitiesAsync(processCommunicationCancellationSource.Token);
            var runningProject = new RunningProject(
                projectNode,
                projectOptions,
                deltaApplier,
                processReporter,
                browserRefreshServer,
                runningProcess,
                processExitedSource: processExitedSource,
                processTerminationSource: processTerminationSource,
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

        public async ValueTask<(IReadOnlySet<ProjectId> projectsToBeRebuilt, IEnumerable<RunningProject> terminatedProjects)> HandleFileChangesAsync(
            Func<IEnumerable<Project>, CancellationToken, Task> restartPrompt,
            CancellationToken cancellationToken)
        {
            var currentSolution = Workspace.CurrentSolution;
            var runningProjects = _runningProjects;

            var updates = await _hotReloadService.GetUpdatesAsync(currentSolution, isRunningProject: p => runningProjects.ContainsKey(p.FilePath!), cancellationToken);
            await DisplayResultsAsync(updates, cancellationToken);

            if (updates.Status is ModuleUpdateStatus.None or ModuleUpdateStatus.Blocked)
            {
                // If Hot Reload is blocked (due to compilation error) we ignore the current
                // changes and await the next file change.
                return (ImmutableHashSet<ProjectId>.Empty, []);
            }

            if (updates.Status == ModuleUpdateStatus.RestartRequired)
            {
                Debug.Assert(updates.ProjectsToRestart.Count > 0);

                await restartPrompt.Invoke(updates.ProjectsToRestart, cancellationToken);

                // Terminate all tracked processes that need to be restarted,
                // except for the root process, which will terminate later on.
                var terminatedProjects = await TerminateNonRootProcessesAsync(updates.ProjectsToRestart.Select(p => p.FilePath!), cancellationToken);

                return (updates.ProjectsToRebuild.Select(p => p.Id).ToHashSet(), terminatedProjects);
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

            return (ImmutableHashSet<ProjectId>.Empty, []);
        }

        private async ValueTask DisplayResultsAsync(WatchHotReloadService.Updates updates, CancellationToken cancellationToken)
        {
            switch (updates.Status)
            {
                case ModuleUpdateStatus.None:
                    _reporter.Output("No hot reload changes to apply.");
                    break;

                case ModuleUpdateStatus.Ready:
                    break;

                case ModuleUpdateStatus.RestartRequired:
                    _reporter.Output("Unable to apply hot reload, restart is needed to apply the changes.");
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
        /// Terminates all processes launched for projects with <paramref name="projectPaths"/>.
        /// Removes corresponding entries from <see cref="_runningProjects"/>.
        /// 
        /// May terminate the root project process as well.
        /// </summary>
        internal async ValueTask<IEnumerable<RunningProject>> TerminateNonRootProcessesAsync(IEnumerable<string> projectPaths, CancellationToken cancellationToken)
        {
            IEnumerable<RunningProject> projectsToRestart;
            lock (_runningProjectsAndUpdatesGuard)
            {
                // capture snapshot of running processes that can be enumerated outside of the lock:
                var runningProjects = _runningProjects;
                projectsToRestart = projectPaths.SelectMany(path => runningProjects[path]);

                _runningProjects = runningProjects.RemoveRange(projectPaths);

                // reset capabilities:
                _currentAggregateCapabilities = default;
            }

            // Do not terminate root process at this time - it would signal the cancellation token we are currently using.
            // The process will be restarted later on.
            var projectsToTerminate = projectsToRestart.Where(p => !p.Options.IsRootProject);

            // wait for all processes to exit to release their resources, so we can rebuild:
            await TerminateAndDisposeRunningProjects(projectsToTerminate);

            return projectsToRestart;
        }

        private static Task ForEachProjectAsync(ImmutableDictionary<string, ImmutableArray<RunningProject>> projects, Func<RunningProject, CancellationToken, Task> action, CancellationToken cancellationToken)
            => Task.WhenAll(projects.SelectMany(entry => entry.Value).Select(project => action(project, cancellationToken))).WaitAsync(cancellationToken);
    }
}
