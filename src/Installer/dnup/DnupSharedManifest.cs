// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal class DnupSharedManifest : IDnupManifest
{
    private static string ManifestPath => GetManifestPath();

    public DnupSharedManifest()
    {
        EnsureManifestExists();
    }

    private void EnsureManifestExists()
    {
        if (!File.Exists(ManifestPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath)!);
            File.WriteAllText(ManifestPath, JsonSerializer.Serialize(new List<DotnetInstall>(), DnupManifestJsonContext.Default.ListDotnetInstall));
        }
    }

    private static string GetManifestPath()
    {
        var overridePath = Environment.GetEnvironmentVariable("DOTNET_TESTHOOK_MANIFEST_PATH");
        if (!string.IsNullOrEmpty(overridePath))
        {
            return overridePath;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "dnup",
            "dnup_manifest.json");
    }

    private void AssertHasFinalizationMutex()
    {
        var mutex = Mutex.OpenExisting(Constants.MutexNames.ModifyInstallationStates);
        if (!mutex.WaitOne(0))
        {
            throw new InvalidOperationException("The dnup manifest was accessed while not holding the mutex.");
        }
        mutex.ReleaseMutex();
        mutex.Dispose();
    }

    public IEnumerable<DotnetInstall> GetInstalledVersions(IInstallationValidator? validator = null)
    {
        AssertHasFinalizationMutex();
        EnsureManifestExists();

        var json = File.ReadAllText(ManifestPath);
        try
        {
            var installs = JsonSerializer.Deserialize(json, DnupManifestJsonContext.Default.ListDotnetInstall);
            var validInstalls = installs ?? new List<DotnetInstall>();

            if (validator != null)
            {
                var invalids = validInstalls.Where(i => !validator.Validate(i)).ToList();
                if (invalids.Count > 0)
                {
                    validInstalls = validInstalls.Except(invalids).ToList();
                    var newJson = JsonSerializer.Serialize(validInstalls, DnupManifestJsonContext.Default.ListDotnetInstall);
                    File.WriteAllText(ManifestPath, newJson);
                }
            }
            return validInstalls;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"The dnup manifest is corrupt or inaccessible", ex);
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
        return GetInstalledVersions(validator)
            .Where(install => DnupUtilities.PathsEqual(
                Path.GetFullPath(install.InstallRoot.Path!),
                Path.GetFullPath(installRoot.Path!)));
    }

    public void AddInstalledVersion(DotnetInstall version)
    {
        AssertHasFinalizationMutex();
        EnsureManifestExists();

        var installs = GetInstalledVersions().ToList();
        installs.Add(version);
        var json = JsonSerializer.Serialize(installs, DnupManifestJsonContext.Default.ListDotnetInstall);
        Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath)!);
        File.WriteAllText(ManifestPath, json);
    }

    public void RemoveInstalledVersion(DotnetInstall version)
    {
        AssertHasFinalizationMutex();
        EnsureManifestExists();

        var installs = GetInstalledVersions().ToList();
        installs.RemoveAll(i => DnupUtilities.PathsEqual(i.InstallRoot.Path, version.InstallRoot.Path) && i.Version.Equals(version.Version));
        var json = JsonSerializer.Serialize(installs, DnupManifestJsonContext.Default.ListDotnetInstall);
        File.WriteAllText(ManifestPath, json);
    }
}
