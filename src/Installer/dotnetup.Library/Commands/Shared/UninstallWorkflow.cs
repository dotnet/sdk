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
    /// <param name="componentFilter">Which component to target.</param>
    /// <param name="interactive">Whether to confirm unexpected outcomes before changing state.</param>
    /// <param name="confirm">Optional confirmation callback for testing.</param>
    public static void Execute(
        string? manifestPath, string? installPath, string versionOrChannel,
        InstallComponent componentFilter, bool interactive = false,
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
                string.Format(CultureInfo.InvariantCulture, Strings.UninstallNoTrackedInstallations, resolvedInstallPath));
        }

        var installRoot = new DotnetInstallRoot(root.Path, root.Architecture);

        var matchingSpecs = FindMatchingSpecs(root, versionOrChannel, componentFilter);

        var targetedInstallations = root.Installations
            .Where(i => i.Component == componentFilter &&
                        matchingSpecs.Any(s => new UpdateChannel(s.VersionOrChannel).Matches(
                            new Microsoft.Deployment.DotNet.Releases.ReleaseVersion(i.Version))))
            .ToList();

        RemoveSpecsAndRunGc(manifest, manifestData, installRoot, matchingSpecs, targetedInstallations, interactive, confirm);

        AnsiConsole.MarkupLine(DotnetupTheme.Brand(Strings.CommandDone.EscapeMarkup()));
    }

    private static List<InstallSpec> FindMatchingSpecs(
        DotnetRootEntry root, string versionOrChannel, InstallComponent componentFilter)
    {
        var allMatchingSpecs = root.InstallSpecs
            .Where(s => s.Component == componentFilter &&
                        string.Equals(s.VersionOrChannel, versionOrChannel, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var matchingSpecs = allMatchingSpecs
            .Where(s => s.InstallSource is InstallSource.Explicit or InstallSource.Migration)
            .ToList();

        if (matchingSpecs.Count == 0)
        {
            ReportRepositoryRequirements(allMatchingSpecs);
            throw new DotnetInstallException(
                DotnetInstallErrorCode.UninstallTargetNotFound,
                string.Format(CultureInfo.InvariantCulture, Strings.UninstallNoMatchingInstallations,
                    componentFilter.GetDisplayName(), versionOrChannel, root.Path));
        }

        return matchingSpecs;
    }

    private static void ReportRepositoryRequirements(IEnumerable<InstallSpec> specs)
    {
        var repositorySpecs = specs.Where(s => s.InstallSource == InstallSource.GlobalJson).ToList();
        if (repositorySpecs.Count > 0)
        {
            AnsiConsole.MarkupLine(DotnetupTheme.Dim(Strings.UninstallRepositoryRequirement.EscapeMarkup()));
            foreach (var spec in repositorySpecs)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [{DotnetupTheme.Current.Dim}]{spec.GlobalJsonPath}[/]");
            }
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
        var hasWarnings = UninstallPreview.Display(
            plan, targetedInstallations, matchingSpecs[0].Component, matchingSpecs[0].VersionOrChannel, matchingSpecs);
        if (hasWarnings && interactive &&
            (confirm?.Invoke() ?? SpectreDisplayHelpers.Confirm(Strings.UninstallConfirmationPrompt)) != ConfirmResult.Yes)
        {
            throw new DotnetInstallException(DotnetInstallErrorCode.OperationCancelled, Strings.UninstallCancelled);
        }

        foreach (var spec in matchingSpecs.DistinctBy(s => (s.Component, s.VersionOrChannel, s.InstallSource, s.GlobalJsonPath)))
        {
            AnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, Strings.UninstallDereferencedSpec,
                spec.Component.GetDisplayName().EscapeMarkup(), DotnetupTheme.Accent(spec.VersionOrChannel.EscapeMarkup()),
                DotnetupTheme.Dim(string.Format(CultureInfo.InvariantCulture, Strings.InstallSpecSource, spec.InstallSource))));
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
