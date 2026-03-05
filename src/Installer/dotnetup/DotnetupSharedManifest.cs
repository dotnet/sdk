// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal class DotnetupSharedManifest : IDotnetupManifest
{
    private string ManifestPath => GetManifestPath();

    public DotnetupSharedManifest(string? manifestPath = null)
    {
        _customManifestPath = manifestPath;
        EnsureManifestExists();
    }

    private void EnsureManifestExists()
    {
        if (!File.Exists(ManifestPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath)!);
            var emptyManifest = new DotnetupManifestData();
            File.WriteAllText(ManifestPath, JsonSerializer.Serialize(emptyManifest, DotnetupManifestJsonContext.Default.DotnetupManifestData));
        }
    }

    private readonly string? _customManifestPath;

    private string GetManifestPath()
    {
        // Use explicitly provided path first (constructor argument)
        if (!string.IsNullOrEmpty(_customManifestPath))
        {
            return _customManifestPath;
        }

        // Use centralized path logic (includes env var override)
        return DotnetupPaths.ManifestPath
            ?? throw new InvalidOperationException("Could not determine dotnetup data directory.");
    }

    private static void AssertHasFinalizationMutex()
    {
        // Instead of attempting to reacquire the named mutex (which can create race conditions
        // or accidentally succeed when we *don't* hold it), rely on the thread-local tracking
        // implemented in ScopedMutex. This ensures we only assert based on a lock we actually obtained.
        if (!ScopedMutex.CurrentThreadHoldsMutex)
        {
            throw new InvalidOperationException("The dotnetup manifest was accessed without holding the installation state mutex.");
        }
    }

    internal DotnetupManifestData ReadManifest()
    {
        AssertHasFinalizationMutex();
        EnsureManifestExists();

        string json;
        try
        {
            json = File.ReadAllText(ManifestPath);
        }
        catch (FileNotFoundException)
        {
            // Manifest doesn't exist yet - return empty list
            return new();
        }
        catch (IOException ex)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.LocalManifestError,
                $"Failed to read dotnetup manifest at {ManifestPath}: {ex.Message}",
                ex);
        }

        try
        {
            // Try new format first
            var manifest = JsonSerializer.Deserialize(json, DotnetupManifestJsonContext.Default.DotnetupManifestData);
            if (manifest is not null)
            {
                return manifest;
            }
        }
        catch (JsonException)
        {
            // Check if it's a legacy format (JSON array of DotnetInstall objects)
            if (json.TrimStart().StartsWith('['))
            {
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.LocalManifestCorrupted,
                    $"The dotnetup manifest at {ManifestPath} uses a legacy format that is no longer supported. " +
                    "Delete the manifest file and reinstall any SDKs/runtimes that were in this dotnet root.");
            }

            throw new DotnetInstallException(
                DotnetInstallErrorCode.LocalManifestCorrupted,
                $"The dotnetup manifest at {ManifestPath} is corrupt. Consider deleting it and re-running the install.");
        }

        // Deserialization returned null — treat as empty manifest
        return new();
    }

    internal void WriteManifest(DotnetupManifestData manifest)
    {
        AssertHasFinalizationMutex();
        Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath)!);
        var json = JsonSerializer.Serialize(manifest, DotnetupManifestJsonContext.Default.DotnetupManifestData);
        File.WriteAllText(ManifestPath, json);
    }

    private static DotnetRootEntry GetOrAddDotnetRoot(DotnetupManifestData manifest, string path, InstallArchitecture architecture)
    {
        var existing = manifest.DotnetRoots.FirstOrDefault(r =>
            DotnetupUtilities.PathsEqual(r.Path, path) && r.Architecture == architecture);

        if (existing is not null)
        {
            return existing;
        }

        // Check for conflict: same path but different architecture
        var conflicting = manifest.DotnetRoots.FirstOrDefault(r =>
            DotnetupUtilities.PathsEqual(r.Path, path) && r.Architecture != architecture);
        if (conflicting is not null)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.LocalManifestError,
                $"The dotnet root at '{path}' is already tracked with architecture '{conflicting.Architecture}'. " +
                $"Cannot add it again with architecture '{architecture}'.");
        }

        var newRoot = new DotnetRootEntry
        {
            Path = path,
            Architecture = architecture
        };
        manifest.DotnetRoots.Add(newRoot);
        return newRoot;
    }

    // --- Install Spec operations ---

    public IEnumerable<InstallSpec> GetInstallSpecs(DotnetInstallRoot installRoot)
    {
        var manifest = ReadManifest();
        var root = manifest.DotnetRoots.FirstOrDefault(r =>
            DotnetupUtilities.PathsEqual(Path.GetFullPath(r.Path), Path.GetFullPath(installRoot.Path)) && r.Architecture == installRoot.Architecture);
        return root?.InstallSpecs ?? [];
    }

    public void AddInstallSpec(DotnetInstallRoot installRoot, InstallSpec spec)
    {
        if (spec.InstallSource == InstallSource.All)
        {
            throw new ArgumentException("InstallSource.All cannot be used in manifest data. It is only valid as a filter.", nameof(spec));
        }

        var manifest = ReadManifest();
        var root = GetOrAddDotnetRoot(manifest, installRoot.Path, installRoot.Architecture);

        // Don't add duplicate install specs
        if (!root.InstallSpecs.Any(s =>
            s.Component == spec.Component &&
            s.VersionOrChannel == spec.VersionOrChannel &&
            s.InstallSource == spec.InstallSource &&
            s.GlobalJsonPath == spec.GlobalJsonPath))
        {
            root.InstallSpecs.Add(spec);
        }

        WriteManifest(manifest);
    }

    public void RemoveInstallSpec(DotnetInstallRoot installRoot, InstallSpec spec)
    {
        var manifest = ReadManifest();
        var root = manifest.DotnetRoots.FirstOrDefault(r =>
            DotnetupUtilities.PathsEqual(Path.GetFullPath(r.Path), Path.GetFullPath(installRoot.Path)) && r.Architecture == installRoot.Architecture);

        if (root is null)
        {
            return;
        }

        root.InstallSpecs.RemoveAll(s =>
            s.Component == spec.Component &&
            s.VersionOrChannel == spec.VersionOrChannel &&
            s.InstallSource == spec.InstallSource &&
            s.GlobalJsonPath == spec.GlobalJsonPath);

        WriteManifest(manifest);
    }

    // --- Installation operations ---

    public IEnumerable<Installation> GetInstallations(DotnetInstallRoot installRoot)
    {
        var manifest = ReadManifest();
        var root = manifest.DotnetRoots.FirstOrDefault(r =>
            DotnetupUtilities.PathsEqual(Path.GetFullPath(r.Path), Path.GetFullPath(installRoot.Path)) && r.Architecture == installRoot.Architecture);
        return root?.Installations ?? [];
    }

    public void AddInstallation(DotnetInstallRoot installRoot, Installation installation)
    {
        var manifest = ReadManifest();
        var root = GetOrAddDotnetRoot(manifest, installRoot.Path, installRoot.Architecture);

        // Don't add duplicate installations
        if (!root.Installations.Any(i => i.Component == installation.Component && i.Version == installation.Version))
        {
            root.Installations.Add(installation);
        }

        WriteManifest(manifest);
    }

    public void RemoveInstallation(DotnetInstallRoot installRoot, Installation installation)
    {
        var manifest = ReadManifest();
        var root = manifest.DotnetRoots.FirstOrDefault(r =>
            DotnetupUtilities.PathsEqual(Path.GetFullPath(r.Path), Path.GetFullPath(installRoot.Path)) && r.Architecture == installRoot.Architecture);

        if (root is null)
        {
            return;
        }

        root.Installations.RemoveAll(i => i.Component == installation.Component && i.Version == installation.Version);

        WriteManifest(manifest);
    }

}
