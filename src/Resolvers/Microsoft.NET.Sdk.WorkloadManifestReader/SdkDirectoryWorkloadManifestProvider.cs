// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable IDE0240
#nullable enable
#pragma warning restore IDE0240

using System.Security.Cryptography;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.NET.Sdk.Localization;
using static Microsoft.NET.Sdk.WorkloadManifestReader.IWorkloadManifestProvider;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public partial class SdkDirectoryWorkloadManifestProvider : IWorkloadManifestProvider
    {
        public const string WorkloadSetsFolderName = "workloadsets";

        private readonly string? _sdkRootPath;
        private readonly string? _sdkOrUserLocalPath;
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
        private bool _useManifestsFromInstallState = true;

        //  This will be non-null if there is an error loading manifests that should be thrown when they need to be accessed.
        //  We delay throwing the error so that in the case where global.json specifies a workload set that isn't installed,
        //  we can successfully construct a resolver and install that workload set
        private Exception? _exceptionToThrow = null;
        string? _globalJsonWorkloadSetVersion;

        public SdkDirectoryWorkloadManifestProvider(string? sdkRootPath, string? sdkVersion, string? userProfileDir, string? globalJsonPath)
            : this(sdkRootPath, sdkVersion, Environment.GetEnvironmentVariable, userProfileDir, globalJsonPath)
        {
        }

        public static SdkDirectoryWorkloadManifestProvider ForWorkloadSet(string sdkRootPath, string sdkVersion, string? userProfileDir, string workloadSetVersion)
        {
            return new SdkDirectoryWorkloadManifestProvider(sdkRootPath, sdkVersion, Environment.GetEnvironmentVariable, userProfileDir, globalJsonPath: null, workloadSetVersion);
        }

        internal SdkDirectoryWorkloadManifestProvider(string? sdkRootPath, string? sdkVersion, Func<string, string?> getEnvironmentVariable, string? userProfileDir, string? globalJsonPath = null, string? workloadSetVersion = null)
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

            string? userManifestsRoot = userProfileDir is null ? null : Path.Combine(userProfileDir, "sdk-manifests");
            string dotnetManifestRoot = Path.Combine(_sdkRootPath, "sdk-manifests");
            if (userManifestsRoot != null && WorkloadFileBasedInstall.IsUserLocal(_sdkRootPath, _sdkVersionBand.ToString()) && Directory.Exists(userManifestsRoot))
            {
                _sdkOrUserLocalPath = userProfileDir ?? _sdkRootPath;
                if (getEnvironmentVariable(EnvironmentVariableNames.WORKLOAD_MANIFEST_IGNORE_DEFAULT_ROOTS) == null)
                {
                    _manifestRoots = new[] { userManifestsRoot, dotnetManifestRoot };
                }
            }
            else if (getEnvironmentVariable(EnvironmentVariableNames.WORKLOAD_MANIFEST_IGNORE_DEFAULT_ROOTS) == null)
            {
                _manifestRoots = new[] { dotnetManifestRoot };
            }

            _sdkOrUserLocalPath ??= _sdkRootPath;

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
            //  Reset exception state, we may be refreshing manifests after a missing workload set was installed
            _exceptionToThrow = null;
            _globalJsonWorkloadSetVersion = null;

            _manifestsFromInstallState = null;
            _installStateFilePath = null;
            _useManifestsFromInstallState = true;
            var availableWorkloadSets = GetAvailableWorkloadSets(_sdkVersionBand);
            var workloadSets80100 = GetAvailableWorkloadSets(new SdkFeatureBand("8.0.100"));
            WorkloadSet? workloadSet = null;

            bool TryGetWorkloadSet(string workloadSetVersion, out WorkloadSet? workloadSet)
            {
                if (availableWorkloadSets.TryGetValue(workloadSetVersion, out workloadSet))
                {
                    return true;
                }

                //  Check to see if workload set is from a different feature band
                var workloadSetFeatureBand = WorkloadSetVersion.GetFeatureBand(workloadSetVersion);
                if (!workloadSetFeatureBand.Equals(_sdkVersionBand))
                {
                    var featureBandWorkloadSets = GetAvailableWorkloadSets(workloadSetFeatureBand);
                    if (featureBandWorkloadSets.TryGetValue(workloadSetVersion, out workloadSet))
                    {
                        return true;
                    }
                }

                // The baseline workload sets were merged with a fixed 8.0.100 feature band. That means they will always be here
                // regardless of where they would otherwise belong. This is a workaround for that.
                if (workloadSets80100.TryGetValue(workloadSetVersion, out workloadSet))
                {
                    return true;
                }

                workloadSet = null;
                return false;
            }

            if (_workloadSetVersionFromConstructor != null)
            {
                _useManifestsFromInstallState = false;
                if (!TryGetWorkloadSet(_workloadSetVersionFromConstructor, out workloadSet))
                {
                    throw new FileNotFoundException(string.Format(Strings.WorkloadVersionNotFound, _workloadSetVersionFromConstructor));
                }
            }

            if (workloadSet is null)
            {
                _globalJsonWorkloadSetVersion = GlobalJsonReader.GetWorkloadVersionFromGlobalJson(_globalJsonPathFromConstructor);
                if (_globalJsonWorkloadSetVersion != null)
                {
                    _useManifestsFromInstallState = false;
                    if (!TryGetWorkloadSet(_globalJsonWorkloadSetVersion, out workloadSet))
                    {
                        _exceptionToThrow = new FileNotFoundException(string.Format(Strings.WorkloadVersionFromGlobalJsonNotFound, _globalJsonWorkloadSetVersion, _globalJsonPathFromConstructor));
                        return;
                    }
                }
            }

            _installStateFilePath = Path.Combine(WorkloadInstallType.GetInstallStateFolder(_sdkVersionBand, _sdkOrUserLocalPath), "default.json");
            var installState = InstallStateContents.FromPath(_installStateFilePath);
            if (workloadSet is null)
            {
                if (!string.IsNullOrEmpty(installState.WorkloadVersion))
                {
                    if (!TryGetWorkloadSet(installState.WorkloadVersion!, out workloadSet))
                    {
                        throw new FileNotFoundException(string.Format(Strings.WorkloadVersionFromInstallStateNotFound, installState.WorkloadVersion, _installStateFilePath));
                    }
                }

                //  Note: It is possible here to have both a workload set and loose manifests listed in the install state.  This might happen if there is a
                //  third-party workload manifest installed that's not part of the workload set
                _manifestsFromInstallState = installState.Manifests is null ? null : WorkloadSet.FromDictionaryForJson(installState.Manifests!, _sdkVersionBand);
            }

            if (workloadSet == null && installState.UseWorkloadSets == true && availableWorkloadSets.Any())
            {
                var maxWorkloadSetVersion = availableWorkloadSets.Keys.Aggregate((s1, s2) => VersionCompare(s1, s2) >= 0 ? s1 : s2);
                workloadSet = availableWorkloadSets[maxWorkloadSetVersion.ToString()];
                _useManifestsFromInstallState = false;
            }

            _workloadSet = workloadSet;
        }

        private static int VersionCompare(string first, string second)
        {
            if (first.Equals(second))
            {
                return 0;
            }

            var firstDash = first.IndexOf('-');
            var secondDash = second.IndexOf('-');
            firstDash = firstDash < 0 ? first.Length : firstDash;
            secondDash = secondDash < 0 ? second.Length : secondDash;

            var firstVersion = new Version(first.Substring(0, firstDash));
            var secondVersion = new Version(second.Substring(0, secondDash));

            var comparison = firstVersion.CompareTo(secondVersion);
            if (comparison != 0)
            {
                return comparison;
            }

            var modifiedFirst = new ReleaseVersion(1, 1, 1, firstDash == first.Length ? null : first.Substring(firstDash));
            var modifiedSecond = new ReleaseVersion(1, 1, 1, secondDash == second.Length ? null : second.Substring(secondDash));

            return modifiedFirst.CompareTo(modifiedSecond);
        }

        void ThrowExceptionIfManifestsNotAvailable()
        {
            if (_exceptionToThrow != null)
            {
                throw _exceptionToThrow;
            }
        }

        public WorkloadVersionInfo GetWorkloadVersion()
        {
            if (_globalJsonWorkloadSetVersion != null)
            {
                // _exceptionToThrow is set to null here if and only if the workload set is not installed.
                // If this came from --info or workload --version, the error won't be thrown, but we should still
                // suggest running `dotnet workload restore` to the user.
                return new WorkloadVersionInfo(_globalJsonWorkloadSetVersion, IsInstalled: _exceptionToThrow == null, WorkloadSetsEnabledWithoutWorkloadSet: false, _globalJsonPathFromConstructor);
            }

            ThrowExceptionIfManifestsNotAvailable();

            if (_workloadSet?.Version is not null)
            {
                return new WorkloadVersionInfo(_workloadSet.Version, IsInstalled: true, WorkloadSetsEnabledWithoutWorkloadSet: false);
            }

            var installStateFilePath = Path.Combine(WorkloadInstallType.GetInstallStateFolder(_sdkVersionBand, _sdkOrUserLocalPath), "default.json");
            var installState = InstallStateContents.FromPath(installStateFilePath)!;

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

                return new WorkloadVersionInfo($"{_sdkVersionBand.ToStringWithoutPrerelease()}-manifests.{sb}", IsInstalled: true, WorkloadSetsEnabledWithoutWorkloadSet: installState.UseWorkloadSets == true);
            }
        }

        public IEnumerable<ReadableWorkloadManifest> GetManifests()
        {
            ThrowExceptionIfManifestsNotAvailable();

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
                (string? id, string? finalManifestDirectory, string? version) = ResolveManifestDirectory(manifestDirectory);
                if (id != null && finalManifestDirectory != null)
                {
                    AddManifest(id, finalManifestDirectory, featureBand, version ?? Path.GetFileName(manifestDirectory));
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
                Dictionary<string, string> directoriesWithManifests = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

            if (_useManifestsFromInstallState)
            {
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
            }

            var missingManifestIds = _knownManifestIdsAndOrder?.Keys.Where(id => !manifestIdsToManifests.ContainsKey(id));
            if (missingManifestIds?.Any() == true)
            {
                foreach (var missingManifestId in missingManifestIds)
                {
                    var (manifestDir, featureBand) = FallbackForMissingManifest(missingManifestId);
                    if (!string.IsNullOrEmpty(manifestDir))
                    {
                        AddManifest(missingManifestId, manifestDir, featureBand, Path.GetFileName(manifestDir));
                    }
                }
            }

            //  Return manifests in a stable order. Manifests in the KnownWorkloadManifests.txt file will be first, and in the same order they appear in that file.
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
        private (string? id, string? manifestDirectory, string? version) ResolveManifestDirectory(string manifestDirectory)
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
                        return (directory: dir, version: Path.GetFileName(dir));
                    });

            //  Assume that if there are any versioned subfolders, they are higher manifest versions than a workload manifest directly in the specified folder, if it exists
            if (manifestVersionDirectories.Any())
            {
                var maxVersionDirectory = manifestVersionDirectories.Aggregate((d1, d2) => VersionCompare(d1.version, d2.version) > 0 ? d1 : d2);
                return (manifestId, maxVersionDirectory.directory, maxVersionDirectory.version);
            }
            else if (File.Exists(Path.Combine(manifestDirectory, "WorkloadManifest.json")))
            {
                var manifestPath = Path.Combine(manifestDirectory, "WorkloadManifest.json");
                try
                {
                    var manifestContents = WorkloadManifestReader.ReadWorkloadManifest(manifestId, File.OpenRead(manifestPath), manifestPath);
                    return (manifestId, manifestDirectory, manifestContents.Version);
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
            return GetAvailableWorkloadSetsInternal(null);
        }

        public Dictionary<string, WorkloadSet> GetAvailableWorkloadSets(SdkFeatureBand workloadSetFeatureBand)
        {
            return GetAvailableWorkloadSetsInternal(workloadSetFeatureBand);
        }

        Dictionary<string, WorkloadSet> GetAvailableWorkloadSetsInternal(SdkFeatureBand? workloadSetFeatureBand)
        {
            //  How to deal with cross-band workload sets?
            Dictionary<string, WorkloadSet> availableWorkloadSets = new Dictionary<string, WorkloadSet>();

            foreach (var manifestRoot in _manifestRoots.Reverse())
            {
                if (workloadSetFeatureBand != null)
                {
                    //  Get workload sets for specific feature band
                    var featureBandDirectory = Path.Combine(manifestRoot, workloadSetFeatureBand.Value.ToString());
                    AddWorkloadSetsForFeatureBand(availableWorkloadSets, featureBandDirectory);
                }
                else
                {
                    //  Get workload sets for all feature bands 
                    foreach (var featureBandDirectory in Directory.GetDirectories(manifestRoot))
                    {
                        AddWorkloadSetsForFeatureBand(availableWorkloadSets, featureBandDirectory);
                    }
                }
            }

            return availableWorkloadSets;

            static void AddWorkloadSetsForFeatureBand(Dictionary<string, WorkloadSet> availableWorkloadSets, string featureBandDirectory)
            {
                var featureBandDirectoryName = Path.GetFileName(featureBandDirectory);
                var featureBand = new SdkFeatureBand(featureBandDirectoryName);
                if (!featureBandDirectoryName.Equals(featureBand.ToString()))
                {
                    //  A folder which should be a feature band parses as something that doesn't match the feature band.  For example,
                    //  a folder named 9.0.100-rtm.24476 would parse as feature band 9.0.100.  When we try to look up the workload set
                    //  later, we would look for it in a 9.0.100 folder, and wouldn't find it.  So we will ignore these incorrect folders
                    return;
                }

                var workloadSetsRoot = Path.Combine(featureBandDirectory, WorkloadSetsFolderName);
                if (Directory.Exists(workloadSetsRoot))
                {
                    foreach (var workloadSetDirectory in Directory.GetDirectories(workloadSetsRoot))
                    {
                        var workloadSetVersion = Path.GetFileName(workloadSetDirectory);
                        var workloadSet = WorkloadSet.FromWorkloadSetFolder(workloadSetDirectory, workloadSetVersion, featureBand);

                        if (!WorkloadSet.GetWorkloadSetFeatureBand(workloadSet.Version!).Equals(featureBand))
                        {
                            //  We have a workload set version where the feature band doesn't match the feature band folder that it's in.
                            //  Skip it, as if we try to actually load it via the workload set version, we'll fail
                            continue;
                        }

                        availableWorkloadSets[workloadSet.Version!] = workloadSet;
                    }
                }
            }
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
