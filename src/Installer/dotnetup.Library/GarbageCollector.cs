// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
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

    /// <summary>
    /// Plans installation-record and directory removals, updating only the in-memory manifest.
    /// </summary>
    internal static GarbageCollectionPlan CreatePlan(DotnetInstallRoot installRoot, DotnetupManifestData manifest)
    {
        var root = manifest.DotnetRoots.FirstOrDefault(r =>
            DotnetupUtilities.PathsEqual(Path.GetFullPath(r.Path), Path.GetFullPath(installRoot.Path)) &&
            r.Architecture == installRoot.Architecture);

        if (root is null)
        {
            return new GarbageCollectionPlan(manifest, null, [], [], []);
        }

        var installSpecsByInstallation = ResolveInstallSpecsByInstallation(root, out bool skipCollection);
        if (skipCollection)
        {
            Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture,
                Strings.GarbageCollectionSkippedUnreadableRequirement, root.Path));
            return new GarbageCollectionPlan(manifest, root, installSpecsByInstallation, [], []);
        }

        var installationsToKeep = installSpecsByInstallation.Keys.Select(i => (i.Component, i.Version)).ToHashSet();
        var installationsToRemove = root.Installations
            .Where(i => !installationsToKeep.Contains((i.Component, i.Version)))
            .ToList();

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

    /// <summary>
    /// Removes planned installation records, persists the manifest, and deletes planned directories.
    /// Does not resolve install specs or rescan the disk.
    /// </summary>
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

    private static Dictionary<Installation, List<InstallSpec>> ResolveInstallSpecsByInstallation(DotnetRootEntry root, out bool skipCollection)
    {
        skipCollection = false;
        var installSpecsByInstallation = new Dictionary<Installation, List<InstallSpec>>();
        foreach (var spec in root.InstallSpecs.ToList())
        {
            var resolution = InstallSpecResolver.Resolve(spec, root.Installations);
            if (!resolution.IsActive)
            {
                root.InstallSpecs.Remove(spec);
                continue;
            }
            if (resolution.Error is not null)
            {
                Console.Error.WriteLine(resolution.Error);
                skipCollection |= resolution.Spec is null;
                if (resolution.UsesFallbackRequirement)
                {
                    Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        Strings.RepositoryUsingLastKnownRequirement, spec.GlobalJsonPath));
                }
            }
            if (spec.InstallSource == InstallSource.GlobalJson && resolution.Spec is { } requirement)
            {
                // Preserve the legacy manifest shape; the file remains authoritative.
                spec.VersionOrChannel = requirement.Name;
                if (!resolution.UsesFallbackRequirement)
                {
                    spec.MinimumVersion = requirement.MinimumVersion?.ToString();
                    spec.AllowPrerelease = requirement.AllowPrerelease;
                }
            }
            if (resolution.Installation is { } matchingInstallation)
            {
                if (!installSpecsByInstallation.TryGetValue(matchingInstallation, out var specs))
                {
                    specs = [];
                    installSpecsByInstallation.Add(matchingInstallation, specs);
                }
                specs.Add(spec);
            }
        }
        return installSpecsByInstallation;
    }

    /// <summary>
    /// Walks the dotnet root and lists subcomponent folders not in the referenced set, without deleting them.
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
