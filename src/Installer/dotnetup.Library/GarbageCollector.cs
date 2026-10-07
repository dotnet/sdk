// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        var deletedPaths = new List<string>();
        var manifest = _manifest.ReadManifest();

        var root = manifest.DotnetRoots.FirstOrDefault(r =>
            DotnetupUtilities.PathsEqual(Path.GetFullPath(r.Path), Path.GetFullPath(installRoot.Path)) &&
            r.Architecture == installRoot.Architecture);

        if (root is null)
        {
            return deletedPaths;
        }

        var installationsToKeep = ResolveInstallationsToKeep(root);

        // Remove unmarked installation records from the manifest.
        var installationsToRemove = root.Installations
            .Where(i => !installationsToKeep.Contains((i.Component, i.Version)))
            .ToList();

        foreach (var installation in installationsToRemove)
        {
            root.Installations.Remove(installation);
        }

        // Collect all subcomponents still referenced by remaining installations.
        var referencedSubcomponents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var installation in root.Installations)
        {
            foreach (var sub in installation.Subcomponents)
            {
                referencedSubcomponents.Add(sub);
            }
        }

        // Write the updated manifest before deleting files, so that a crash
        // during deletion leaves the manifest consistent (orphaned dirs are cleaned next GC).
        _manifest.WriteManifest(manifest);

        // Walk the dotnet root on disk and delete orphaned subcomponent folders.
        deletedPaths = DeleteOrphanedSubcomponents(installRoot.Path, referencedSubcomponents);

        return deletedPaths;
    }

    private static HashSet<(InstallComponent Component, string Version)> ResolveInstallationsToKeep(DotnetRootEntry root)
    {
        var installationsToKeep = new HashSet<(InstallComponent Component, string Version)>();
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
            }
            if (spec.InstallSource == InstallSource.GlobalJson && resolution.Spec is { } requirement)
            {
                // Preserve the legacy manifest shape; the file remains authoritative.
                spec.VersionOrChannel = requirement.Name;
            }
            if (resolution.Installation is { } matchingInstallation)
            {
                installationsToKeep.Add((matchingInstallation.Component, matchingInstallation.Version));
            }
        }
        return installationsToKeep;
    }

    /// <summary>
    /// Walks the dotnet root and deletes subcomponent folders not in the referenced set.
    /// </summary>
    private static List<string> DeleteOrphanedSubcomponents(string dotnetRootPath, HashSet<string> referencedSubcomponents)
    {
        using var op = Metrics.Track("gc/delete-orphaned-subcomponents", activityName: "gc.delete-orphaned-subcomponents");
        var deleted = new List<string>();
        var failedCount = 0;
        string? lastFailedPath = null;

        if (!Directory.Exists(dotnetRootPath))
        {
            return deleted;
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
                    try
                    {
                        Directory.Delete(subDir, recursive: true);
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
