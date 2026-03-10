// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Shared update workflow that can be used by both SDK and runtime update commands.
/// </summary>
internal class UpdateWorkflow
{
    private readonly ChannelVersionResolver _channelVersionResolver;

    public UpdateWorkflow(ChannelVersionResolver channelVersionResolver)
    {
        _channelVersionResolver = channelVersionResolver;
    }

    /// <summary>
    /// Updates all install specs matching the given component filter.
    /// </summary>
    /// <param name="manifestPath">Custom manifest path, or null for default.</param>
    /// <param name="installPath">Specific install path to update, or null for all roots.</param>
    /// <param name="componentFilter">Which component(s) to update. Null means update all.</param>
    /// <param name="noProgress">Whether to suppress progress display.</param>
    /// <param name="updateGlobalJson">Whether to update global.json files after updating global.json-sourced SDK specs.</param>
    /// <returns>Exit code (0 for success).</returns>
    public int Execute(string? manifestPath, string? installPath, InstallComponent? componentFilter, bool noProgress, bool updateGlobalJson = false)
    {
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);

        var manifest = new DotnetupSharedManifest(manifestPath);
        var manifestData = manifest.ReadManifest();

        // Determine which dotnet root(s) to update
        // TODO: Discuss whether we should update all install roots or just the default one when --install-path is not specified.
        var rootsToUpdate = manifestData.DotnetRoots.AsEnumerable();
        if (!string.IsNullOrEmpty(installPath))
        {
            rootsToUpdate = rootsToUpdate.Where(r =>
                DotnetupUtilities.PathsEqual(Path.GetFullPath(r.Path), Path.GetFullPath(installPath)));
        }

        var rootsList = rootsToUpdate.ToList();
        if (rootsList.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No tracked dotnet installations found to update.[/]");
            return 0;
        }

        bool anyUpdated = false;
        bool anyFailed = false;

        foreach (var root in rootsList)
        {
            bool rootUpdated = false;
            var installRoot = new DotnetInstallRoot(root.Path, root.Architecture);

            foreach (var spec in root.InstallSpecs.ToList())
            {
                if (componentFilter is not null && spec.Component != componentFilter.Value)
                {
                    continue;
                }

                var (updated, failed) = UpdateSpec(spec, root, installRoot, manifestPath, noProgress, updateGlobalJson);
                if (updated) { anyUpdated = true; rootUpdated = true; }
                if (failed) { anyFailed = true; }
            }

            // Run garbage collection
            if (rootUpdated)
            {
                GarbageCollectionRunner.RunAndDisplay(manifestPath, installRoot);
            }
        }

        if (!anyUpdated)
        {
            AnsiConsole.MarkupLine("Everything is up to date.");
        }

        return anyFailed ? 1 : 0;
    }

    /// <summary>
    /// Processes a single install spec: checks for updates, installs if newer version available,
    /// and optionally updates the corresponding global.json.
    /// </summary>
    /// <returns>A tuple of (wasUpdated, hadFailure).</returns>
    private (bool Updated, bool Failed) UpdateSpec(
        InstallSpec spec,
        DotnetRootEntry root,
        DotnetInstallRoot installRoot,
        string? manifestPath,
        bool noProgress,
        bool updateGlobalJson)
    {
        var channel = new UpdateChannel(spec.VersionOrChannel);

        // Skip fully-specified versions — they can't be updated
        if (channel.IsFullySpecifiedVersion())
        {
            return (false, false);
        }

        var latestVersion = _channelVersionResolver.GetLatestVersionForChannel(channel, spec.Component);
        string displayName = spec.Component.GetDisplayName();
        if (latestVersion is null)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]Could not resolve latest version for {displayName} '{spec.VersionOrChannel}'.[/]");
            return (false, false);
        }

        // Check if this version is already installed
        var alreadyInstalled = root.Installations.Any(i =>
            i.Component == spec.Component && i.Version == latestVersion.ToString());

        bool updated = false;
        if (alreadyInstalled)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]{displayName} {spec.VersionOrChannel} is already up to date ({latestVersion}).[/]");
        }
        else
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"Updating {displayName} {spec.VersionOrChannel} to [blue]{latestVersion}[/]...");

            var installRequest = new DotnetInstallRequest(
                installRoot,
                channel,
                spec.Component,
                new InstallRequestOptions { ManifestPath = manifestPath, SkipInstallSpecRecording = true })
            {
                ResolvedVersion = latestVersion
            };

            try
            {
                var result = InstallerOrchestratorSingleton.Instance.Install(installRequest, noProgress);
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]Updated {displayName} {spec.VersionOrChannel} to {latestVersion}.[/]");
                updated = true;
            }
            catch (DotnetInstallException)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]Failed to update {displayName} {spec.VersionOrChannel} to {latestVersion}.[/]");
                return (false, true);
            }
        }

        // Update global.json if requested and this spec came from a global.json,
        // but only if the latest version is newer than what's already specified.
        if (updateGlobalJson
            && spec.InstallSource == InstallSource.GlobalJson
            && spec.GlobalJsonPath is not null
            && spec.Component == InstallComponent.SDK)
        {
            UpdateGlobalJsonFile(spec.GlobalJsonPath, latestVersion);
        }

        return (updated, false);
    }

    /// <summary>
    /// Updates a global.json file to the latest version if it's newer than the current one.
    /// </summary>
    private static void UpdateGlobalJsonFile(string globalJsonPath, ReleaseVersion latestVersion)
    {
        string? currentVersionString = null;
        try
        {
            var json = File.ReadAllText(globalJsonPath);
            var contents = System.Text.Json.JsonSerializer.Deserialize(json, GlobalJsonContentsJsonContext.Default.GlobalJsonContents);
            currentVersionString = contents?.Sdk?.Version;
        }
        catch { }

        if (currentVersionString is null
            || !ReleaseVersion.TryParse(currentVersionString, out var currentVersion)
            || latestVersion > currentVersion)
        {
            new DotnetInstallManager().UpdateGlobalJson(globalJsonPath, latestVersion.ToString());
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  Updated [dim]{globalJsonPath}[/] to {latestVersion}.");
        }
    }
}
