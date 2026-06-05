// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Non-interactive helpers that orchestrate migration of system-managed .NET installs into the
/// dotnetup-managed install root. The interactive prompt that drives this — together with display
/// formatting and the user-facing yes/no decision — lives on <see cref="Init.InitWorkflows"/>.
/// </summary>
internal static class MigrationWorkflow
{
    /// <summary>
    /// The number of migration candidates shown before the list is truncated or scrolled — used by
    /// both the summary preview ("… and N more") and the interactive migration prompt's scroll window.
    /// </summary>
    internal const int MigrationPreviewCount = 3;

    internal sealed record MigrationSelection(
        InstallComponent Component,
        UpdateChannel Channel,
        ReleaseVersion ExampleVersion,
        InstallArchitecture Architecture);

    /// <summary>
    /// Returns the system-managed .NET installs that are candidates for migration into the dotnetup
    /// install root, optionally filtered to a specific set of components.
    /// </summary>
    internal static List<DotnetInstall> GetMigrationCandidates(
        IDotnetEnvironmentManager dotnetEnvironment,
        IReadOnlyCollection<InstallComponent>? components = null)
    {
        var systemInstalls = dotnetEnvironment.GetExistingSystemInstalls();
        if (systemInstalls.Count == 0)
        {
            return [];
        }

        if (components is { Count: > 0 })
        {
            systemInstalls = [.. systemInstalls.Where(i => components.Contains(i.Component))];
        }
        return systemInstalls;
    }

    /// <summary>
    /// Reduces the system-installed components to a deduplicated set of channel-level migration
    /// selections, skipping anything that is already tracked in the manifest or in the existing
    /// request set for the target install root.
    /// </summary>
    internal static List<MigrationSelection> BuildMigrationSelections(
        List<DotnetInstall> systemInstalls,
        DotnetInstallRoot installRoot,
        string? manifestPath = null,
        IReadOnlyCollection<ResolvedInstallRequest>? existingRequests = null)
    {
        if (systemInstalls.Count == 0)
        {
            return [];
        }

        var trackedChannels = GetTrackedMigrationChannels(installRoot, manifestPath, existingRequests);
        var deduped = new List<MigrationSelection>();
        var seenChannels = new HashSet<(InstallComponent Component, string Channel)>();

        foreach (var install in systemInstalls.OrderBy(i => i.Component).ThenByDescending(i => i.Version))
        {
            string channelName = DotnetupUtilities.VersionToPatchBasedChannel(install.Version, install.Component);
            var key = (install.Component, GetTrackedMigrationChannelName(install.Component, channelName));
            if (trackedChannels.Contains(key))
            {
                continue;
            }

            if (!seenChannels.Add(key))
            {
                continue;
            }

            deduped.Add(new MigrationSelection(
                install.Component,
                new UpdateChannel(channelName),
                install.Version,
                install.InstallRoot.Architecture));
        }

        return deduped;
    }

    /// <summary>
    /// Runs a two-phase migration install batch. Phase 1 is the user's primary requests merged with
    /// SDK migrations; Phase 2 is the runtime-style migrations whose target version is not already on
    /// disk after Phase 1. Both phases are dispatched through <paramref name="runner"/>.
    /// </summary>
    internal static List<ResolvedInstallRequest> ExecuteMigrationInPhases(
        List<ResolvedInstallRequest> existingRequests,
        List<MigrationSelection> migrations,
        InstallCommand command,
        DotnetInstallRoot installRoot,
        string? manifestPath,
        Action<List<ResolvedInstallRequest>> runner)
    {
        var (phase1, deferred) = BuildMigrationPhase1Requests(
            existingRequests, migrations, command, installRoot, manifestPath);
        runner(phase1);

        var phase2 = BuildMigrationPhase2Requests(deferred, command, installRoot, manifestPath);
        if (phase2.Count > 0)
        {
            runner(phase2);
        }

        return [..phase1, ..phase2];
    }

    /// <summary>
    /// Builds the Phase 1 install batch (existing requests merged with SDK migrations) and
    /// returns the runtime-style migrations that should be considered for Phase 2 after the
    /// Phase 1 install completes.
    /// </summary>
    internal static (List<ResolvedInstallRequest> Phase1Requests, List<MigrationSelection> DeferredRuntimeMigrations)
        BuildMigrationPhase1Requests(
            List<ResolvedInstallRequest> existingRequests,
            List<MigrationSelection> migrations,
            InstallCommand command,
            DotnetInstallRoot installRoot,
            string? manifestPath)
    {
        var sdkMigrations = migrations.Where(m => m.Component == InstallComponent.SDK).ToList();
        var runtimeMigrations = migrations.Where(m => m.Component != InstallComponent.SDK).ToList();

        var phase1 = MergeInstallRequests(
            existingRequests,
            sdkMigrations,
            installRoot,
            command,
            manifestPath);

        return (phase1, runtimeMigrations);
    }

    /// <summary>
    /// Builds the Phase 2 install batch by filtering deferred runtime-style migrations against
    /// what is already on disk after Phase 1. Returns an empty list when nothing needs to install.
    /// </summary>
    internal static List<ResolvedInstallRequest> BuildMigrationPhase2Requests(
        List<MigrationSelection> deferredRuntimeMigrations,
        InstallCommand command,
        DotnetInstallRoot installRoot,
        string? manifestPath)
    {
        if (deferredRuntimeMigrations.Count == 0)
        {
            return [];
        }

        var remaining = FilterRuntimeMigrationsAgainstDisk(
            deferredRuntimeMigrations, installRoot, command.ChannelVersionResolver);
        if (remaining.Count == 0)
        {
            return [];
        }

        return MergeInstallRequests(
            [],
            remaining,
            installRoot,
            command,
            manifestPath);
    }

    internal static List<ResolvedInstallRequest> MergeInstallRequests(
        List<ResolvedInstallRequest> requests,
        List<MigrationSelection> toMigrate,
        DotnetInstallRoot installRoot,
        InstallCommand? command = null,
        string? manifestPath = null)
    {
        if (toMigrate.Count == 0)
        {
            return requests;
        }

        var migrationOptions = BuildMigrationInstallOptions(command, manifestPath);

        var mergedRequests = new List<ResolvedInstallRequest>(requests);
        var existingRequests = requests
            .Select(r => (r.Request.Component, Channel: GetTrackedMigrationChannelName(r.Request.Component, r.Request.Channel.Name)))
            .ToHashSet();

        foreach (var migration in toMigrate.OrderBy(i => i.Component).ThenBy(i => i.Channel.Name))
        {
            var requestKey = (migration.Component, Channel: GetTrackedMigrationChannelName(migration.Component, migration.Channel.Name));
            if (!existingRequests.Add(requestKey))
            {
                continue;
            }

            mergedRequests.Add(new ResolvedInstallRequest(
                new DotnetInstallRequest(
                    installRoot,
                    migration.Channel,
                    migration.Component,
                    migrationOptions),
                migration.ExampleVersion));
        }

        return mergedRequests;
    }

    /// <summary>
    /// Returns the install-spec channels already represented in the manifest or the
    /// current request set for the target install root.
    /// </summary>
    private static HashSet<(InstallComponent Component, string Channel)> GetTrackedMigrationChannels(
        DotnetInstallRoot installRoot,
        string? manifestPath,
        IReadOnlyCollection<ResolvedInstallRequest>? existingRequests = null)
    {
        var trackedChannels = new HashSet<(InstallComponent Component, string Channel)>();

        using (new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(manifestPath);
            var manifestData = manifest.ReadManifest();
            var root = manifestData.DotnetRoots.FirstOrDefault(r =>
                DotnetupUtilities.PathsEqual(Path.GetFullPath(r.Path), Path.GetFullPath(installRoot.Path)) && r.Architecture == installRoot.Architecture);

            foreach (var installSpec in root?.InstallSpecs ?? [])
            {
                trackedChannels.Add((installSpec.Component, GetTrackedMigrationChannelName(installSpec.Component, installSpec.VersionOrChannel)));
            }
        }

        if (existingRequests is not null)
        {
            foreach (var request in existingRequests)
            {
                trackedChannels.Add((request.Request.Component, GetTrackedMigrationChannelName(request.Request.Component, request.Request.Channel.Name)));
            }
        }

        return trackedChannels;
    }

    private static string GetTrackedMigrationChannelName(InstallComponent component, string channelName)
    {
        if (component != InstallComponent.SDK &&
            int.TryParse(channelName, out int major))
        {
            return $"{major}.0";
        }

        return channelName;
    }

    /// <summary>
    /// Filters out runtime-style migrations (Runtime / ASPNETCore / WindowsDesktop) whose
    /// resolved channel version is already present on disk after Phase 1 installs (e.g. the SDK
    /// install brought down its bundled runtime).
    /// SDK migrations are not handled here — they are installed in Phase 1.
    /// </summary>
    /// <remarks>
    /// We resolve each migration's channel to its exact latest version via
    /// <paramref name="channelVersionResolver"/> (an in-process lookup using cached release
    /// manifest data) and then compare against the actual on-disk shared-framework folder.
    /// This avoids both false negatives (skipping a migration when the SDK shipped an older
    /// runtime patch than the public channel) and false positives (re-downloading a runtime
    /// the SDK already installed). If resolution returns null we keep the migration so the
    /// install attempt can surface a clear error rather than silently skipping.
    /// </remarks>
    private static List<MigrationSelection> FilterRuntimeMigrationsAgainstDisk(
        List<MigrationSelection> runtimeMigrations,
        DotnetInstallRoot installRoot,
        ChannelVersionResolver channelVersionResolver)
    {
        if (runtimeMigrations.Count == 0)
        {
            return runtimeMigrations;
        }

        var remaining = new List<MigrationSelection>(runtimeMigrations.Count);
        foreach (var migration in runtimeMigrations)
        {
            ReleaseVersion? resolvedVersion = channelVersionResolver.GetLatestVersionForChannel(
                migration.Channel, migration.Component, installRoot.Architecture);

            if (resolvedVersion is null ||
                !RuntimeFolderExistsOnDisk(installRoot, migration.Component, resolvedVersion))
            {
                remaining.Add(migration);
            }
        }

        return remaining;
    }

    private static bool RuntimeFolderExistsOnDisk(
        DotnetInstallRoot installRoot,
        InstallComponent component,
        ReleaseVersion version)
    {
        if (component == InstallComponent.SDK)
        {
            return false;
        }

        string folder = Path.Combine(
            installRoot.Path,
            "shared",
            component.GetFrameworkName(),
            version.ToString());
        return Directory.Exists(folder);
    }

    /// <summary>
    /// Builds the <see cref="InstallRequestOptions"/> used for migration install requests.
    /// Migrations only need the command-wide flags (untracked/verbosity/manifest/muxer) — they do not carry
    /// global.json or InstallSource information because migrations originate from disk discovery, not from
    /// user-supplied specs or global.json.
    /// </summary>
    private static InstallRequestOptions BuildMigrationInstallOptions(InstallCommand? command, string? manifestPath) =>
        new()
        {
            ManifestPath = manifestPath,
            Untracked = command?.Untracked ?? false,
            Verbosity = command?.Verbosity ?? Verbosity.Normal,
            RequireMuxerUpdate = command?.RequireMuxerUpdate ?? false,
        };
}
