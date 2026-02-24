// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Dotnet.Installation;
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
            File.WriteAllText(ManifestPath, JsonSerializer.Serialize(new List<DotnetInstall>(), DotnetupManifestJsonContext.Default.ListDotnetInstall));
        }
    }

    private string? _customManifestPath;

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

    private void AssertHasFinalizationMutex()
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
                    File.WriteAllText(ManifestPath, newJson);
                }
            }
            return validInstalls;
        }
        catch (JsonException ex)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.LocalManifestCorrupted,
                $"The dotnetup manifest at {ManifestPath} is corrupt. Consider deleting it and re-running the install.",
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
        Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath)!);
        File.WriteAllText(ManifestPath, json);
    }

    public void RemoveInstalledVersion(DotnetInstall version)
    {
        AssertHasFinalizationMutex();
        EnsureManifestExists();

        var installs = GetInstalledVersions().ToList();
        installs.RemoveAll(i => DotnetupUtilities.PathsEqual(i.InstallRoot.Path, version.InstallRoot.Path) && i.Version.Equals(version.Version));
        var json = JsonSerializer.Serialize(installs, DotnetupManifestJsonContext.Default.ListDotnetInstall);
        File.WriteAllText(ManifestPath, json);
    }
}
