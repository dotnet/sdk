// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.ExternalAccess.HotReload.Api;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal sealed class RunningProjectsManager(ProcessRunner processRunner, ILogger logger)
{
    /// <summary>
    /// Lock to synchronize:
    /// <see cref="_runningProjects"/>
    /// <see cref="_activeProjectRelaunchOperations"/>
    /// <see cref="_previousUpdates"/>
    /// </summary>
    private readonly object _runningProjectsAndUpdatesGuard = new();

    /// <summary>
    /// Projects that have been launched and to which we apply changes.
    /// Maps <see cref="ProjectInstance.FullPath"/> to the list of running instances of that project.
    /// </summary>
    private ImmutableDictionary<string, ImmutableArray<RunningProject>> _runningProjects
        = ImmutableDictionary<string, ImmutableArray<RunningProject>>.Empty.WithComparers(PathUtilities.OSSpecificPathComparer);

    /// <summary>
    /// Maps <see cref="ProjectInstance.FullPath"/> to the list of active restart operations for the project.
    /// The <see cref="RestartOperation"/> of the project instance is added whenever a process crashes (terminated with non-zero exit code)
    /// and the corresponding <see cref="RunningProject"/> is removed from <see cref="_runningProjects"/>.
    ///
    /// When a file change is observed whose containing project is listed here, the associated relaunch operations are executed.
    /// </summary>
    private ImmutableDictionary<string, ImmutableArray<RestartOperation>> _activeProjectRelaunchOperations
        = ImmutableDictionary<string, ImmutableArray<RestartOperation>>.Empty.WithComparers(PathUtilities.OSSpecificPathComparer);

    /// <summary>
    /// All updates that were attempted. Includes updates whose application failed.
    /// </summary>
    private ImmutableList<HotReloadService.Update> _previousUpdates = [];

    public ImmutableDictionary<string, ImmutableArray<RunningProject>> CurrentRunningProjects
        => _runningProjects;

    public async ValueTask TerminatePeripheralProcesses(CancellationToken cancellationToken)
    {
        logger.LogDebug("Terminating remaining child processes.");

        await TerminatePeripheralProcessesAsync([.. _runningProjects.SelectMany(entry => entry.Value)], cancellationToken);
    }

    public async Task<RunningProject?> TrackRunningProjectAsync(
        ProjectGraphNode projectNode,
        ProjectOptions projectOptions,
        HotReloadClients clients,
        ILogger clientLogger,
        ProcessSpec processSpec,
        RestartOperation restartOperation,
        CancellationToken cancellationToken)
    {
        var processExitedSource = new CancellationTokenSource();
        var processTerminationSource = new CancellationTokenSource();

        // Cancel process communication as soon as process termination is requested, shutdown is requested, or the process exits (whichever comes first).
        // If we only cancel after we process exit event handler is triggered the pipe might have already been closed and may fail unexpectedly.
        using var processCommunicationCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(processTerminationSource.Token, processExitedSource.Token, cancellationToken);
        var processCommunicationCancellationToken = processCommunicationCancellationSource.Token;

        // Dispose these objects on failure:
        await using var disposables = new Disposables([clients, processExitedSource, processTerminationSource]);

        // It is important to first create the named pipe connection (Hot Reload client is the named pipe server)
        // and then start the process (named pipe client). Otherwise, the connection would fail.
        clients.InitiateConnection(processCommunicationCancellationToken);

        RunningProject? publishedRunningProject = null;

        var previousOnExit = processSpec.OnExit;
        processSpec.OnExit = async (processId, exitCode) =>
        {
            // Await the previous action so that we only clean up after all requested "on exit" actions have been completed.
            if (previousOnExit != null)
            {
                await previousOnExit(processId, exitCode);
            }

            if (publishedRunningProject != null)
            {
                var relaunch =
                    !cancellationToken.IsCancellationRequested &&
                    !publishedRunningProject.Options.IsMainProject &&
                    exitCode.HasValue &&
                    exitCode.Value != 0;

                // Remove the running project if it has been published to _runningProjects (if it hasn't exited during initialization):
                if (RemoveRunningProject(publishedRunningProject, relaunch))
                {
                    await publishedRunningProject.DisposeAsync(isExiting: true);
                }
            }
        };

        var launchResult = new ProcessLaunchResult();
        var processTask = processRunner.RunAsync(processSpec, clientLogger, launchResult, processTerminationSource.Token);
        if (launchResult.ProcessId == null)
        {
            // process failed to start:
            Debug.Assert(processTask.IsCompleted && processTask.Result == int.MinValue);

            // error already reported
            return null;
        }

        var runningProcess = new RunningProcess(launchResult.ProcessId.Value, processTask, processExitedSource, processTerminationSource);

        // transfer ownership to the running process:
        disposables.Items.Remove(processExitedSource);
        disposables.Items.Remove(processTerminationSource);
        disposables.Items.Add(runningProcess);

        var projectPath = projectNode.ProjectInstance.FullPath;

        try
        {
            // Wait for agent to create the name pipe and send capabilities over.
            // the agent blocks the app execution until initial updates are applied (if any).
            var managedCodeUpdateCapabilities = await clients.GetUpdateCapabilitiesAsync(processCommunicationCancellationToken);

            var runningProject = new RunningProject(
                projectNode,
                projectOptions,
                clients,
                clientLogger,
                runningProcess,
                restartOperation,
                managedCodeUpdateCapabilities);

            // transfer ownership to the running project:
            disposables.Items.Remove(clients);
            disposables.Items.Remove(runningProcess);
            disposables.Items.Add(runningProject);

            var appliedUpdateCount = 0;
            while (true)
            {
                // Observe updates that need to be applied to the new process
                // and apply them before adding it to running processes.
                // Do not block on udpates being made to other processes to avoid delaying the new process being up-to-date.
                var updatesToApply = _previousUpdates.Skip(appliedUpdateCount).ToImmutableArray();
                if (updatesToApply.Any() && clients.IsManagedAgentSupported)
                {
                    await await clients.ApplyManagedCodeUpdatesAsync(
                        ToManagedCodeUpdates(updatesToApply),
                        applyOperationCancellationToken: processExitedSource.Token,
                        cancellationToken: processCommunicationCancellationToken);
                }

                appliedUpdateCount += updatesToApply.Length;

                lock (_runningProjectsAndUpdatesGuard)
                {
                    // More updates might have come in while we have been applying updates.
                    // If so, continue updating.
                    if (_previousUpdates.Count > appliedUpdateCount)
                    {
                        continue;
                    }

                    // Only add the running process after it has been up-to-date.
                    // This will prevent new updates being applied before we have applied all the previous updates.
                    _runningProjects = _runningProjects.Add(projectPath, runningProject);

                    // transfer ownership to _runningProjects
                    publishedRunningProject = runningProject;
                    disposables.Items.Remove(runningProject);
                    Debug.Assert(disposables.Items is []);
                    break;
                }
            }

            if (clients.IsManagedAgentSupported)
            {
                clients.OnRuntimeRudeEdit += (code, message) =>
                {
                    // fire and forget:
                    _ = HandleRuntimeRudeEditAsync(publishedRunningProject, message, cancellationToken);
                };

                // Notifies the agent that it can unblock the execution of the process:
                await clients.InitialUpdatesAppliedAsync(processCommunicationCancellationToken);
            }

            return publishedRunningProject;
        }
        catch (OperationCanceledException) when (processExitedSource.IsCancellationRequested)
        {
            // Process exited during initialization. This should not happen since we control the process during this time.
            logger.LogError("Failed to launch '{ProjectPath}'. Process {PID} exited during initialization.", projectPath, launchResult.ProcessId);
            return null;
        }
    }

    private async Task HandleRuntimeRudeEditAsync(RunningProject runningProject, string rudeEditMessage, CancellationToken cancellationToken)
    {
        var logger = runningProject.ClientLogger;

        try
        {
            // Always auto-restart on runtime rude edits regardless of the settings.
            // Since there is no debugger attached the process would crash on an unhandled HotReloadException if
            // we let it continue executing.
            logger.LogWarning(rudeEditMessage);
            logger.Log(MessageDescriptor.RestartingApplication);

            if (!runningProject.InitiateRestart())
            {
                // Already in the process of restarting, possibly because of another runtime rude edit.
                return;
            }

            await runningProject.Clients.ReportCompilationErrorsInApplicationAsync([rudeEditMessage, MessageDescriptor.RestartingApplication.GetMessage()], cancellationToken);

            // Terminate the process.
            await runningProject.Process.TerminateAsync();

            // Creates a new running project and launches it:
            await runningProject.RestartAsync(cancellationToken);
        }
        catch (Exception e)
        {
            if (e is not OperationCanceledException)
            {
                logger.LogError("Failed to handle runtime rude edit: {Exception}", e.ToString());
            }
        }
    }

    public async ValueTask ApplyManagedCodeAndStaticAssetUpdatesAndRelaunchAsync(
        ProjectUpdatesBuilder builder,
        ImmutableArray<ChangedFile> changedFiles,
        LoadedProjectGraph projectGraph,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var applyTasks = new List<Task>();
        ImmutableDictionary<string, ImmutableArray<RunningProject>> projectsToUpdate = [];

        IReadOnlyList<RestartOperation> relaunchOperations;
        lock (_runningProjectsAndUpdatesGuard)
        {
            // Remove previous updates to all modules that were affected by rude edits.
            // All running projects that statically reference these modules have been terminated.
            // If we missed any project that dynamically references one of these modules its rebuild will fail.
            // At this point there is thus no process that these modules loaded and any process created in future
            // that will load their rebuilt versions.
            _previousUpdates = _previousUpdates.RemoveAll(update => builder.PreviousProjectUpdatesToDiscard.Contains(update.ProjectId));

            // Adding the updates makes sure that all new processes receive them before they are added to running processes.
            _previousUpdates = _previousUpdates.AddRange(builder.ManagedCodeUpdates);

            // Capture the set of processes that do not have the currently calculated deltas yet.
            projectsToUpdate = _runningProjects;

            // Determine relaunch operations at the same time as we capture running processes,
            // so that these sets are consistent even if another process crashes while doing so.
            relaunchOperations = GetRelaunchOperations_NoLock(changedFiles, projectGraph);
        }

        // Relaunch projects after _previousUpdates were updated above.
        // Ensures that the current and previous updates will be applied as initial updates to the newly launched processes.
        // We also capture _runningProjects above, before launching new ones, so that the current updates are not applied twice to the relaunched processes.
        // Static asset changes do not need to be updated in the newly launched processes since the application will read their updated content once it launches.
        // Fire and forget.
        foreach (var relaunchOperation in relaunchOperations)
        {
            // fire and forget:
            _ = Task.Run(async () =>
            {
                try
                {
                    await relaunchOperation.Invoke(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // nop
                }
                catch (Exception e)
                {
                    // Handle all exceptions since this is a fire-and-forget task.
                    logger.LogError("Failed to relaunch: {Exception}", e.ToString());
                }
            }, cancellationToken);
        }

        if (builder.ManagedCodeUpdates is not [])
        {
            // Apply changes to all running projects, even if they do not have a static project dependency on any project that changed.
            // The process may load any of the binaries using MEF or some other runtime dependency loader.

            foreach (var (_, projects) in projectsToUpdate)
            {
                foreach (var runningProject in projects)
                {
                    Debug.Assert(runningProject.Clients.IsManagedAgentSupported);

                    // Only cancel applying updates when the process exits. Canceling disables further updates since the state of the runtime becomes unknown.
                    var applyTask = await runningProject.Clients.ApplyManagedCodeUpdatesAsync(
                        ToManagedCodeUpdates(builder.ManagedCodeUpdates),
                        applyOperationCancellationToken: runningProject.Process.ExitedCancellationToken,
                        cancellationToken);

                    applyTasks.Add(runningProject.CompleteApplyOperationAsync(applyTask));
                }
            }
        }

        // Creating apply tasks involves reading static assets from disk. Parallelize this IO.
        var staticAssetApplyTaskProducers = new List<Task<Task>>();

        foreach (var (runningProject, assets) in builder.StaticAssetUpdates)
        {
            // Only cancel applying updates when the process exits. Canceling in-progress static asset update might be ok,
            // but for consistency with managed code updates we only cancel when the process exits.
            staticAssetApplyTaskProducers.Add(runningProject.Clients.ApplyStaticAssetUpdatesAsync(
                assets,
                applyOperationCancellationToken: runningProject.Process.ExitedCancellationToken,
                cancellationToken));
        }

        applyTasks.AddRange(await Task.WhenAll(staticAssetApplyTaskProducers));

        // fire and forget:
        _ = CompleteApplyOperationAsync();

        async Task CompleteApplyOperationAsync()
        {
            try
            {
                await Task.WhenAll(applyTasks);

                var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;

                if (builder.ManagedCodeUpdates.Count > 0)
                {
                    logger.Log(MessageDescriptor.ManagedCodeChangesApplied, elapsedMilliseconds);
                }

                if (builder.StaticAssetUpdates.Count > 0)
                {
                    logger.Log(MessageDescriptor.StaticAssetsChangesApplied, elapsedMilliseconds);
                }

                logger.Log(MessageDescriptor.ChangesAppliedToProjectsNotification,
                    projectsToUpdate.Select(e => e.Value.First().Options.Representation).Concat(
                        builder.StaticAssetUpdates.Select(e => e.Key.Options.Representation)));
            }
            catch (OperationCanceledException)
            {
                // nop
            }
            catch (Exception e)
            {
                // Handle all exceptions since this is a fire-and-forget task.
                logger.LogError("Failed to apply managedCodeUpdates: {Exception}", e.ToString());
            }
        }
    }

    /// <summary>
    /// Terminates all processes launched for peripheral projects with <paramref name="projectPaths"/>,
    /// or all running peripheral project processes if <paramref name="projectPaths"/> is null.
    /// 
    /// Removes corresponding entries from <see cref="_runningProjects"/>.
    /// 
    /// Does not terminate the main project.
    /// </summary>
    /// <returns>All processes (including main) to be restarted.</returns>
    internal async ValueTask TerminatePeripheralProcessesAsync(
        IEnumerable<RunningProject> projectsToRestart, CancellationToken cancellationToken)
    {
        // Do not terminate root process at this time - it would signal the cancellation token we are currently using.
        // The process will be restarted later on.
        // Wait for all processes to exit to release their resources, so we can rebuild.
        await Task.WhenAll(projectsToRestart.Where(p => !p.Options.IsMainProject).Select(p => p.TerminateForRestartAsync())).WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Restarts given projects after their process have been terminated via <see cref="TerminatePeripheralProcessesAsync"/>.
    /// </summary>
    internal async Task RestartPeripheralProjectsAsync(IReadOnlyList<RunningProject> projectsToRestart, CancellationToken cancellationToken)
    {
        if (projectsToRestart.Any(p => p.Options.IsMainProject))
        {
            throw new InvalidOperationException("Main project can't be restarted.");
        }

        logger.Log(MessageDescriptor.RestartingProjectsNotification, projectsToRestart.Select(p => p.Options.Representation));

        await Task.WhenAll(
            projectsToRestart.Select(async runningProject => runningProject.RestartAsync(cancellationToken)))
            .WaitAsync(cancellationToken);

        logger.Log(MessageDescriptor.ProjectsRestarted, projectsToRestart.Count);
    }

    internal IEnumerable<RunningProject> GetRunningProjects(IEnumerable<ProjectRepresentation> projects)
    {
        var runningProjects = _runningProjects;
        return projects.SelectMany(project => runningProjects.TryGetValue(project.ProjectGraphPath, out var array) ? array : []);
    }

    private bool RemoveRunningProject(RunningProject project, bool relaunch)
    {
        var projectPath = project.ProjectNode.ProjectInstance.FullPath;

        lock (_runningProjectsAndUpdatesGuard)
        {
            var newRunningProjects = _runningProjects.Remove(projectPath, project);
            if (newRunningProjects == _runningProjects)
            {
                return false;
            }

            if (relaunch)
            {
                // Create re-launch operation for each instance that crashed
                // even if other instances of the project are still running.
                _activeProjectRelaunchOperations = _activeProjectRelaunchOperations.Add(projectPath, project.GetRelaunchOperation());
            }

            _runningProjects = newRunningProjects;
        }

        if (relaunch)
        {
            project.ClientLogger.Log(MessageDescriptor.ProcessCrashedAndWillBeRelaunched);
        }

        return true;
    }

    private IReadOnlyList<RestartOperation> GetRelaunchOperations_NoLock(IReadOnlyList<ChangedFile> changedFiles, LoadedProjectGraph projectGraph)
    {
        if (_activeProjectRelaunchOperations.IsEmpty)
        {
            return [];
        }

        var relaunchOperations = new List<RestartOperation>();
        foreach (var changedFile in changedFiles)
        {
            foreach (var containingProjectPath in changedFile.Item.ContainingProjectPaths)
            {
                var containingProjectNodes = projectGraph.GetProjectNodes(containingProjectPath);

                // Relaunch all projects whose dependency is affected by this file change.
                foreach (var ancestor in containingProjectNodes[0].GetAncestorsAndSelf())
                {
                    var ancestorPath = ancestor.ProjectInstance.FullPath;
                    if (_activeProjectRelaunchOperations.TryGetValue(ancestorPath, out var operations))
                    {
                        relaunchOperations.AddRange(operations);
                        _activeProjectRelaunchOperations = _activeProjectRelaunchOperations.Remove(ancestorPath);

                        if (_activeProjectRelaunchOperations.IsEmpty)
                        {
                            break;
                        }
                    }
                }
            }
        }

        return relaunchOperations;
    }

    private static ImmutableArray<HotReloadManagedCodeUpdate> ToManagedCodeUpdates(IEnumerable<HotReloadService.Update> updates)
        => [.. updates.Select(update => new HotReloadManagedCodeUpdate(update.ModuleId, update.MetadataDelta, update.ILDelta, update.PdbDelta, update.UpdatedTypes, update.RequiredCapabilities))];
}
