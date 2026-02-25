// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Dotnet.Installation.Internal;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Uninstall;

internal class SdkUninstallCommand(ParseResult result) : CommandBase(result)
{
    private readonly string _versionOrChannel = result.GetValue(SdkUninstallCommandParser.ChannelArgument)!;
    private readonly string? _manifestPath = result.GetValue(SdkUninstallCommandParser.ManifestPathOption);
    private readonly string? _installPath = result.GetValue(SdkUninstallCommandParser.InstallPathOption);

    private readonly IDotnetInstallManager _dotnetInstaller = new DotnetInstallManager();

    public override int Execute()
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

        // Find matching install spec(s)
        var matchingSpecs = root.InstallSpecs
            .Where(s => s.Component == InstallComponent.SDK &&
                        string.Equals(s.VersionOrChannel, _versionOrChannel, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matchingSpecs.Count == 0)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]No install spec found for SDK channel '{_versionOrChannel}' at {resolvedInstallPath}.[/]");
            return 1;
        }

        // Remove the install spec(s)
        foreach (var spec in matchingSpecs)
        {
            manifest.RemoveInstallSpec(installRoot, spec);
            AnsiConsole.MarkupLineInterpolated($"Removed install spec: {spec.Component.GetDisplayName()} [blue]{spec.VersionOrChannel}[/]");
        }

        // Run garbage collection
        AnsiConsole.WriteLine("Running garbage collection...");
        var gc = new GarbageCollector(new DotnetupSharedManifest(_manifestPath));
        var deleted = gc.Collect(installRoot);

        if (deleted.Count > 0)
        {
            foreach (var d in deleted)
            {
                AnsiConsole.MarkupLineInterpolated($"  Removed [dim]{d}[/]");
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
                    AnsiConsole.MarkupLineInterpolated($"  [dim]{spec.Component.GetDisplayName()} {spec.VersionOrChannel} ({spec.InstallSource})[/]");
                }
            }
        }

        AnsiConsole.MarkupLine("[green]Done.[/]");
        return 0;
    }
}
