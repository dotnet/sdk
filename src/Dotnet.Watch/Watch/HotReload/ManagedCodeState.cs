// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.HotReload.Api;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal sealed class ManagedCodeWorkspace : IDisposable
{
    private readonly ILogger _logger;
    private readonly RunningProjectsManager _runningProjectsManager;
    private readonly HotReloadMSBuildWorkspace _workspace;
    private readonly HotReloadService _hotReloadService;

    private int _solutionUpdateId;

    /// <summary>
    /// Current set of project instances indexed by <see cref="ProjectInstance.FullPath"/>.
    /// Updated whenever the project graph changes.
    /// </summary>
    private ImmutableDictionary<string, ImmutableArray<ProjectInstance>> _projectInstances
        = ImmutableDictionary<string, ImmutableArray<ProjectInstance>>.Empty.WithComparers(PathUtilities.OSSpecificPathComparer);

    public ManagedCodeWorkspace(ILogger logger, RunningProjectsManager runningProjectsManager)
    {
        _logger = logger;
        _runningProjectsManager = runningProjectsManager;
        _workspace = new HotReloadMSBuildWorkspace(logger, projectFile => (instances: _projectInstances.GetValueOrDefault(projectFile, []), project: null));
        _hotReloadService = new HotReloadService(_workspace.CurrentSolution.Services, () => ValueTask.FromResult(GetAggregateCapabilities()));
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    // for testing
    internal Solution CurrentSolution
        => _workspace.CurrentSolution;

    private ImmutableArray<string> GetAggregateCapabilities()
    {
        var capabilities = _runningProjectsManager.CurrentRunningProjects
            .SelectMany(p => p.Value)
            .SelectMany(p => p.ManagedCodeUpdateCapabilities)
            .Distinct(StringComparer.Ordinal)
            .Order()
            .ToImmutableArray();

        _logger.Log(MessageDescriptor.HotReloadCapabilities, string.Join(" ", capabilities));
        return capabilities;
    }

    public void PrepareCompilation(string projectPath, CancellationToken cancellationToken)
    {
        // Warm up the compilation. This would help make the deltas for first edit appear much more quickly.
        // Preparing the compilation is a perf optimization. We can skip it if the session hasn't been started yet. 
        foreach (var project in _workspace.CurrentSolution.Projects)
        {
            if (project.FilePath == projectPath)
            {
                // fire and forget:
                _ = project.GetCompilationAsync(cancellationToken);
            }
        }
    }

    public async ValueTask StartSessionAsync(ProjectGraph graph, CancellationToken cancellationToken)
    {
        var solution = await UpdateProjectGraphAsync(graph, cancellationToken);

        await _hotReloadService.StartSessionAsync(solution, cancellationToken);

        _logger.Log(MessageDescriptor.HotReloadSessionStarted);
    }

    public ProjectUpdatesBuilder CreateUpdatesBuilder()
        => new()
        {
            Logger = _logger,
            HotReloadService = _hotReloadService,

            // capture snapshots:
            Solution = _workspace.CurrentSolution,
            ProjectInstances = _projectInstances,
            RunningProjects = _runningProjectsManager.CurrentRunningProjects
        };

    public async Task<Solution> UpdateProjectGraphAsync(ProjectGraph projectGraph, CancellationToken cancellationToken)
    {
        _projectInstances = CreateProjectInstanceMap(projectGraph);

        var solution = await _workspace.UpdateProjectGraphAsync([.. projectGraph.EntryPointNodes.Select(n => n.ProjectInstance.FullPath)], cancellationToken);
        await SolutionUpdatedAsync(solution, "project update", cancellationToken);
        return solution;
    }

    private static ImmutableDictionary<string, ImmutableArray<ProjectInstance>> CreateProjectInstanceMap(ProjectGraph graph)
        => graph.ProjectNodes
            .GroupBy(static node => node.ProjectInstance.FullPath)
            .ToImmutableDictionary(
                keySelector: static group => group.Key,
                elementSelector: static group => group.Select(static node => node.ProjectInstance).ToImmutableArray());

    public async Task UpdateFileContentAsync(IReadOnlyList<ChangedFile> changedFiles, CancellationToken cancellationToken)
    {
        var solution = await _workspace.UpdateFileContentAsync(changedFiles.Select(static f => (f.Item.FilePath, f.Kind.Convert())), cancellationToken);
        await SolutionUpdatedAsync(solution, "document update", cancellationToken);
    }

    private Task SolutionUpdatedAsync(Solution newSolution, string operationDisplayName, CancellationToken cancellationToken)
        => ReportSolutionFilesAsync(newSolution, Interlocked.Increment(ref _solutionUpdateId), operationDisplayName, cancellationToken);

    private async Task ReportSolutionFilesAsync(Solution solution, int updateId, string operationDisplayName, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Solution after {Operation}: v{Version}", operationDisplayName, updateId);

        if (!_logger.IsEnabled(LogLevel.Trace))
        {
            return;
        }

        foreach (var project in solution.Projects)
        {
            _logger.LogDebug("  Project: {Path}", project.FilePath);

            foreach (var document in project.Documents)
            {
                await InspectDocumentAsync(document, "Document").ConfigureAwait(false);
            }

            foreach (var document in project.AdditionalDocuments)
            {
                await InspectDocumentAsync(document, "Additional").ConfigureAwait(false);
            }

            foreach (var document in project.AnalyzerConfigDocuments)
            {
                await InspectDocumentAsync(document, "Config").ConfigureAwait(false);
            }
        }

        async ValueTask InspectDocumentAsync(TextDocument document, string kind)
        {
            var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("    {Kind}: {FilePath} [{Checksum}]", kind, document.FilePath, Convert.ToBase64String(text.GetChecksum().ToArray()));
        }
    }
}
