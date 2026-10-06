// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Holds a GC snapshot for preview and application while the installation-state mutex is held.
/// Planning changes only this in-memory manifest; application persists it and deletes the listed paths.
/// </summary>
internal sealed class GarbageCollectionPlan(
    DotnetupManifestData manifest,
    DotnetRootEntry? root,
    Dictionary<Installation, List<InstallSpec>> references,
    List<Installation> installationsToRemove,
    List<string> pathsToDelete)
{
    internal DotnetupManifestData Manifest { get; } = manifest;
    internal DotnetRootEntry? Root { get; } = root;
    internal IReadOnlyDictionary<Installation, List<InstallSpec>> References { get; } = references;
    internal IReadOnlyList<Installation> InstallationsToRemove { get; } = installationsToRemove;
    internal IReadOnlyList<string> PathsToDelete { get; } = pathsToDelete;

    internal List<InstallSpec> GetReferencingSpecs(Installation installation)
    {
        var primaryPath = GetPrimaryPath(installation);
        return References.Where(pair => pair.Key == installation ||
                (primaryPath is not null && pair.Key.Subcomponents.Contains(primaryPath, StringComparer.OrdinalIgnoreCase)))
            .SelectMany(pair => pair.Value)
            .DistinctBy(spec => (spec.Component, spec.VersionOrChannel, spec.InstallSource, spec.GlobalJsonPath))
            .ToList();
    }

    internal string? GetPrimaryPath(Installation installation)
    {
        if (Root is null)
        {
            return null;
        }

        var directory = DotnetupSharedManifest.GetComponentDirectory(Root.Path, installation);
        return directory is null ? null : Path.GetRelativePath(Root.Path, directory).Replace('\\', '/');
    }
}
