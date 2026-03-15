// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
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
        var deletedPaths = new List<string>();
        var manifest = _manifest.ReadManifest();

        var root = manifest.DotnetRoots.FirstOrDefault(r =>
            DotnetupUtilities.PathsEqual(Path.GetFullPath(r.Path), Path.GetFullPath(installRoot.Path)) &&
            r.Architecture == installRoot.Architecture);

        if (root is null)
        {
            return deletedPaths;
        }

        // Step 1: Refresh global.json install specs
        RefreshGlobalJsonSpecs(root);

        // Step 2: For each install spec, resolve the latest matching installation and mark it to keep
        var installationsToKeep = new HashSet<(InstallComponent Component, string Version)>();
        foreach (var spec in root.InstallSpecs)
        {
            var matchingInstallation = ResolveLatestMatchingInstallation(spec, root.Installations);
            if (matchingInstallation is not null)
            {
                installationsToKeep.Add((matchingInstallation.Component, matchingInstallation.Version));
            }
        }

        // Step 3: Remove unmarked installation records from the manifest
        var installationsToRemove = root.Installations
            .Where(i => !installationsToKeep.Contains((i.Component, i.Version)))
            .ToList();

        foreach (var installation in installationsToRemove)
        {
            root.Installations.Remove(installation);
        }

        // Step 4: Collect all subcomponents still referenced by remaining installations
        var referencedSubcomponents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var installation in root.Installations)
        {
            foreach (var sub in installation.Subcomponents)
            {
                referencedSubcomponents.Add(sub);
            }
        }

        // Step 5: Write the updated manifest before deleting files, so that a crash
        // during deletion leaves the manifest consistent (orphaned dirs are cleaned next GC).
        _manifest.WriteManifest(manifest);

        // Step 6: Walk the dotnet root on disk and delete orphaned subcomponent folders
        deletedPaths = DeleteOrphanedSubcomponents(installRoot.Path, referencedSubcomponents);

        return deletedPaths;
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
            .First();
    }

    /// <summary>
    /// Walks the dotnet root and deletes subcomponent folders not in the referenced set.
    /// </summary>
    private static List<string> DeleteOrphanedSubcomponents(string dotnetRootPath, HashSet<string> referencedSubcomponents)
    {
        using var activity = InstallationActivitySource.ActivitySource.StartActivity("gc.delete-orphaned-subcomponents");
        var deleted = new List<string>();
        var failedCount = 0;

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
                        var lockInfo = FileLockDetector.GetLockingProcessDescription(subDir);
                        var lockMessage = lockInfo is not null
                            ? $" File is in use by: {lockInfo}"
                            : "";
                        Console.Error.WriteLine($"Warning: Could not delete '{relativePath}': {ex.Message}{lockMessage}");
                        ++failedCount;
                        DotnetupTelemetry.Instance.RecordException(activity, ex, errorCode: "gc.delete_failed");
                        activity?.SetTag("gc.failed_path", relativePath);
                    }
                }
            }
        }

        activity?.SetTag("gc.deleted_count", deleted.Count);
        activity?.SetTag("gc.failed_count", failedCount);
        if (failedCount > 0)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Some subcomponents could not be deleted");
        }

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
