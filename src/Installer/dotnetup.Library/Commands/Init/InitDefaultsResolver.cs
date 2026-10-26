// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;

/// <summary>
/// Resolves the defaults and supporting context used by the init form. Resolution here is
/// side-effect-free: it performs no network calls, writes no console output, and does not throw on
/// an unresolvable channel. The actual install requests are resolved separately (and only once the
/// user accepts the form) via <see cref="ResolveDefaultRequests"/>, so simply viewing or exiting
/// the form never triggers version resolution.
/// </summary>
internal static class InitDefaultsResolver
{
    /// <summary>
    /// Resolves the defaults and context for the init form without prompting, resolving versions,
    /// or emitting output. When <paramref name="preResolvedRequests"/> is supplied, its
    /// already-resolved root, channel, and manifest are reused instead of being re-derived.
    /// </summary>
    public static InitFormDefaults ResolveFormDefaults(
        InstallCommand command,
        List<ResolvedInstallRequest>? preResolvedRequests,
        IDotnetEnvironmentManager dotnetEnvironment,
        DotnetAccessMode? configuredAccessMode = null,
        bool ignoreGlobalJson = false)
    {
        IEnvShellProvider? shellProvider = command.ShellProvider ?? ShellDetection.GetCurrentShellProvider();
        DotnetAccessMode accessMode = GetDefaultAccessMode(shellProvider, configuredAccessMode);
        // Dry-run previews the form as it would look in an ordinary directory, so global.json (which
        // could pin a local install path or channel) is ignored.
        var globalJson = ignoreGlobalJson
            ? new GlobalJsonInfo()
            : GlobalJsonModifier.GetGlobalJsonInfo(Environment.CurrentDirectory);
        var pathResolution = new InstallPathResolver(dotnetEnvironment).Resolve(
            command.InstallPath, globalJson);

        if (preResolvedRequests is { Count: > 0 })
        {
            var first = preResolvedRequests[0];
            DotnetInstallRoot resolvedRoot = first.Request.InstallRoot;
            var resolvedMigrations = ResolveDefaultMigrations(
                dotnetEnvironment, resolvedRoot, first.Request.Options.ManifestPath);
            var preResolvedInstallSpecs = preResolvedRequests
                .Select(request => new MinimalInstallSpec(request.Request.Component, request.Request.Channel.Name))
                .ToList();

            return new InitFormDefaults(
                resolvedRoot,
                accessMode,
                ShouldMigrateSystemInstallsByDefault(
                    accessMode,
                    resolvedMigrations,
                    preResolvedInstallSpecs),
                resolvedMigrations,
                new DefaultChannelDisplay(first.Request.Channel.Name, first.Request.Options.GlobalJsonPath),
                preResolvedInstallSpecs,
                shellProvider,
                GetInstallRootGlobalJsonPath(pathResolution, globalJson, resolvedRoot));
        }

        var installRoot = new DotnetInstallRoot(
            pathResolution.ResolvedInstallPath,
            InstallerUtilities.GetDefaultInstallArchitecture());

        var migrations = ResolveDefaultMigrations(dotnetEnvironment, installRoot, command.ManifestPath);
        DefaultChannelDisplay channelDisplay = ResolveChannelDisplay(globalJson);

        MinimalInstallSpec[] defaultInstallSpecs =
            [new(InstallComponent.SDK, channelDisplay.ChannelLabel)];

        return new InitFormDefaults(
            installRoot,
            accessMode,
            ShouldMigrateSystemInstallsByDefault(accessMode, migrations, defaultInstallSpecs),
            migrations,
            channelDisplay,
            defaultInstallSpecs,
            shellProvider,
            GetInstallRootGlobalJsonPath(pathResolution, globalJson, installRoot));
    }

    private static bool ShouldMigrateSystemInstallsByDefault(
        DotnetAccessMode accessMode,
        List<MigrationWorkflow.MigrationSelection> migrations,
        IReadOnlyCollection<MinimalInstallSpec> defaultInstallSpecs) =>
        DotnetAccessModePolicy.ShouldMigrateSystemInstallsByDefault(accessMode)
        && MigrationWorkflow.FilterMigrationSelections(migrations, defaultInstallSpecs).Count > 0;

    private static string? GetInstallRootGlobalJsonPath(
        InstallPathResolver.InstallPathResolutionResult pathResolution,
        GlobalJsonInfo globalJson,
        DotnetInstallRoot installRoot)
        => pathResolution.PathSource is PathSource.GlobalJson
            && DotnetupUtilities.PathsEqual(pathResolution.ResolvedInstallPath, installRoot.Path)
                ? globalJson.GlobalJsonPath
                : null;

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
        return requests ?? GenerateSdkInstallRequests(command, channel: null);
    }

    /// <summary>
    /// Generates a single SDK install request for the given channel (or the default channel when
    /// null), resolving the path, global.json, version, and validation through the install workflow.
    /// </summary>
    public static List<ResolvedInstallRequest> GenerateSdkInstallRequests(InstallCommand command, string? channel)
    {
        return GenerateInstallRequests(command, [new MinimalInstallSpec(InstallComponent.SDK, channel)]);
    }

    /// <summary>
    /// Generates resolved install requests for the supplied component specifications.
    /// </summary>
    public static List<ResolvedInstallRequest> GenerateInstallRequests(
        InstallCommand command,
        MinimalInstallSpec[] componentSpecs)
    {
        var workflow = new InstallWorkflow(command);
        return workflow.GenerateInstallRequests(componentSpecs);
    }

    /// <summary>
    /// Returns the access mode to select by default without prompting. A configured, supported mode
    /// takes precedence. Otherwise, Windows defaults to everywhere mode; other platforms use
    /// terminal-profile mode when a supported shell is available and isolation mode when it is not.
    /// </summary>
    public static DotnetAccessMode GetDefaultAccessMode(
        IEnvShellProvider? shellProvider = null,
        DotnetAccessMode? configuredAccessMode = null)
    {
        if (configuredAccessMode is DotnetAccessMode configured &&
            DotnetAccessModePolicy.IsSupportedOnCurrentPlatform(configured))
        {
            return configured;
        }

        // Default to Everywhere mode on Windows
        if (OperatingSystem.IsWindows())
        {
            return DotnetAccessMode.Everywhere;
        }

        if ((shellProvider ?? ShellDetection.GetCurrentShellProvider()) is null)
        {
            return DotnetAccessMode.None;
        }

        return DotnetAccessMode.Shell;
    }

    /// <summary>
    /// Preserves the configured dotnetup PATH setting when init is rerun, defaulting on for first use.
    /// </summary>
    public static bool GetDefaultDotnetupOnPath(DotnetupConfigData? configured) =>
        configured?.DotnetupOnPath ?? true;

    /// <summary>
    /// Builds the deduplicated migration candidates without prompting or considering pending install
    /// requests. Migration is independent of how dotnetup's installs are exposed through PATH or the
    /// shell environment.
    /// </summary>
    public static List<MigrationWorkflow.MigrationSelection> ResolveDefaultMigrations(
        IDotnetEnvironmentManager dotnetEnvironment,
        DotnetInstallRoot installRoot,
        string? manifestPath)
    {
        var systemInstalls = MigrationWorkflow.GetMigrationCandidates(dotnetEnvironment);
        return MigrationWorkflow.BuildMigrationSelections(systemInstalls, installRoot, manifestPath);
    }

    /// <summary>
    /// Resolves the default channel label directly from global.json (or "latest") without resolving
    /// a concrete version, so displaying the form never triggers a network call.
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
