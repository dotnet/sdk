// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.List;

/// <summary>
/// Builds and renders the human-readable list without changing the manifest or
/// expanding SDK subcomponents into separately installed runtimes.
/// </summary>
internal static class InstallationListRenderer
{
    internal static List<InstallationListRow> CreateRows(IEnumerable<InstallSpecInfo> specs, List<InstallationInfo> installations)
    {
        var installationInfo = installations.ToDictionary(
            i => new Installation { Component = i.Component, Version = i.Version }, i => i);
        var rows = new List<InstallationListRow>();
        foreach (var spec in specs)
        {
            var candidates = installationInfo.Where(i => i.Value.Architecture == spec.Architecture
                && DotnetupUtilities.PathsEqual(i.Value.InstallRoot, spec.InstallRoot)).Select(i => i.Key);
            var resolution = InstallSpecResolver.Resolve(new InstallSpec
            {
                Component = spec.Component,
                VersionOrChannel = spec.VersionOrChannel,
                InstallSource = spec.Source,
                GlobalJsonPath = spec.GlobalJsonPath
            }, candidates);
            var row = CreateSpecRow(spec, resolution,
                resolution.Installation is { } selected ? installationInfo[selected] : null);
            if (row is not null)
            {
                rows.Add(row);
            }
        }

        foreach (var installation in installations.Where(i => !rows.Any(r => r.Installation == i)))
        {
            rows.Add(new InstallationListRow { Component = installation.Component, Installation = installation });
        }

        return rows.OrderBy(r => r.Component == InstallComponent.SDK ? 0 : 1)
            .ThenBy(r => r.Installation is not null ? 0 : r.Error is null ? 1 : 2)
            .ThenByDescending(r => ReleaseVersion.TryParse(r.Installation?.Version, out var version) ? version : null)
            .ThenBy(r => ComponentOrder(r.Component))
            .ThenBy(r => r.SpecDisplay, StringComparer.Ordinal)
            .ThenBy(r => r.SourceDisplay, StringComparer.Ordinal)
            .ToList();
    }

    private static InstallationListRow? CreateSpecRow(
        InstallSpecInfo spec, InstallSpecResolution resolution, InstallationInfo? selected)
    {
        if (!resolution.IsActive)
        {
            return null;
        }

        var source = spec.Source switch
        {
            InstallSource.Explicit => Strings.ListCommandLineSource,
            InstallSource.Migration => Strings.ListMigrationSource,
            InstallSource.GlobalJson => spec.GlobalJsonPath ?? Strings.ListUnavailable,
            _ => spec.Source.ToString()
        };
        var display = resolution.GlobalJsonSdk is { } requirement
            ? FormatRequirement(requirement) : spec.VersionOrChannel;
        return new InstallationListRow
        {
            Component = spec.Component,
            Spec = spec,
            Installation = selected,
            SpecDisplay = resolution.Error is null ? display : Strings.ListUnavailable,
            SourceDisplay = source,
            Error = resolution.Error
        };
    }

    private static string FormatRequirement(GlobalJsonContents.SdkSection requirement)
    {
        var policies = new List<string>();
        if (requirement.RollForward is not null)
        {
            policies.Add($"rollForward: {requirement.RollForward}");
        }
        if (requirement.AllowPrerelease is bool allowPrerelease)
        {
            policies.Add($"allowPrerelease: {allowPrerelease.ToString().ToLowerInvariant()}");
        }
        return policies.Count == 0 ? requirement.Version!
            : $"{requirement.Version} ({string.Join(", ", policies)})";
    }

    public static void Write(TextWriter writer, ListData listData, int? width = null)
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(writer) });
        console.Profile.Width = width ?? AnsiConsole.Profile.Width;

        var roots = listData.InstallSpecs.Select(s => new { s.InstallRoot, s.Architecture })
            .Union(listData.Installations.Select(i => new { i.InstallRoot, i.Architecture })).ToList();
        var specCount = 0;
        var installationCount = 0;
        var sdkCount = 0;
        var diagnostics = new List<string>();
        foreach (var root in roots)
        {
            var specs = listData.InstallSpecs.Where(s => s.InstallRoot == root.InstallRoot && s.Architecture == root.Architecture);
            var installations = listData.Installations.Where(i => i.InstallRoot == root.InstallRoot && i.Architecture == root.Architecture)
                .DistinctBy(i => new { i.Component, i.Version }).ToList();
            var rows = CreateRows(specs, installations);
            specCount += rows.Count(r => r.Spec is not null);
            installationCount += installations.Count;
            sdkCount += installations.Count(i => i.Component == InstallComponent.SDK);

            if (roots.Count > 1)
            {
                writer.WriteLine($"{root.InstallRoot} ({root.Architecture})");
            }
            if (rows.Count == 0)
            {
                continue;
            }

            console.Write(CreateTable(rows));
            foreach (var row in rows.Where(r => r.Error is not null))
            {
                diagnostics.Add(row.Error!);
            }
            foreach (var installation in installations.Where(i => i.IsValid == false))
            {
                diagnostics.Add(string.Format(CultureInfo.InvariantCulture, Strings.ListValidationError,
                    ComponentName(installation.Component), installation.Version, installation.ValidationFailure));
            }
        }

        if (roots.Count == 0)
        {
            writer.WriteLine(Strings.ListNoInstallations);
        }
        writer.WriteLine(string.Format(CultureInfo.InvariantCulture, Strings.ListSummary,
            FormatCount(specCount, Strings.ListOneSpec, Strings.ListSpecCount),
            FormatCount(installationCount, Strings.ListOneInstallation, Strings.ListInstallationCount),
            FormatCount(sdkCount, Strings.ListOneSdk, Strings.ListSdkCount),
            FormatCount(installationCount - sdkCount, Strings.ListOneRuntime, Strings.ListRuntimeCount)));
        foreach (var diagnostic in diagnostics)
        {
            writer.WriteLine(diagnostic);
        }
    }

    private static string FormatCount(int count, string singular, string plural) =>
        count == 1 ? singular : string.Format(CultureInfo.InvariantCulture, plural, count);

    private static Table CreateTable(List<InstallationListRow> rows)
    {
        var table = new Table().Border(TableBorder.Simple);
        table.AddColumn(new TableColumn(new Text(Strings.ListComponentColumn)));
        table.AddColumn(new TableColumn(new Text(Strings.ListSpecColumn)));
        table.AddColumn(new TableColumn(new Text(Strings.ListSourceColumn)));
        table.AddColumn(new TableColumn(new Text(Strings.ListInstalledVersionColumn)));
        foreach (var row in rows)
        {
            var version = row.Installation?.Version ?? (row.Error is null ? Strings.ListNotInstalled : Strings.ListUnknown);
            if (row.Installation?.IsValid == false)
            {
                version = string.Format(CultureInfo.InvariantCulture, Strings.ListInvalidInstallation, version);
            }
            table.AddRow(new Text(ComponentName(row.Component)), new Text(row.SpecDisplay),
                new Text(row.SourceDisplay), new Text(version));
        }
        return table;
    }

    private static int ComponentOrder(InstallComponent component) => component switch
    {
        InstallComponent.SDK => 0,
        InstallComponent.Runtime => 1,
        InstallComponent.ASPNETCore => 2,
        InstallComponent.WindowsDesktop => 3,
        _ => 4
    };

    private static string ComponentName(InstallComponent component) => component switch
    {
        InstallComponent.SDK => "SDK",
        InstallComponent.Runtime => Strings.ListRuntimeComponent,
        InstallComponent.ASPNETCore => "ASP.NET Core",
        InstallComponent.WindowsDesktop => "Windows Desktop",
        _ => component.GetDisplayName()
    };
}
