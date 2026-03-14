// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Dotnet.Installation.Internal;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Shared uninstall workflow that can be used by both SDK and runtime uninstall commands.
/// </summary>
internal class UninstallWorkflow
{
    /// <summary>
    /// Uninstalls install specs matching the given parameters and runs garbage collection.
    /// </summary>
    /// <param name="manifestPath">Custom manifest path, or null for default.</param>
    /// <param name="installPath">Specific install path, or null for default.</param>
    /// <param name="versionOrChannel">The channel/version to uninstall.</param>
    /// <param name="sourceFilter">Which install source to filter by.</param>
    /// <param name="componentFilter">Which component to target.</param>
    /// <returns>Exit code (0 for success).</returns>
    public static int Execute(string? manifestPath, string? installPath, string versionOrChannel, InstallSource sourceFilter, InstallComponent componentFilter)
    {
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);

        var manifest = new DotnetupSharedManifest(manifestPath);
        var manifestData = manifest.ReadManifest();

        // Resolve install path
        var dotnetInstaller = new DotnetInstallManager();
        string resolvedInstallPath = installPath
            ?? dotnetInstaller.GetConfiguredInstallType()?.Path
            ?? dotnetInstaller.GetDefaultDotnetInstallPath();

        var root = manifestData.DotnetRoots.FirstOrDefault(r =>
            DotnetupUtilities.PathsEqual(Path.GetFullPath(r.Path), Path.GetFullPath(resolvedInstallPath)));

        if (root is null)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.UninstallTargetNotFound,
                $"No tracked installations found at {resolvedInstallPath}.");
        }

        var installRoot = new DotnetInstallRoot(root.Path, root.Architecture);

        // Find all specs matching the channel and component
        var allMatchingSpecs = root.InstallSpecs
            .Where(s => s.Component == componentFilter &&
                        string.Equals(s.VersionOrChannel, versionOrChannel, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Filter by source
        var matchingSpecs = allMatchingSpecs
            .Where(s => sourceFilter == InstallSource.All || s.InstallSource == sourceFilter)
            .ToList();

        if (matchingSpecs.Count == 0)
        {
            ReportNoMatchingSpecs(allMatchingSpecs, matchingSpecs, sourceFilter, componentFilter, versionOrChannel, resolvedInstallPath);
            return 1;
        }

        // Snapshot installations matching the target component/channel before GC
        var targetedInstallations = root.Installations
            .Where(i => i.Component == componentFilter &&
                        matchingSpecs.Any(s => new UpdateChannel(s.VersionOrChannel).Matches(
                            new Microsoft.Deployment.DotNet.Releases.ReleaseVersion(i.Version))))
            .Select(i => (i.Component, i.Version))
            .ToHashSet();

        RemoveSpecsAndRunGc(manifest, installRoot, matchingSpecs, manifestPath);

        // Check if the targeted installations are still present (referenced by another spec)
        CheckAndReportStillPresent(manifestPath, installRoot, targetedInstallations);

        AnsiConsole.MarkupLine(DotnetupTheme.Success("Done."));
        return 0;
    }

    private static void ReportNoMatchingSpecs(
        List<InstallSpec> allMatchingSpecs,
        List<InstallSpec> matchingSpecs,
        InstallSource sourceFilter,
        InstallComponent componentFilter,
        string versionOrChannel,
        string resolvedInstallPath)
    {
        // Check if there are matches with other sources
        var otherSourceSpecs = allMatchingSpecs.Except(matchingSpecs).ToList();
        if (otherSourceSpecs.Count > 0)
        {
            if (sourceFilter != InstallSource.All)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture,
                    $"[{DotnetupTheme.Current.Warning}]No [bold]{sourceFilter}[/] {componentFilter.GetDisplayName()} install spec found for '{versionOrChannel}', but matching specs exist with other sources:[/]");
            }
            else
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture,
                    $"[{DotnetupTheme.Current.Warning}]No {componentFilter.GetDisplayName()} install spec found for '{versionOrChannel}', but matching specs exist with other sources:[/]");
            }

            foreach (var spec in otherSourceSpecs)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [{DotnetupTheme.Current.Dim}]{spec.Component.GetDisplayName()} {spec.VersionOrChannel} (source: {spec.InstallSource})[/]");
            }

            if (sourceFilter != InstallSource.All)
            {
                AnsiConsole.MarkupLine(DotnetupTheme.Dim("Use --source all to target these specs."));
            }
        }
        else
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[{DotnetupTheme.Current.Warning}]No {componentFilter.GetDisplayName()} install spec found for '{versionOrChannel}' at {resolvedInstallPath}.[/]");
        }
    }

    private static void RemoveSpecsAndRunGc(
        DotnetupSharedManifest manifest,
        DotnetInstallRoot installRoot,
        List<InstallSpec> matchingSpecs,
        string? manifestPath)
    {
        // Remove the install spec(s)
        foreach (var spec in matchingSpecs)
        {
            manifest.RemoveInstallSpec(installRoot, spec);
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"Removed install spec: {spec.Component.GetDisplayName()} [{DotnetupTheme.Current.Accent}]{spec.VersionOrChannel}[/] (source: {spec.InstallSource})");
        }

        // Run garbage collection
        GarbageCollectionRunner.RunAndDisplay(manifestPath, installRoot, showEmptyMessage: true);
    }

    private static void CheckAndReportStillPresent(
        string? manifestPath,
        DotnetInstallRoot installRoot,
        HashSet<(InstallComponent Component, string Version)> targetedInstallations)
    {
        if (targetedInstallations.Count > 0)
        {
            var updatedManifest = new DotnetupSharedManifest(manifestPath);
            var stillPresent = updatedManifest.GetInstallations(installRoot)
                .Where(i => targetedInstallations.Contains((i.Component, i.Version)))
                .ToList();

            if (stillPresent.Count > 0)
            {
                AnsiConsole.MarkupLine(DotnetupTheme.Dim("Some installations were not removed because they are still referenced by other install specs."));
            }
        }
    }
}
