// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Explains retained targets and unexpected GC removals from the prepared uninstall snapshot.
/// Does not resolve specs again or modify installation state.
/// </summary>
internal static class UninstallPreview
{
    internal static bool Display(
        GarbageCollectionPlan plan, List<Installation> targets,
        InstallComponent requestedComponent, string versionOrChannel, IReadOnlyList<InstallSpec> specsToRemove)
    {
        var hasWarnings = false;
        foreach (var target in targets.DistinctBy(i => (i.Component, i.Version)))
        {
            var specs = plan.GetReferencingSpecs(target);
            if (specs.Count == 0)
            {
                continue;
            }

            hasWarnings = true;
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture,
                $"[{DotnetupTheme.Current.Accent}]{target.Component.GetDisplayName()} {target.Version}[/] will [bold]not[/] be uninstalled because it has other install specs that require it:");
            foreach (var spec in specs)
            {
                var source = spec.GlobalJsonPath ?? spec.InstallSource.ToString();
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture,
                    $"  {spec.Component.GetDisplayName()} {spec.VersionOrChannel} [{DotnetupTheme.Current.Dim}](source: {source})[/]");
            }
        }

        var hasRetainedTargets = hasWarnings;
        var deletedPaths = plan.PathsToDelete.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unexpected = plan.InstallationsToRemove.Except(targets)
            .Where(i => plan.GetPrimaryPath(i) is { } path && deletedPaths.Contains(path))
            .ToList();
        AddUntrackedRemovals(plan, targets, unexpected);
        var channel = new UpdateChannel(versionOrChannel);
        foreach (var installation in unexpected.DistinctBy(i => (i.Component, i.Version))
            .Where(i => i.Component != requestedComponent ||
                !ReleaseVersion.TryParse(i.Version, out var version) || !channel.Matches(version)))
        {
            hasWarnings = true;
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture,
                $"[{DotnetupTheme.Current.Accent}]{installation.Component.GetDisplayName()} {installation.Version}[/] will be [bold]uninstalled[/] because it is no longer referenced by any install specs.");
        }

        if (hasWarnings)
        {
            DisplayRemovalSummary(specsToRemove, hasRetainedTargets);
        }

        return hasWarnings;
    }

    private static void DisplayRemovalSummary(IReadOnlyList<InstallSpec> specsToRemove, bool hasRetainedTargets)
    {
        AnsiConsole.WriteLine();
        if (hasRetainedTargets)
        {
            AnsiConsole.WriteLine("The retained versions listed above will remain installed. To fully uninstall them, you must also remove the other install specs keeping them installed.");
        }

        foreach (var spec in specsToRemove.DistinctBy(s => (s.Component, s.VersionOrChannel, s.InstallSource, s.GlobalJsonPath)))
        {
            var source = spec.GlobalJsonPath ?? spec.InstallSource.ToString();
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture,
                $"Install spec '[{DotnetupTheme.Current.Accent}]{spec.VersionOrChannel}[/]' ({spec.Component.GetDisplayName()}; source: {source}) will be removed from tracking.");
        }
    }

    private static void AddUntrackedRemovals(
        GarbageCollectionPlan plan, List<Installation> targets, List<Installation> unexpected)
    {
        var knownPaths = plan.InstallationsToRemove.SelectMany(i => i.Subcomponents)
            .Concat(targets.SelectMany(i => i.Subcomponents))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in plan.PathsToDelete.Where(p => !knownPaths.Contains(p)))
        {
            var parts = path.Split('/');
            InstallComponent? component = parts switch
            {
                ["sdk", _] => InstallComponent.SDK,
                ["shared", InstallComponentExtensions.RuntimeFrameworkName, _] => InstallComponent.Runtime,
                ["shared", InstallComponentExtensions.AspNetCoreFrameworkName, _] => InstallComponent.ASPNETCore,
                ["shared", InstallComponentExtensions.WindowsDesktopFrameworkName, _] => InstallComponent.WindowsDesktop,
                _ => null,
            };
            if (component is not null && !unexpected.Any(i => i.Component == component && i.Version == parts[^1]))
            {
                unexpected.Add(new Installation { Component = component.Value, Version = parts[^1] });
            }
        }
    }
}
