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
            AnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, Strings.UninstallRetainedInstallation,
                DotnetupTheme.Accent($"{target.Component.GetDisplayName()} {target.Version}".EscapeMarkup())));
            foreach (var spec in specs)
            {
                var source = spec.GlobalJsonPath ?? spec.InstallSource.ToString();
                AnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture,
                    "  {0} {1} {2}", spec.Component.GetDisplayName().EscapeMarkup(), spec.VersionOrChannel.EscapeMarkup(),
                    DotnetupTheme.Dim(string.Format(CultureInfo.InvariantCulture, Strings.InstallSpecSource, source.EscapeMarkup()))));
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
            AnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, Strings.UninstallUnexpectedRemoval,
                DotnetupTheme.Accent($"{installation.Component.GetDisplayName()} {installation.Version}".EscapeMarkup())));
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
            AnsiConsole.WriteLine(Strings.UninstallRetainedGuidance);
        }

        foreach (var spec in specsToRemove.DistinctBy(s => (s.Component, s.VersionOrChannel, s.InstallSource, s.GlobalJsonPath)))
        {
            var source = spec.GlobalJsonPath ?? spec.InstallSource.ToString();
            AnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, Strings.UninstallRemovalSummary,
                DotnetupTheme.Accent(spec.VersionOrChannel.EscapeMarkup()), spec.Component.GetDisplayName().EscapeMarkup(),
                source.EscapeMarkup()));
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
