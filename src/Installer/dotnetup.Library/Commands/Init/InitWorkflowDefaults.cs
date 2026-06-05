// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;

/// <summary>
/// Resolves the recommended init setup (path preference, install root, channel display, and
/// migration candidates) for the walkthrough summary. Resolution here is side-effect-free: it
/// performs no network calls, writes no console output, and does not throw on an unresolvable
/// channel. The actual install requests are resolved separately (and only once the user commits
/// to installing) via <see cref="ResolveDefaultRequests"/>, so simply viewing the summary or
/// choosing to exit never triggers version resolution.
/// </summary>
internal static class InitWorkflowDefaults
{
    /// <summary>
    /// Resolves the recommended setup to display in the summary (install root, path preference,
    /// migration candidates, and channel display) without prompting, resolving versions, or
    /// emitting output. When <paramref name="preResolvedRequests"/> is supplied, its already-resolved
    /// root/channel/manifest are reused instead of being re-derived.
    /// </summary>
    public static WalkthroughPlan ResolveWalkthroughPlan(
        InstallCommand command,
        List<ResolvedInstallRequest>? preResolvedRequests,
        IDotnetEnvironmentManager dotnetEnvironment)
    {
        PathPreference pathPreference = GetDefaultPathPreference(command.ShellProvider);

        if (preResolvedRequests is { Count: > 0 })
        {
            var first = preResolvedRequests[0];
            DotnetInstallRoot resolvedRoot = first.Request.InstallRoot;
            var resolvedMigrations = ResolveDefaultMigrations(
                dotnetEnvironment, pathPreference, resolvedRoot, first.Request.Options.ManifestPath, preResolvedRequests);

            return new WalkthroughPlan(
                resolvedRoot,
                pathPreference,
                resolvedMigrations,
                new DefaultChannelDisplay(first.Request.Channel.Name, first.Request.Options.GlobalJsonPath));
        }

        var globalJson = GlobalJsonModifier.GetGlobalJsonInfo(Environment.CurrentDirectory);
        var currentInstallRoot = dotnetEnvironment.GetCurrentPathConfiguration();
        var pathResolution = new InstallPathResolver(dotnetEnvironment).Resolve(
            command.InstallPath, globalJson, currentInstallRoot);
        var installRoot = new DotnetInstallRoot(
            pathResolution.ResolvedInstallPath,
            InstallerUtilities.GetDefaultInstallArchitecture());

        var migrations = ResolveDefaultMigrations(
            dotnetEnvironment, pathPreference, installRoot, command.ManifestPath, existingRequests: null);

        return new WalkthroughPlan(installRoot, pathPreference, migrations, ResolveChannelDisplay(globalJson));
    }

    /// <summary>
    /// Resolves the default install requests. Uses the pre-resolved requests when supplied;
    /// otherwise resolves the default SDK channel (from global.json or "latest"). This performs
    /// version resolution and may print global.json messaging, so it is only called once the user
    /// has committed to installing.
    /// </summary>
    public static List<ResolvedInstallRequest> ResolveDefaultRequests(
        InstallCommand command,
        List<ResolvedInstallRequest>? requests)
    {
        if (requests is not null)
        {
            return requests;
        }

        var workflow = new InstallWorkflow(command);
        return workflow.GenerateInstallRequests(
            [new MinimalInstallSpec(InstallComponent.SDK, null)]);
    }

    /// <summary>
    /// Returns the recommended path preference without prompting: terminal-profile mode when a
    /// supported shell is available, otherwise isolation mode. This is the value shown in the
    /// summary and used by the "proceed with defaults" branch.
    /// </summary>
    public static PathPreference GetDefaultPathPreference(IEnvShellProvider? shellProvider = null)
    {
        if (!OperatingSystem.IsWindows() && (shellProvider ?? ShellDetection.GetCurrentShellProvider()) is null)
        {
            return PathPreference.DotnetupDotnet;
        }

        return PathPreference.ShellProfile;
    }

    /// <summary>
    /// Builds the deduplicated migration candidates for the recommended mode without prompting.
    /// Returns an empty list when the mode does not migrate system installs.
    /// </summary>
    public static List<MigrationWorkflow.MigrationSelection> ResolveDefaultMigrations(
        IDotnetEnvironmentManager dotnetEnvironment,
        PathPreference pathPreference,
        DotnetInstallRoot installRoot,
        string? manifestPath,
        IReadOnlyCollection<ResolvedInstallRequest>? existingRequests)
    {
        if (!InitWorkflows.ShouldPromptToConvertSystemInstalls(pathPreference))
        {
            return [];
        }

        var systemInstalls = MigrationWorkflow.GetMigrationCandidates(dotnetEnvironment);
        return MigrationWorkflow.BuildMigrationSelections(systemInstalls, installRoot, manifestPath, existingRequests);
    }

    /// <summary>
    /// Resolves the channel label to display in the summary directly from global.json (or "latest")
    /// without resolving a concrete version, so the summary never triggers a network call.
    /// </summary>
    private static DefaultChannelDisplay ResolveChannelDisplay(GlobalJsonInfo globalJson)
    {
        if (globalJson.GlobalJsonPath is not null
            && GlobalJsonChannelResolver.ResolveChannel(globalJson.GlobalJsonPath) is { } channel)
        {
            return new DefaultChannelDisplay(channel, globalJson.GlobalJsonPath);
        }

        return new DefaultChannelDisplay(ChannelVersionResolver.LatestChannel, null);
    }
}

