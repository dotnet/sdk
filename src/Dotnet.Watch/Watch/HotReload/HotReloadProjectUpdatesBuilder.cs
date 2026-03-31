// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Build.Execution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.ExternalAccess.HotReload.Api;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal sealed class HotReloadProjectUpdatesBuilder
{
    public required ILogger Logger { get; init; }
    public required HotReloadService HotReloadService { get; init; }

    // inputs:
    public required Solution Solution { get; init; }
    public required ImmutableDictionary<string, ImmutableArray<ProjectInstance>> ProjectInstances { get; init; }
    public required ImmutableDictionary<string, ImmutableArray<RunningProject>> RunningProjects { get; init; }

    // outputs:
    private readonly List<ProjectId> _previousProjectUpdatesToDiscard = [];
    private readonly List<HotReloadService.Update> _managedCodeUpdates = [];
    private readonly Dictionary<RunningProject, List<StaticWebAsset>> _staticAssetUpdates = [];
    private readonly List<string> _projectsToRebuild = [];
    private readonly List<string> _projectsToRedeploy = [];
    private readonly List<RunningProject> _projectsToRestart = [];

    public IReadOnlyList<ProjectId> PreviousProjectUpdatesToDiscard => _previousProjectUpdatesToDiscard;
    public IReadOnlyList<HotReloadService.Update> ManagedCodeUpdates => _managedCodeUpdates;
    public IReadOnlyDictionary<RunningProject, List<StaticWebAsset>> StaticAssetUpdates => _staticAssetUpdates;
    public IReadOnlyList<string> ProjectsToRebuild => _projectsToRebuild;
    public IReadOnlyList<string> ProjectsToRedeploy => _projectsToRedeploy;
    public IReadOnlyList<RunningProject> ProjectsToRestart => _projectsToRestart;

    public async ValueTask GetManagedCodeUpdatesAsync(
        Func<IEnumerable<string>, CancellationToken, Task<bool>> restartPrompt,
        bool autoRestart,
        CancellationToken cancellationToken)
    {
        var runningProjectInfos =
           (from project in Solution.Projects
            let runningProject = GetCorrespondingRunningProjects(project).FirstOrDefault()
            where runningProject != null
            let autoRestartProject = autoRestart || runningProject.ProjectNode.IsAutoRestartEnabled()
            select (project.Id, info: new HotReloadService.RunningProjectInfo() { RestartWhenChangesHaveNoEffect = autoRestartProject }))
            .ToImmutableDictionary(e => e.Id, e => e.info);

        var updates = await HotReloadService.GetUpdatesAsync(Solution, runningProjectInfos, cancellationToken);

        await DisplayResultsAsync(updates, runningProjectInfos, cancellationToken);

        if (updates.Status is HotReloadService.Status.NoChangesToApply or HotReloadService.Status.Blocked)
        {
            // If Hot Reload is blocked (due to compilation error) we ignore the current
            // changes and await the next file change.

            // Note: CommitUpdate/DiscardUpdate is not expected to be called.
            return;
        }

        var projectsToPromptForRestart =
            (from projectId in updates.ProjectsToRestart.Keys
             where !runningProjectInfos[projectId].RestartWhenChangesHaveNoEffect // equivallent to auto-restart
             select Solution.GetProject(projectId)!.Name).ToList();

        if (projectsToPromptForRestart.Any() &&
            !await restartPrompt.Invoke(projectsToPromptForRestart, cancellationToken))
        {
            HotReloadService.DiscardUpdate();

            Logger.Log(MessageDescriptor.HotReloadSuspended);
            await Task.Delay(-1, cancellationToken);

            return;
        }

        // Note: Releases locked project baseline readers, so we can rebuild any projects that need rebuilding.
        HotReloadService.CommitUpdate();

        _previousProjectUpdatesToDiscard.AddRange(updates.ProjectsToRebuild);
        _managedCodeUpdates.AddRange(updates.ProjectUpdates);
        _projectsToRebuild.AddRange(updates.ProjectsToRebuild.Select(GetRequiredProjectFilePath));
        _projectsToRedeploy.AddRange(updates.ProjectsToRedeploy.Select(GetRequiredProjectFilePath));

        // Terminate all tracked processes that need to be restarted,
        // except for the root process, which will terminate later on.
        _projectsToRestart.AddRange(
            updates.ProjectsToRestart.SelectMany(e => RunningProjects.TryGetValue(GetRequiredProjectFilePath(e.Key), out var array) ? array : []));
    }

    private string GetRequiredProjectFilePath(ProjectId projectId)
        => (Solution.GetProject(projectId) ?? throw new InvalidOperationException()).FilePath ?? throw new InvalidOperationException();

    private async ValueTask DisplayResultsAsync(
        HotReloadService.Updates updates,
        ImmutableDictionary<ProjectId, HotReloadService.RunningProjectInfo> runningProjectInfos,
        CancellationToken cancellationToken)
    {
        switch (updates.Status)
        {
            case HotReloadService.Status.ReadyToApply:
                break;

            case HotReloadService.Status.NoChangesToApply:
                Logger.Log(MessageDescriptor.NoManagedCodeChangesToApply);
                break;

            case HotReloadService.Status.Blocked:
                Logger.Log(MessageDescriptor.UnableToApplyChanges);
                break;

            default:
                throw new InvalidOperationException();
        }

        if (!updates.ProjectsToRestart.IsEmpty)
        {
            Logger.Log(MessageDescriptor.RestartNeededToApplyChanges);
        }

        var errorsToDisplayInApp = new List<string>();

        // Display errors first, then warnings:
        ReportCompilationDiagnostics(DiagnosticSeverity.Error);
        ReportCompilationDiagnostics(DiagnosticSeverity.Warning);
        ReportRudeEdits();

        // report or clear diagnostics in the browser UI
        await RunningProjects.ForEachValueAsync(
            (project, cancellationToken) => project.Clients.ReportCompilationErrorsInApplicationAsync([.. errorsToDisplayInApp], cancellationToken).AsTask() ?? Task.CompletedTask,
            cancellationToken);

        void ReportCompilationDiagnostics(DiagnosticSeverity severity)
        {
            foreach (var diagnostic in updates.PersistentDiagnostics)
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

                // TODO: we don't currently have a project associated with the diagnostic
                ReportDiagnostic(diagnostic, projectDisplayPrefix: "", autoPrefix: "");
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

            foreach (var (projectId, diagnostics) in updates.TransientDiagnostics)
            {
                // The diagnostic may be reported for a project that has been deleted.
                var project = Solution.GetProject(projectId);
                var projectDisplay = project != null ? $"[{GetProjectInstance(project).GetDisplayName()}] " : "";

                foreach (var diagnostic in diagnostics)
                {
                    var prefix =
                        projectsRestartedDueToRudeEdits.Contains(projectId) ? "[auto-restart] " :
                        projectsRebuiltDueToRudeEdits.Contains(projectId) ? "[auto-rebuild] " :
                        "";

                    ReportDiagnostic(diagnostic, projectDisplay, prefix);
                }
            }
        }

        bool IsAutoRestartEnabled(ProjectId id)
            => runningProjectInfos.TryGetValue(id, out var info) && info.RestartWhenChangesHaveNoEffect;

        void ReportDiagnostic(Diagnostic diagnostic, string projectDisplayPrefix, string autoPrefix)
        {
            var message = projectDisplayPrefix + autoPrefix + CSharpDiagnosticFormatter.Instance.Format(diagnostic);

            if (autoPrefix != "")
            {
                Logger.Log(MessageDescriptor.ApplyUpdate_AutoVerbose, message);
                errorsToDisplayInApp.Add(MessageDescriptor.RestartingApplicationToApplyChanges.GetMessage());
            }
            else
            {
                var descriptor = GetMessageDescriptor(diagnostic);
                Logger.Log(descriptor, message);

                if (descriptor.Level != LogLevel.None)
                {
                    errorsToDisplayInApp.Add(descriptor.GetMessage(message));
                }
            }
        }

        // Use the default severity of the diagnostic as it conveys impact on Hot Reload
        // (ignore warnings as errors and other severity configuration).
        static MessageDescriptor<string> GetMessageDescriptor(Diagnostic diagnostic)
        {
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

    private static readonly ImmutableArray<string> s_targets = [TargetNames.GenerateComputedBuildStaticWebAssets, TargetNames.ResolveReferencedProjectsStaticWebAssets];

    private static bool HasScopedCssTargets(ProjectInstance projectInstance)
        => s_targets.All(projectInstance.Targets.ContainsKey);

    public async ValueTask GetStaticAssetUpdatesAsync(
        IReadOnlyList<ChangedFile> files,
        EvaluationResult evaluationResult,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var assets = new Dictionary<ProjectInstance, Dictionary<string, StaticWebAsset>>();
        var projectInstancesToRegenerate = new HashSet<ProjectInstanceId>();

        foreach (var changedFile in files)
        {
            var file = changedFile.Item;
            var isScopedCss = StaticWebAsset.IsScopedCssFile(file.FilePath);

            if (!isScopedCss && file.StaticWebAssetRelativeUrl is null)
            {
                continue;
            }

            foreach (var containingProjectPath in file.ContainingProjectPaths)
            {
                foreach (var containingProjectNode in evaluationResult.ProjectGraph.GetProjectNodes(containingProjectPath))
                {
                    if (isScopedCss)
                    {
                        if (!HasScopedCssTargets(containingProjectNode.ProjectInstance))
                        {
                            continue;
                        }

                        projectInstancesToRegenerate.Add(containingProjectNode.ProjectInstance.GetId());
                    }

                    foreach (var referencingProjectNode in containingProjectNode.GetAncestorsAndSelf())
                    {
                        var applicationProjectInstance = referencingProjectNode.ProjectInstance;
                        var runningApplicationProject = GetCorrespondingRunningProjects(applicationProjectInstance).FirstOrDefault();
                        if (runningApplicationProject == null)
                        {
                            continue;
                        }

                        string filePath;
                        string relativeUrl;

                        if (isScopedCss)
                        {
                            // Razor class library may be referenced by application that does not have static assets:
                            if (!HasScopedCssTargets(applicationProjectInstance))
                            {
                                continue;
                            }

                            projectInstancesToRegenerate.Add(applicationProjectInstance.GetId());

                            var bundleFileName = StaticWebAsset.GetScopedCssBundleFileName(
                                applicationProjectFilePath: applicationProjectInstance.FullPath,
                                containingProjectFilePath: containingProjectNode.ProjectInstance.FullPath);

                            if (!evaluationResult.StaticWebAssetsManifests.TryGetValue(applicationProjectInstance.GetId(), out var manifest))
                            {
                                // Shouldn't happen.
                                runningApplicationProject.ClientLogger.Log(MessageDescriptor.StaticWebAssetManifestNotFound);
                                continue;
                            }

                            if (!manifest.TryGetBundleFilePath(bundleFileName, out var bundleFilePath))
                            {
                                // Shouldn't happen.
                                runningApplicationProject.ClientLogger.Log(MessageDescriptor.ScopedCssBundleFileNotFound, bundleFileName);
                                continue;
                            }

                            filePath = bundleFilePath;
                            relativeUrl = bundleFileName;
                        }
                        else
                        {
                            Debug.Assert(file.StaticWebAssetRelativeUrl != null);
                            filePath = file.FilePath;
                            relativeUrl = file.StaticWebAssetRelativeUrl;
                        }

                        if (!assets.TryGetValue(applicationProjectInstance, out var applicationAssets))
                        {
                            applicationAssets = [];
                            assets.Add(applicationProjectInstance, applicationAssets);
                        }
                        else if (applicationAssets.ContainsKey(filePath))
                        {
                            // asset already being updated in this application project:
                            continue;
                        }

                        applicationAssets.Add(filePath, new StaticWebAsset(
                            filePath,
                            StaticWebAsset.WebRoot + "/" + relativeUrl,
                            containingProjectNode.GetAssemblyName(),
                            isApplicationProject: containingProjectNode.ProjectInstance == applicationProjectInstance));
                    }
                }
            }
        }

        if (assets.Count == 0)
        {
            return;
        }

        HashSet<ProjectInstance>? failedApplicationProjectInstances = null;
        if (projectInstancesToRegenerate.Count > 0)
        {
            Logger.LogDebug("Regenerating scoped CSS bundles.");

            // Deep copy instances so that we don't pollute the project graph:
            var buildRequests = projectInstancesToRegenerate
                .Select(instanceId => BuildRequest.Create(evaluationResult.RestoredProjectInstances[instanceId].DeepCopy(), s_targets))
                .ToArray();

            _ = await evaluationResult.BuildManager.BuildAsync(
                buildRequests,
                onFailure: failedInstance =>
                {
                    Logger.LogWarning("[{ProjectName}] Failed to regenerate scoped CSS bundle.", failedInstance.GetDisplayName());

                    failedApplicationProjectInstances ??= [];
                    failedApplicationProjectInstances.Add(failedInstance);

                    // continue build
                    return true;
                },
                operationName: "ScopedCss",
                cancellationToken);
        }

        foreach (var (applicationProjectInstance, instanceAssets) in assets)
        {
            if (failedApplicationProjectInstances?.Contains(applicationProjectInstance) == true)
            {
                continue;
            }

            foreach (var runningProject in GetCorrespondingRunningProjects(applicationProjectInstance))
            {
                if (!_staticAssetUpdates.TryGetValue(runningProject, out var updatesPerRunningProject))
                {
                    _staticAssetUpdates.Add(runningProject, updatesPerRunningProject = []);
                }

                if (!runningProject.Clients.UseRefreshServerToApplyStaticAssets && !runningProject.Clients.IsManagedAgentSupported)
                {
                    // Static assets are applied via managed Hot Reload agent (e.g. in MAUI Blazor app), but managed Hot Reload is not supported (e.g. startup hooks are disabled).
                    _projectsToRebuild.Add(runningProject.ProjectNode.ProjectInstance.FullPath);
                    _projectsToRestart.Add(runningProject);
                }
                else
                {
                    updatesPerRunningProject.AddRange(instanceAssets.Values);
                }
            }
        }
    }

    private IEnumerable<RunningProject> GetCorrespondingRunningProjects(Project project)
    {
        if (project.FilePath == null || !RunningProjects.TryGetValue(project.FilePath, out var projectsWithPath))
        {
            return [];
        }

        // msbuild workspace doesn't set TFM if the project is not multi-targeted
        var tfm = HotReloadService.GetTargetFramework(project);
        if (tfm == null)
        {
            Debug.Assert(projectsWithPath.All(p => string.Equals(p.GetTargetFramework(), projectsWithPath[0].GetTargetFramework(), StringComparison.OrdinalIgnoreCase)));
            return projectsWithPath;
        }

        return projectsWithPath.Where(p => string.Equals(p.GetTargetFramework(), tfm, StringComparison.OrdinalIgnoreCase));
    }

    private IEnumerable<RunningProject> GetCorrespondingRunningProjects(ProjectInstance project)
    {
        if (!RunningProjects.TryGetValue(project.FullPath, out var projectsWithPath))
        {
            return [];
        }

        var tfm = project.GetTargetFramework();
        return projectsWithPath.Where(p => string.Equals(p.GetTargetFramework(), tfm, StringComparison.OrdinalIgnoreCase));
    }

    private ProjectInstance GetProjectInstance(Project project)
    {
        Debug.Assert(project.FilePath != null);

        if (!ProjectInstances.TryGetValue(project.FilePath, out var instances))
        {
            throw new InvalidOperationException($"Project '{project.FilePath}' (id = '{project.Id}') not found in project graph");
        }

        // msbuild workspace doesn't set TFM if the project is not multi-targeted
        var tfm = HotReloadService.GetTargetFramework(project);
        if (tfm == null)
        {
            return instances.Single();
        }

        return instances.Single(instance => string.Equals(instance.GetTargetFramework(), tfm, StringComparison.OrdinalIgnoreCase));
    }
}
