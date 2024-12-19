// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Credentials;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader
{
    // TODO: Never name a class the same name as the namespace. Update either for easier type resolution.
    internal class NuGetPackageDownloader : INuGetPackageDownloader
    {
        private readonly SourceCacheContext _cacheSettings;
        private readonly IFilePermissionSetter _filePermissionSetter;

        /// <summary>
        /// In many commands we don't passing NuGetConsoleLogger and pass NullLogger instead to reduce the verbosity
        /// </summary>
        private readonly ILogger _verboseLogger;
        private readonly DirectoryPath _packageInstallDir;
        private readonly RestoreActionConfig _restoreActionConfig;
        private readonly Func<IEnumerable<Task>> _retryTimer;

        /// <summary>
        /// Reporter would output to the console regardless
        /// </summary>
        private readonly IReporter _reporter;
        private readonly IFirstPartyNuGetPackageSigningVerifier _firstPartyNuGetPackageSigningVerifier;
        private bool _validationMessagesDisplayed = false;
        private readonly Dictionary<PackageSource, SourceRepository> _sourceRepositories;
        private readonly bool _shouldUsePackageSourceMapping;

        private readonly bool _verifySignatures;
        private readonly VerbosityOptions _verbosityOptions;
        private readonly string _currentWorkingDirectory;

        public NuGetPackageDownloader(
            DirectoryPath packageInstallDir,
            IFilePermissionSetter filePermissionSetter = null,
            IFirstPartyNuGetPackageSigningVerifier firstPartyNuGetPackageSigningVerifier = null,
            ILogger verboseLogger = null,
            IReporter reporter = null,
            RestoreActionConfig restoreActionConfig = null,
            Func<IEnumerable<Task>> timer = null,
            bool verifySignatures = false,
            bool shouldUsePackageSourceMapping = false,
            VerbosityOptions verbosityOptions = VerbosityOptions.normal,
            string currentWorkingDirectory = null)
        {
            _currentWorkingDirectory = currentWorkingDirectory;
            _packageInstallDir = packageInstallDir;
            _reporter = reporter ?? Reporter.Output;
            _verboseLogger = verboseLogger ?? new NuGetConsoleLogger();
            _firstPartyNuGetPackageSigningVerifier = firstPartyNuGetPackageSigningVerifier ??
                                                     new FirstPartyNuGetPackageSigningVerifier();
            _filePermissionSetter = filePermissionSetter ?? new FilePermissionSetter();
            _restoreActionConfig = restoreActionConfig ?? new RestoreActionConfig();
            _retryTimer = timer;
            _sourceRepositories = new();
            _verifySignatures = verifySignatures;

            _cacheSettings = new SourceCacheContext
            {
                NoCache = _restoreActionConfig.NoCache,
                DirectDownload = true,
                IgnoreFailedSources = _restoreActionConfig.IgnoreFailedSources,
            };

            DefaultCredentialServiceUtility.SetupDefaultCredentialService(new NuGetConsoleLogger(),
                !_restoreActionConfig.Interactive);
            _shouldUsePackageSourceMapping = shouldUsePackageSourceMapping;
            _verbosityOptions = verbosityOptions;
        }

        public async Task<string> DownloadPackageAsync(PackageId packageId,
            NuGetVersion packageVersion = null,
            PackageSourceLocation packageSourceLocation = null,
            bool includePreview = false,
            bool? includeUnlisted = null,
            DirectoryPath? downloadFolder = null,
            PackageSourceMapping packageSourceMapping = null)
        {
            CancellationToken cancellationToken = CancellationToken.None;

            (var source, var resolvedPackageVersion) = await GetPackageSourceAndVersion(packageId, packageVersion,
                packageSourceLocation, includePreview, includeUnlisted ?? packageVersion is not null, packageSourceMapping).ConfigureAwait(false);

            FindPackageByIdResource resource = null;
            SourceRepository repository = GetSourceRepository(source);

            resource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken)
                .ConfigureAwait(false);

            if (resource == null)
            {
                throw new NuGetPackageNotFoundException(
                    string.Format(LocalizableStrings.IsNotFoundInNuGetFeeds, packageId, source.Source));
            }

            var pathResolver = new VersionFolderPathResolver(downloadFolder == null || !downloadFolder.HasValue ? _packageInstallDir.Value : downloadFolder.Value.Value);
            string nupkgPath = pathResolver.GetPackageFilePath(packageId.ToString(), resolvedPackageVersion);
            Directory.CreateDirectory(Path.GetDirectoryName(nupkgPath));

            using FileStream destinationStream = File.Create(nupkgPath);
            bool success = await ExponentialRetry.ExecuteWithRetryOnFailure(async () => await resource.CopyNupkgToStreamAsync(
                packageId.ToString(),
                resolvedPackageVersion,
                destinationStream,
                _cacheSettings,
                _verboseLogger,
                cancellationToken));
            destinationStream.Close();

            if (!success)
            {
                throw new NuGetPackageInstallerException(
                    string.Format("Downloading {0} version {1} failed", packageId,
                        packageVersion.ToNormalizedString()));
            }

            await VerifySigning(nupkgPath, repository);
            
            return nupkgPath;
        }

        private bool VerbosityGreaterThanMinimal() =>
            _verbosityOptions != VerbosityOptions.quiet && _verbosityOptions != VerbosityOptions.q &&
            _verbosityOptions != VerbosityOptions.minimal && _verbosityOptions != VerbosityOptions.m;

        private bool DiagnosticVerbosity() => _verbosityOptions == VerbosityOptions.diag || _verbosityOptions == VerbosityOptions.diagnostic;

        private async Task VerifySigning(string nupkgPath, SourceRepository repository)
        {
            if (!_verifySignatures && !_validationMessagesDisplayed)
            {
                if (VerbosityGreaterThanMinimal())
                {
                    _reporter.WriteLine(LocalizableStrings.NuGetPackageSignatureVerificationSkipped);
                }
                _validationMessagesDisplayed = true;
            }

            if (!_verifySignatures)
            {
                return;
            }

            if (repository is not null &&
                await repository.GetResourceAsync<RepositorySignatureResource>().ConfigureAwait(false) is RepositorySignatureResource resource &&
                resource.AllRepositorySigned)
            {
                string commandOutput;
                // The difference between _firstPartyNuGetPackageSigningVerifier.Verify and FirstPartyNuGetPackageSigningVerifier.NuGetVerify is that while NuGetVerify
                // just ensures that the package is signed properly, Verify additionally requires that the package be from Microsoft. NuGetVerify does not require that
                // the package be from Microsoft.
                if ((!_shouldUsePackageSourceMapping && !_firstPartyNuGetPackageSigningVerifier.Verify(new FilePath(nupkgPath), out commandOutput)) ||
                    (_shouldUsePackageSourceMapping && !FirstPartyNuGetPackageSigningVerifier.NuGetVerify(new FilePath(nupkgPath), out commandOutput, _currentWorkingDirectory)))
                {
                    throw new NuGetPackageInstallerException(string.Format(LocalizableStrings.FailedToValidatePackageSigning, commandOutput));
                }

                if (DiagnosticVerbosity())
                {
                    _reporter.WriteLine(LocalizableStrings.VerifyingNuGetPackageSignature, Path.GetFileNameWithoutExtension(nupkgPath));
                }
            }
            else if (DiagnosticVerbosity())
            {
                _reporter.WriteLine(LocalizableStrings.NuGetPackageShouldNotBeSigned, Path.GetFileNameWithoutExtension(nupkgPath));
            }
        }

        public async Task<string> GetPackageUrl(PackageId packageId,
            NuGetVersion packageVersion = null,
            PackageSourceLocation packageSourceLocation = null,
            bool includePreview = false)
        {
            (var source, var resolvedPackageVersion) = await GetPackageSourceAndVersion(packageId, packageVersion, packageSourceLocation, includePreview).ConfigureAwait(false);

            SourceRepository repository = GetSourceRepository(source);
            if (repository.PackageSource.IsLocal)
            {
                return Path.Combine(
                    repository.PackageSource.Source,
                    new VersionFolderPathResolver(repository.PackageSource.Source).GetPackageFileName(packageId.ToString(), resolvedPackageVersion)
                );
            }

            ServiceIndexResourceV3 serviceIndexResource = repository.GetResourceAsync<ServiceIndexResourceV3>().Result;
            IReadOnlyList<Uri> packageBaseAddress =
                serviceIndexResource?.GetServiceEntryUris(ServiceTypes.PackageBaseAddress);

            return GetNupkgUrl(packageBaseAddress[0].ToString(), packageId, resolvedPackageVersion);
        }

        public async Task<IEnumerable<string>> ExtractPackageAsync(string packagePath, DirectoryPath targetFolder)
        {
            await using FileStream packageStream = File.OpenRead(packagePath);
            PackageFolderReader packageReader = new(targetFolder.Value);
            PackageExtractionContext packageExtractionContext = new(
                PackageSaveMode.Defaultv3,
                XmlDocFileSaveMode.None,
                null,
                _verboseLogger);
            NuGetPackagePathResolver packagePathResolver = new(targetFolder.Value);
            CancellationToken cancellationToken = CancellationToken.None;

            var allFilesInPackage = await PackageExtractor.ExtractPackageAsync(
                targetFolder.Value,
                packageStream,
                packagePathResolver,
                packageExtractionContext,
                cancellationToken);

            if (!OperatingSystem.IsWindows())
            {
                string workloadUnixFilePermissions = allFilesInPackage.SingleOrDefault(p =>
                    Path.GetRelativePath(targetFolder.Value, p).Equals("data/UnixFilePermissions.xml",
                        StringComparison.OrdinalIgnoreCase));

                if (workloadUnixFilePermissions != default)
                {
                    var permissionList = WorkloadUnixFilePermissions.FileList.Deserialize(workloadUnixFilePermissions);
                    foreach (var fileAndPermission in permissionList.File)
                    {
                        _filePermissionSetter
                            .SetPermission(
                                Path.Combine(targetFolder.Value, fileAndPermission.Path),
                                fileAndPermission.Permission);
                    }
                }
            }

            return allFilesInPackage;
        }

        public async Task<IEnumerable<IPackageSearchMetadata>> GetLatestVersionsOfPackage(string packageId, bool includePreview, int numberOfResults)
        {
            IEnumerable<PackageSource> packageSources = LoadNuGetSources(new PackageId(packageId), null, null);
            return (await GetLatestVersionsInternalAsync(packageId, packageSources, includePreview, CancellationToken.None, numberOfResults)).Select(result => result.Item2);
        }

        private async Task<(PackageSource, NuGetVersion)> GetPackageSourceAndVersion(PackageId packageId,
             NuGetVersion packageVersion = null,
             PackageSourceLocation packageSourceLocation = null,
             bool includePreview = false,
             bool includeUnlisted = false,
             PackageSourceMapping packageSourceMapping = null)
        {
            CancellationToken cancellationToken = CancellationToken.None;

            IPackageSearchMetadata packageMetadata;

            IEnumerable<PackageSource> packagesSources = LoadNuGetSources(packageId, packageSourceLocation, packageSourceMapping);
            PackageSource source;

            if (packageVersion is null)
            {
                (source, packageMetadata) = await GetLatestVersionInternalAsync(packageId.ToString(), packagesSources,
                    includePreview, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                packageVersion = new NuGetVersion(packageVersion);
                (source, packageMetadata) =
                    await GetPackageMetadataAsync(packageId.ToString(), packageVersion, packagesSources,
                        cancellationToken, includeUnlisted).ConfigureAwait(false);
            }

            packageVersion = packageMetadata.Identity.Version;

            return (source, packageVersion);
        }

        private string GetNupkgUrl(string baseUri, PackageId id, NuGetVersion version) => baseUri + id.ToString() + "/" + version.ToNormalizedString() + "/" + id.ToString() +
            "." + version.ToNormalizedString().ToLowerInvariant() + ".nupkg";

        internal IEnumerable<FilePath> FindAllFilesNeedExecutablePermission(IEnumerable<string> files,
            string targetPath)
        {
            if (!PackageIsInAllowList(files))
            {
                return Array.Empty<FilePath>();
            }

            bool FileUnderToolsWithoutSuffix(string p)
            {
                return Path.GetRelativePath(targetPath, p).StartsWith("tools" + Path.DirectorySeparatorChar) &&
                       (Path.GetFileName(p) == Path.GetFileNameWithoutExtension(p));
            }

            return files
                .Where(FileUnderToolsWithoutSuffix)
                .Select(f => new FilePath(f));
        }

        private static bool PackageIsInAllowList(IEnumerable<string> files)
        {
            var allowListOfPackage = new string[] {
                "microsoft.android.sdk.darwin",
                "Microsoft.MacCatalyst.Sdk",
                "Microsoft.iOS.Sdk",
                "Microsoft.macOS.Sdk",
                "Microsoft.tvOS.Sdk"};

            var allowListNuspec = allowListOfPackage.Select(s => s + ".nuspec");

            if (!files.Any(f =>
                allowListNuspec.Contains(Path.GetFileName(f), comparer: StringComparer.OrdinalIgnoreCase)))
            {
                return false;
            }

            return true;
        }

        private IEnumerable<PackageSource> LoadOverrideSources(PackageSourceLocation packageSourceLocation = null)
        {
            foreach (string source in packageSourceLocation?.SourceFeedOverrides)
            {
                if (string.IsNullOrWhiteSpace(source))
                {
                    continue;
                }

                PackageSource packageSource = new(source);
                if (packageSource.TrySourceAsUri == null)
                {
                    _verboseLogger.LogWarning(string.Format(
                        LocalizableStrings.FailedToLoadNuGetSource,
                        source));
                    continue;
                }

                yield return packageSource;
            }

        }

        private List<PackageSource> LoadDefaultSources(PackageId packageId, PackageSourceLocation packageSourceLocation = null, PackageSourceMapping packageSourceMapping = null)
        {
            List<PackageSource> defaultSources = new();
            string currentDirectory = Directory.GetCurrentDirectory();
            ISettings settings;
            if (packageSourceLocation?.NugetConfig != null)
            {
                string nugetConfigParentDirectory =
                    packageSourceLocation.NugetConfig.Value.GetDirectoryPath().Value;
                string nugetConfigFileName = Path.GetFileName(packageSourceLocation.NugetConfig.Value.Value);
                settings = Settings.LoadSpecificSettings(nugetConfigParentDirectory,
                    nugetConfigFileName);
            }
            else
            {
                settings = Settings.LoadDefaultSettings(
                    packageSourceLocation?.RootConfigDirectory?.Value ?? currentDirectory);
            }

            PackageSourceProvider packageSourceProvider = new(settings);
            defaultSources = packageSourceProvider.LoadPackageSources().Where(source => source.IsEnabled).ToList();

            packageSourceMapping ??= PackageSourceMapping.GetPackageSourceMapping(settings);

            // filter package patterns if enabled
            if (_shouldUsePackageSourceMapping && packageSourceMapping?.IsEnabled == true)
            {
                IReadOnlyList<string> sources = packageSourceMapping.GetConfiguredPackageSources(packageId.ToString());

                if (sources.Count == 0)
                {
                    throw new NuGetPackageInstallerException(string.Format(LocalizableStrings.FailedToFindSourceUnderPackageSourceMapping, packageId));
                }
                defaultSources = defaultSources.Where(source => sources.Contains(source.Name)).ToList();
                if (defaultSources.Count == 0)
                {
                    throw new NuGetPackageInstallerException(string.Format(LocalizableStrings.FailedToMapSourceUnderPackageSourceMapping, packageId));
                }
            }

            if (packageSourceLocation?.AdditionalSourceFeed?.Any() ?? false)
            {
                foreach (string source in packageSourceLocation?.AdditionalSourceFeed)
                {
                    if (string.IsNullOrWhiteSpace(source))
                    {
                        continue;
                    }

                    PackageSource packageSource = new(source);
                    if (packageSource.TrySourceAsUri == null)
                    {
                        _verboseLogger.LogWarning(string.Format(
                            LocalizableStrings.FailedToLoadNuGetSource,
                            source));
                        continue;
                    }

                    if (defaultSources.Any(defaultSource => defaultSource.SourceUri == packageSource.SourceUri))
                    {
                        continue;
                    }

                    defaultSources.Add(packageSource);
                }
            }

            return defaultSources;
        }

        public IEnumerable<PackageSource> LoadNuGetSources(PackageId packageId, PackageSourceLocation packageSourceLocation = null, PackageSourceMapping packageSourceMapping = null)
        {
            var sources = (packageSourceLocation?.SourceFeedOverrides.Any() ?? false) ?
                LoadOverrideSources(packageSourceLocation) :
                LoadDefaultSources(packageId, packageSourceLocation, packageSourceMapping);

            if (!sources.Any())
            {
                throw new NuGetPackageInstallerException("No NuGet sources are defined or enabled");
            }

            return sources;
        }

        private async Task<(PackageSource, IPackageSearchMetadata)> GetMatchingVersionInternalAsync(
            string packageIdentifier, IEnumerable<PackageSource> packageSources, VersionRange versionRange,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(packageSources);

            if (string.IsNullOrWhiteSpace(packageIdentifier))
            {
                throw new ArgumentException($"{nameof(packageIdentifier)} cannot be null or empty",
                    nameof(packageIdentifier));
            }

            (PackageSource source, IEnumerable<IPackageSearchMetadata> foundPackages)[] foundPackagesBySource;

            if (_restoreActionConfig.DisableParallel)
            {
                foundPackagesBySource = packageSources.Select(source => GetPackageMetadataAsync(source,
                    packageIdentifier,
                    true, false, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult()).ToArray();
            }
            else
            {
                foundPackagesBySource =
                    await Task.WhenAll(
                            packageSources.Select(source => GetPackageMetadataAsync(source, packageIdentifier,
                                true, false, cancellationToken)))
                        .ConfigureAwait(false);
            }

            IEnumerable<(PackageSource source, IPackageSearchMetadata package)> accumulativeSearchResults =
                foundPackagesBySource
                    .SelectMany(result => result.foundPackages.Select(package => (result.source, package)));

            var availableVersions = accumulativeSearchResults.Select(t => t.package.Identity.Version).ToList();
            var bestVersion = versionRange.FindBestMatch(availableVersions);
            if (bestVersion != null)
            {
                var bestResult = accumulativeSearchResults.First(t => t.package.Identity.Version == bestVersion);
                return bestResult;
            }
            else
            {
                throw new NuGetPackageNotFoundException(
                    string.Format(
                        LocalizableStrings.IsNotFoundInNuGetFeeds,
                        GenerateVersionRangeErrorDescription(packageIdentifier, versionRange),
                        string.Join(", ", packageSources.Select(source => source.Source))));
            }
        }

        private static string GenerateVersionRangeErrorDescription(string packageIdentifier, VersionRange versionRange)
        {
            if (!string.IsNullOrEmpty(versionRange.OriginalString) && versionRange.OriginalString == "*")
            {
                return $"{packageIdentifier}";
            }
            else if (versionRange.HasLowerAndUpperBounds && versionRange.MinVersion == versionRange.MaxVersion)
            {
                return string.Format(LocalizableStrings.PackageVersionDescriptionForExactVersionMatch,
                    versionRange.MinVersion, packageIdentifier);
            }
            else if (versionRange.HasLowerAndUpperBounds)
            {
                return string.Format(LocalizableStrings.PackageVersionDescriptionForVersionWithLowerAndUpperBounds,
                    versionRange.MinVersion, versionRange.MaxVersion, packageIdentifier);
            }
            else if (versionRange.HasLowerBound)
            {
                return string.Format(LocalizableStrings.PackageVersionDescriptionForVersionWithLowerBound,
                    versionRange.MinVersion, packageIdentifier);
            }
            else if (versionRange.HasUpperBound)
            {
                return string.Format(LocalizableStrings.PackageVersionDescriptionForVersionWithUpperBound,
                    versionRange.MaxVersion, packageIdentifier);
            }

            // Default message if the format doesn't match any of the expected cases
            return string.Format(LocalizableStrings.PackageVersionDescriptionDefault, versionRange, packageIdentifier);
        }

        private async Task<(PackageSource, IPackageSearchMetadata)> GetLatestVersionInternalAsync(
        string packageIdentifier, IEnumerable<PackageSource> packageSources, bool includePreview,
        CancellationToken cancellationToken)
        {
            return (await GetLatestVersionsInternalAsync(packageIdentifier, packageSources, includePreview, cancellationToken, 1)).First();
        }

        private async Task<IEnumerable<(PackageSource, IPackageSearchMetadata)>> GetLatestVersionsInternalAsync(
            string packageIdentifier, IEnumerable<PackageSource> packageSources, bool includePreview, CancellationToken cancellationToken, int numberOfResults)
        {
            ArgumentNullException.ThrowIfNull(packageSources);
            if (string.IsNullOrWhiteSpace(packageIdentifier))
            {
                throw new ArgumentException($"{nameof(packageIdentifier)} cannot be null or empty",
                    nameof(packageIdentifier));
            }

            (PackageSource source, IEnumerable<IPackageSearchMetadata> foundPackages)[] foundPackagesBySource;

            if (_restoreActionConfig.DisableParallel)
            {
                foundPackagesBySource = packageSources.Select(source => GetPackageMetadataAsync(source,
                    packageIdentifier,
                    true, false, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult()).ToArray();
            }
            else
            {
                foundPackagesBySource =
                    await Task.WhenAll(
                            packageSources.Select(source => GetPackageMetadataAsync(source, packageIdentifier,
                                true, false, cancellationToken)))
                        .ConfigureAwait(false);
            }

            if (foundPackagesBySource.Length == 0)
            {
                throw new NuGetPackageNotFoundException(
                    string.Format(LocalizableStrings.IsNotFoundInNuGetFeeds, packageIdentifier, packageSources.Select(s => s.Source)));
            }

            IEnumerable<(PackageSource source, IPackageSearchMetadata package)> accumulativeSearchResults =
                foundPackagesBySource
                    .SelectMany(result => result.foundPackages.Select(package => (result.source, package)));

            if (!accumulativeSearchResults.Any())
            {
                throw new NuGetPackageNotFoundException(
                    string.Format(
                        LocalizableStrings.IsNotFoundInNuGetFeeds,
                        packageIdentifier,
                        string.Join(", ", packageSources.Select(source => source.Source))));
            }

            if (!includePreview)
            {
                var stableVersions = accumulativeSearchResults
                    .Where(r => !r.package.Identity.Version.IsPrerelease);

                if (stableVersions.Any())
                {
                    return stableVersions.OrderByDescending(r => r.package.Identity.Version).Take(numberOfResults);
                }
            }

            IEnumerable<(PackageSource, IPackageSearchMetadata)> latestVersions = accumulativeSearchResults
                .OrderByDescending(r => r.package.Identity.Version);
            return latestVersions.Take(numberOfResults);
        }

        public async Task<NuGetVersion> GetBestPackageVersionAsync(PackageId packageId,
            VersionRange versionRange,
             PackageSourceLocation packageSourceLocation = null)
        {
            if (versionRange.MinVersion != null && versionRange.MaxVersion != null && versionRange.MinVersion == versionRange.MaxVersion)
            {
                return versionRange.MinVersion;
            }

            CancellationToken cancellationToken = CancellationToken.None;
            IPackageSearchMetadata packageMetadata;

            IEnumerable<PackageSource> packagesSources = LoadNuGetSources(packageId, packageSourceLocation);
            (_, packageMetadata) = await GetMatchingVersionInternalAsync(packageId.ToString(), packagesSources,
                    versionRange, cancellationToken).ConfigureAwait(false);

            return packageMetadata.Identity.Version;
        }

        private async Task<(PackageSource, IPackageSearchMetadata)> GetPackageMetadataAsync(string packageIdentifier,
            NuGetVersion packageVersion, IEnumerable<PackageSource> sources, CancellationToken cancellationToken, bool includeUnlisted = false)
        {
            if (string.IsNullOrWhiteSpace(packageIdentifier))
            {
                throw new ArgumentException($"{nameof(packageIdentifier)} cannot be null or empty",
                    nameof(packageIdentifier));
            }

            ArgumentNullException.ThrowIfNull(packageVersion);
            ArgumentNullException.ThrowIfNull(sources);

            bool atLeastOneSourceValid = false;
            using CancellationTokenSource linkedCts =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            List<Task<(PackageSource source, IEnumerable<IPackageSearchMetadata> foundPackages)>> tasks = sources
                .Select(source =>
                    GetPackageMetadataAsync(source, packageIdentifier, true, includeUnlisted, linkedCts.Token)).ToList();

            bool TryGetPackageMetadata(
                (PackageSource source, IEnumerable<IPackageSearchMetadata> foundPackages) sourceAndFoundPackages,
                out (PackageSource, IPackageSearchMetadata) packageMetadataAsync)
            {
                packageMetadataAsync = default;
                if (sourceAndFoundPackages.foundPackages == null)
                {
                    return false;
                }

                atLeastOneSourceValid = true;
                IPackageSearchMetadata matchedVersion =
                    sourceAndFoundPackages.foundPackages.FirstOrDefault(package =>
                        package.Identity.Version == packageVersion);
                if (matchedVersion != null)
                {
                    linkedCts.Cancel();
                    {
                        packageMetadataAsync = (sourceAndFoundPackages.source, matchedVersion);
                        return true;
                    }
                }

                return false;
            }

            if (_restoreActionConfig.DisableParallel)
            {
                foreach (Task<(PackageSource source, IEnumerable<IPackageSearchMetadata> foundPackages)> task in tasks)
                {
                    var result = task.ConfigureAwait(false).GetAwaiter().GetResult();
                    if (TryGetPackageMetadata(result, out (PackageSource, IPackageSearchMetadata) packageMetadataAsync))
                    {
                        return packageMetadataAsync;
                    }
                }
            }
            else
            {
                while (tasks.Any())
                {
                    Task<(PackageSource source, IEnumerable<IPackageSearchMetadata> foundPackages)> finishedTask =
                        await Task.WhenAny(tasks).ConfigureAwait(false);
                    tasks.Remove(finishedTask);
                    (PackageSource source, IEnumerable<IPackageSearchMetadata> foundPackages) result =
                        await finishedTask.ConfigureAwait(false);
                    if (TryGetPackageMetadata(result, out (PackageSource, IPackageSearchMetadata) packageMetadataAsync))
                    {
                        return packageMetadataAsync;
                    }
                }
            }

            if (!atLeastOneSourceValid)
            {
                throw new NuGetPackageInstallerException(string.Format(LocalizableStrings.FailedToLoadNuGetSource,
                    string.Join(";", sources.Select(s => s.Source))));
            }

            throw new NuGetPackageNotFoundException(string.Format(LocalizableStrings.IsNotFoundInNuGetFeeds,
                                        GenerateVersionRangeErrorDescription(packageIdentifier, new VersionRange(minVersion: packageVersion, maxVersion: packageVersion, includeMaxVersion: true)),
                                        string.Join(";", sources.Select(s => s.Source))));
        }

        private async Task<(PackageSource source, IEnumerable<IPackageSearchMetadata> foundPackages)>
            GetPackageMetadataAsync(PackageSource source, string packageIdentifier, bool includePrerelease = false, bool includeUnlisted = false,
                CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packageIdentifier))
            {
                throw new ArgumentException($"{nameof(packageIdentifier)} cannot be null or empty",
                    nameof(packageIdentifier));
            }

            _ = source ?? throw new ArgumentNullException(nameof(source));

            IEnumerable<IPackageSearchMetadata> foundPackages;

            try
            {
                SourceRepository repository = GetSourceRepository(source);
                DefaultCredentialServiceUtility.SetupDefaultCredentialService(new NuGetConsoleLogger(), !_restoreActionConfig.Interactive);
                PackageMetadataResource resource = await repository
                    .GetResourceAsync<PackageMetadataResource>(cancellationToken).ConfigureAwait(false);

                foundPackages = await resource.GetMetadataAsync(
                    packageIdentifier,
                    includePrerelease,
                    includeUnlisted,
                    _cacheSettings,
                    _verboseLogger,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (FatalProtocolException e) when (_restoreActionConfig.IgnoreFailedSources)
            {
                _verboseLogger.LogWarning(e.ToString());
                foundPackages = Enumerable.Empty<PackageSearchMetadata>();
            }

            return (source, foundPackages);
        }

        public async Task<NuGetVersion> GetLatestPackageVersion(PackageId packageId,
             PackageSourceLocation packageSourceLocation = null,
             bool includePreview = false)
        {
            return (await GetLatestPackageVersions(packageId, numberOfResults: 1, packageSourceLocation, includePreview)).First();
        }

        public async Task<IEnumerable<NuGetVersion>> GetLatestPackageVersions(PackageId packageId, int numberOfResults, PackageSourceLocation packageSourceLocation = null, bool includePreview = false)
        {
            CancellationToken cancellationToken = CancellationToken.None;
            IEnumerable<PackageSource> packagesSources = LoadNuGetSources(packageId, packageSourceLocation);

            return (await GetLatestVersionsInternalAsync(packageId.ToString(), packagesSources,
                includePreview, cancellationToken, numberOfResults).ConfigureAwait(false)).Select(result =>
                result.Item2.Identity.Version);
        }

        public async Task<IEnumerable<string>> GetPackageIdsAsync(string idStem, bool allowPrerelease, PackageSourceLocation packageSourceLocation = null, CancellationToken cancellationToken = default)
        {
            // grab allowed sources for the package in question
            PackageId packageId = new(idStem);
            IEnumerable<PackageSource> packagesSources = LoadNuGetSources(packageId, packageSourceLocation);
            var autoCompletes = await Task.WhenAll(packagesSources.Select(async (source) => await GetAutocompleteAsync(source, cancellationToken).ConfigureAwait(false))).ConfigureAwait(false);
            // filter down to autocomplete endpoints (not all sources support this)
            var validAutoCompletes = autoCompletes.SelectMany(x => x);
            // get versions valid for this source
            var packageIdTasks = validAutoCompletes.Select(autocomplete => GetPackageIdsForSource(autocomplete, packageId, allowPrerelease, cancellationToken)).ToArray();
            var packageIdLists = await Task.WhenAll(packageIdTasks).ConfigureAwait(false);
            // sources may have the same versions, so we have to dedupe.
            return packageIdLists.SelectMany(v => v).Distinct().OrderDescending();
        }

        public async Task<IEnumerable<NuGetVersion>> GetPackageVersionsAsync(PackageId packageId, string versionPrefix = null, bool allowPrerelease = false, PackageSourceLocation packageSourceLocation = null, CancellationToken cancellationToken = default)
        {
            // grab allowed sources for the package in question
            IEnumerable<PackageSource> packagesSources = LoadNuGetSources(packageId, packageSourceLocation);
            var autoCompletes = await Task.WhenAll(packagesSources.Select(async (source) => await GetAutocompleteAsync(source, cancellationToken).ConfigureAwait(false))).ConfigureAwait(false);
            // filter down to autocomplete endpoints (not all sources support this)
            var validAutoCompletes = autoCompletes.SelectMany(x => x);
            // get versions valid for this source
            var versionTasks = validAutoCompletes.Select(autocomplete => GetPackageVersionsForSource(autocomplete, packageId, versionPrefix, allowPrerelease, cancellationToken)).ToArray();
            var versions = await Task.WhenAll(versionTasks).ConfigureAwait(false);
            // sources may have the same versions, so we have to dedupe.
            return versions.SelectMany(v => v).Distinct().OrderDescending();
        }

        private async Task<IEnumerable<AutoCompleteResource>> GetAutocompleteAsync(PackageSource source, CancellationToken cancellationToken)
        {
            SourceRepository repository = GetSourceRepository(source);
            if (await repository.GetResourceAsync<AutoCompleteResource>(cancellationToken).ConfigureAwait(false) is var resource)
            {
                return [resource];
            }
            else return Enumerable.Empty<AutoCompleteResource>();
        }

        // only exposed for testing
        internal static TimeSpan CliCompletionsTimeout { get; set; } = TimeSpan.FromMilliseconds(500);

        private async Task<IEnumerable<NuGetVersion>> GetPackageVersionsForSource(AutoCompleteResource autocomplete, PackageId packageId, string versionPrefix, bool allowPrerelease, CancellationToken cancellationToken)
        {
            try
            {
                var timeoutCts = new CancellationTokenSource(CliCompletionsTimeout);
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                // we use the NullLogger because we don't want to log to stdout for completions - they interfere with the completions mechanism of the shell program.
                return await autocomplete.VersionStartsWith(packageId.ToString(), versionPrefix: versionPrefix ?? "", includePrerelease: allowPrerelease, sourceCacheContext: _cacheSettings, log: NullLogger.Instance, token: linkedCts.Token);
            }
            catch (FatalProtocolException)  // this most often means that the source didn't actually have a SearchAutocompleteService
            {
                return Enumerable.Empty<NuGetVersion>();
            }
            catch (Exception) // any errors (i.e. auth) should just be ignored for completions
            {
                return Enumerable.Empty<NuGetVersion>();
            }
        }

        private static async Task<IEnumerable<string>> GetPackageIdsForSource(AutoCompleteResource autocomplete, PackageId packageId, bool allowPrerelease, CancellationToken cancellationToken)
        {
            try
            {
                var timeoutCts = new CancellationTokenSource(CliCompletionsTimeout);
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                // we use the NullLogger because we don't want to log to stdout for completions - they interfere with the completions mechanism of the shell program.
                return await autocomplete.IdStartsWith(packageId.ToString(), includePrerelease: allowPrerelease, log: NullLogger.Instance, token: linkedCts.Token);
            }
            catch (FatalProtocolException)  // this most often means that the source didn't actually have a SearchAutocompleteService
            {
                return Enumerable.Empty<string>();
            }
            catch (Exception) // any errors (i.e. auth) should just be ignored for completions
            {
                return Enumerable.Empty<string>();
            }
        }

        private SourceRepository GetSourceRepository(PackageSource source)
        {
            if (!_sourceRepositories.TryGetValue(source, out SourceRepository value))
            {
                value = Repository.Factory.GetCoreV3(source);
                _sourceRepositories.Add(source, value);
            }

            return value;
        }
    }
}
