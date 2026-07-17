// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Graph;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal enum FSharpManagedUpdateStatus
{
    NoChanges,
    ReadyToApply,
    RestartRequired,
    Blocked,
}

internal readonly record struct FSharpManagedUpdate(string ProjectPath, HotReloadManagedCodeUpdate Update);

internal readonly record struct FSharpManagedUpdateIssue(string ProjectPath, string? Message);

internal readonly record struct FSharpManagedUpdateResult(
    FSharpManagedUpdateStatus Status,
    ImmutableArray<FSharpManagedUpdate> Updates,
    ImmutableArray<FSharpManagedUpdateIssue> Issues);

internal sealed class FSharpHotReloadService
{
    private const string RudeEditHelpLink = "https://github.com/dotnet/fsharp/blob/main/docs/hot-reload-rude-edits.md";

    private readonly ILogger _logger;
    private readonly bool _trace;

    /// <summary>
    /// Master kill switch for the entire F# hot reload bridge. Set
    /// DOTNET_WATCH_FSHARP_HOTRELOAD=0 (or false) to make this service behave as if no F#
    /// projects exist: session prestart does nothing, <see cref="TryEmitUpdatesAsync"/> reports
    /// no changes, and <see cref="OwnsChangedFile"/> claims nothing, restoring the stock
    /// restart-on-edit behavior for F# projects without having to know the narrower
    /// DOTNET_WATCH_FSHARP_* tuning variables. Unset (the default) means enabled. Read once at
    /// construction.
    /// </summary>
    private readonly bool _disabled;

    /// <summary>
    /// Provides the aggregate runtime edit-and-continue capabilities of the running processes,
    /// matching the capabilities the Roslyn hot reload service receives. Evaluated lazily at
    /// session start so newly launched processes are taken into account.
    /// </summary>
    private readonly Func<ImmutableArray<string>>? _getCapabilities;

    private ImmutableDictionary<ProjectInstanceId, FSharpProjectInfo> _projects = ImmutableDictionary<ProjectInstanceId, FSharpProjectInfo>.Empty;
    private ImmutableDictionary<ProjectInstanceId, object> _cachedProjectInputs = ImmutableDictionary<ProjectInstanceId, object>.Empty;
    private ImmutableDictionary<ProjectInstanceId, Guid> _runtimeModuleIds = ImmutableDictionary<ProjectInstanceId, Guid>.Empty;
    private FSharpReflectionHost? _host;

    /// <summary>
    /// The active project for the LEGACY single-active-project checker surface (older compiler
    /// service builds without <c>FSharpChecker.CreateHotReloadSession</c>). The legacy surface
    /// holds one process-wide session, so the bridge has to switch it between projects. Unused
    /// in session-object mode.
    /// </summary>
    private ProjectInstanceId? _activeProject;
    private object? _activeProjectInput;

    /// <summary>
    /// The FSharpHotReloadSession instance (reflection-typed) when the loaded compiler service
    /// exposes the session-object API. One session per watch session; each discovered F#
    /// project is captured into it via AddProject (eagerly at session start, lazily on first
    /// edit otherwise), edits emit per-project deltas, and pending updates are resolved by
    /// <see cref="CommitUpdates"/>/<see cref="DiscardUpdates"/>. Disposed by
    /// <see cref="EndSession"/>.
    /// </summary>
    private object? _sessionObject;

    /// <summary>Projects whose baseline has been captured into <see cref="_sessionObject"/>.</summary>
    private ImmutableHashSet<ProjectInstanceId> _sessionObjectProjects = [];

    /// <summary>
    /// The capability set the active session currently uses. The session is prestarted before
    /// any agent connects (so this is often empty initially); once processes report their
    /// capabilities the live session is updated in place via
    /// FSharpChecker.UpdateHotReloadCapabilities — never restarted, because a restart would
    /// re-capture the baseline from sources that may already contain pending edits.
    /// </summary>
    private ImmutableArray<string> _activeSessionCapabilities = [];

    public FSharpHotReloadService(ILogger logger, Func<ImmutableArray<string>>? getCapabilities = null)
    {
        _logger = logger;
        _trace = IsTraceEnabled();
        _disabled = IsDisabled();
        _getCapabilities = getCapabilities;
    }

    /// <summary>True when the F# bridge owns F# source updates instead of the stock restart path.</summary>
    public bool IsEnabled => !_disabled;

    private ImmutableArray<string> GetRuntimeCapabilities()
    {
        try
        {
            return _getCapabilities?.Invoke() ?? [];
        }
        catch (Exception ex)
        {
            if (_trace)
            {
                _logger.LogDebug("Failed to query runtime hot reload capabilities: {Message}", ex.Message);
            }

            return [];
        }
    }

    public void UpdateProjects(ProjectGraph projectGraph)
    {
        if (_disabled)
        {
            return;
        }

        _projects = FSharpProjectInfo.Collect(projectGraph, _logger);
        _cachedProjectInputs = _cachedProjectInputs.RemoveRange(_cachedProjectInputs.Keys.Where(key => !_projects.ContainsKey(key)));
        _runtimeModuleIds = _runtimeModuleIds.RemoveRange(_runtimeModuleIds.Keys.Where(key => !_projects.ContainsKey(key)));

        // A project that leaves the graph and later returns must be re-added to the session
        // object so its baseline is recaptured from the then-current build output.
        _sessionObjectProjects = _sessionObjectProjects.Except(_sessionObjectProjects.Where(key => !_projects.ContainsKey(key)));

        if (_activeProject is { } activeProject && !_projects.ContainsKey(activeProject))
        {
            EndSession();
        }
    }

    public ValueTask StartSessionAsync(CancellationToken cancellationToken)
    {
        if (_disabled)
        {
            return ValueTask.CompletedTask;
        }

        // Prime per-project inputs from the baseline build before edits arrive.
        // This mirrors Roslyn's committed-solution model where the first edit is compared
        // against the last built state rather than being used as the session baseline.
        var cachedInputsBuilder = ImmutableDictionary.CreateBuilder<ProjectInstanceId, object>();

        foreach (var projectInfo in _projects.Values)
        {
            if (!TryGetHost(projectInfo, out var host, out var hostError))
            {
                if (_trace)
                {
                    _logger.LogDebug(
                        "Skipping F# project input bootstrap for '{ProjectPath}': {Message}",
                        projectInfo.ProjectPath,
                        hostError);
                }

                continue;
            }

            if (!host.TryCreateProjectInput(projectInfo, out var projectInput, out var inputError) || projectInput == null)
            {
                if (_trace)
                {
                    _logger.LogDebug(
                        "Skipping F# project input bootstrap for '{ProjectPath}': {Message}",
                        projectInfo.ProjectPath,
                        inputError ?? "Project input was null.");
                }

                continue;
            }

            cachedInputsBuilder[projectInfo.ProjectId] = projectInput;
        }

        _cachedProjectInputs = cachedInputsBuilder.ToImmutable();
        _runtimeModuleIds = ImmutableDictionary<ProjectInstanceId, Guid>.Empty;

        if (_host is { } sessionHost && sessionHost.SupportsSessionObject)
        {
            // Session-object mode: create ONE session for the whole watch session and capture
            // every discovered F# project into it up front (the launched project and the F#
            // libraries loaded into it alike), so first edits diff against the pre-edit
            // baseline build. Projects that fail to capture here are retried lazily on first
            // edit by EnsureSession.
            foreach (var projectInfo in _projects.Values)
            {
                if (!_cachedProjectInputs.ContainsKey(projectInfo.ProjectId))
                {
                    continue;
                }

                if (!EnsureSession(sessionHost, projectInfo, cancellationToken, out _, out var status, out var message))
                {
                    if (_trace)
                    {
                        _logger.LogDebug(
                            "Unable to prestart F# hot reload session project '{ProjectPath}': {Status} ({Message})",
                            projectInfo.ProjectPath,
                            status,
                            message);
                    }
                }
                else if (_trace)
                {
                    _logger.LogDebug("F# hot reload session project prestarted for '{ProjectPath}'.", projectInfo.ProjectPath);
                }
            }
        }
        else if (_projects.Count == 1)
        {
            var projectInfo = _projects.Values.First();
            if (TryGetHost(projectInfo, out var host, out var hostError))
            {
                if (!EnsureSession(host, projectInfo, cancellationToken, out _, out var status, out var message))
                {
                    if (_trace)
                    {
                        _logger.LogDebug(
                            "Unable to prestart F# hot reload session for '{ProjectPath}': {Status} ({Message})",
                            projectInfo.ProjectPath,
                            status,
                            message);
                    }
                }
                else if (_trace)
                {
                    _logger.LogDebug("F# hot reload session prestarted for '{ProjectPath}'.", projectInfo.ProjectPath);
                }
            }
            else if (_trace)
            {
                _logger.LogDebug(
                    "Unable to prestart F# hot reload session for '{ProjectPath}': {Message}",
                    projectInfo.ProjectPath,
                    hostError);
            }
        }

        return ValueTask.CompletedTask;
    }

    public void EndSession()
    {
        if (_disabled)
        {
            return;
        }

        if (_sessionObject is { } sessionObject)
        {
            // Session-object mode: disposing the session ends it; per-project baselines and any
            // pending updates go with it.
            _host?.DisposeSession(sessionObject);
            _sessionObject = null;
            _sessionObjectProjects = [];
            _runtimeModuleIds = ImmutableDictionary<ProjectInstanceId, Guid>.Empty;
            _activeSessionCapabilities = [];
            return;
        }

        if (_activeProject is { } activeProject)
        {
            _runtimeModuleIds = _runtimeModuleIds.Remove(activeProject);
        }

        _host?.TryEndSession();
        _activeProject = null;
        _activeProjectInput = null;
        _activeSessionCapabilities = [];
    }

    /// <summary>
    /// True when the changed file belongs exclusively to F# projects whose updates this service
    /// owns end-to-end (session-object mode). Such changes must not be surfaced to the Roslyn
    /// workspace: its EnC service has no F# support, so it reports rude edit ENC1009 ("project
    /// does not support Hot Reload") and schedules a redundant rebuild for every F# edit —
    /// including edits the F# compiler service applies in place or correctly classifies as
    /// no-ops, where the empty Roslyn result would otherwise swallow the user-facing
    /// "no changes"/"applied" decision message. In legacy single-session mode files are not
    /// claimed: the Roslyn auto-rebuild remains the fallback for edits the single-active-project
    /// bridge cannot target (such as F# library projects).
    /// </summary>
    public bool OwnsChangedFile(ChangedFile changedFile)
    {
        if (_disabled)
        {
            return false;
        }

        if (_host?.SupportsSessionObject != true)
        {
            return false;
        }

        // Project files and other configuration inputs must stay visible to the stock watch
        // pipeline so they trigger rebuild/restart semantics. Claim only file kinds this bridge
        // can actually invalidate and classify.
        if (!IsSupportedChangedFile(changedFile.Item.FilePath))
        {
            return false;
        }

        var containingProjectPaths = changedFile.Item.ContainingProjectPaths;
        return containingProjectPaths.Count > 0 &&
               containingProjectPaths.All(containingProjectPath =>
                   _projects.Keys.Any(knownProject =>
                       PathUtilities.OSSpecificPathComparer.Equals(knownProject.ProjectPath, containingProjectPath)));
    }

    private static bool IsSupportedChangedFile(string filePath)
        => IsFSharpSourcePath(filePath) || IsManagedDependencyCandidatePath(filePath);

    /// <summary>
    /// Commits ALL pending F# project updates staged by the last successful emit. dotnet-watch
    /// hands the emitted deltas to every running process immediately and unconditionally once it
    /// decides to apply them, so the committed per-project baselines are advanced at hand-off
    /// time — the same point the Roslyn watch service calls CommitUpdate. No-op for the legacy
    /// checker surface, which auto-commits each successful emit.
    /// </summary>
    public void CommitUpdates()
    {
        if (_disabled)
        {
            return;
        }

        if (_sessionObject is { } sessionObject)
        {
            _host?.SessionCommit(sessionObject);
        }
    }

    /// <summary>
    /// Discards ALL pending F# project updates when the watch decides not to apply the emitted
    /// deltas (hot reload blocked, or the user declined the restart prompt), so the next emit
    /// diffs against the unchanged committed view. No-op for the legacy checker surface.
    /// </summary>
    public void DiscardUpdates()
    {
        if (_disabled)
        {
            return;
        }

        if (_sessionObject is { } sessionObject)
        {
            _host?.SessionDiscard(sessionObject);
        }
    }

#pragma warning disable CS1998 // Intentional sync fast-path wrapped in ValueTask-returning API.
    public async ValueTask<FSharpManagedUpdateResult> TryEmitUpdatesAsync(
        IReadOnlyList<ChangedFile> changedFiles,
        ImmutableDictionary<string, ImmutableArray<RunningProject>> runningProjects,
        CancellationToken cancellationToken)
    {
        if (_disabled)
        {
            return new FSharpManagedUpdateResult(FSharpManagedUpdateStatus.NoChanges, [], []);
        }

        var changedProjects = GetChangedFSharpProjects(changedFiles, runningProjects);
        if (changedProjects.IsEmpty)
        {
            if (_trace)
            {
                _logger.LogDebug("No running F# project matched the current file changes.");
            }

            return new FSharpManagedUpdateResult(FSharpManagedUpdateStatus.NoChanges, [], []);
        }

        var updates = ImmutableArray.CreateBuilder<FSharpManagedUpdate>();
        var issues = ImmutableArray.CreateBuilder<FSharpManagedUpdateIssue>();

        foreach (var changedProject in changedProjects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_projects.TryGetValue(changedProject, out var projectInfo))
            {
                if (_trace)
                {
                    _logger.LogDebug(
                        "Changed F# project '{ProjectPath}' is not present in the current project graph snapshot.",
                        changedProject.ProjectPath);
                }

                continue;
            }

            var projectResult = TryEmitProjectUpdate(
                projectInfo,
                changedFiles,
                cancellationToken,
                allowSessionReset: updates.Count == 0);

            if (projectResult.Status == FSharpManagedUpdateStatus.Blocked)
            {
                // A session commit/discard is solution-wide. If any project is blocked, discard
                // deltas already staged for other projects and leave Roslyn to do the same.
                DiscardUpdates();
                return new FSharpManagedUpdateResult(FSharpManagedUpdateStatus.Blocked, [], projectResult.Issues);
            }

            updates.AddRange(projectResult.Updates);
            issues.AddRange(projectResult.Issues);
        }

        var status =
            issues.Count > 0
                ? FSharpManagedUpdateStatus.RestartRequired
                : updates.Count > 0
                    ? FSharpManagedUpdateStatus.ReadyToApply
                    : FSharpManagedUpdateStatus.NoChanges;

        return new FSharpManagedUpdateResult(status, updates.ToImmutable(), issues.ToImmutable());
    }

    private FSharpManagedUpdateResult TryEmitProjectUpdate(
        FSharpProjectInfo projectInfo,
        IReadOnlyList<ChangedFile> changedFiles,
        CancellationToken cancellationToken,
        bool allowSessionReset)
    {
        if (!TryGetHost(projectInfo, out var host, out var hostError))
        {
            _logger.LogDebug(
                "F# managed hot reload bridge unavailable for '{ProjectPath}': {Message}. Falling back to restart.",
                projectInfo.ProjectPath,
                hostError);

            return new FSharpManagedUpdateResult(
                FSharpManagedUpdateStatus.RestartRequired,
                [],
                [new FSharpManagedUpdateIssue(projectInfo.ProjectPath, hostError)]);
        }

        if (!EnsureSession(host, projectInfo, cancellationToken, out var projectInput, out var ensureStatus, out var ensureMessage))
        {
            return new FSharpManagedUpdateResult(
                ensureStatus,
                [],
                [new FSharpManagedUpdateIssue(projectInfo.ProjectPath, ensureMessage)]);
        }

        var moduleIdBeforeCompile = TryGetModuleVersionId(projectInfo.TargetPath);
        if (!TryCompileProjectOutput(projectInfo, out var compileMessage))
        {
            return new FSharpManagedUpdateResult(
                FSharpManagedUpdateStatus.Blocked,
                [],
                [new FSharpManagedUpdateIssue(projectInfo.ProjectPath, compileMessage)]);
        }

        var changedSourceFiles = GetChangedSourceFilesForProject(changedFiles, projectInfo.ProjectId);
        var changedDependencyFiles = GetChangedDependencyFilesForProject(changedFiles, projectInfo);

        if (_trace && !changedDependencyFiles.IsEmpty)
        {
            _logger.LogDebug(
                "F# dependency changes considered for managed update in '{ProjectPath}': {ChangedFiles}",
                projectInfo.ProjectPath,
                string.Join(", ", changedDependencyFiles));
        }

        foreach (var changedSourceFile in changedSourceFiles)
        {
            host.NotifyFileChanged(changedSourceFile, projectInfo, projectInput!, cancellationToken);
        }

        if (!changedDependencyFiles.IsEmpty)
        {
            host.InvalidateConfiguration(projectInput!, projectInfo.ProjectPath);
        }

        if (!host.TryRefreshProjectInput(projectInfo, projectInput!, out var refreshedProjectInput, out var refreshMessage))
        {
            return new FSharpManagedUpdateResult(
                FSharpManagedUpdateStatus.RestartRequired,
                [],
                [new FSharpManagedUpdateIssue(projectInfo.ProjectPath, refreshMessage)]);
        }

        projectInput = refreshedProjectInput;
        if (_activeProject is { } legacyActiveProject && legacyActiveProject.Equals(projectInfo.ProjectId))
        {
            _activeProjectInput = refreshedProjectInput;
        }

        _cachedProjectInputs = _cachedProjectInputs.SetItem(projectInfo.ProjectId, refreshedProjectInput!);

        var moduleIdAfterCompile = TryGetModuleVersionId(projectInfo.TargetPath);
        var targetModuleId =
            _runtimeModuleIds.TryGetValue(projectInfo.ProjectId, out var runtimeModuleId)
                ? runtimeModuleId
                : moduleIdBeforeCompile ?? moduleIdAfterCompile;
        if (targetModuleId == null)
        {
            var message = $"Unable to read module id from '{projectInfo.TargetPath}'.";
            return new FSharpManagedUpdateResult(
                FSharpManagedUpdateStatus.RestartRequired,
                [],
                [new FSharpManagedUpdateIssue(projectInfo.ProjectPath, message)]);
        }

        if (_trace)
        {
            if (moduleIdBeforeCompile == null)
            {
                _logger.LogDebug(
                    "F# target module id for '{ProjectPath}' was unavailable before forced compile; post-compile id={ModuleId}.",
                    projectInfo.ProjectPath,
                    moduleIdAfterCompile);
            }
            else if (moduleIdAfterCompile == null)
            {
                _logger.LogDebug(
                    "F# target module id unavailable after forced compile for '{ProjectPath}'; using pre-compile id={ModuleId}.",
                    projectInfo.ProjectPath,
                    moduleIdBeforeCompile.Value);
            }
            else if (moduleIdBeforeCompile != moduleIdAfterCompile.Value)
            {
                _logger.LogDebug(
                    "F# target module id changed after forced compile for '{ProjectPath}': before={BeforeModuleId}, after={AfterModuleId}; targeting loaded module id={TargetModuleId}.",
                    projectInfo.ProjectPath,
                    moduleIdBeforeCompile.Value,
                    moduleIdAfterCompile.Value,
                    targetModuleId.Value);
            }
            else
            {
                _logger.LogDebug(
                    "F# target module id for '{ProjectPath}' is stable across forced compile: {ModuleId}.",
                    projectInfo.ProjectPath,
                    targetModuleId.Value);
            }
        }

        var emit = EmitDeltaCore(host, projectInput!, cancellationToken);
        if (!emit.IsSuccess)
        {
            var mappedStatus = MapErrorStatus(emit.ErrorCase);

            if (emit.ErrorCase == "NoActiveSession" && allowSessionReset)
            {
                if (_trace)
                {
                    _logger.LogDebug("F# hot reload session went inactive for '{ProjectPath}', restarting session.", projectInfo.ProjectPath);
                }

                EndSession();
                if (EnsureSession(host, projectInfo, cancellationToken, out projectInput, out var retryStatus, out var retryMessage))
                {
                    emit = EmitDeltaCore(host, projectInput!, cancellationToken);
                    if (emit.IsSuccess)
                    {
                        mappedStatus = FSharpManagedUpdateStatus.ReadyToApply;
                    }
                    else
                    {
                        mappedStatus = MapErrorStatus(emit.ErrorCase);
                    }
                }
                else
                {
                    return new FSharpManagedUpdateResult(
                        retryStatus,
                        [],
                        [new FSharpManagedUpdateIssue(projectInfo.ProjectPath, retryMessage)]);
                }
            }

            if (mappedStatus == FSharpManagedUpdateStatus.NoChanges)
            {
                if (!changedSourceFiles.IsEmpty)
                {
                    var message = "F# managed update produced no semantic delta for edited source files.";

                    if (_trace)
                    {
                        _logger.LogDebug(
                            "{Message} Project='{ProjectPath}', Files='{ChangedFiles}'",
                            message,
                            projectInfo.ProjectPath,
                            string.Join(", ", changedSourceFiles));
                    }
                }
                else if (!changedDependencyFiles.IsEmpty && _trace)
                {
                    _logger.LogDebug(
                        "F# managed update produced no semantic delta for edited dependency files. Project='{ProjectPath}', Files='{ChangedFiles}'",
                        projectInfo.ProjectPath,
                        string.Join(", ", changedDependencyFiles));
                }

                // Roslyn parity: source edits with insignificant/no semantic changes stay in NoChangesToApply
                // and should not force restart/rebuild of the running process.
                return new FSharpManagedUpdateResult(FSharpManagedUpdateStatus.NoChanges, [], []);
            }

            if (mappedStatus == FSharpManagedUpdateStatus.Blocked)
            {
                _logger.Log(MessageDescriptor.UnableToApplyChanges);
                if (!string.IsNullOrEmpty(emit.ErrorText))
                {
                    _logger.LogWarning("F# compilation blocked hot reload: {Message}", emit.ErrorText);
                }
            }
            else
            {
                // Surface the rude-edit reason to the user (the structured FSHRDL id + message),
                // not just at Debug, so an edit that forces a restart explains why. Mirrors how the
                // C# path reports rude edits rather than silently restarting.
                if (!string.IsNullOrEmpty(emit.ErrorText))
                {
                    _logger.LogWarning(
                        "F# hot reload cannot apply the edit; a restart is required: {Message} Learn more: {HelpLink}",
                        emit.ErrorText,
                        RudeEditHelpLink);
                }
                else
                {
                    _logger.LogDebug(
                        "F# managed hot reload requires restart for '{ProjectPath}': {ErrorCase}",
                        projectInfo.ProjectPath,
                        emit.ErrorCase);
                }
            }

            // Session-object mode keeps the module id recorded at baseline capture while the
            // process keeps running (a blocked emit does not change the loaded module); the
            // legacy path keeps its historical reset-on-blocked behavior.
            if (mappedStatus == FSharpManagedUpdateStatus.RestartRequired ||
                (mappedStatus == FSharpManagedUpdateStatus.Blocked && _sessionObject == null))
            {
                _runtimeModuleIds = _runtimeModuleIds.Remove(projectInfo.ProjectId);
            }

            if (mappedStatus == FSharpManagedUpdateStatus.RestartRequired)
            {
                // The watch rebuilds and restarts the project; drop it from the session object so
                // the next edit recaptures the baseline (and its module id) from the rebuilt
                // output the new process actually loads.
                _sessionObjectProjects = _sessionObjectProjects.Remove(projectInfo.ProjectId);
            }

            return new FSharpManagedUpdateResult(
                mappedStatus,
                [],
                [new FSharpManagedUpdateIssue(projectInfo.ProjectPath, emit.ErrorText)]);
        }

        var update = host.CreateManagedUpdate(targetModuleId.Value, emit.Value!);
        if (update == null)
        {
            return new FSharpManagedUpdateResult(
                FSharpManagedUpdateStatus.RestartRequired,
                [],
                [new FSharpManagedUpdateIssue(projectInfo.ProjectPath, "Unable to decode F# delta payload.")]);
        }

        if (_trace)
        {
            _logger.LogDebug(
                "F# managed delta ready for '{ProjectPath}' (metadata={MetadataBytes} il={IlBytes} pdb={PdbBytes}).",
                projectInfo.ProjectPath,
                update.Value.MetadataDelta.Length,
                update.Value.ILDelta.Length,
                update.Value.PdbDelta.Length);
        }

        _runtimeModuleIds = _runtimeModuleIds.SetItem(projectInfo.ProjectId, targetModuleId.Value);

        return new FSharpManagedUpdateResult(
            FSharpManagedUpdateStatus.ReadyToApply,
            [new FSharpManagedUpdate(projectInfo.ProjectPath, update.Value)],
            []);
    }
#pragma warning restore CS1998

    private ImmutableArray<ProjectInstanceId> GetChangedFSharpProjects(
        IReadOnlyList<ChangedFile> changedFiles,
        ImmutableDictionary<string, ImmutableArray<RunningProject>> runningProjects)
    {
        // The session object emits per-project deltas, so an edit to an F# library that is
        // loaded into a running process (but is not itself a running project) is matched to the
        // library project itself. The legacy single-session surface can only target the running
        // project, so it keeps the running-projects-only matching.
        var includeNonRunningProjects = _host?.SupportsSessionObject == true;

        var matchedProjects = ImmutableArray.CreateBuilder<ProjectInstanceId>();

        foreach (var projectInfo in _projects.Values
                     .OrderBy(static project => project.ProjectPath, StringComparer.Ordinal)
                     .ThenBy(static project => project.TargetFramework, StringComparer.OrdinalIgnoreCase))
        {
            if (!includeNonRunningProjects && !runningProjects.ContainsKey(projectInfo.ProjectPath))
            {
                continue;
            }

            var matched = changedFiles.Any(file =>
            {
                var filePath = file.Item.FilePath;
                var isSourceChange = IsFSharpSourcePath(filePath);
                var isDependencyChange = IsManagedDependencyCandidatePath(filePath);
                if (!isSourceChange && !isDependencyChange)
                {
                    return false;
                }

                var containingProjectMatch = file.Item.ContainingProjectPaths.Any(path =>
                    PathUtilities.OSSpecificPathComparer.Equals(path, projectInfo.ProjectPath));
                var dependencyMatch = isDependencyChange && IsCommandLineDependencyPath(filePath, projectInfo);
                var directoryFallbackMatch = IsFileWithinProjectDirectory(filePath, projectInfo.ProjectPath);

                if (_trace && (containingProjectMatch || dependencyMatch || directoryFallbackMatch))
                {
                    _logger.LogDebug(
                        "F# changed file '{FilePath}' matched project '{ProjectPath}' (containing={ContainingMatch}, dependency={DependencyMatch}, directory={DirectoryMatch}).",
                        filePath,
                        projectInfo.ProjectPath,
                        containingProjectMatch,
                        dependencyMatch,
                        directoryFallbackMatch);
                }

                return containingProjectMatch || dependencyMatch || directoryFallbackMatch;
            });

            if (matched)
            {
                matchedProjects.Add(projectInfo.ProjectId);
            }
        }

        if (_trace && matchedProjects.Count == 0)
        {
            var candidateFiles = changedFiles
                .Where(file =>
                {
                    var candidatePath = file.Item.FilePath;
                    return IsFSharpSourcePath(candidatePath) || IsManagedDependencyCandidatePath(candidatePath);
                })
                .Select(file => file.Item.FilePath)
                .ToImmutableArray();

            if (!candidateFiles.IsEmpty)
            {
                _logger.LogDebug(
                    "F# change matching failed. Candidates=[{ChangedFiles}] RunningProjects=[{RunningProjects}] KnownProjects=[{KnownProjects}].",
                    string.Join(", ", candidateFiles),
                    string.Join(", ", runningProjects.Keys.OrderBy(static key => key)),
                    string.Join(", ", _projects.Keys.Select(static project => project.ProjectPath).OrderBy(static key => key)));
            }
        }

        return matchedProjects.ToImmutable();
    }

    private ImmutableArray<string> GetChangedDependencyFilesForProject(
        IReadOnlyList<ChangedFile> changedFiles,
        FSharpProjectInfo projectInfo)
    {
        var changedDependencyFiles = new HashSet<string>(PathUtilities.OSSpecificPathComparer);

        foreach (var changedFile in changedFiles)
        {
            var filePath = changedFile.Item.FilePath;
            if (!IsManagedDependencyCandidatePath(filePath))
            {
                continue;
            }

            var isProjectMatchFromContainingPaths =
                changedFile.Item.ContainingProjectPaths.Any(containingProjectPath =>
                    PathUtilities.OSSpecificPathComparer.Equals(containingProjectPath, projectInfo.ProjectPath));
            var isCommandLineDependency = IsCommandLineDependencyPath(filePath, projectInfo);
            var isProjectDirectoryDependency = IsFileWithinProjectDirectory(filePath, projectInfo.ProjectPath);
            if (!isProjectMatchFromContainingPaths && !isCommandLineDependency && !isProjectDirectoryDependency)
            {
                continue;
            }

            if (TryNormalizeFullPath(filePath, out var normalizedFilePath))
            {
                changedDependencyFiles.Add(normalizedFilePath);
            }
            else
            {
                changedDependencyFiles.Add(filePath);
            }
        }

        return [.. changedDependencyFiles];
    }

    private ImmutableArray<string> GetChangedSourceFilesForProject(
        IReadOnlyList<ChangedFile> changedFiles,
        ProjectInstanceId projectId)
    {
        var changedSourceFiles = new HashSet<string>(PathUtilities.OSSpecificPathComparer);

        foreach (var changedFile in changedFiles)
        {
            var filePath = changedFile.Item.FilePath;
            if (!IsFSharpSourcePath(filePath))
            {
                continue;
            }

            var isProjectMatchFromContainingPaths =
                changedFile.Item.ContainingProjectPaths.Any(containingProjectPath =>
                    PathUtilities.OSSpecificPathComparer.Equals(containingProjectPath, projectId.ProjectPath));

            if (isProjectMatchFromContainingPaths || IsFileWithinProjectDirectory(filePath, projectId.ProjectPath))
            {
                try
                {
                    changedSourceFiles.Add(Path.GetFullPath(filePath));
                }
                catch
                {
                    changedSourceFiles.Add(filePath);
                }
            }
        }

        return [.. changedSourceFiles];
    }

    private static bool IsFileWithinProjectDirectory(string filePath, string projectPath)
    {
        try
        {
            var projectDirectory = Path.GetDirectoryName(projectPath);
            if (projectDirectory == null)
            {
                return false;
            }

            var normalizedFilePath = Path.GetFullPath(filePath);
            var normalizedProjectDirectory = PathUtilities.EnsureTrailingSlash(Path.GetFullPath(projectDirectory));
            return normalizedFilePath.StartsWith(normalizedProjectDirectory, PathUtilities.OSSpecificPathComparison);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Makes the compiler-service session ready to emit a delta for the given project and
    /// returns the project input (snapshot or options) to emit with. In session-object mode the
    /// ONE session is created on first use and the project is added to it; the legacy checker
    /// surface keeps its single-active-project switching behavior unchanged.
    /// </summary>
    private bool EnsureSession(
        FSharpReflectionHost host,
        FSharpProjectInfo projectInfo,
        CancellationToken cancellationToken,
        out object? projectInput,
        out FSharpManagedUpdateStatus status,
        out string? message)
    {
        if (host.SupportsSessionObject)
        {
            return EnsureSessionObject(host, projectInfo, cancellationToken, out projectInput, out status, out message);
        }

        projectInput = null;
        status = FSharpManagedUpdateStatus.NoChanges;
        message = null;

        var currentCapabilities = GetRuntimeCapabilities();

        if (_activeProject is { } activeProject &&
            _activeProjectInput != null &&
            activeProject.Equals(projectInfo.ProjectId))
        {
            // The prestarted session is created before any agent reports its capabilities
            // (empty set => baseline-only classification). Update the live session in place
            // once the real set is available — never restart it: a restart would re-capture
            // the baseline from sources that already contain the pending edit.
            if (!CapabilitySetsEqual(_activeSessionCapabilities, currentCapabilities))
            {
                var updated = host.TryUpdateSessionCapabilities(currentCapabilities);

                if (_trace)
                {
                    _logger.LogDebug(
                        updated switch
                        {
                            true => "F# hot reload session capabilities refreshed ({Capabilities}).",
                            false => "F# hot reload session capability refresh failed; keeping previous set ({Capabilities}).",
                            null => "Loaded F# compiler service does not support capability refresh; keeping session capabilities ({Capabilities}).",
                        },
                        string.Join(" ", currentCapabilities));
                }

                // Record regardless of outcome so an unsupported/failed refresh is not retried
                // on every edit.
                _activeSessionCapabilities = currentCapabilities;
            }

            projectInput = _activeProjectInput;
            return true;
        }

        EndSession();

        if (!_cachedProjectInputs.TryGetValue(projectInfo.ProjectId, out projectInput) &&
            !host.TryCreateProjectInput(projectInfo, out projectInput, out message))
        {
            status = FSharpManagedUpdateStatus.RestartRequired;
            return false;
        }

        var start = host.StartSession(projectInput!, currentCapabilities, cancellationToken);
        if (!start.IsSuccess)
        {
            status = MapErrorStatus(start.ErrorCase);
            message = start.ErrorText;

            if (status == FSharpManagedUpdateStatus.Blocked)
            {
                _logger.Log(MessageDescriptor.UnableToApplyChanges);
            }

            return false;
        }

        _activeProject = projectInfo.ProjectId;
        _activeProjectInput = projectInput;
        _activeSessionCapabilities = currentCapabilities;
        _cachedProjectInputs = _cachedProjectInputs.SetItem(projectInfo.ProjectId, projectInput!);
        return true;
    }

    /// <summary>
    /// Session-object mode: creates the watch-session-wide FSharpHotReloadSession on first use,
    /// keeps its capability set current, and captures the project's baseline into it (AddProject)
    /// the first time the project is seen. Never switches sessions between projects — every
    /// tracked project keeps its own committed baseline and generation chain inside the session.
    /// </summary>
    private bool EnsureSessionObject(
        FSharpReflectionHost host,
        FSharpProjectInfo projectInfo,
        CancellationToken cancellationToken,
        out object? projectInput,
        out FSharpManagedUpdateStatus status,
        out string? message)
    {
        projectInput = null;
        status = FSharpManagedUpdateStatus.NoChanges;
        message = null;

        var currentCapabilities = GetRuntimeCapabilities();

        if (_sessionObject == null)
        {
            if (!host.TryCreateSession(currentCapabilities, out _sessionObject, out message))
            {
                status = FSharpManagedUpdateStatus.RestartRequired;
                return false;
            }

            _sessionObjectProjects = [];
            _activeSessionCapabilities = currentCapabilities;

            if (_trace)
            {
                _logger.LogDebug(
                    "F# hot reload session object created (capabilities: {Capabilities}).",
                    currentCapabilities.IsDefaultOrEmpty ? "<none>" : string.Join(" ", currentCapabilities));
            }
        }
        else if (!CapabilitySetsEqual(_activeSessionCapabilities, currentCapabilities))
        {
            // The session is created before any agent reports its capabilities (empty set =>
            // baseline-only classification). Replace the live session's capability set in place
            // once the real set is available — never recreate the session: that would drop every
            // project baseline and recapture from sources that already contain pending edits.
            var updated = host.TrySessionUpdateCapabilities(_sessionObject, currentCapabilities);

            if (_trace)
            {
                _logger.LogDebug(
                    updated
                        ? "F# hot reload session capabilities refreshed ({Capabilities})."
                        : "F# hot reload session capability refresh failed; keeping previous set ({Capabilities}).",
                    string.Join(" ", currentCapabilities));
            }

            // Record regardless of outcome so a failed refresh is not retried on every edit.
            _activeSessionCapabilities = currentCapabilities;
        }

        if (!_cachedProjectInputs.TryGetValue(projectInfo.ProjectId, out projectInput) &&
            !host.TryCreateProjectInput(projectInfo, out projectInput, out message))
        {
            status = FSharpManagedUpdateStatus.RestartRequired;
            return false;
        }

        if (!_sessionObjectProjects.Contains(projectInfo.ProjectId))
        {
            // AddProject captures the project's committed baseline from the intermediate assembly
            // on disk. At this point it is still the pre-edit build (forced compilation of the
            // edited sources happens after EnsureSession), and at generation 0 the intermediate
            // assembly is a byte copy of the bin output the running process loaded, so the baseline
            // matches the loaded module.
            var add = host.SessionAddProject(_sessionObject!, projectInput!, projectInfo.IntermediateAssemblyPath, cancellationToken);
            if (!add.IsSuccess)
            {
                status = MapErrorStatus(add.ErrorCase);
                message = add.ErrorText;

                if (status == FSharpManagedUpdateStatus.Blocked)
                {
                    _logger.Log(MessageDescriptor.UnableToApplyChanges);
                }

                return false;
            }

            _sessionObjectProjects = _sessionObjectProjects.Add(projectInfo.ProjectId);
            _cachedProjectInputs = _cachedProjectInputs.SetItem(projectInfo.ProjectId, projectInput!);

            // Record the module id of the bin output the running process actually loaded. Per-edit
            // compiles now target the intermediate assembly only and never rewrite this bin file,
            // so its MVID stays the loaded module's across the whole session; emits keep targeting
            // this baseline id.
            if (TryGetModuleVersionId(projectInfo.TargetPath) is { } baselineModuleId)
            {
                _runtimeModuleIds = _runtimeModuleIds.SetItem(projectInfo.ProjectId, baselineModuleId);
            }

            if (_trace)
            {
                _logger.LogDebug("F# hot reload session now tracks '{ProjectPath}'.", projectInfo.ProjectPath);
            }
        }

        return true;
    }

    /// <summary>
    /// Emits the per-project delta through the session object when available, otherwise through
    /// the legacy process-wide checker session.
    /// </summary>
    private FSharpReflectionHost.FSharpInvocationResult EmitDeltaCore(FSharpReflectionHost host, object projectInput, CancellationToken cancellationToken)
        => host.SupportsSessionObject && _sessionObject is { } sessionObject
            ? host.SessionEmitDelta(sessionObject, projectInput, cancellationToken)
            : host.EmitDelta(projectInput, cancellationToken);

    private static bool CapabilitySetsEqual(ImmutableArray<string> left, ImmutableArray<string> right)
        => left.Length == right.Length &&
           left.OrderBy(static c => c, StringComparer.Ordinal)
               .SequenceEqual(right.OrderBy(static c => c, StringComparer.Ordinal), StringComparer.Ordinal);

    private bool TryGetHost(FSharpProjectInfo projectInfo, out FSharpReflectionHost host, out string? message)
    {
        host = _host!;
        message = null;

        if (_host != null)
        {
            host = _host;
            return true;
        }

        if (FSharpReflectionHost.TryCreate(projectInfo, _logger, _trace, out var createdHost, out message))
        {
            _host = createdHost;
            host = createdHost;
            return true;
        }

        return false;
    }

    private static FSharpManagedUpdateStatus MapErrorStatus(string? errorCase)
        => errorCase switch
        {
            "NoChanges" => FSharpManagedUpdateStatus.NoChanges,
            "CompilationFailed" => FSharpManagedUpdateStatus.Blocked,
            _ => FSharpManagedUpdateStatus.RestartRequired,
        };

    private static Guid? TryGetModuleVersionId(string assemblyPath)
    {
        try
        {
            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream);
            var metadataReader = peReader.GetMetadataReader();
            return metadataReader.GetGuid(metadataReader.GetModuleDefinition().Mvid);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsFSharpSourcePath(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".fs", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".fsi", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".fsx", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsManagedDependencyCandidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || IsFSharpSourcePath(path))
        {
            return false;
        }

        var fileName = Path.GetFileName(path);
        if (fileName.Length == 0)
        {
            return false;
        }

        if (fileName.EndsWith("~", StringComparison.Ordinal) ||
            fileName.StartsWith("~$", StringComparison.Ordinal) ||
            (fileName.StartsWith("#", StringComparison.Ordinal) && fileName.EndsWith("#", StringComparison.Ordinal)))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension))
        {
            return false;
        }

        if (string.Equals(extension, ".fsproj", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".props", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".targets", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".proj", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(extension, ".swp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".swo", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".swx", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".tmp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".temp", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static bool IsCommandLineDependencyPath(string filePath, FSharpProjectInfo projectInfo)
    {
        if (!TryNormalizeFullPath(filePath, out var normalizedFilePath))
        {
            return false;
        }

        var projectDirectory = Path.GetDirectoryName(projectInfo.ProjectPath) ?? Directory.GetCurrentDirectory();
        foreach (var commandLineArg in projectInfo.CommandLineArgs)
        {
            if (!TryGetCommandLineDependencyPath(commandLineArg, projectDirectory, out var dependencyPath))
            {
                continue;
            }

            if (PathUtilities.OSSpecificPathComparer.Equals(normalizedFilePath, dependencyPath))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetCommandLineDependencyPath(string commandLineArg, string projectDirectory, out string? dependencyPath)
    {
        dependencyPath = null;

        if (string.IsNullOrWhiteSpace(commandLineArg))
        {
            return false;
        }

        var arg = commandLineArg.Trim().Trim('"');
        if (arg.Length == 0)
        {
            return false;
        }

        if (arg.StartsWith("-r:", StringComparison.OrdinalIgnoreCase) ||
            arg.StartsWith("--reference:", StringComparison.OrdinalIgnoreCase) ||
            arg.StartsWith("-o:", StringComparison.OrdinalIgnoreCase) ||
            arg.StartsWith("--out:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (arg.StartsWith("-", StringComparison.Ordinal))
        {
            if (!TryExtractValueFromKnownPrefix(
                    arg,
                    ["--resource:", "-resource:", "--res:", "-res:", "--win32res:", "--keyfile:", "--load:", "--use:"],
                    out var optionValue,
                    out var matchedPrefix))
            {
                return false;
            }

            if (matchedPrefix is "--resource:" or "-resource:" or "--res:" or "-res:" or "--win32res:")
            {
                // F# resource switches can include metadata after commas, e.g. --resource:path,logicalName.
                // We only need the physical file path for dependency invalidation matching.
                var commaIndex = optionValue!.IndexOf(',');
                if (commaIndex >= 0)
                {
                    optionValue = optionValue[..commaIndex];
                }
            }

            return TryNormalizeDependencyPath(optionValue!, projectDirectory, out dependencyPath);
        }

        return TryNormalizeDependencyPath(arg, projectDirectory, out dependencyPath);
    }

    private static bool TryExtractValueFromKnownPrefix(
        string value,
        IReadOnlyList<string> prefixes,
        out string? extractedValue,
        out string? matchedPrefix)
    {
        foreach (var prefix in prefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && value.Length > prefix.Length)
            {
                extractedValue = value[prefix.Length..];
                matchedPrefix = prefix;
                return true;
            }
        }

        extractedValue = null;
        matchedPrefix = null;
        return false;
    }

    private static bool TryNormalizeDependencyPath(string path, string projectDirectory, out string? normalizedPath)
    {
        normalizedPath = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var candidatePath = path.Trim().Trim('"');
        if (candidatePath.Length == 0)
        {
            return false;
        }

        if (!Path.IsPathRooted(candidatePath))
        {
            candidatePath = Path.Combine(projectDirectory, candidatePath);
        }

        return TryNormalizeFullPath(candidatePath, out normalizedPath);
    }

    private static bool TryNormalizeFullPath(string path, out string normalizedPath)
    {
        normalizedPath = path;
        try
        {
            normalizedPath = Path.GetFullPath(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsTraceEnabled()
    {
        var value = Environment.GetEnvironmentVariable("DOTNET_WATCH_TRACE_FSHARP_HOTRELOAD");
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// DOTNET_WATCH_FSHARP_HOTRELOAD=0 (or false) disables the F# hot reload bridge entirely;
    /// any other value, including unset, leaves it enabled. See <see cref="_disabled"/>.
    /// </summary>
    private static bool IsDisabled()
    {
        var value = Environment.GetEnvironmentVariable("DOTNET_WATCH_FSHARP_HOTRELOAD");
        return string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// FSHARP_HOTRELOAD_INPROCESS_COMPILE=1 (or true) opts into the experimental FCS in-process
    /// compile: the hot reload session refreshes the obj assembly itself during EmitDelta, so the
    /// external per-edit "dotnet build -t:Compile" is skipped. The same variable gates the FCS side,
    /// so one setting flips the whole chain. Unset or any other value keeps the external build.
    /// </summary>
    private static bool IsInProcessCompileEnabled()
    {
        var value = Environment.GetEnvironmentVariable("FSHARP_HOTRELOAD_INPROCESS_COMPILE");
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryCompileProjectOutput(FSharpProjectInfo projectInfo, out string? message)
    {
        message = null;

        if (IsInProcessCompileEnabled())
        {
            // The FCS session compiles in-process during EmitDelta (same flag); running the
            // external build too would pay the redundant MSBuild+fsc the flag exists to remove.
            return true;
        }

        try
        {
            var projectDirectory = Path.GetDirectoryName(projectInfo.ProjectPath) ?? Directory.GetCurrentDirectory();

            var dotnetHostPath =
                Environment.ProcessPath ??
                Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ??
                "dotnet";

            var startInfo = new ProcessStartInfo
            {
                FileName = dotnetHostPath,
                WorkingDirectory = projectDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            // Compile only the intermediate assembly: fsc refreshes obj/<name>.dll and its .pdb,
            // which the hot reload session reads (FSharpProjectInfo.IntermediateAssemblyPath). The
            // full Build target would also copy the result over the bin output the running process
            // has loaded; Windows locks that file against writes while the app runs, so the copy
            // fails and every edit is blocked. -t:Compile leaves the loaded module untouched and
            // works the same on every OS.
            startInfo.ArgumentList.Add("build");
            startInfo.ArgumentList.Add(projectInfo.ProjectPath);
            startInfo.ArgumentList.Add("-t:Compile");
            // Pin the forced compile to the TFM the running app was launched with, the same way the
            // watcher passes --framework to build/run elsewhere. Without this a multi-targeted project
            // compiles every TFM (or the wrong one) instead of the single TFM the hot reload session
            // loaded, so the refreshed obj assembly would not match the baseline the delta chains from.
            if (!string.IsNullOrWhiteSpace(projectInfo.TargetFramework))
            {
                startInfo.ArgumentList.Add("--framework");
                startInfo.ArgumentList.Add(projectInfo.TargetFramework);
            }
            startInfo.ArgumentList.Add("-nologo");
            startInfo.ArgumentList.Add("-consoleLoggerParameters:NoSummary;Verbosity=minimal");
            startInfo.ArgumentList.Add("-p:NuGetInteractive=true");
            // The initial watch build and every forced compile must use identical F# lowering;
            // Microsoft.NET.Sdk.FSharp.targets adds the hot reload compiler flag for this build.
            startInfo.ArgumentList.Add($"-p:{PropertyNames.DotNetWatchBuild}=true");

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                message = $"Failed to start dotnet build for '{projectInfo.ProjectPath}'.";
                return false;
            }

            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                return true;
            }

            var standardOutput = standardOutputTask.GetAwaiter().GetResult();
            var standardError = standardErrorTask.GetAwaiter().GetResult();
            var details = string.IsNullOrWhiteSpace(standardError) ? standardOutput : standardError;
            message = $"dotnet build failed for '{projectInfo.ProjectPath}' (exit code {process.ExitCode}). {details.Trim()}";
            return false;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    private sealed class FSharpReflectionHost
    {
        private readonly ILogger _logger;
        private readonly bool _trace;
        private readonly object _checker;
        private readonly MethodInfo _getProjectOptions;

        // The legacy process-wide checker surface (StartHotReloadSession/EmitHotReloadDelta/
        // EndHotReloadSession/NotifyFileChanged). Current compiler service builds no longer ship
        // it — all four are null in session-object mode, where FSharpHotReloadSession members
        // (via _sessionApi) are the replacements.
        private readonly MethodInfo? _startSession;
        private readonly MethodInfo? _notifyFileChanged;
        private readonly MethodInfo? _emitDelta;
        private readonly MethodInfo? _endSession;

        /// <summary>
        /// FSharpChecker.UpdateHotReloadCapabilities, probed lazily because older compiler
        /// service builds do not expose it. Null after probing means unsupported.
        /// </summary>
        private MethodInfo? _updateCapabilities;
        private bool _updateCapabilitiesProbed;

        /// <summary>
        /// Reflection surface of <c>FSharpHotReloadSession</c> (the session-object API). Non-null
        /// only when the loaded compiler service exposes <c>FSharpChecker.CreateHotReloadSession</c>
        /// and the workspace snapshot bridge is available (the session API consumes project
        /// snapshots). Null means the legacy single-active-project checker surface is used.
        /// </summary>
        private readonly SessionObjectApi? _sessionApi;
        private readonly ImmutableArray<MethodInfo> _invalidateConfigurationMethods;
        private readonly MethodInfo _runSynchronously;
        private readonly bool _useWorkspaceSnapshots;
        private readonly object? _workspaceProjects;
        private readonly object? _workspaceFiles;
        private readonly object? _workspaceQuery;
        private readonly MethodInfo? _workspaceProjectAddOrUpdate;
        private readonly MethodInfo? _workspaceQueryGetProjectSnapshot;
        private readonly MethodInfo? _workspaceFilesEdit;
        private readonly MethodInfo? _workspaceFilesClose;

        private FSharpReflectionHost(
            ILogger logger,
            bool trace,
            object checker,
            MethodInfo getProjectOptions,
            MethodInfo? startSession,
            MethodInfo? notifyFileChanged,
            MethodInfo? emitDelta,
            MethodInfo? endSession,
            SessionObjectApi? sessionApi,
            ImmutableArray<MethodInfo> invalidateConfigurationMethods,
            MethodInfo runSynchronously,
            bool useWorkspaceSnapshots,
            object? workspaceProjects,
            object? workspaceFiles,
            object? workspaceQuery,
            MethodInfo? workspaceProjectAddOrUpdate,
            MethodInfo? workspaceQueryGetProjectSnapshot,
            MethodInfo? workspaceFilesEdit,
            MethodInfo? workspaceFilesClose)
        {
            _logger = logger;
            _trace = trace;
            _checker = checker;
            _getProjectOptions = getProjectOptions;
            _startSession = startSession;
            _notifyFileChanged = notifyFileChanged;
            _emitDelta = emitDelta;
            _endSession = endSession;
            _sessionApi = sessionApi;
            _invalidateConfigurationMethods = invalidateConfigurationMethods;
            _runSynchronously = runSynchronously;
            _useWorkspaceSnapshots = useWorkspaceSnapshots;
            _workspaceProjects = workspaceProjects;
            _workspaceFiles = workspaceFiles;
            _workspaceQuery = workspaceQuery;
            _workspaceProjectAddOrUpdate = workspaceProjectAddOrUpdate;
            _workspaceQueryGetProjectSnapshot = workspaceQueryGetProjectSnapshot;
            _workspaceFilesEdit = workspaceFilesEdit;
            _workspaceFilesClose = workspaceFilesClose;
        }

        public static bool TryCreate(
            FSharpProjectInfo projectInfo,
            ILogger logger,
            bool trace,
            out FSharpReflectionHost host,
            out string? error)
        {
            host = null!;
            error = null;

            var servicePathOverride = Environment.GetEnvironmentVariable("DOTNET_WATCH_FSHARP_COMPILER_SERVICE_PATH");
            var servicePath = servicePathOverride;
            if (string.IsNullOrEmpty(servicePath))
            {
                var compilerDirectory = Path.GetDirectoryName(projectInfo.DotnetFscCompilerPath);
                servicePath = compilerDirectory == null ? null : Path.Combine(compilerDirectory, "FSharp.Compiler.Service.dll");
            }

            if (string.IsNullOrEmpty(servicePath) || !File.Exists(servicePath))
            {
                error = "FSharp.Compiler.Service.dll with hot reload APIs was not found.";
                return false;
            }

            try
            {
                var assembly = Assembly.LoadFrom(servicePath);
                var checkerType = assembly.GetType("FSharp.Compiler.CodeAnalysis.FSharpChecker", throwOnError: true)!;

                var createMethod = checkerType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)
                    ?? throw new MissingMethodException(checkerType.FullName, "Create");

                var createArguments = CreateCheckerArguments(createMethod.GetParameters());
                var checker = createMethod.Invoke(null, createArguments)
                    ?? throw new InvalidOperationException("FSharpChecker.Create returned null.");

                var getProjectOptions = checkerType.GetMethod("GetProjectOptionsFromCommandLineArgs", BindingFlags.Public | BindingFlags.Instance)
                    ?? throw new MissingMethodException(checkerType.FullName, "GetProjectOptionsFromCommandLineArgs");

                var startSessionMethods = checkerType
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(method => method.Name == "StartHotReloadSession")
                    .ToImmutableArray();

                var notifyFileChanged = checkerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(method => method.Name == "NotifyFileChanged" && method.GetParameters().Length >= 2);

                var emitDeltaMethods = checkerType
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(method => method.Name == "EmitHotReloadDelta")
                    .ToImmutableArray();

                // Null on current compiler service builds: the legacy process-wide checker
                // surface was retired in favor of CreateHotReloadSession. Only required when the
                // legacy path ends up selected below.
                var endSession = checkerType.GetMethod("EndHotReloadSession", BindingFlags.Public | BindingFlags.Instance);

                var invalidateConfigurationMethods = checkerType
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(method => method.Name == "InvalidateConfiguration" && method.GetParameters().Length >= 1)
                    .ToImmutableArray();

                var fsharpAsyncType = Type.GetType("Microsoft.FSharp.Control.FSharpAsync, FSharp.Core", throwOnError: true)!;
                var runSynchronously = fsharpAsyncType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .First(method => method.Name == "RunSynchronously" && method.IsGenericMethod && method.GetParameters().Length == 3);

                var startSessionWithOptions =
                    startSessionMethods.FirstOrDefault(HasFirstParameterNamedProjectOptions)
                    ?? startSessionMethods.FirstOrDefault(method => method.GetParameters().Length > 0);
                var emitDeltaWithOptions =
                    emitDeltaMethods.FirstOrDefault(HasFirstParameterNamedProjectOptions)
                    ?? emitDeltaMethods.FirstOrDefault(method => method.GetParameters().Length > 0);

                var projectSnapshotType = assembly.GetType("FSharp.Compiler.CodeAnalysis.ProjectSnapshot+FSharpProjectSnapshot", throwOnError: false);
                var startSessionWithSnapshot = projectSnapshotType == null
                    ? null
                    : startSessionMethods.FirstOrDefault(method => HasFirstParameterType(method, projectSnapshotType));
                var emitDeltaWithSnapshot = projectSnapshotType == null
                    ? null
                    : emitDeltaMethods.FirstOrDefault(method => HasFirstParameterType(method, projectSnapshotType));

                var useWorkspaceSnapshots = false;
                object? workspaceProjects = null;
                object? workspaceFiles = null;
                object? workspaceQuery = null;
                MethodInfo? workspaceProjectAddOrUpdate = null;
                MethodInfo? workspaceQueryGetProjectSnapshot = null;
                MethodInfo? workspaceFilesEdit = null;
                MethodInfo? workspaceFilesClose = null;
                string? workspaceError = null;
                MethodInfo? selectedStartSession;
                MethodInfo? selectedEmitDelta;
                var preferWorkspaceSnapshots = ShouldPreferWorkspaceSnapshots(servicePathOverride);

                // The session-object API (FSharpChecker.CreateHotReloadSession) supersedes the
                // process-wide single-session checker surface. It consumes project snapshots, so
                // it requires the workspace bridge regardless of the snapshot-mode preference.
                // DOTNET_WATCH_FSHARP_USE_SESSION_OBJECT=0 is a safety valve forcing the legacy
                // single-active-project path even when the new API is available — honored only
                // while the loaded compiler service still ships that legacy surface.
                var legacySurfaceAvailable = startSessionMethods.Length > 0 && emitDeltaMethods.Length > 0 && endSession != null;
                var sessionApi = SessionObjectApi.TryCreate(checkerType);
                if (sessionApi != null && !ShouldUseSessionObject())
                {
                    if (legacySurfaceAvailable)
                    {
                        sessionApi = null;
                    }
                    else
                    {
                        // The valve has nothing to fall back to; ignore it loudly rather than
                        // failing host creation (which would silently degrade to restarts).
                        logger.LogDebug(
                            "Ignoring DOTNET_WATCH_FSHARP_USE_SESSION_OBJECT=0: the loaded F# compiler service no longer exposes the legacy hot reload checker surface (StartHotReloadSession/EmitHotReloadDelta/EndHotReloadSession). Proceeding with the session-object API.");
                    }
                }

                if ((preferWorkspaceSnapshots || sessionApi != null) &&
                    // Session-object mode drives emit through FSharpHotReloadSession members and
                    // does not need the legacy snapshot overloads (current compiler service
                    // builds no longer ship them).
                    (sessionApi != null || (startSessionWithSnapshot != null && emitDeltaWithSnapshot != null)) &&
                    TryCreateWorkspaceBridge(
                        assembly,
                        checkerType,
                        checker,
                        out workspaceProjects,
                        out workspaceFiles,
                        out workspaceQuery,
                        out workspaceProjectAddOrUpdate,
                        out workspaceQueryGetProjectSnapshot,
                        out workspaceFilesEdit,
                        out workspaceFilesClose,
                        out workspaceError))
                {
                    useWorkspaceSnapshots = true;
                    selectedStartSession = startSessionWithSnapshot;
                    selectedEmitDelta = emitDeltaWithSnapshot;

                    if (trace)
                    {
                        logger.LogDebug(
                            sessionApi != null
                                ? "F# managed hot reload is using the session-object API over the workspace snapshot bridge."
                                : "F# managed hot reload is using workspace snapshot bridge.");
                    }
                }
                else
                {
                    // Without snapshot inputs the session-object API cannot be used; fall back to
                    // the legacy single-active-project checker surface.
                    sessionApi = null;
                    selectedStartSession = startSessionWithOptions
                        ?? throw new MissingMethodException(checkerType.FullName, "StartHotReloadSession(projectOptions)");
                    selectedEmitDelta = emitDeltaWithOptions
                        ?? throw new MissingMethodException(checkerType.FullName, "EmitHotReloadDelta(projectOptions)");

                    if (endSession == null)
                    {
                        throw new MissingMethodException(checkerType.FullName, "EndHotReloadSession");
                    }

                    if (trace && !string.IsNullOrEmpty(workspaceError))
                    {
                        logger.LogDebug("F# workspace snapshot bridge unavailable, using project-options path: {Message}", workspaceError);
                    }
                    else if (trace && !preferWorkspaceSnapshots && startSessionWithSnapshot != null && emitDeltaWithSnapshot != null)
                    {
                        logger.LogDebug(
                            "F# workspace snapshot bridge is disabled by default for bundled compiler service builds. Set DOTNET_WATCH_FSHARP_USE_WORKSPACE_SNAPSHOTS=1 to force-enable it.");
                    }
                }

                host = new FSharpReflectionHost(
                    logger,
                    trace,
                    checker,
                    getProjectOptions,
                    selectedStartSession,
                    notifyFileChanged,
                    selectedEmitDelta,
                    endSession,
                    sessionApi,
                    invalidateConfigurationMethods,
                    runSynchronously,
                    useWorkspaceSnapshots,
                    workspaceProjects,
                    workspaceFiles,
                    workspaceQuery,
                    workspaceProjectAddOrUpdate,
                    workspaceQueryGetProjectSnapshot,
                    workspaceFilesEdit,
                    workspaceFilesClose);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool ShouldUseSessionObject()
        {
            var overrideValue = Environment.GetEnvironmentVariable("DOTNET_WATCH_FSHARP_USE_SESSION_OBJECT");
            return string.IsNullOrEmpty(overrideValue) ||
                   (!string.Equals(overrideValue, "0", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(overrideValue, "false", StringComparison.OrdinalIgnoreCase));
        }

        private static bool ShouldPreferWorkspaceSnapshots(string? servicePathOverride)
        {
            var overrideValue = Environment.GetEnvironmentVariable("DOTNET_WATCH_FSHARP_USE_WORKSPACE_SNAPSHOTS");
            if (!string.IsNullOrEmpty(overrideValue))
            {
                return string.Equals(overrideValue, "1", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(overrideValue, "true", StringComparison.OrdinalIgnoreCase);
            }

            // Default to project-options mode for bundled SDK bits and enable workspace snapshots
            // when the user explicitly points watch at a custom FSharp.Compiler.Service build.
            return !string.IsNullOrEmpty(servicePathOverride);
        }

        private static bool TryCreateWorkspaceBridge(
            Assembly assembly,
            Type checkerType,
            object checker,
            out object? workspaceProjects,
            out object? workspaceFiles,
            out object? workspaceQuery,
            out MethodInfo? workspaceProjectAddOrUpdate,
            out MethodInfo? workspaceQueryGetProjectSnapshot,
            out MethodInfo? workspaceFilesEdit,
            out MethodInfo? workspaceFilesClose,
            out string? error)
        {
            workspaceProjects = null;
            workspaceFiles = null;
            workspaceQuery = null;
            workspaceProjectAddOrUpdate = null;
            workspaceQueryGetProjectSnapshot = null;
            workspaceFilesEdit = null;
            workspaceFilesClose = null;
            error = null;

            var workspaceType = assembly.GetType("FSharp.Compiler.CodeAnalysis.Workspace.FSharpWorkspace", throwOnError: false);
            if (workspaceType == null)
            {
                error = "FSharpWorkspace type was not found in FSharp.Compiler.Service.";
                return false;
            }

            var workspaceCtor = workspaceType.GetConstructor([checkerType]) ?? workspaceType.GetConstructor(Type.EmptyTypes);
            if (workspaceCtor == null)
            {
                error = "FSharpWorkspace constructor was not found.";
                return false;
            }

            var workspace = workspaceCtor.GetParameters().Length == 1
                ? workspaceCtor.Invoke([checker])
                : workspaceCtor.Invoke([]);

            var projects = workspaceType.GetProperty("Projects", BindingFlags.Public | BindingFlags.Instance)?.GetValue(workspace);
            var files = workspaceType.GetProperty("Files", BindingFlags.Public | BindingFlags.Instance)?.GetValue(workspace);
            var query = workspaceType.GetProperty("Query", BindingFlags.Public | BindingFlags.Instance)?.GetValue(workspace);

            if (projects == null || files == null || query == null)
            {
                error = "FSharpWorkspace properties (Projects/Files/Query) were not available.";
                return false;
            }

            workspaceProjectAddOrUpdate = projects.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(method =>
                {
                    if (method.Name != "AddOrUpdate")
                    {
                        return false;
                    }

                    var parameters = method.GetParameters();
                    return parameters.Length == 3 &&
                           parameters[0].ParameterType == typeof(string) &&
                           parameters[1].ParameterType == typeof(string);
                });

            workspaceQueryGetProjectSnapshot = query.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(method => method.Name == "GetProjectSnapshot" && method.GetParameters().Length == 1);

            workspaceFilesEdit = files.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(method =>
                {
                    if (method.Name != "Edit")
                    {
                        return false;
                    }

                    var parameters = method.GetParameters();
                    return parameters.Length == 2 &&
                           parameters[0].ParameterType == typeof(Uri) &&
                           parameters[1].ParameterType == typeof(string);
                });

            workspaceFilesClose = files.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(method =>
                {
                    if (method.Name != "Close")
                    {
                        return false;
                    }

                    var parameters = method.GetParameters();
                    return parameters.Length == 1 && parameters[0].ParameterType == typeof(Uri);
                });

            if (workspaceProjectAddOrUpdate == null ||
                workspaceQueryGetProjectSnapshot == null ||
                (workspaceFilesEdit == null && workspaceFilesClose == null))
            {
                error = "FSharpWorkspace method surface is missing required members.";
                return false;
            }

            workspaceProjects = projects;
            workspaceFiles = files;
            workspaceQuery = query;
            return true;
        }

        private static bool HasFirstParameterNamedProjectOptions(MethodInfo method)
        {
            var parameters = method.GetParameters();
            return parameters.Length > 0 &&
                   string.Equals(parameters[0].Name, "projectOptions", StringComparison.Ordinal);
        }

        private static bool HasFirstParameterType(MethodInfo method, Type parameterType)
        {
            var parameters = method.GetParameters();
            return parameters.Length > 0 && parameters[0].ParameterType == parameterType;
        }

        public bool TryCreateProjectInput(FSharpProjectInfo projectInfo, out object? projectInput, out string? error)
            => _useWorkspaceSnapshots
                ? TryCreateWorkspaceSnapshotInput(projectInfo, out projectInput, out error)
                : TryCreateProjectOptionsInput(projectInfo, out projectInput, out error);

        public bool TryRefreshProjectInput(FSharpProjectInfo projectInfo, object currentProjectInput, out object? refreshedProjectInput, out string? error)
        {
            if (_useWorkspaceSnapshots)
            {
                return TryCreateWorkspaceSnapshotInput(projectInfo, out refreshedProjectInput, out error);
            }

            return TryCreateProjectOptionsInput(projectInfo, out refreshedProjectInput, out error);
        }

        public FSharpInvocationResult StartSession(object projectInput, ImmutableArray<string> capabilities, CancellationToken cancellationToken)
            => _startSession == null
                ? new FSharpInvocationResult(false, null, null, "The loaded F# compiler service does not expose StartHotReloadSession; use the session-object API.")
                : InvokeResult(_checker, _startSession, CreateStartSessionArguments(_startSession.GetParameters(), projectInput, capabilities), cancellationToken);

        public bool SupportsSessionObject => _sessionApi != null;

        /// <summary>
        /// Creates one FSharpHotReloadSession for the whole watch session. Per-project committed
        /// baselines and generation chains live inside the session object; projects are captured
        /// into it via <see cref="SessionAddProject"/> and edits emitted via
        /// <see cref="SessionEmitDelta"/>.
        /// </summary>
        public bool TryCreateSession(ImmutableArray<string> capabilities, out object? session, out string? error)
        {
            session = null;
            error = null;

            if (_sessionApi == null)
            {
                error = "The loaded F# compiler service does not expose CreateHotReloadSession.";
                return false;
            }

            try
            {
                var parameters = _sessionApi.Create.GetParameters();
                var arguments = new object?[parameters.Length];

                if (!capabilities.IsDefaultOrEmpty)
                {
                    for (var i = 0; i < parameters.Length; i++)
                    {
                        if (string.Equals(parameters[i].Name, "capabilities", StringComparison.Ordinal) &&
                            IsFSharpOptionOfStringSequence(parameters[i].ParameterType))
                        {
                            arguments[i] = CreateFSharpOptionSomeStringSequence(parameters[i].ParameterType, [.. capabilities]);
                        }
                    }
                }

                session = _sessionApi.Create.Invoke(_checker, arguments);
                if (session == null)
                {
                    error = "CreateHotReloadSession returned null.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                var rootException = (ex as TargetInvocationException)?.InnerException ?? ex.GetBaseException();
                error = rootException.Message;
                return false;
            }
        }

        /// <summary>
        /// Captures the project's committed baseline into the session from the built output on
        /// disk. Re-adding a project the session already tracks recaptures its baseline.
        /// </summary>
        public FSharpInvocationResult SessionAddProject(object session, object projectInput, string outputPath, CancellationToken cancellationToken)
        {
            if (_sessionApi == null)
            {
                return new FSharpInvocationResult(false, null, null, "The session-object API is unavailable.");
            }

            var parameters = _sessionApi.AddProject.GetParameters();
            var arguments = new object?[parameters.Length];
            arguments[0] = projectInput;

            for (var i = 1; i < parameters.Length; i++)
            {
                if (string.Equals(parameters[i].Name, "outputPath", StringComparison.Ordinal) &&
                    IsFSharpOptionOfString(parameters[i].ParameterType))
                {
                    arguments[i] = CreateFSharpOptionSomeString(parameters[i].ParameterType, outputPath);
                }
            }

            return InvokeResult(session, _sessionApi.AddProject, arguments, cancellationToken);
        }

        /// <summary>
        /// Emits a delta for one project against its committed baseline in the session. The
        /// emitted update is staged as pending until <see cref="SessionCommit"/> or
        /// <see cref="SessionDiscard"/>.
        /// </summary>
        public FSharpInvocationResult SessionEmitDelta(object session, object projectInput, CancellationToken cancellationToken)
        {
            if (_sessionApi == null)
            {
                return new FSharpInvocationResult(false, null, null, "The session-object API is unavailable.");
            }

            var arguments = new object?[_sessionApi.EmitDelta.GetParameters().Length];
            arguments[0] = projectInput;
            return InvokeResult(session, _sessionApi.EmitDelta, arguments, cancellationToken);
        }

        public void SessionCommit(object session)
        {
            try
            {
                _sessionApi?.Commit.Invoke(session, null);
            }
            catch (Exception ex)
            {
                if (_trace)
                {
                    var rootException = (ex as TargetInvocationException)?.InnerException ?? ex.GetBaseException();
                    _logger.LogDebug("F# session Commit failed: {Message}", rootException.Message);
                }
            }
        }

        public void SessionDiscard(object session)
        {
            try
            {
                _sessionApi?.Discard.Invoke(session, null);
            }
            catch (Exception ex)
            {
                if (_trace)
                {
                    var rootException = (ex as TargetInvocationException)?.InnerException ?? ex.GetBaseException();
                    _logger.LogDebug("F# session Discard failed: {Message}", rootException.Message);
                }
            }
        }

        /// <summary>
        /// Replaces the session-wide capability set in place on the session object (the
        /// session-object analogue of <see cref="TryUpdateSessionCapabilities"/>).
        /// </summary>
        public bool TrySessionUpdateCapabilities(object session, ImmutableArray<string> capabilities)
        {
            if (_sessionApi == null)
            {
                return false;
            }

            try
            {
                _sessionApi.UpdateCapabilities.Invoke(session, [capabilities.ToArray()]);
                return true;
            }
            catch (Exception ex)
            {
                if (_trace)
                {
                    var rootException = (ex as TargetInvocationException)?.InnerException ?? ex.GetBaseException();
                    _logger.LogDebug("F# session UpdateCapabilities failed: {Message}", rootException.Message);
                }

                return false;
            }
        }

        /// <summary>Ends the session: FSharpHotReloadSession implements IDisposable.</summary>
        public void DisposeSession(object session)
        {
            try
            {
                (session as IDisposable)?.Dispose();
            }
            catch (Exception ex)
            {
                if (_trace)
                {
                    _logger.LogDebug("Ignoring F# session dispose failure: {Message}", ex.Message);
                }
            }
        }

        /// <summary>
        /// Replaces the active session's capability set without restarting it (restarting would
        /// re-capture the baseline from already-edited sources). Returns null when the loaded
        /// compiler service predates FSharpChecker.UpdateHotReloadCapabilities, false when the
        /// call failed or no session was active, true on success.
        /// </summary>
        public bool? TryUpdateSessionCapabilities(ImmutableArray<string> capabilities)
        {
            if (!_updateCapabilitiesProbed)
            {
                _updateCapabilities = _checker.GetType().GetMethod("UpdateHotReloadCapabilities", BindingFlags.Public | BindingFlags.Instance);
                _updateCapabilitiesProbed = true;
            }

            if (_updateCapabilities == null)
            {
                return null;
            }

            try
            {
                return _updateCapabilities.Invoke(_checker, [capabilities.ToArray()]) as bool? ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Builds the StartHotReloadSession argument list against whatever FCS surface is loaded.
        /// Recent FCS builds expose an optional <c>capabilities: string seq</c> parameter
        /// (surfaced via reflection as <c>FSharpOption&lt;IEnumerable&lt;string&gt;&gt;</c>); when present the
        /// aggregate runtime capabilities are forwarded, otherwise they are gracefully omitted so the
        /// host keeps working against older FCS builds. Unrecognized optional parameters are passed
        /// <c>null</c> (None), matching the existing userOpName handling.
        /// </summary>
        private static object?[] CreateStartSessionArguments(ParameterInfo[] parameters, object projectInput, ImmutableArray<string> capabilities)
        {
            var arguments = new object?[parameters.Length];

            if (parameters.Length > 0)
            {
                arguments[0] = projectInput;
            }

            if (capabilities.IsDefaultOrEmpty)
            {
                return arguments;
            }

            for (var i = 1; i < parameters.Length; i++)
            {
                var parameter = parameters[i];

                if (string.Equals(parameter.Name, "capabilities", StringComparison.Ordinal) &&
                    IsFSharpOptionOfStringSequence(parameter.ParameterType))
                {
                    arguments[i] = CreateFSharpOptionSomeStringSequence(parameter.ParameterType, [.. capabilities]);
                }
            }

            return arguments;
        }

        public void InvalidateConfiguration(object projectInput, string projectPath)
        {
            if (_invalidateConfigurationMethods.IsDefaultOrEmpty)
            {
                return;
            }

            try
            {
                var projectInputType = projectInput.GetType();
                var method = _invalidateConfigurationMethods.FirstOrDefault(candidate =>
                {
                    var parameters = candidate.GetParameters();
                    return parameters.Length >= 1 && parameters[0].ParameterType.IsAssignableFrom(projectInputType);
                });

                if (method == null)
                {
                    return;
                }

                var parameterCount = method.GetParameters().Length;
                var args = parameterCount switch
                {
                    1 => [projectInput],
                    _ => new object?[] { projectInput, null },
                };

                _ = method.Invoke(_checker, args);
            }
            catch (Exception ex)
            {
                if (_trace)
                {
                    var rootException = (ex as TargetInvocationException)?.InnerException ?? ex.GetBaseException();
                    _logger.LogDebug(
                        "F# InvalidateConfiguration failed for '{ProjectPath}': {Message}",
                        projectPath,
                        rootException.Message);
                }
            }
        }

        public void NotifyFileChanged(string filePath, FSharpProjectInfo projectInfo, object projectInput, CancellationToken cancellationToken)
        {
            if (_useWorkspaceSnapshots)
            {
                NotifyWorkspaceFileChanged(filePath);
                NotifyCheckerFileChanged(filePath, projectInfo);
                return;
            }

            NotifyCheckerFileChanged(filePath, projectInput);
        }

        private void NotifyCheckerFileChanged(string filePath, FSharpProjectInfo projectInfo)
        {
            if (!TryCreateProjectOptionsInput(projectInfo, out var projectOptions, out _))
            {
                return;
            }

            NotifyCheckerFileChanged(filePath, projectOptions);
        }

        private void NotifyCheckerFileChanged(string filePath, object? projectInput)
        {
            if (_notifyFileChanged == null)
            {
                return;
            }

            try
            {
                var asyncComputation = _notifyFileChanged.Invoke(_checker, [filePath, projectInput, null]);
                if (asyncComputation == null)
                {
                    return;
                }

                var asyncResultType = _notifyFileChanged.ReturnType.GetGenericArguments().Single();
                var runSync = _runSynchronously.MakeGenericMethod(asyncResultType);
                _ = runSync.Invoke(null, [asyncComputation, null, null]);
            }
            catch (Exception ex)
            {
                if (_trace)
                {
                    var rootException = (ex as TargetInvocationException)?.InnerException ?? ex.GetBaseException();
                    _logger.LogDebug("F# NotifyFileChanged failed for '{FilePath}': {Message}", filePath, rootException.Message);
                }
            }
        }

        public FSharpInvocationResult EmitDelta(object projectInput, CancellationToken cancellationToken)
            => _emitDelta == null
                ? new FSharpInvocationResult(false, null, null, "The loaded F# compiler service does not expose EmitHotReloadDelta; use the session-object API.")
                : InvokeResult(_checker, _emitDelta, [projectInput, null], cancellationToken);

        private bool TryCreateProjectOptionsInput(FSharpProjectInfo projectInfo, out object? projectInput, out string? error)
        {
            projectInput = null;
            error = null;

            try
            {
                var args = EnsureHotReloadFlag(projectInfo.CommandLineArgs);
                projectInput = _getProjectOptions.Invoke(_checker, [projectInfo.ProjectPath, args.ToArray(), null, null, null]);
                return projectInput != null;
            }
            catch (Exception ex)
            {
                var rootException = (ex as TargetInvocationException)?.InnerException ?? ex.GetBaseException();
                error = rootException.Message;
                return false;
            }
        }

        private bool TryCreateWorkspaceSnapshotInput(FSharpProjectInfo projectInfo, out object? projectInput, out string? error)
        {
            projectInput = null;
            error = null;

            if (_workspaceProjects == null ||
                _workspaceQuery == null ||
                _workspaceProjectAddOrUpdate == null ||
                _workspaceQueryGetProjectSnapshot == null)
            {
                error = "FSharpWorkspace bridge is not initialized.";
                return false;
            }

            try
            {
                // The snapshot's output path is what the session reads back at emit time, so it must
                // be the intermediate assembly the per-edit compile refreshes, not the bin copy
                // (which stays at generation 0 because the running process locks it on Windows).
                var args = EnsureHotReloadFlag(projectInfo.CommandLineArgs);
                var projectIdentifier = _workspaceProjectAddOrUpdate.Invoke(
                    _workspaceProjects,
                    [projectInfo.ProjectPath, projectInfo.IntermediateAssemblyPath, args.ToArray()]);

                if (projectIdentifier == null)
                {
                    error = $"FSharpWorkspace.Projects.AddOrUpdate returned null for '{projectInfo.ProjectPath}'.";
                    return false;
                }

                var snapshotOption = _workspaceQueryGetProjectSnapshot.Invoke(_workspaceQuery, [projectIdentifier]);
                if (!TryGetFSharpOptionValue(snapshotOption, out var snapshot))
                {
                    error = $"FSharpWorkspace.Query.GetProjectSnapshot returned None for '{projectInfo.ProjectPath}'.";
                    return false;
                }

                if (snapshot == null)
                {
                    error = $"FSharpWorkspace.Query.GetProjectSnapshot returned Some(null) for '{projectInfo.ProjectPath}'.";
                    return false;
                }

                projectInput = snapshot;
                return true;
            }
            catch (Exception ex)
            {
                var rootException = (ex as TargetInvocationException)?.InnerException ?? ex.GetBaseException();
                error = rootException.Message;
                return false;
            }
        }

        private void NotifyWorkspaceFileChanged(string filePath)
        {
            if (_workspaceFiles == null || (_workspaceFilesEdit == null && _workspaceFilesClose == null))
            {
                return;
            }

            try
            {
                var normalizedPath = Path.GetFullPath(filePath);
                var fileUri = new Uri(normalizedPath);

                if (_workspaceFilesEdit != null)
                {
                    var content = File.ReadAllText(normalizedPath);
                    _workspaceFilesEdit.Invoke(_workspaceFiles, [fileUri, content]);
                    return;
                }

                _workspaceFilesClose!.Invoke(_workspaceFiles, [fileUri]);
            }
            catch (Exception ex)
            {
                if (_trace)
                {
                    var rootException = (ex as TargetInvocationException)?.InnerException ?? ex.GetBaseException();
                    _logger.LogDebug("F# workspace file refresh failed for '{FilePath}': {Message}", filePath, rootException.Message);
                }
            }
        }

        public HotReloadManagedCodeUpdate? CreateManagedUpdate(Guid moduleId, object delta)
        {
            try
            {
                var deltaType = delta.GetType();
                var metadata = (byte[]?)deltaType.GetProperty("Metadata")?.GetValue(delta) ?? [];
                var il = (byte[]?)deltaType.GetProperty("IL")?.GetValue(delta) ?? [];

                var pdbOption = deltaType.GetProperty("Pdb")?.GetValue(delta);
                var pdb = pdbOption == null
                    ? []
                    : (byte[]?)pdbOption.GetType().GetProperty("Value")?.GetValue(pdbOption) ?? [];

                var updatedTypesEnumerable = deltaType.GetProperty("UpdatedTypes")?.GetValue(delta) as IEnumerable;
                var updatedTypes = updatedTypesEnumerable == null
                    ? ImmutableArray<int>.Empty
                    : updatedTypesEnumerable.Cast<object>().Select(Convert.ToInt32).ToImmutableArray();

                var updatedMethodsEnumerable = deltaType.GetProperty("UpdatedMethods")?.GetValue(delta) as IEnumerable;
                var updatedMethods = updatedMethodsEnumerable == null
                    ? ImmutableArray<int>.Empty
                    : updatedMethodsEnumerable.Cast<object>().Select(Convert.ToInt32).ToImmutableArray();
                var requiredCapabilities = GetRequiredCapabilities(deltaType, delta);

                if (_trace)
                {
                    _logger.LogDebug(
                        "F# managed delta token summary: UpdatedTypes=[{UpdatedTypes}], UpdatedMethods=[{UpdatedMethods}], RequiredCapabilities=[{RequiredCapabilities}].",
                        FormatTokenSet(updatedTypes),
                        FormatTokenSet(updatedMethods),
                        string.Join(", ", requiredCapabilities));
                }

                return new HotReloadManagedCodeUpdate(
                    moduleId,
                    ImmutableArray.CreateRange(metadata),
                    ImmutableArray.CreateRange(il),
                    ImmutableArray.CreateRange(pdb),
                    updatedTypes,
                    requiredCapabilities);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Failed to materialize F# managed update payload: {Message}", ex.Message);
                return null;
            }
        }

        private static ImmutableArray<string> GetRequiredCapabilities(Type deltaType, object delta)
        {
            // Older FCS builds predate the public RequiredCapabilities property. Baseline is
            // the conservative Roslyn-compatible requirement for their method-body deltas.
            if (deltaType.GetProperty("RequiredCapabilities")?.GetValue(delta) is not IEnumerable values)
            {
                return ["Baseline"];
            }

            var capabilities = values
                .Cast<object>()
                .Select(static value => value?.ToString())
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value!)
                .Distinct(StringComparer.Ordinal)
                .ToImmutableArray();

            return capabilities.IsEmpty ? ["Baseline"] : capabilities;
        }

        public void TryEndSession()
        {
            if (_endSession == null)
            {
                // Session-object mode: sessions end via DisposeSession; there is no process-wide
                // legacy session to tear down.
                return;
            }

            try
            {
                _ = _endSession.Invoke(_checker, null);
            }
            catch (Exception ex)
            {
                if (_trace)
                {
                    _logger.LogDebug("Ignoring F# session cleanup failure: {Message}", ex.Message);
                }
            }
        }

        private FSharpInvocationResult InvokeResult(object target, MethodInfo method, object?[] args, CancellationToken cancellationToken)
        {
            try
            {
                var asyncComputation = method.Invoke(target, args)
                    ?? throw new InvalidOperationException($"{method.Name} returned null.");

                var asyncResultType = method.ReturnType.GetGenericArguments().Single();
                var runSync = _runSynchronously.MakeGenericMethod(asyncResultType);
                var runSyncParameters = runSync.GetParameters();
                var cancellationOption = CreateFSharpOptionSome(runSyncParameters[2].ParameterType, cancellationToken);
                var result = runSync.Invoke(null, [asyncComputation, null, cancellationOption])
                    ?? throw new InvalidOperationException($"{method.Name} returned null result.");

                var tag = (int)(result.GetType().GetProperty("Tag")?.GetValue(result) ?? -1);
                if (tag == 0)
                {
                    var value = result.GetType().GetProperty("ResultValue")?.GetValue(result);
                    return new FSharpInvocationResult(true, value, null, null);
                }

                var error = result.GetType().GetProperty("ErrorValue")?.GetValue(result);
                var errorText = error?.ToString() ?? "Unknown error";
                var errorCase = ParseErrorCase(errorText);

                // FSharpHotReloadError.UnsupportedEdit carries a structured FSharpHotReloadRudeEdit
                // list (Id + Severity + Message). Render the per-edit reason cleanly instead of the
                // default DU/record ToString() dump so the host can surface an actionable message.
                if (errorCase == "UnsupportedEdit")
                {
                    errorText = TryFormatRudeEdits(error) ?? errorText;
                }

                return new FSharpInvocationResult(false, null, errorCase, errorText);
            }
            catch (Exception ex)
            {
                var rootException = (ex as TargetInvocationException)?.InnerException ?? ex.GetBaseException();
                return new FSharpInvocationResult(false, null, null, rootException.Message);
            }
        }

        private static ImmutableArray<string> EnsureHotReloadFlag(ImmutableArray<string> args)
            => args.Any(static arg => string.Equals(arg, "--test:HotReloadDeltas", StringComparison.OrdinalIgnoreCase))
                ? args
                : args.Add("--test:HotReloadDeltas");

        private static bool TryGetFSharpOptionValue(object? option, out object? value)
        {
            value = null;
            if (option == null)
            {
                return false;
            }

            var optionType = option.GetType();
            var isSomeAccessor = optionType.GetMethod("get_IsSome", BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            var valueAccessor = optionType.GetMethod("get_Value", BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            if (isSomeAccessor == null || valueAccessor == null)
            {
                return false;
            }

            if (InvokeFSharpOptionAccessor(isSomeAccessor, option) is not bool isSome || !isSome)
            {
                return false;
            }

            value = InvokeFSharpOptionAccessor(valueAccessor, option);
            return true;
        }

        private static object? InvokeFSharpOptionAccessor(MethodInfo accessor, object option)
            => accessor.GetParameters().Length switch
            {
                0 => accessor.Invoke(option, null),
                1 => accessor.Invoke(null, [option]),
                _ => throw new InvalidOperationException($"Unexpected option accessor shape: {accessor}")
            };

        private static string? ParseErrorCase(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            // Include newline/tab: the F# DU ToString() can render a single-field case as
            // "UnsupportedEdit\n  [ ... ]", so the case name is followed by a newline, not a space.
            // Without these, ParseErrorCase returns "UnsupportedEdit\n" and the == comparison (and the
            // rude-edit formatting it gates) is skipped.
            var delimiters = new[] { ' ', '(', ':', '\n', '\r', '\t' };
            var index = text.IndexOfAny(delimiters);
            return index > 0 ? text[..index] : text;
        }

        /// <summary>
        /// Renders the FSharpHotReloadError.UnsupportedEdit payload (an FSharpList of
        /// FSharpHotReloadRudeEdit records, each with Id/Severity/Message/SymbolName) to a clean
        /// "{Id}: {Message}" reason string via reflection. Returns null on any shape mismatch so the
        /// caller falls back to the default ToString(), keeping the bridge robust across FCS versions.
        /// </summary>
        private static string? TryFormatRudeEdits(object? error)
        {
            if (error == null)
            {
                return null;
            }

            // Preferred: read the structured FSharpHotReloadRudeEdit list (Id + Message). Use the
            // no-arg get_Item() accessor rather than GetProperty("Item"), which can throw
            // AmbiguousMatchException on an F# single-field union case.
            try
            {
                var item = error.GetType().GetMethod("get_Item", Type.EmptyTypes)?.Invoke(error, null);
                if (item is System.Collections.IEnumerable edits)
                {
                    var lines = new List<string>();
                    foreach (var edit in edits)
                    {
                        if (edit == null)
                        {
                            continue;
                        }

                        var editType = edit.GetType();
                        var id = editType.GetProperty("Id")?.GetValue(edit) as string ?? "";
                        var message = editType.GetProperty("Message")?.GetValue(edit) as string ?? "";

                        if (id.Length > 0 || message.Length > 0)
                        {
                            lines.Add(id.Length == 0 ? message : $"{id}: {message}");
                        }
                    }

                    if (lines.Count > 0)
                    {
                        return string.Join(Environment.NewLine, lines);
                    }
                }
            }
            catch
            {
                // Fall through to the tidy-dump fallback below.
            }

            // Fallback: the default F# record/DU ToString() is multi-line and noisy; collapse it to
            // a single tidy line so the reason is still readable for the user.
            var text = error.ToString();
            return string.IsNullOrWhiteSpace(text)
                ? null
                : System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        }

        private static string FormatTokenSet(ImmutableArray<int> tokens)
            => tokens.IsDefaultOrEmpty
                ? "<none>"
                : string.Join(", ", tokens.Select(static token => $"0x{token:X8}"));

        private static object?[] CreateCheckerArguments(ParameterInfo[] parameters)
        {
            var arguments = new object?[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];

                // Hot reload APIs require keepAssemblyContents=true in recent FCS versions.
                if (string.Equals(parameter.Name, "keepAssemblyContents", StringComparison.Ordinal))
                {
                    if (parameter.ParameterType == typeof(bool) || parameter.ParameterType == typeof(bool?))
                    {
                        arguments[i] = true;
                        continue;
                    }

                    if (IsFSharpOptionOfBoolean(parameter.ParameterType))
                    {
                        arguments[i] = CreateFSharpOptionSomeBoolean(parameter.ParameterType, value: true);
                        continue;
                    }
                }

                // Use the TransparentCompiler: it caches per-file typecheck results keyed by content
                // hash and a parse-time dependency graph, so a per-edit re-check only re-typechecks the
                // changed file's dependency cone instead of the whole project. For hot reload this
                // roughly halves the per-edit FCS check and, crucially, bounds the cache growth that
                // otherwise makes the check degrade steadily over an editing session.
                if (string.Equals(parameter.Name, "useTransparentCompiler", StringComparison.Ordinal))
                {
                    if (parameter.ParameterType == typeof(bool) || parameter.ParameterType == typeof(bool?))
                    {
                        arguments[i] = true;
                        continue;
                    }

                    if (IsFSharpOptionOfBoolean(parameter.ParameterType))
                    {
                        arguments[i] = CreateFSharpOptionSomeBoolean(parameter.ParameterType, value: true);
                        continue;
                    }
                }
            }

            return arguments;
        }

        private static bool IsFSharpOptionOfBoolean(Type parameterType)
            => parameterType.IsGenericType &&
               string.Equals(parameterType.GetGenericTypeDefinition().FullName, "Microsoft.FSharp.Core.FSharpOption`1", StringComparison.Ordinal) &&
               parameterType.GetGenericArguments() is [var argumentType] &&
               argumentType == typeof(bool);

        private static object? CreateFSharpOptionSomeBoolean(Type optionType, bool value)
            => optionType.GetMethod("Some", BindingFlags.Public | BindingFlags.Static, [typeof(bool)])?.Invoke(null, [value]);

        private static bool IsFSharpOptionOfString(Type parameterType)
            => parameterType.IsGenericType &&
               string.Equals(parameterType.GetGenericTypeDefinition().FullName, "Microsoft.FSharp.Core.FSharpOption`1", StringComparison.Ordinal) &&
               parameterType.GetGenericArguments() is [var argumentType] &&
               argumentType == typeof(string);

        private static object? CreateFSharpOptionSomeString(Type optionType, string value)
            => optionType.GetMethod("Some", BindingFlags.Public | BindingFlags.Static, [typeof(string)])?.Invoke(null, [value]);

        private static bool IsFSharpOptionOfStringSequence(Type parameterType)
            => parameterType.IsGenericType &&
               string.Equals(parameterType.GetGenericTypeDefinition().FullName, "Microsoft.FSharp.Core.FSharpOption`1", StringComparison.Ordinal) &&
               parameterType.GetGenericArguments() is [var argumentType] &&
               argumentType.IsAssignableFrom(typeof(string[]));

        private static object? CreateFSharpOptionSomeStringSequence(Type optionType, string[] value)
            => optionType.GetGenericArguments() is [var argumentType]
                ? optionType.GetMethod("Some", BindingFlags.Public | BindingFlags.Static, [argumentType])?.Invoke(null, [value])
                : null;

        private static object? CreateFSharpOptionSome(Type optionType, object value)
            => optionType.GetGenericArguments() is [var argumentType]
                ? optionType.GetMethod("Some", BindingFlags.Public | BindingFlags.Static, [argumentType])?.Invoke(null, [value])
                : null;

        internal readonly record struct FSharpInvocationResult(bool IsSuccess, object? Value, string? ErrorCase, string? ErrorText);

        /// <summary>
        /// Late-bound member set of <c>FSharp.Compiler.CodeAnalysis.FSharpHotReloadSession</c>:
        /// one session object per watch session, per-project baselines added via AddProject,
        /// deltas staged by EmitDelta and resolved by Commit/Discard, session-wide capabilities
        /// replaced via UpdateCapabilities, ended by IDisposable.Dispose.
        /// </summary>
        private sealed class SessionObjectApi
        {
            public required MethodInfo Create { get; init; }
            public required MethodInfo AddProject { get; init; }
            public required MethodInfo EmitDelta { get; init; }
            public required MethodInfo Commit { get; init; }
            public required MethodInfo Discard { get; init; }
            public required MethodInfo UpdateCapabilities { get; init; }

            public static SessionObjectApi? TryCreate(Type checkerType)
            {
                var create = checkerType.GetMethod("CreateHotReloadSession", BindingFlags.Public | BindingFlags.Instance);
                if (create == null || !typeof(IDisposable).IsAssignableFrom(create.ReturnType))
                {
                    return null;
                }

                var sessionType = create.ReturnType;
                var addProject = sessionType.GetMethod("AddProject", BindingFlags.Public | BindingFlags.Instance);
                var emitDelta = sessionType.GetMethod("EmitDelta", BindingFlags.Public | BindingFlags.Instance);
                var commit = sessionType.GetMethod("Commit", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
                var discard = sessionType.GetMethod("Discard", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
                var updateCapabilities = sessionType.GetMethod("UpdateCapabilities", BindingFlags.Public | BindingFlags.Instance);

                if (addProject == null || emitDelta == null || commit == null || discard == null || updateCapabilities == null)
                {
                    return null;
                }

                return new SessionObjectApi
                {
                    Create = create,
                    AddProject = addProject,
                    EmitDelta = emitDelta,
                    Commit = commit,
                    Discard = discard,
                    UpdateCapabilities = updateCapabilities,
                };
            }
        }
    }
}
