// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
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
            var json = JsonSerializer.Serialize((List<DotnetInstall>)[], DotnetupManifestJsonContext.Default.ListDotnetInstall);
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

    public IEnumerable<DotnetInstall> GetInstalledVersions(IInstallationValidator? validator = null)
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
            var installs = JsonSerializer.Deserialize(json, DotnetupManifestJsonContext.Default.ListDotnetInstall);
            var validInstalls = installs ?? [];

            if (validator is not null)
            {
                var invalids = validInstalls.Where(i => !validator.Validate(i)).ToList();
                if (invalids.Count > 0)
                {
                    validInstalls = validInstalls.Except(invalids).ToList();
                    var newJson = JsonSerializer.Serialize(validInstalls, DotnetupManifestJsonContext.Default.ListDotnetInstall);
                    WriteManifestWithChecksum(newJson);
                }
            }
            return validInstalls;
        }
        catch (JsonException ex)
        {
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
    }

    /// <summary>
    /// Gets installed versions filtered by a specific muxer directory.
    /// </summary>
    /// <param name="installRoot">Directory to filter by (must match the InstallRoot property)</param>
    /// <param name="validator">Optional validator to check installation validity</param>
    /// <returns>Installations that match the specified directory</returns>
    public IEnumerable<DotnetInstall> GetInstalledVersions(DotnetInstallRoot installRoot, IInstallationValidator? validator = null)
    {
        // TODO: Manifest read operations should protect against data structure changes and be able to reformat an old manifest version.
        var installedVersions = GetInstalledVersions(validator);
        var expectedInstallRootPath = Path.GetFullPath(installRoot.Path);
        var installedVersionsInRoot = installedVersions
            .Where(install => DotnetupUtilities.PathsEqual(Path.GetFullPath(install.InstallRoot.Path!), expectedInstallRootPath));
        return installedVersionsInRoot;
    }

    public void AddInstalledVersion(DotnetInstall version)
    {
        AssertHasFinalizationMutex();
        EnsureManifestExists();

        var installs = GetInstalledVersions().ToList();
        installs.Add(version);
        var json = JsonSerializer.Serialize(installs, DotnetupManifestJsonContext.Default.ListDotnetInstall);
        WriteManifestWithChecksum(json);
    }

    public void RemoveInstalledVersion(DotnetInstall version)
    {
        AssertHasFinalizationMutex();
        EnsureManifestExists();

        var installs = GetInstalledVersions().ToList();
        installs.RemoveAll(i => DotnetupUtilities.PathsEqual(i.InstallRoot.Path, version.InstallRoot.Path) && i.Version.Equals(version.Version));
        var json = JsonSerializer.Serialize(installs, DotnetupManifestJsonContext.Default.ListDotnetInstall);
        WriteManifestWithChecksum(json);
    }

    /// <summary>
    /// Writes manifest JSON and a companion SHA-256 checksum file atomically.
    /// The checksum lets us distinguish product corruption (our bug) from
    /// user edits on the next read.
    /// </summary>
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
}
