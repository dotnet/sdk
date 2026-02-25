// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Performs garbage collection on a dotnet install root by removing installations
/// and subcomponent folders that are no longer referenced by any install spec.
/// </summary>
internal class GarbageCollector
{
    private readonly DotnetupSharedManifest _manifest;
    private readonly ChannelVersionResolver _channelVersionResolver;

    public GarbageCollector(DotnetupSharedManifest manifest, ChannelVersionResolver? channelVersionResolver = null)
    {
        _manifest = manifest;
        _channelVersionResolver = channelVersionResolver ?? new ChannelVersionResolver();
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
        var installationsToKeep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var spec in root.InstallSpecs)
        {
            var matchingInstallation = ResolveLatestMatchingInstallation(spec, root.Installations);
            if (matchingInstallation is not null)
            {
                installationsToKeep.Add(InstallationKey(matchingInstallation));
            }
        }

        // Step 3: Remove unmarked installation records from the manifest
        var installationsToRemove = root.Installations
            .Where(i => !installationsToKeep.Contains(InstallationKey(i)))
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

        // Step 5: Walk the dotnet root on disk and delete orphaned subcomponent folders
        deletedPaths = DeleteOrphanedSubcomponents(installRoot.Path, referencedSubcomponents);

        // Step 6: Write the updated manifest
        _manifest.WriteManifestData(manifest);

        return deletedPaths;
    }

    /// <summary>
    /// Refreshes global.json install specs. Removes specs whose global.json file no longer
    /// exists or no longer specifies a version.
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

            // TODO: Read the global.json file and update the spec's version if it has changed.
            // For now, we keep the spec as-is if the file still exists.
        }
    }

    /// <summary>
    /// Finds the latest installation record that matches an install spec.
    /// </summary>
    private Installation? ResolveLatestMatchingInstallation(InstallSpec spec, List<Installation> installations)
    {
        var matchingInstallations = installations
            .Where(i => i.Component == spec.Component && VersionMatchesSpec(i.Version, spec.VersionOrChannel))
            .ToList();

        if (matchingInstallations.Count == 0)
        {
            return null;
        }

        // Return the one with the highest version
        return matchingInstallations
            .OrderByDescending(i => ReleaseVersion.TryParse(i.Version, out var v) ? v : null)
            .First();
    }

    /// <summary>
    /// Checks if an installed version matches an install spec's channel/version pattern.
    /// </summary>
    private static bool VersionMatchesSpec(string installedVersion, string versionOrChannel)
    {
        if (string.IsNullOrEmpty(versionOrChannel))
        {
            return false;
        }

        // Exact version match
        if (string.Equals(installedVersion, versionOrChannel, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!ReleaseVersion.TryParse(installedVersion, out var installed))
        {
            return false;
        }

        // Named channels (latest, lts, sts, preview) — match any version of the same component
        if (versionOrChannel.Equals("latest", StringComparison.OrdinalIgnoreCase) ||
            versionOrChannel.Equals("lts", StringComparison.OrdinalIgnoreCase) ||
            versionOrChannel.Equals("sts", StringComparison.OrdinalIgnoreCase) ||
            versionOrChannel.Equals("preview", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Major version match (e.g., "10" matches "10.0.103")
        if (int.TryParse(versionOrChannel, out var major))
        {
            return installed.Major == major;
        }

        var parts = versionOrChannel.Split('.');

        // Major.Minor match (e.g., "10.0" matches "10.0.103")
        if (parts.Length == 2 && int.TryParse(parts[0], out var specMajor) && int.TryParse(parts[1], out var specMinor))
        {
            return installed.Major == specMajor && installed.Minor == specMinor;
        }

        // Feature band match (e.g., "10.0.1xx" matches "10.0.103")
        if (parts.Length == 3 && parts[2].EndsWith("xx", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(parts[0], out var fbMajor) && int.TryParse(parts[1], out var fbMinor))
            {
                var bandPrefix = parts[2].Substring(0, parts[2].Length - 2);
                if (int.TryParse(bandPrefix, out var band))
                {
                    return installed.Major == fbMajor && installed.Minor == fbMinor &&
                           installed.Patch / 100 == band;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Walks the dotnet root and deletes subcomponent folders not in the referenced set.
    /// </summary>
    private static List<string> DeleteOrphanedSubcomponents(string dotnetRootPath, HashSet<string> referencedSubcomponents)
    {
        var deleted = new List<string>();

        foreach (var topLevelDir in Directory.GetDirectories(dotnetRootPath))
        {
            var topLevelName = Path.GetFileName(topLevelDir);

            // Get the subcomponent depth for this folder
            if (!SubcomponentResolver.TryGetDepth(topLevelName, out int depth))
            {
                continue; // Unknown folder, skip
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
                    catch (IOException)
                    {
                        // Best effort — files may be locked
                    }
                }
            }
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

    private static string InstallationKey(Installation installation) =>
        $"{installation.Component}|{installation.Version}";
}
