// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Performs garbage collection on a dotnet install root by removing installations
/// and subcomponent folders that are no longer referenced by any install spec.
/// </summary>
internal class GarbageCollector
{
    private readonly DotnetupSharedManifest _manifest;

    public GarbageCollector(DotnetupSharedManifest manifest)
    {
        _manifest = manifest;
    }

    /// <summary>
    /// Runs garbage collection for a specific dotnet root.
    /// Returns the list of subcomponent paths that were deleted from disk.
    /// </summary>
    public List<string> Collect(DotnetInstallRoot installRoot)
    {
        return Apply(CreatePlan(installRoot, _manifest.ReadManifest()));
    }

    internal static GarbageCollectionPlan CreatePlan(DotnetInstallRoot installRoot, DotnetupManifestData manifest)
    {
        var root = manifest.DotnetRoots.FirstOrDefault(r =>
            DotnetupUtilities.PathsEqual(Path.GetFullPath(r.Path), Path.GetFullPath(installRoot.Path)) &&
            r.Architecture == installRoot.Architecture);

        if (root is null)
        {
            return new GarbageCollectionPlan(manifest, null, [], [], []);
        }

        // Step 1: Refresh global.json install specs
        RefreshGlobalJsonSpecs(root);

        // Step 2: For each install spec, resolve the latest matching installation and mark it to keep
        var installSpecsByInstallation = new Dictionary<Installation, List<InstallSpec>>();
        foreach (var spec in root.InstallSpecs)
        {
            var matchingInstallation = ResolveLatestMatchingInstallation(spec, root.Installations);
            if (matchingInstallation is not null)
            {
                if (!installSpecsByInstallation.TryGetValue(matchingInstallation, out var specs))
                {
                    specs = [];
                    installSpecsByInstallation.Add(matchingInstallation, specs);
                }

                specs.Add(spec);
            }
        }

        // Step 3: Find unmarked installation records
        var installationsToKeep = installSpecsByInstallation.Keys.Select(i => (i.Component, i.Version)).ToHashSet();
        var installationsToRemove = root.Installations
            .Where(i => !installationsToKeep.Contains((i.Component, i.Version)))
            .ToList();

        // Step 4: Collect all subcomponents still referenced by remaining installations
        var referencedSubcomponents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var installation in root.Installations.Except(installationsToRemove))
        {
            foreach (var sub in installation.Subcomponents)
            {
                referencedSubcomponents.Add(sub);
            }
        }

        return new GarbageCollectionPlan(manifest, root, installSpecsByInstallation, installationsToRemove,
            FindOrphanedSubcomponents(installRoot.Path, referencedSubcomponents));
    }

    internal List<string> Apply(GarbageCollectionPlan plan)
    {
        if (plan.Root is null)
        {
            return [];
        }

        foreach (var installation in plan.InstallationsToRemove)
        {
            plan.Root.Installations.Remove(installation);
        }

        // Persist before deletion so a crash leaves orphaned directories for the next GC.
        _manifest.WriteManifest(plan.Manifest);
        return DeleteOrphanedSubcomponents(plan.Root.Path, plan.PathsToDelete);
    }

    /// <summary>
    /// Refreshes global.json install specs. Removes specs whose global.json file no longer
    /// exists or no longer specifies a version. Updates the channel if the version changed.
    /// </summary>
    private static void RefreshGlobalJsonSpecs(DotnetRootEntry root)
    {
        var globalJsonSpecs = root.InstallSpecs
            .Where(s => s.InstallSource == InstallSource.GlobalJson)
            .ToList();

        foreach (var spec in globalJsonSpecs)
        {
            if (string.IsNullOrEmpty(spec.GlobalJsonPath) || !File.Exists(spec.GlobalJsonPath))
            {
                root.InstallSpecs.Remove(spec);
                continue;
            }

            var resolvedChannel = GlobalJsonChannelResolver.ResolveChannel(spec.GlobalJsonPath);
            if (resolvedChannel is null)
            {
                // global.json no longer specifies an SDK version
                root.InstallSpecs.Remove(spec);
                continue;
            }

            // Update the channel if it changed
            if (!string.Equals(spec.VersionOrChannel, resolvedChannel, StringComparison.OrdinalIgnoreCase))
            {
                spec.VersionOrChannel = resolvedChannel;
            }
        }
    }

    /// <summary>
    /// Finds the latest installation record that matches an install spec.
    /// </summary>
    private static Installation? ResolveLatestMatchingInstallation(InstallSpec spec, List<Installation> installations)
    {
        var matchingInstallations = installations
            .Where(i => i.Component == spec.Component && ReleaseVersion.TryParse(i.Version, out var v) && new UpdateChannel(spec.VersionOrChannel).Matches(v))
            .ToList();

        if (matchingInstallations.Count == 0)
        {
            return null;
        }

        // Return the one with the highest version
        return matchingInstallations
            .Select(i => (Installation: i, Version: ReleaseVersion.TryParse(i.Version, out var v) ? v : null))
            .Where(x => x.Version is not null)
            .OrderByDescending(x => x.Version)
            .Select(x => x.Installation)
            .FirstOrDefault();
    }

    /// <summary>
    /// Walks the dotnet root and deletes subcomponent folders not in the referenced set.
    /// </summary>
    private static List<string> FindOrphanedSubcomponents(string dotnetRootPath, HashSet<string> referencedSubcomponents)
    {
        var paths = new List<string>();

        if (!Directory.Exists(dotnetRootPath))
        {
            return paths;
        }

        foreach (var topLevelDir in Directory.GetDirectories(dotnetRootPath))
        {
            var topLevelName = Path.GetFileName(topLevelDir);

            // Get the subcomponent depth for this folder
            if (!SubcomponentResolver.TryGetDepth(topLevelName, out int depth))
            {
                if (!SubcomponentResolver.IsIgnoredFolder(topLevelName))
                {
                    Console.Error.WriteLine($"Note: Unknown folder '{topLevelName}' found in dotnet root, skipping.");
                }
                continue;
            }

            // Enumerate subcomponent-level directories
            var subcomponentDirs = GetDirectoriesAtDepth(topLevelDir, depth - 1); // depth-1 because we're already inside the top-level
            foreach (var subDir in subcomponentDirs)
            {
                var relativePath = Path.GetRelativePath(dotnetRootPath, subDir).Replace('\\', '/');
                if (!referencedSubcomponents.Contains(relativePath))
                {
                    paths.Add(relativePath);
                }
            }
        }

        return paths;
    }

    private static List<string> DeleteOrphanedSubcomponents(string dotnetRootPath, IReadOnlyList<string> paths)
    {
        using var op = Metrics.Track("gc/delete-orphaned-subcomponents", activityName: "gc.delete-orphaned-subcomponents");
        var deleted = new List<string>();
        var failedCount = 0;
        string? lastFailedPath = null;

        foreach (var relativePath in paths)
        {
            try
            {
                Directory.Delete(Path.Combine(dotnetRootPath, relativePath), recursive: true);
                deleted.Add(relativePath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Could not delete '{relativePath}': {ex.Message}");
                ++failedCount;
                lastFailedPath = relativePath;
                DotnetupTelemetry.Instance.RecordException(op, ex, errorCode: "gc.delete_failed");
            }
        }

        op.Tag("gc.deleted_count", deleted.Count);
        op.Tag("gc.failed_count", failedCount);
        op.Tag("gc.failed_path", lastFailedPath);

        return deleted;
    }

    /// <summary>
    /// Recursively enumerates directories at a specific depth below a starting directory.
    /// A remainingDepth of 1 returns direct subdirectories of startDir.
    /// </summary>
    private static IEnumerable<string> GetDirectoriesAtDepth(string startDir, int remainingDepth)
    {
        if (!Directory.Exists(startDir))
        {
            return [];
        }

        if (remainingDepth <= 1)
        {
            return Directory.GetDirectories(startDir);
        }

        return Directory.GetDirectories(startDir)
            .SelectMany(d => GetDirectoriesAtDepth(d, remainingDepth - 1));
    }
}
