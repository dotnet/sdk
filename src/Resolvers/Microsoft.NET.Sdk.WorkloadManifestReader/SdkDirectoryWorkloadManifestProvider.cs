﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.NET.Sdk.Localization;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public partial class SdkDirectoryWorkloadManifestProvider : IWorkloadManifestProvider
    {
        public const string WorkloadSetsFolderName = "workloadsets";

        private readonly string _sdkRootPath;
        private readonly SdkFeatureBand _sdkVersionBand;
        private readonly string[] _manifestRoots;
        private static HashSet<string> _outdatedManifestIds = new(StringComparer.OrdinalIgnoreCase) { "microsoft.net.workload.android", "microsoft.net.workload.blazorwebassembly", "microsoft.net.workload.ios",
            "microsoft.net.workload.maccatalyst", "microsoft.net.workload.macos", "microsoft.net.workload.tvos", "microsoft.net.workload.mono.toolchain" };
        private readonly Dictionary<string, int>? _knownManifestIdsAndOrder;

        private readonly string? _workloadSetVersionFromConstructor;
        private readonly string? _globalJsonPathFromConstructor;

        private WorkloadSet? _workloadSet;
        private WorkloadSet? _manifestsFromInstallState;
        private string? _installStateFilePath;

        public SdkDirectoryWorkloadManifestProvider(string sdkRootPath, string sdkVersion, string? userProfileDir, string? globalJsonPath)
            : this(sdkRootPath, sdkVersion, Environment.GetEnvironmentVariable, userProfileDir, globalJsonPath)
        {
        }

        public static SdkDirectoryWorkloadManifestProvider ForWorkloadSet(string sdkRootPath, string sdkVersion, string? userProfileDir, string workloadSetVersion)
        {
            return new SdkDirectoryWorkloadManifestProvider(sdkRootPath, sdkVersion, Environment.GetEnvironmentVariable, userProfileDir, globalJsonPath: null, workloadSetVersion);
        }

        internal SdkDirectoryWorkloadManifestProvider(string sdkRootPath, string sdkVersion, Func<string, string?> getEnvironmentVariable, string? userProfileDir, string? globalJsonPath = null, string? workloadSetVersion = null)
        {
            if (string.IsNullOrWhiteSpace(sdkVersion))
            {
                throw new ArgumentException($"'{nameof(sdkVersion)}' cannot be null or whitespace", nameof(sdkVersion));
            }

            if (string.IsNullOrWhiteSpace(sdkRootPath))
            {
                throw new ArgumentException($"'{nameof(sdkRootPath)}' cannot be null or whitespace",
                    nameof(sdkRootPath));
            }

            if (globalJsonPath != null && workloadSetVersion != null)
            {
                throw new ArgumentException($"Cannot specify both {nameof(globalJsonPath)} and {nameof(workloadSetVersion)}");
            }

            _sdkRootPath = sdkRootPath;
            _sdkVersionBand = new SdkFeatureBand(sdkVersion);
            _workloadSetVersionFromConstructor = workloadSetVersion;
            _globalJsonPathFromConstructor = globalJsonPath;

            var knownManifestIdsFilePath = Path.Combine(_sdkRootPath, "sdk", sdkVersion, "KnownWorkloadManifests.txt");
            if (!File.Exists(knownManifestIdsFilePath))
            {
                knownManifestIdsFilePath = Path.Combine(_sdkRootPath, "sdk", sdkVersion, "IncludedWorkloadManifests.txt");
            }

            if (File.Exists(knownManifestIdsFilePath))
            {
                int lineNumber = 0;
                _knownManifestIdsAndOrder = new Dictionary<string, int>();
                foreach (var manifestId in File.ReadAllLines(knownManifestIdsFilePath).Where(l => !string.IsNullOrEmpty(l)))
                {
                    _knownManifestIdsAndOrder[manifestId] = lineNumber++;
                }
            }

            if (getEnvironmentVariable(EnvironmentVariableNames.WORKLOAD_MANIFEST_IGNORE_DEFAULT_ROOTS) == null)
            {
                string? userManifestsRoot = userProfileDir is null ? null : Path.Combine(userProfileDir, "sdk-manifests");
                string dotnetManifestRoot = Path.Combine(_sdkRootPath, "sdk-manifests");
                if (userManifestsRoot != null && WorkloadFileBasedInstall.IsUserLocal(_sdkRootPath, _sdkVersionBand.ToString()) && Directory.Exists(userManifestsRoot))
                {
                    _manifestRoots = new[] { userManifestsRoot, dotnetManifestRoot };
                }
                else
                {
                    _manifestRoots = new[] { dotnetManifestRoot };
                }
            }

            var manifestDirectoryEnvironmentVariable = getEnvironmentVariable(EnvironmentVariableNames.WORKLOAD_MANIFEST_ROOTS);
            if (manifestDirectoryEnvironmentVariable != null)
            {
                //  Append the SDK version band to each manifest root specified via the environment variable.  This allows the same
                //  environment variable settings to be shared by multiple SDKs.
                _manifestRoots = manifestDirectoryEnvironmentVariable.Split(Path.PathSeparator)
                                    .Concat(_manifestRoots ?? Array.Empty<string>()).ToArray();

            }

            _manifestRoots ??= Array.Empty<string>();

            RefreshWorkloadManifests();
        }

        public void RefreshWorkloadManifests()
        {
            var availableWorkloadSets = GetAvailableWorkloadSets();

            if (_workloadSetVersionFromConstructor != null)
            {
                if (!availableWorkloadSets.TryGetValue(_workloadSetVersionFromConstructor, out _workloadSet))
                {
                    throw new FileNotFoundException(string.Format(Strings.WorkloadVersionNotFound, _workloadSetVersionFromConstructor));
                }
            }
            else
            {
                string? globalJsonWorkloadSetVersion = GlobalJsonReader.GetWorkloadVersionFromGlobalJson(_globalJsonPathFromConstructor);
                if (globalJsonWorkloadSetVersion != null)
                {
                    if (!availableWorkloadSets.TryGetValue(globalJsonWorkloadSetVersion, out _workloadSet))
                    {
                        throw new FileNotFoundException(string.Format(Strings.WorkloadVersionFromGlobalJsonNotFound, globalJsonWorkloadSetVersion, _globalJsonPathFromConstructor));
                    }
                }
                else
                {
                    var installStateFilePath = Path.Combine(WorkloadInstallType.GetInstallStateFolder(_sdkVersionBand, _sdkRootPath), "default.json");
                    if (File.Exists(installStateFilePath))
                    {
                        var installState = InstallStateReader.ReadInstallState(installStateFilePath);
                        if (!string.IsNullOrEmpty(installState.WorkloadSetVersion))
                        {
                            if (!availableWorkloadSets.TryGetValue(installState.WorkloadSetVersion!, out _workloadSet))
                            {
                                throw new FileNotFoundException(string.Format(Strings.WorkloadVersionFromInstallStateNotFound, installState.WorkloadSetVersion, installStateFilePath));
                            }
                        }
                        _manifestsFromInstallState = installState.Manifests;
                        _installStateFilePath = installStateFilePath;
                    }
                }
            }

            if (_workloadSet == null && availableWorkloadSets.Any())
            {
                var maxWorkloadSetVersion = availableWorkloadSets.Keys.Select(k => new ReleaseVersion(k)).Max()!;
                _workloadSet = availableWorkloadSets[maxWorkloadSetVersion.ToString()];
            }
        }

        public string GetWorkloadVersion()
        {
            if (_workloadSet?.Version is not null)
            {
                return _workloadSet?.Version!;
            }

            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(string.Join(";",
                            GetManifests().OrderBy(m => m.ManifestId).Select(m => $"{m.ManifestId}.{m.ManifestFeatureBand}.{m.ManifestVersion}").ToArray()
                        )));

                // Only append the first four bytes to the version hash.
                // We want the versions outputted here to be unique but ideally not too long.
                StringBuilder sb = new();
                for (int b = 0; b < 4 && b < bytes.Length; b++)
                {
                    sb.Append(bytes[b].ToString("x2"));
                }

                return $"{_sdkVersionBand.ToStringWithoutPrerelease()}-manifests.{sb}";
            }
        }

        public IEnumerable<ReadableWorkloadManifest> GetManifests()
        {
            //  Scan manifest directories
            var manifestIdsToManifests = new Dictionary<string, ReadableWorkloadManifest>(StringComparer.OrdinalIgnoreCase);

            void AddManifest(string manifestId, string manifestDirectory, string featureBand, string manifestVersion)
            {
                var workloadManifestPath = Path.Combine(manifestDirectory, "WorkloadManifest.json");

                var readableManifest = new ReadableWorkloadManifest(
                    manifestId,
                    manifestDirectory,
                    workloadManifestPath,
                    featureBand,
                    manifestVersion,
                    () => File.OpenRead(workloadManifestPath),
                    () => WorkloadManifestReader.TryOpenLocalizationCatalogForManifest(workloadManifestPath));

                manifestIdsToManifests[manifestId] = readableManifest;
            }

            void ProbeDirectory(string manifestDirectory, string featureBand)
            {
                (string? id, string? finalManifestDirectory, ReleaseVersion? version) = ResolveManifestDirectory(manifestDirectory);
                if (id != null && finalManifestDirectory != null)
                {
                    AddManifest(id, finalManifestDirectory, featureBand, version?.ToString() ?? Path.GetFileName(manifestDirectory));
                }
            }

            if (_manifestRoots.Length == 1)
            {
                //  Optimization for common case where test hook to add additional directories isn't being used
                var manifestVersionBandDirectory = Path.Combine(_manifestRoots[0], _sdkVersionBand.ToString());
                if (Directory.Exists(manifestVersionBandDirectory))
                {
                    foreach (var workloadManifestDirectory in Directory.EnumerateDirectories(manifestVersionBandDirectory))
                    {
                        ProbeDirectory(workloadManifestDirectory, _sdkVersionBand.ToString());
                    }
                }
            }
            else
            {
                //  If the same folder name is in multiple of the workload manifest directories, take the first one
                Dictionary<string, string> directoriesWithManifests = new(StringComparer.OrdinalIgnoreCase);
                foreach (var manifestRoot in _manifestRoots.Reverse())
                {
                    var manifestVersionBandDirectory = Path.Combine(manifestRoot, _sdkVersionBand.ToString());
                    if (Directory.Exists(manifestVersionBandDirectory))
                    {
                        foreach (var workloadManifestDirectory in Directory.EnumerateDirectories(manifestVersionBandDirectory))
                        {
                            directoriesWithManifests[Path.GetFileName(workloadManifestDirectory)] = workloadManifestDirectory;
                        }
                    }
                }

                foreach (var workloadManifestDirectory in directoriesWithManifests.Values)
                {
                    ProbeDirectory(workloadManifestDirectory, _sdkVersionBand.ToString());
                }
            }

            //  Load manifests from workload set, if any.  This will override any manifests for the same IDs that were loaded previously in this method
            if (_workloadSet != null)
            {
                foreach (var kvp in _workloadSet.ManifestVersions)
                {
                    var manifestSpecifier = new ManifestSpecifier(kvp.Key, kvp.Value.Version, kvp.Value.FeatureBand);
                    var manifestDirectory = GetManifestDirectoryFromSpecifier(manifestSpecifier);
                    if (manifestDirectory == null)
                    {
                        throw new FileNotFoundException(string.Format(Strings.ManifestFromWorkloadSetNotFound, manifestSpecifier.ToString(), _workloadSet.Version));
                    }
                    AddManifest(manifestSpecifier.Id.ToString(), manifestDirectory, manifestSpecifier.FeatureBand.ToString(), kvp.Value.Version.ToString());
                }
            }

            //  Load manifests from install state
            if (_manifestsFromInstallState != null)
            {
                foreach (var kvp in _manifestsFromInstallState.ManifestVersions)
                {
                    var manifestSpecifier = new ManifestSpecifier(kvp.Key, kvp.Value.Version, kvp.Value.FeatureBand);
                    var manifestDirectory = GetManifestDirectoryFromSpecifier(manifestSpecifier);
                    if (manifestDirectory == null)
                    {
                        throw new FileNotFoundException(string.Format(Strings.ManifestFromInstallStateNotFound, manifestSpecifier.ToString(), _installStateFilePath));
                    }
                    AddManifest(manifestSpecifier.Id.ToString(), manifestDirectory, manifestSpecifier.FeatureBand.ToString(), kvp.Value.Version.ToString());
                }
            }

            if (_knownManifestIdsAndOrder != null && _knownManifestIdsAndOrder.Keys.Any(id => !manifestIdsToManifests.ContainsKey(id)))
            {
                var missingManifestIds = _knownManifestIdsAndOrder.Keys.Where(id => !manifestIdsToManifests.ContainsKey(id));
                foreach (var missingManifestId in missingManifestIds)
                {
                    var (manifestDir, featureBand) = FallbackForMissingManifest(missingManifestId);
                    if (!string.IsNullOrEmpty(manifestDir))
                    {
                        AddManifest(missingManifestId, manifestDir, featureBand, Path.GetFileName(manifestDir));
                    }
                }
            }

            //  Return manifests in a stable order.  Manifests in the KnownWorkloadManifests.txt file will be first, and in the same order they appear in that file.
            //  Then the rest of the manifests (if any) will be returned in (ordinal case-insensitive) alphabetical order.
            return manifestIdsToManifests
                .OrderBy(kvp =>
                {
                    if (_knownManifestIdsAndOrder != null &&
                        _knownManifestIdsAndOrder.TryGetValue(kvp.Key, out var order))
                    {
                        return order;
                    }
                    return int.MaxValue;
                })
                .ThenBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kvp => kvp.Value)
                .ToList();
        }

        /// <summary>
        /// Given a folder that may directly include a WorkloadManifest.json file, or may have the workload manifests in version subfolders, choose the directory
        /// with the latest workload manifest.
        /// </summary>
        private (string? id, string? manifestDirectory, ReleaseVersion? version) ResolveManifestDirectory(string manifestDirectory)
        {
            string manifestId = Path.GetFileName(manifestDirectory);
            if (_outdatedManifestIds.Contains(manifestId) ||
                manifestId.Equals(WorkloadSetsFolderName, StringComparison.OrdinalIgnoreCase))
            {
                return (null, null, null);
            }

            var manifestVersionDirectories = Directory.GetDirectories(manifestDirectory)
                    .Where(dir => File.Exists(Path.Combine(dir, "WorkloadManifest.json")))
                    .Select(dir =>
                    {
                        ReleaseVersion? releaseVersion = null;
                        ReleaseVersion.TryParse(Path.GetFileName(dir), out releaseVersion);
                        return (directory: dir, version: releaseVersion);
                    })
                    .Where(t => t.version != null)
                    .OrderByDescending(t => t.version)
                    .ToList();

            //  Assume that if there are any versioned subfolders, they are higher manifest versions than a workload manifest directly in the specified folder, if it exists
            if (manifestVersionDirectories.Any())
            {
                return (manifestId, manifestVersionDirectories.First().directory, manifestVersionDirectories.First().version);
            }
            else if (File.Exists(Path.Combine(manifestDirectory, "WorkloadManifest.json")))
            {
                var manifestPath = Path.Combine(manifestDirectory, "WorkloadManifest.json");
                try
                {
                    var manifestContents = WorkloadManifestReader.ReadWorkloadManifest(manifestId, File.OpenRead(manifestPath), manifestPath);
                    return (manifestId, manifestDirectory, new ReleaseVersion(manifestContents.Version));
                }
                catch
                { }

                return (manifestId, manifestDirectory, null);
            }
            return (null, null, null);
        }

        private (string manifestDirectory, string manifestFeatureBand) FallbackForMissingManifest(string manifestId)
        {
            //  Only use the last manifest root (usually the dotnet folder itself) for fallback
            var sdkManifestPath = _manifestRoots.Last();
            if (!Directory.Exists(sdkManifestPath))
            {
                return (string.Empty, string.Empty);
            }

            var candidateFeatureBands = Directory.GetDirectories(sdkManifestPath)
                .Select(dir => Path.GetFileName(dir))
                .Select(featureBand => new SdkFeatureBand(featureBand))
                .Where(featureBand => featureBand < _sdkVersionBand || _sdkVersionBand.ToStringWithoutPrerelease().Equals(featureBand.ToString(), StringComparison.Ordinal));

            var matchingManifestFeatureBandsAndResolvedManifestDirectories = candidateFeatureBands
                //  Calculate path to <FeatureBand>\<ManifestID>
                .Select(featureBand => (featureBand, manifestDirectory: Path.Combine(sdkManifestPath, featureBand.ToString(), manifestId)))
                //  Filter out directories that don't exist
                .Where(t => Directory.Exists(t.manifestDirectory))
                //  Inside directory, resolve where to find WorkloadManifest.json
                .Select(t => (t.featureBand, res: ResolveManifestDirectory(t.manifestDirectory)))
                //  Filter out directories where no WorkloadManifest.json was resolved
                .Where(t => t.res.id != null && t.res.manifestDirectory != null)
                .ToList();

            if (matchingManifestFeatureBandsAndResolvedManifestDirectories.Any())
            {
                var selectedFeatureBandAndManifestDirectory = matchingManifestFeatureBandsAndResolvedManifestDirectories.OrderByDescending(t => t.featureBand).First();
                return (selectedFeatureBandAndManifestDirectory.res.manifestDirectory!, selectedFeatureBandAndManifestDirectory.featureBand.ToString());
            }
            else
            {
                // Manifest does not exist
                return (string.Empty, string.Empty);
            }
        }

        private string? GetManifestDirectoryFromSpecifier(ManifestSpecifier manifestSpecifier)
        {
            foreach (var manifestDirectory in _manifestRoots)
            {
                var specifiedManifestDirectory = Path.Combine(manifestDirectory, manifestSpecifier.FeatureBand.ToString(), manifestSpecifier.Id.ToString(),
                    manifestSpecifier.Version.ToString());
                if (File.Exists(Path.Combine(specifiedManifestDirectory, "WorkloadManifest.json")))
                {
                    return specifiedManifestDirectory;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns installed workload sets that are available for this SDK (ie are in the same feature band)
        /// </summary>
        public Dictionary<string, WorkloadSet> GetAvailableWorkloadSets()
        {
            Dictionary<string, WorkloadSet> availableWorkloadSets = new();

            foreach (var manifestRoot in _manifestRoots.Reverse())
            {
                //  Workload sets must match the SDK feature band, we don't support any fallback to a previous band
                var workloadSetsRoot = Path.Combine(manifestRoot, _sdkVersionBand.ToString(), WorkloadSetsFolderName);
                if (Directory.Exists(workloadSetsRoot))
                {
                    foreach (var workloadSetDirectory in Directory.GetDirectories(workloadSetsRoot))
                    {
                        WorkloadSet? workloadSet = null;
                        foreach (var jsonFile in Directory.GetFiles(workloadSetDirectory, "*.workloadset.json"))
                        {
                            var newWorkloadSet = WorkloadSet.FromJson(File.ReadAllText(jsonFile), _sdkVersionBand);
                            if (workloadSet == null)
                            {
                                workloadSet = newWorkloadSet;
                            }
                            else
                            {
                                //  If there are multiple workloadset.json files, merge them
                                foreach (var kvp in newWorkloadSet.ManifestVersions)
                                {
                                    workloadSet.ManifestVersions.Add(kvp.Key, kvp.Value);
                                }
                            }
                        }
                        if (workloadSet != null)
                        {
                            if (File.Exists(Path.Combine(workloadSetDirectory, "baseline.workloadset.json")))
                            {
                                workloadSet.IsBaselineWorkloadSet = true;
                            }

                            workloadSet.Version = Path.GetFileName(workloadSetDirectory);
                            availableWorkloadSets[workloadSet.Version] = workloadSet;
                        }
                    }
                }
            }

            return availableWorkloadSets;
        }

        public string GetSdkFeatureBand()
        {
            return _sdkVersionBand.ToString();
        }

        public static string? GetGlobalJsonPath(string? globalJsonStartDir)
        {
            string? directory = globalJsonStartDir;
            while (directory != null)
            {
                string globalJsonPath = Path.Combine(directory, "global.json");
                if (File.Exists(globalJsonPath))
                {
                    return globalJsonPath;
                }
                directory = Path.GetDirectoryName(directory);
            }
            return null;
        }
    }
}
