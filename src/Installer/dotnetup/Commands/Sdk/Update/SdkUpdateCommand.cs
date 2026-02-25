// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Update;

internal class SdkUpdateCommand(ParseResult result) : CommandBase(result)
{
    private readonly bool _updateAll = result.GetValue(SdkUpdateCommandParser.UpdateAllOption);
    private readonly bool _noProgress = result.GetValue(SdkUpdateCommandParser.NoProgressOption);
    private readonly string? _manifestPath = result.GetValue(SdkUpdateCommandParser.ManifestPathOption);
    private readonly string? _installPath = result.GetValue(SdkUpdateCommandParser.InstallPathOption);

    private readonly IDotnetInstallManager _dotnetInstaller = new DotnetInstallManager();
    private readonly ChannelVersionResolver _channelVersionResolver = new();

    public override int Execute()
    {
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);

        var manifest = new DotnetupSharedManifest(_manifestPath);
        var manifestData = manifest.ReadManifest();

        // Determine which dotnet root(s) to update
        var rootsToUpdate = manifestData.DotnetRoots.AsEnumerable();
        if (!string.IsNullOrEmpty(_installPath))
        {
            rootsToUpdate = rootsToUpdate.Where(r =>
                DotnetupUtilities.PathsEqual(Path.GetFullPath(r.Path), Path.GetFullPath(_installPath)));
        }

        var rootsList = rootsToUpdate.ToList();
        if (rootsList.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No managed dotnet installations found to update.[/]");
            return 0;
        }

        bool anyUpdated = false;

        foreach (var root in rootsList)
        {
            var installRoot = new DotnetInstallRoot(root.Path, root.Architecture);

            foreach (var spec in root.InstallSpecs.ToList())
            {
                if (spec.Component != InstallComponent.SDK && !_updateAll)
                {
                    continue;
                }

                var channel = new UpdateChannel(spec.VersionOrChannel);

                // Skip fully-specified versions — they can't be updated
                if (channel.IsFullySpecifiedVersion())
                {
                    continue;
                }

                var latestVersion = _channelVersionResolver.GetLatestVersionForChannel(channel, spec.Component);
                if (latestVersion is null)
                {
                    AnsiConsole.MarkupLineInterpolated($"[yellow]Could not resolve latest version for channel '{spec.VersionOrChannel}'.[/]");
                    continue;
                }

                // Check if this version is already installed
                var alreadyInstalled = root.Installations.Any(i =>
                    i.Component == spec.Component && i.Version == latestVersion.ToString());

                if (alreadyInstalled)
                {
                    AnsiConsole.MarkupLineInterpolated($"{spec.Component.GetDisplayName()} [blue]{latestVersion}[/] is already up to date.");
                    continue;
                }

                AnsiConsole.MarkupLineInterpolated($"Updating {spec.Component.GetDisplayName()} to [blue]{latestVersion}[/]...");

                // Install the new version (this releases and reacquires the mutex internally)
                var installRequest = new DotnetInstallRequest(
                    installRoot,
                    channel,
                    spec.Component,
                    new InstallRequestOptions { ManifestPath = _manifestPath });

                // Release the mutex for the install operation (it acquires its own)
                mutex.Dispose();
                var result = InstallerOrchestratorSingleton.Instance.Install(installRequest, _noProgress);

                // Reacquire for GC
                using var gcMutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);

                if (result is not null)
                {
                    AnsiConsole.MarkupLineInterpolated($"[green]Updated {spec.Component.GetDisplayName()} to {latestVersion}.[/]");
                    anyUpdated = true;
                }
                else
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]Failed to update {spec.Component.GetDisplayName()} to {latestVersion}.[/]");
                }
            }

            // Run garbage collection
            if (anyUpdated)
            {
                AnsiConsole.WriteLine("Cleaning up old installations...");
                var gc = new GarbageCollector(new DotnetupSharedManifest(_manifestPath));
                var deleted = gc.Collect(installRoot);
                if (deleted.Count > 0)
                {
                    foreach (var d in deleted)
                    {
                        AnsiConsole.MarkupLineInterpolated($"  Removed [dim]{d}[/]");
                    }
                }
            }
        }

        if (!anyUpdated)
        {
            AnsiConsole.MarkupLine("Everything is up to date.");
        }

        return 0;
    }
}
