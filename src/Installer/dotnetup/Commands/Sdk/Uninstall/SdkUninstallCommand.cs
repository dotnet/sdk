// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Globalization;
using Microsoft.Dotnet.Installation.Internal;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Uninstall;

internal class SdkUninstallCommand(ParseResult result) : CommandBase(result)
{
    private readonly string _versionOrChannel = result.GetValue(SdkUninstallCommandParser.ChannelArgument)!;
    private readonly string _sourceFilter = result.GetValue(SdkUninstallCommandParser.SourceOption)!;
    private readonly string? _manifestPath = result.GetValue(SdkUninstallCommandParser.ManifestPathOption);
    private readonly string? _installPath = result.GetValue(SdkUninstallCommandParser.InstallPathOption);

    private readonly DotnetInstallManager _dotnetInstaller = new();

    protected override string GetCommandName() => "sdk/uninstall";

    protected override int ExecuteCore()
    {
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);

        var manifest = new DotnetupSharedManifest(_manifestPath);
        var manifestData = manifest.ReadManifest();

        // Resolve install path
        string resolvedInstallPath = _installPath
            ?? _dotnetInstaller.GetConfiguredInstallType()?.Path
            ?? _dotnetInstaller.GetDefaultDotnetInstallPath();

        var root = manifestData.DotnetRoots.FirstOrDefault(r =>
            DotnetupUtilities.PathsEqual(Path.GetFullPath(r.Path), Path.GetFullPath(resolvedInstallPath)));

        if (root is null)
        {
            AnsiConsole.MarkupLine($"[yellow]No managed installations found at {resolvedInstallPath}.[/]");
            return 1;
        }

        var installRoot = new DotnetInstallRoot(root.Path, root.Architecture);

        // Parse the source filter
        var allowedSources = ParseSourceFilter(_sourceFilter);
        if (allowedSources is null)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]Invalid --source value '{_sourceFilter}'. Valid values: explicit, previous, globaljson, all.[/]");
            return 1;
        }

        // Find all specs matching the channel
        var allMatchingSpecs = root.InstallSpecs
            .Where(s => s.Component == InstallComponent.SDK &&
                        string.Equals(s.VersionOrChannel, _versionOrChannel, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Filter by source
        var matchingSpecs = allMatchingSpecs
            .Where(s => allowedSources.Contains(s.InstallSource))
            .ToList();

        if (matchingSpecs.Count == 0)
        {
            // Check if there are matches with other sources
            var otherSourceSpecs = allMatchingSpecs.Except(matchingSpecs).ToList();
            if (otherSourceSpecs.Count > 0)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]No [bold]explicit[/] install spec found for SDK channel '{_versionOrChannel}', but matching specs exist with other sources:[/]");
                foreach (var spec in otherSourceSpecs)
                {
                    AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [dim]{spec.Component.GetDisplayName()} {spec.VersionOrChannel} (source: {spec.InstallSource})[/]");
                }
                AnsiConsole.MarkupLine("[dim]Use --source previous, --source globaljson, or --source all to target these specs.[/]");
            }
            else
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]No install spec found for '{_versionOrChannel}' at {resolvedInstallPath}.[/]");
            }
            return 1;
        }

        // Remove the install spec(s)
        foreach (var spec in matchingSpecs)
        {
            manifest.RemoveInstallSpec(installRoot, spec);
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"Removed install spec: {spec.Component.GetDisplayName()} [blue]{spec.VersionOrChannel}[/] (source: {spec.InstallSource})");
        }

        // Run garbage collection
        AnsiConsole.WriteLine("Running garbage collection...");
        var gc = new GarbageCollector(new DotnetupSharedManifest(_manifestPath));
        var deleted = gc.Collect(installRoot);

        if (deleted.Count > 0)
        {
            foreach (var d in deleted)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  Removed [dim]{d}[/]");
            }
        }
        else
        {
            // Check if the installation is still present (referenced by another spec)
            var updatedManifest = new DotnetupSharedManifest(_manifestPath);
            var remainingInstallations = updatedManifest.GetInstallations(installRoot)
                .Where(i => i.Component == InstallComponent.SDK)
                .ToList();

            var remainingSpecs = updatedManifest.GetInstallSpecs(installRoot)
                .Where(s => s.Component == InstallComponent.SDK)
                .ToList();

            if (remainingInstallations.Count > 0 && remainingSpecs.Count > 0)
            {
                AnsiConsole.MarkupLine("[dim]The installation was not removed because it is still referenced by other install specs:[/]");
                foreach (var spec in remainingSpecs)
                {
                    AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [dim]{spec.Component.GetDisplayName()} {spec.VersionOrChannel} ({spec.InstallSource})[/]");
                }
            }
        }

        AnsiConsole.MarkupLine("[green]Done.[/]");
        return 0;
    }

    internal static HashSet<InstallSource>? ParseSourceFilter(string source)
    {
        return source.ToLowerInvariant() switch
        {
            "explicit" => [InstallSource.Explicit],
            "previous" => [InstallSource.Previous],
            "globaljson" => [InstallSource.GlobalJson],
            "all" => [InstallSource.Explicit, InstallSource.Previous, InstallSource.GlobalJson],
            _ => null
        };
    }
}
