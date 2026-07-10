// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal sealed partial class ProjectUpdatesBuilder
{
    private static readonly ImmutableArray<string> s_targets = [TargetNames.GenerateComputedBuildStaticWebAssets, TargetNames.ResolveReferencedProjectsStaticWebAssets];

    private sealed class AssetsBuilder(ProjectUpdatesBuilder projectUpdatesBuilder, EvaluationResult evaluationResult) : StaticWebAssetUpdateBuilder
    {
        protected override bool TryGetManifest(ProjectInstanceId id, [NotNullWhen(true)] out StaticWebAssetsManifest? manifest)
            => evaluationResult.StaticWebAssetsManifests.TryGetValue(id, out manifest);

        protected override IEnumerable<ProjectInstanceInfo> GetProjectInstances(string projectPath)
            => evaluationResult.ProjectGraph.GetProjectNodes(projectPath).Select(GetInfo);

        protected override IEnumerable<(ProjectInstanceInfo info, ILogger logger)> GetApplicationProjectAncestors(ProjectInstanceId projectInstanceId)
            => from node in evaluationResult.ProjectGraph.GetProjectNode(projectInstanceId).GetAncestorsAndSelf()
               // use any of the running projects associated with the ancestor:
               let runningProject = projectUpdatesBuilder.GetCorrespondingRunningProjects(node.ProjectInstance.GetId()).FirstOrDefault()
               where runningProject != null
               select (GetInfo(node), runningProject.ClientLogger);

        private static ProjectInstanceInfo GetInfo(ProjectGraphNode projectNode)
            => new()
            {
                Id = projectNode.ProjectInstance.GetId(),
                AssemblyName = projectNode.ProjectInstance.GetAssemblyName(),
                HasScopedCssTargets = HasScopedCssTargets(projectNode.ProjectInstance),
            };

        private static bool HasScopedCssTargets(ProjectInstance projectInstance)
            => s_targets.All(projectInstance.Targets.ContainsKey);
    }

    public async ValueTask AddStaticAssetUpdatesAsync(
        IReadOnlyList<ChangedFile> files,
        EvaluationResult evaluationResult,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var builder = new AssetsBuilder(this, evaluationResult);

        foreach (var file in files)
        {
            builder.AddAssets(file.Item.FilePath, file.Item.ContainingProjectPaths, file.Item.StaticWebAssetRelativeUrl);
        }

        if (builder.Assets.Count == 0)
        {
            return;
        }

        HashSet<ProjectInstanceId>? failedApplicationProjectInstances = null;
        if (builder.ProjectInstancesToRegenerate.Count > 0)
        {
            Logger.LogDebug("Regenerating scoped CSS bundles.");

            // Deep copy instances so that we don't pollute the project graph:
            var buildRequests = builder.ProjectInstancesToRegenerate
                .Select(instanceId => BuildRequest.Create(evaluationResult.RestoredProjectInstances[instanceId].DeepCopy(), s_targets))
                .ToArray();

            _ = await evaluationResult.BuildManager.BuildAsync(
                buildRequests,
                onFailure: failedInstance =>
                {
                    Logger.LogWarning("[{ProjectName}] Failed to regenerate scoped CSS bundle.", failedInstance.GetDisplayName());

                    failedApplicationProjectInstances ??= [];
                    failedApplicationProjectInstances.Add(failedInstance.GetId());

                    // continue build
                    return true;
                },
                operationName: "ScopedCss",
                cancellationToken);
        }

        foreach (var (applicationProjectInstance, instanceAssets) in builder.Assets)
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

    private IEnumerable<RunningProject> GetCorrespondingRunningProjects(ProjectInstanceId project)
    {
        if (!RunningProjects.TryGetValue(project.ProjectPath, out var projectsWithPath))
        {
            return [];
        }

        return projectsWithPath.Where(p => string.Equals(p.GetTargetFramework(), project.TargetFramework, StringComparison.OrdinalIgnoreCase));
    }
}
