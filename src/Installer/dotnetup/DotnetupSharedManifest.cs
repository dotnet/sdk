// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal class DotnetupSharedManifest : IDotnetupManifest
{
    private string ManifestPath => GetManifestPath();

    public DotnetupSharedManifest(string? manifestPath = null)
    {
        _customManifestPath = manifestPath is not null ? Path.GetFullPath(manifestPath) : null;
        EnsureManifestExists();
    }

    private void EnsureManifestExists()
    {
        if (!File.Exists(ManifestPath))
        {
            var directory = Path.GetDirectoryName(ManifestPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var emptyManifest = new DotnetupManifestData();
            var json = JsonSerializer.Serialize(emptyManifest, DotnetupManifestJsonContext.Default.DotnetupManifestData);
            WriteManifestWithChecksum(json);
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

        return ParseManifestJson(json);
    }

    private DotnetupManifestData ParseManifestJson(string json)
    {
        try
        {
            // Try new format first
            var manifest = JsonSerializer.Deserialize(json, DotnetupManifestJsonContext.Default.DotnetupManifestData);
            if (manifest is not null)
            {
                // Only validate field values if the checksum doesn't match — if we wrote
                // the file ourselves the data is trusted; validation is only needed to
                // catch external / manual edits.
                if (!VerifyChecksumMatches(json))
                {
                    ValidateManifestData(manifest);
                }

                return manifest;
            }
        }
        catch (JsonException ex)
        {
            // Check if it's a legacy format (JSON array of DotnetInstall objects)
            if (json.TrimStart().StartsWith('['))
            {
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.LocalManifestCorrupted,
                    $"The dotnetup manifest at {ManifestPath} uses a legacy format that is no longer supported. " +
                    "Delete the manifest file and reinstall any SDKs/runtimes that were in this dotnet root.");
            }

            // Check whether dotnetup last wrote the file. If the checksum matches,
            // we produced invalid JSON ourselves (product bug). If it doesn't match,
            // an external edit broke the file (user error).
            var errorCode = VerifyChecksumMatches(json)
                ? DotnetInstallErrorCode.LocalManifestCorrupted
                : DotnetInstallErrorCode.LocalManifestUserCorrupted;

            var suffix = errorCode == DotnetInstallErrorCode.LocalManifestUserCorrupted
                ? " The file appears to have been modified outside of dotnetup."
                : string.Empty;

            throw new DotnetInstallException(
                errorCode,
                $"The dotnetup manifest at {ManifestPath} is corrupt. Consider deleting it and re-running the install.{suffix}",
                ex);
        }

        // Deserialization returned null — treat as empty manifest
        return new();
    }

    internal void WriteManifest(DotnetupManifestData manifest)
    {
        AssertHasFinalizationMutex();
        var json = JsonSerializer.Serialize(manifest, DotnetupManifestJsonContext.Default.DotnetupManifestData);
        WriteManifestWithChecksum(json);
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

    /// <summary>
    /// Writes manifest JSON and a companion SHA-256 checksum file.
    /// The checksum lets us distinguish product corruption (our bug) from
    /// user edits on the next read.
    /// </summary>
    /// <remarks>
    /// The manifest and checksum are written as two separate file operations.
    /// A crash between the writes could leave them inconsistent, but this is
    /// self-correcting on the next successful write. All callers are expected
    /// to hold the installation-state mutex.
    /// </remarks>
    private void WriteManifestWithChecksum(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath)!);
        File.WriteAllText(ManifestPath, json);
        File.WriteAllText(ChecksumPath, ComputeHash(json));
    }

    /// <summary>
    /// Returns true if the raw JSON content matches the last checksum dotnetup wrote.
    /// If the checksum file is missing or unreadable, returns false (assume external edit).
    /// </summary>
    private bool VerifyChecksumMatches(string rawJson)
    {
        try
        {
            if (!File.Exists(ChecksumPath))
            {
                return false;
            }

            var storedHash = File.ReadAllText(ChecksumPath).Trim();
            var currentHash = ComputeHash(rawJson);
            return string.Equals(storedHash, currentHash, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // If we can't read the checksum file, assume external modification.
            return false;
        }
    }

    private string ChecksumPath => ManifestPath + ".sha256";

    private static string ComputeHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Validates manifest data after deserialization when the checksum does not match
    /// (i.e., the file was modified outside of dotnetup). Checks that version strings
    /// in installation records are valid <see cref="ReleaseVersion"/> values and
    /// that required fields are non-empty.
    /// </summary>
    private void ValidateManifestData(DotnetupManifestData manifest)
    {
        var errors = new List<string>();

        foreach (var root in manifest.DotnetRoots)
        {
            if (string.IsNullOrWhiteSpace(root.Path))
            {
                errors.Add("A dotnet root entry has an empty path.");
            }

            foreach (var installation in root.Installations)
            {
                if (!Enum.IsDefined(installation.Component))
                {
                    errors.Add($"Unknown component type '{(int)installation.Component}' in an installation record in root '{root.Path}'.");
                }

                if (string.IsNullOrWhiteSpace(installation.Version))
                {
                    errors.Add($"An installation record for {installation.Component} in root '{root.Path}' has an empty version.");
                    continue;
                }

                if (!ReleaseVersion.TryParse(installation.Version, out _))
                {
                    errors.Add($"Invalid version '{installation.Version}' for {installation.Component} in root '{root.Path}'.");
                }
            }

            foreach (var spec in root.InstallSpecs)
            {
                if (!Enum.IsDefined(spec.Component))
                {
                    errors.Add($"Unknown component type '{(int)spec.Component}' in an install spec in root '{root.Path}'.");
                }

                if (string.IsNullOrWhiteSpace(spec.VersionOrChannel))
                {
                    errors.Add($"An install spec for {spec.Component} in root '{root.Path}' has an empty version/channel.");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.LocalManifestUserCorrupted,
                $"The dotnetup manifest at {ManifestPath} contains invalid data: {string.Join("; ", errors)}. " +
                $"The file appears to have been modified outside of dotnetup. Consider deleting it and re-running the install.");
        }
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
