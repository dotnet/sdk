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
            return [];
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
            // May be old format — try migration
        }

        try
        {
            var legacyInstalls = JsonSerializer.Deserialize(json, DotnetupManifestJsonContext.Default.ListDotnetInstall);
            var migrated = MigrateFromLegacy(legacyInstalls ?? []);
            WriteManifest(migrated);
            return migrated;
        }
        catch (JsonException ex)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.LocalManifestCorrupted,
                $"The dotnetup manifest at {ManifestPath} is corrupt. Consider deleting it and re-running the install.",
                ex);
        }
    }

    private void WriteManifest(DotnetupManifestData manifest)
    {
        AssertHasFinalizationMutex();
        Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath)!);
        var json = JsonSerializer.Serialize(manifest, DotnetupManifestJsonContext.Default.DotnetupManifestData);
        File.WriteAllText(ManifestPath, json);
    }

    /// <summary>
    /// Migrates a legacy flat list of DotnetInstall records to the new manifest format.
    /// Each legacy install becomes both an install spec (pinned to exact version) and an installation record.
    /// </summary>
    private static DotnetupManifestData MigrateFromLegacy(List<DotnetInstall> legacyInstalls)
    {
        var manifest = new DotnetupManifestData();

        foreach (var install in legacyInstalls)
        {
            var root = GetOrAddDotnetRoot(manifest, install.InstallRoot.Path, install.InstallRoot.Architecture);

            root.InstallSpecs.Add(new InstallSpec
            {
                Component = install.Component,
                VersionOrChannel = install.Version.ToString(),
                InstallSource = InstallSource.Previous
            });

            if (!root.Installations.Any(i => i.Component == install.Component && i.Version == install.Version.ToString()))
            {
                root.Installations.Add(new Installation
                {
                    Component = install.Component,
                    Version = install.Version.ToString()
                });
            }
        }

        return manifest;
    }

    private static DotnetRootEntry GetOrAddDotnetRoot(DotnetupManifestData manifest, string path, InstallArchitecture architecture)
    {
        var existing = manifest.DotnetRoots.FirstOrDefault(r =>
            DotnetupUtilities.PathsEqual(r.Path, path) && r.Architecture == architecture);

        if (existing is not null)
        {
            return existing;
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

    // --- Backward-compatible DotnetInstall operations ---

    public IEnumerable<DotnetInstall> GetInstalledVersions(IInstallationValidator? validator = null)
    {
        var manifest = ReadManifest();
        var installs = ManifestToLegacyInstalls(manifest);

        if (validator is not null)
        {
            var validInstalls = installs.Where(i => validator.Validate(i)).ToList();
            if (validInstalls.Count != installs.Count)
            {
                // Remove invalid installations from the manifest
                foreach (var invalid in installs.Except(validInstalls))
                {
                    var root = manifest.DotnetRoots.FirstOrDefault(r =>
                        DotnetupUtilities.PathsEqual(r.Path, invalid.InstallRoot.Path) && r.Architecture == invalid.InstallRoot.Architecture);
                    root?.Installations.RemoveAll(i => i.Component == invalid.Component && i.Version == invalid.Version.ToString());
                }
                WriteManifest(manifest);
                return validInstalls;
            }
        }

        return installs;
    }

    public IEnumerable<DotnetInstall> GetInstalledVersions(DotnetInstallRoot installRoot, IInstallationValidator? validator = null)
    {
        var installedVersions = GetInstalledVersions(validator);
        var expectedInstallRootPath = Path.GetFullPath(installRoot.Path);
        return installedVersions
            .Where(install => DotnetupUtilities.PathsEqual(Path.GetFullPath(install.InstallRoot.Path!), expectedInstallRootPath));
    }

    public void AddInstalledVersion(DotnetInstall version)
    {
        var manifest = ReadManifest();
        var root = GetOrAddDotnetRoot(manifest, version.InstallRoot.Path, version.InstallRoot.Architecture);

        if (!root.Installations.Any(i => i.Component == version.Component && i.Version == version.Version.ToString()))
        {
            root.Installations.Add(new Installation
            {
                Component = version.Component,
                Version = version.Version.ToString()
            });
        }

        WriteManifest(manifest);
    }

    public void RemoveInstalledVersion(DotnetInstall version)
    {
        var manifest = ReadManifest();
        var root = manifest.DotnetRoots.FirstOrDefault(r =>
            DotnetupUtilities.PathsEqual(r.Path, version.InstallRoot.Path) && r.Architecture == version.InstallRoot.Architecture);

        if (root is null)
        {
            return;
        }

        root.Installations.RemoveAll(i => i.Component == version.Component && i.Version == version.Version.ToString());

        WriteManifest(manifest);
    }

    /// <summary>
    /// Converts the new manifest model to legacy DotnetInstall records for backward compatibility.
    /// </summary>
    private static List<DotnetInstall> ManifestToLegacyInstalls(DotnetupManifestData manifest)
    {
        var installs = new List<DotnetInstall>();
        foreach (var root in manifest.DotnetRoots)
        {
            var installRoot = new DotnetInstallRoot(root.Path, root.Architecture);
            foreach (var installation in root.Installations)
            {
                installs.Add(new DotnetInstall(
                    installRoot,
                    new Microsoft.Deployment.DotNet.Releases.ReleaseVersion(installation.Version),
                    installation.Component));
            }
        }
        return installs;
    }
}
