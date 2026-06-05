// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;

/// <summary>
/// Resolves the recommended init defaults (install requests, path preference, and migrations)
/// without prompting the user. The walkthrough summary renders these values and the
/// "proceed with defaults" branch reuses them, so the displayed and applied defaults stay in sync.
/// </summary>
internal static class InitWorkflowDefaults
{
    /// <summary>
    /// Resolves the recommended default setup (install requests, install root, path preference,
    /// migration candidates, and channel display) without prompting the user.
    /// </summary>
    public static WalkthroughPlan ResolveWalkthroughDefaults(
        InstallCommand command,
        List<ResolvedInstallRequest>? requests,
        IDotnetEnvironmentManager dotnetEnvironment)
    {
        List<ResolvedInstallRequest> defaultRequests = ResolveDefaultRequests(command, requests);

        DotnetInstallRoot installRoot = defaultRequests.Count > 0
            ? defaultRequests[0].Request.InstallRoot
            : new DotnetInstallRoot(
                dotnetEnvironment.GetDefaultDotnetInstallPath(),
                InstallerUtilities.GetDefaultInstallArchitecture());

        PathPreference pathPreference = GetDefaultPathPreference(command.ShellProvider);
        string? manifestPath = defaultRequests.Count > 0 ? defaultRequests[0].Request.Options.ManifestPath : null;

        List<MigrationWorkflow.MigrationSelection> migrations = ResolveDefaultMigrations(
            dotnetEnvironment, pathPreference, installRoot, manifestPath, defaultRequests);

        return new WalkthroughPlan(
            defaultRequests, installRoot, pathPreference, migrations, ResolveChannelDisplay(defaultRequests));
    }

    /// <summary>
    /// Resolves the default install requests without prompting. Uses the pre-resolved requests
    /// when supplied; otherwise resolves the default SDK channel (from global.json or "latest").
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

    private static DefaultChannelDisplay ResolveChannelDisplay(List<ResolvedInstallRequest> requests)
    {
        if (requests.Count == 0)
        {
            return new DefaultChannelDisplay(null, null);
        }

        var first = requests[0];
        return new DefaultChannelDisplay(first.Request.Channel.Name, first.Request.Options.GlobalJsonPath);
    }
}
