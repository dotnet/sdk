// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Dotnet.Installation;
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
    /// <param name="interactive">Whether to confirm unexpected outcomes before changing state.</param>
    /// <param name="confirm">Optional confirmation callback for testing.</param>
    public static void Execute(
        string? manifestPath, string? installPath, string versionOrChannel,
        InstallSource sourceFilter, InstallComponent componentFilter, bool interactive = false,
        Func<ConfirmResult>? confirm = null)
    {
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);

        var manifest = new DotnetupSharedManifest(manifestPath);
        var manifestData = manifest.ReadManifest(persistPruning: false);

        var dotnetEnvironment = new DotnetEnvironmentManager();
        string resolvedInstallPath = ResolveInstallPath(installPath, dotnetEnvironment);

        var root = manifestData.DotnetRoots.FirstOrDefault(r =>
            DotnetupUtilities.PathsEqual(Path.GetFullPath(r.Path), Path.GetFullPath(resolvedInstallPath)));

        if (root is null)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.UninstallTargetNotFound,
                $"No tracked installations found at {resolvedInstallPath}.");
        }

        var installRoot = new DotnetInstallRoot(root.Path, root.Architecture);

        var matchingSpecs = FindMatchingSpecs(root, versionOrChannel, sourceFilter, componentFilter);

        var targetedInstallations = root.Installations
            .Where(i => i.Component == componentFilter &&
                        matchingSpecs.Any(s => new UpdateChannel(s.VersionOrChannel).Matches(
                            new Microsoft.Deployment.DotNet.Releases.ReleaseVersion(i.Version))))
            .ToList();

        RemoveSpecsAndRunGc(manifest, manifestData, installRoot, matchingSpecs, targetedInstallations, interactive, confirm);

        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[{DotnetupTheme.Current.Brand}]Done.[/]");
    }

    private static List<InstallSpec> FindMatchingSpecs(
        DotnetRootEntry root, string versionOrChannel, InstallSource sourceFilter, InstallComponent componentFilter)
    {
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
            ReportNoMatchingSpecs(allMatchingSpecs, matchingSpecs, sourceFilter, componentFilter, versionOrChannel, root.Path);
            throw new DotnetInstallException(
                DotnetInstallErrorCode.UninstallTargetNotFound,
                $"No tracked installations matched component={componentFilter}, version='{versionOrChannel}', source={sourceFilter} at {root.Path}.");
        }

        return matchingSpecs;
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
                    $"[{DotnetupTheme.Current.Warning}]No [bold]{sourceFilter}[/] {componentFilter.GetDisplayName()} install spec found for '{versionOrChannel.EscapeMarkup()}', but matching specs exist with other sources:[/]");
            }
            else
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture,
                    $"[{DotnetupTheme.Current.Warning}]No {componentFilter.GetDisplayName()} install spec found for '{versionOrChannel.EscapeMarkup()}', but matching specs exist with other sources:[/]");
            }

            foreach (var spec in otherSourceSpecs)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [{DotnetupTheme.Current.Dim}]{spec.Component.GetDisplayName()} {spec.VersionOrChannel.EscapeMarkup()} (source: {spec.InstallSource})[/]");
            }

            if (sourceFilter != InstallSource.All)
            {
                AnsiConsole.MarkupLine(DotnetupTheme.Dim("Use --source all to target these specs."));
            }
        }
        else
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[{DotnetupTheme.Current.Warning}]No {componentFilter.GetDisplayName()} install spec found for '{versionOrChannel.EscapeMarkup()}' at {resolvedInstallPath.EscapeMarkup()}.[/]");
        }
    }

    private static void RemoveSpecsAndRunGc(
        DotnetupSharedManifest manifest,
        DotnetupManifestData manifestData,
        DotnetInstallRoot installRoot,
        List<InstallSpec> matchingSpecs,
        List<Installation> targetedInstallations,
        bool interactive,
        Func<ConfirmResult>? confirm)
    {
        var root = manifestData.DotnetRoots.First(r => r.Path == installRoot.Path && r.Architecture == installRoot.Architecture);
        foreach (var spec in matchingSpecs)
        {
            root.InstallSpecs.Remove(spec);
        }

        var plan = GarbageCollector.CreatePlan(installRoot, manifestData);
        if (UninstallPreview.Display(plan, targetedInstallations, matchingSpecs[0].Component, matchingSpecs[0].VersionOrChannel) && interactive &&
            (confirm?.Invoke() ?? SpectreDisplayHelpers.Confirm("Proceed with uninstall?")) != ConfirmResult.Yes)
        {
            throw new DotnetInstallException(DotnetInstallErrorCode.OperationCancelled, "Uninstall cancelled. No install specs or files were removed.");
        }

        foreach (var spec in matchingSpecs.DistinctBy(s => (s.Component, s.VersionOrChannel, s.InstallSource, s.GlobalJsonPath)))
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"Dereferenced {spec.Component.GetDisplayName()} [{DotnetupTheme.Current.Accent}]{spec.VersionOrChannel}[/] [{DotnetupTheme.Current.Dim}](source: {spec.InstallSource})[/]");
        }

        GarbageCollectionRunner.ApplyAndDisplay(new GarbageCollector(manifest), plan);
    }

    /// <summary>
    /// Resolves the install path for uninstall using the same logic as the install command:
    /// only use the configured path if it is a dotnetup-managed hive, otherwise fall back to
    /// default. This prevents uninstall from targeting a dotnet that dotnetup does not own (e.g.
    /// a system install or a hand-extracted dotnet that happens to win on PATH).
    /// </summary>
    internal static string ResolveInstallPath(string? explicitInstallPath, IDotnetEnvironmentManager dotnetEnvironment)
    {
        if (explicitInstallPath is not null)
        {
            return explicitInstallPath;
        }

        var configuredInstall = dotnetEnvironment.GetCurrentPathConfiguration();
        if (configuredInstall is { IsDotnetupHive: true })
        {
            return configuredInstall.Path;
        }

        return dotnetEnvironment.GetDefaultDotnetInstallPath();
    }
}
