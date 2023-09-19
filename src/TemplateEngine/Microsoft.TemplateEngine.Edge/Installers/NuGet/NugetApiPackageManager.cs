// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using ILogger = NuGet.Common.ILogger;

namespace Microsoft.TemplateEngine.Edge.Installers.NuGet
{
    internal class NuGetApiPackageManager : IDownloader, IUpdateChecker
    {
        private static readonly ConcurrentDictionary<PackageSource, SourceRepository> SourcesCache = new();
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly ILogger _nugetLogger;

        private readonly SourceCacheContext _cacheSettings = new SourceCacheContext()
        {
            NoCache = true,
            DirectDownload = true
        };

        internal NuGetApiPackageManager(IEngineEnvironmentSettings settings)
        {
            _environmentSettings = settings ?? throw new ArgumentNullException(nameof(settings));
            _nugetLogger = new NuGetLogger(_environmentSettings.Host.LoggerFactory);
        }

        /// <summary>
        /// Downloads the package from configured NuGet package feeds. NuGet feeds to use are read for current directory, if additional feeds are specified in installation request, they are checked as well.
        /// </summary>
        /// <param name="downloadPath">path to download to.</param>
        /// <param name="identifier">NuGet package identifier.</param>
        /// <param name="version">The version to download. If empty, the latest stable version will be downloaded. If stable version is not available, the latest preview will be downloaded.</param>
        /// <param name="additionalSources">Additional NuGet feeds to use (in addition to default feeds configured for current directory).</param>
        /// <param name="force">If true, overwriting existing package is allowed.</param>
        /// <param name="cancellationToken"></param>
        /// <returns><see cref="NuGetPackageInfo"/>containing full path to downloaded package and package details.</returns>
        /// <exception cref="InvalidNuGetSourceException">when sources passed to install request are not valid NuGet sources or failed to read default NuGet configuration.</exception>
        /// <exception cref="DownloadException">when the download of the package failed.</exception>
        /// <exception cref="PackageNotFoundException">when the package cannot be find in default or passed to install request NuGet feeds.</exception>
        /// <exception cref="VulnerablePackageException">when the package has any vulnerabilities.</exception>
        public async Task<NuGetPackageInfo> DownloadPackageAsync(string downloadPath, string identifier, string? version = null, IEnumerable<string>? additionalSources = null, bool force = false, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException($"{nameof(identifier)} cannot be null or empty", nameof(identifier));
            }
            if (string.IsNullOrWhiteSpace(downloadPath))
            {
                throw new ArgumentException($"{nameof(downloadPath)} cannot be null or empty", nameof(downloadPath));
            }

            IEnumerable<PackageSource> packagesSources = LoadNuGetSources(additionalSources?.ToArray() ?? Array.Empty<string>());

            if (!force)
            {
                packagesSources = RemoveInsecurePackages(packagesSources);
            }

            PackageSource source;
            NugetPackageMetadata packageMetadata;

            if (NuGetVersionHelper.TryParseFloatRangeEx(version, out FloatRange floatRange))
            {
                (source, packageMetadata) =
                    await GetLatestVersionInternalAsync(
                        identifier,
                        packagesSources,
                        floatRange,
                        cancellationToken)
                        .ConfigureAwait(false);
            }
            else
            {
                NuGetVersion packageVersion = new NuGetVersion(version!);
                (source, packageMetadata) = await GetPackageMetadataAsync(identifier, packageVersion, packagesSources, cancellationToken).ConfigureAwait(false);
            }

            if (packageMetadata.Vulnerabilities.Any() && !force)
            {
                var foundPackageVersion = packageMetadata.Identity.Version.OriginalVersion;
                throw new VulnerablePackageException(
                    string.Format(LocalizableStrings.NuGetApiPackageManager_DownloadError_VulnerablePackage, source),
                    packageMetadata.Identity.Id,
                    foundPackageVersion!,
                    packageMetadata.Vulnerabilities);
            }

            FindPackageByIdResource resource;
            SourceRepository repository = SourcesCache.GetOrAdd(source, Repository.Factory.GetCoreV3(source));
            try
            {
                resource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _nugetLogger.LogError(string.Format(LocalizableStrings.NuGetApiPackageManager_Error_FailedToLoadSource, source.Source));
                _nugetLogger.LogDebug($"Details: {e}.");
                throw new InvalidNuGetSourceException("Failed to load NuGet source", new[] { source.Source }, e);
            }

            string filePath = Path.Combine(downloadPath, packageMetadata.Identity.Id + "." + packageMetadata.Identity.Version + ".nupkg");
            if (!force && _environmentSettings.Host.FileSystem.FileExists(filePath))
            {
                _nugetLogger.LogError(string.Format(LocalizableStrings.NuGetApiPackageManager_Error_FileAlreadyExists, filePath));
                throw new DownloadException(packageMetadata.Identity.Id, packageMetadata.Identity.Version.ToNormalizedString(), new[] { source.Source });
            }
            try
            {
                using Stream packageStream = _environmentSettings.Host.FileSystem.CreateFile(filePath);
                if (await resource.CopyNupkgToStreamAsync(
                    packageMetadata.Identity.Id,
                    packageMetadata.Identity.Version,
                    packageStream,
                    _cacheSettings,
                    _nugetLogger,
                    cancellationToken).ConfigureAwait(false))
                {
                    return new NuGetPackageInfo(
                        packageMetadata.Authors,
                        packageMetadata.Owners,
                        reserved: packageMetadata.PrefixReserved,
                        filePath,
                        source.Source,
                        packageMetadata.Identity.Id,
                        packageMetadata.Identity.Version.ToNormalizedString(),
                        packageMetadata.Vulnerabilities);
                }
                else
                {
                    _nugetLogger.LogWarning(
                        string.Format(
                            LocalizableStrings.NuGetApiPackageManager_Warning_FailedToDownload,
                            $"{packageMetadata.Identity.Id}::{packageMetadata.Identity.Version}",
                            source.Source));
                    try
                    {
                        _environmentSettings.Host.FileSystem.FileDelete(filePath);
                    }
                    catch (Exception ex)
                    {
                        _nugetLogger.LogWarning(
                            string.Format(
                                LocalizableStrings.NuGetApiPackageManager_Warning_FailedToDelete,
                                filePath));
                        _nugetLogger.LogDebug($"Details: {ex}.");
                    }
                    throw new DownloadException(packageMetadata.Identity.Id, packageMetadata.Identity.Version.ToNormalizedString(), new[] { source.Source });
                }
            }
            catch (Exception e)
            {
                _nugetLogger.LogWarning(
                    string.Format(
                        LocalizableStrings.NuGetApiPackageManager_Warning_FailedToDownload,
                        $"{packageMetadata.Identity.Id}::{packageMetadata.Identity.Version}",
                        source.Source));
                _nugetLogger.LogDebug($"Details: {e}.");
                try
                {
                    _environmentSettings.Host.FileSystem.FileDelete(filePath);
                }
                catch (Exception ex)
                {
                    _nugetLogger.LogWarning(
                        string.Format(
                            LocalizableStrings.NuGetApiPackageManager_Warning_FailedToDelete,
                            filePath));
                    _nugetLogger.LogDebug($"Details: {ex}.");
                }
                throw new DownloadException(packageMetadata.Identity.Id, packageMetadata.Identity.Version.ToNormalizedString(), new[] { source.Source }, e.InnerException);
            }
        }

        /// <summary>
        /// Gets the latest stable version for the package. If the package has preview version installed, returns the latest preview.
        /// Uses NuGet feeds configured for current directory and the source if specified from <paramref name="additionalSource"/>.
        /// </summary>
        /// <param name="identifier">NuGet package identifier.</param>
        /// <param name="version">current version of NuGet package.</param>
        /// <param name="additionalSource">additional NuGet feeds to check from.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>the latest version for the <paramref name="identifier"/> and indication if installed version is latest.</returns>
        /// <exception cref="InvalidNuGetSourceException">when sources passed to install request are not valid NuGet feeds or failed to read default NuGet configuration.</exception>
        /// <exception cref="PackageNotFoundException">when the package cannot be find in default or source NuGet feeds.</exception>
        public async Task<(string LatestVersion, bool IsLatestVersion, NugetPackageMetadata PackageMetadata)> GetLatestVersionAsync(string identifier, string? version = null, string? additionalSource = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException($"{nameof(identifier)} cannot be null or empty", nameof(identifier));
            }

            //if preview version is installed, check for the latest preview version, otherwise for latest stable
            bool previewVersionInstalled = false;
            if (NuGetVersion.TryParse(version, out NuGetVersion? currentVersion))
            {
                previewVersionInstalled = currentVersion!.IsPrerelease;
            }

            FloatRange floatRange = new FloatRange(previewVersionInstalled ? NuGetVersionFloatBehavior.AbsoluteLatest : NuGetVersionFloatBehavior.Major);

            string[] additionalSources = string.IsNullOrWhiteSpace(additionalSource) ? Array.Empty<string>() : new[] { additionalSource! };
            IEnumerable<PackageSource> packageSources = LoadNuGetSources(additionalSources);
            var (_, package) = await GetLatestVersionInternalAsync(identifier, packageSources, floatRange, cancellationToken).ConfigureAwait(false);
            bool isLatestVersion = currentVersion != null && currentVersion >= package.Identity.Version;

            return (package.Identity.Version.ToNormalizedString(), isLatestVersion, package);
        }

        internal IEnumerable<PackageSource> RemoveInsecurePackages(IEnumerable<PackageSource> packagesSources)
        {
            var insecurePackages = new List<PackageSource>();
            var securePackages = new List<PackageSource>();
            foreach (var packageSource in packagesSources)
            {
                // NuGet IsHttp property can be both http and https sources
                if (packageSource.IsHttp && !packageSource.IsHttps)
                {
                    insecurePackages.Add(packageSource);
                }
                else
                {
                    securePackages.Add(packageSource);
                }
            }

            if (insecurePackages.Any())
            {
                var packagesString = string.Join(", ", insecurePackages.Select(package => package.Source));
                _nugetLogger.LogWarning(string.Format(LocalizableStrings.NuGetApiPackageManager_Warning_InsecureFeed, packagesString));
            }

            return securePackages;
        }

        private async Task<(PackageSource, NugetPackageMetadata)> GetLatestVersionInternalAsync(
            string packageIdentifier,
            IEnumerable<PackageSource> packageSources,
            FloatRange floatRange,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(packageIdentifier))
            {
                throw new ArgumentException($"{nameof(packageIdentifier)} cannot be null or empty", nameof(packageIdentifier));
            }
            _ = packageSources ?? throw new ArgumentNullException(nameof(packageSources));

            (PackageSource Source, IEnumerable<NugetPackageMetadata>? FoundPackages)[] foundPackagesBySource =
                await Task.WhenAll(
                    packageSources.Select(source => GetPackageMetadataAsync(source, packageIdentifier, includePrerelease: true, cancellationToken)))
                          .ConfigureAwait(false);

            if (!foundPackagesBySource.Any(result => result.FoundPackages != null))
            {
                throw new InvalidNuGetSourceException("Failed to load NuGet sources", packageSources.Select(source => source.Source));
            }

            var accumulativeSearchResults = foundPackagesBySource
                .Where(result => result.FoundPackages != null)
                .SelectMany(result => result.FoundPackages.Select(package => (result.Source, package)));

            (PackageSource, NugetPackageMetadata)? latestVersion = accumulativeSearchResults.Aggregate(
                ((PackageSource, NugetPackageMetadata)?)null,
                (max, current) =>
                {
                    return
                        (max == null || current.package.Identity.Version > max.Value.Item2.Identity.Version)
                        &&
                        floatRange.Satisfies(current.package.Identity.Version) ?
                            current : max;
                });

            // In case no package was found and we haven't been restricting versions - try prerelease as well (so behave like '*-*')
            if (latestVersion == null && floatRange.IsUnrestricted())
            {
                latestVersion = accumulativeSearchResults.Aggregate(
                    ((PackageSource, NugetPackageMetadata)?)null,
                    (max, current) =>
                    {
                        return
                            (max == null || current.package.Identity.Version > max.Value.Item2.Identity.Version)
                                ? current
                                : max;
                    });
            }

            if (latestVersion == null)
            {
                _nugetLogger.LogDebug(
                    string.Format(
                        LocalizableStrings.NuGetApiPackageManager_Warning_PackageNotFound,
                        packageIdentifier,
                        string.Join(", ", packageSources.Select(source => source.Source))));
                throw new PackageNotFoundException(packageIdentifier, packageSources.Select(source => source.Source));
            }

            return latestVersion.Value;
        }

        private async Task<(PackageSource, NugetPackageMetadata)> GetPackageMetadataAsync(
            string packageIdentifier,
            NuGetVersion packageVersion,
            IEnumerable<PackageSource> sources,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(packageIdentifier))
            {
                throw new ArgumentException($"{nameof(packageIdentifier)} cannot be null or empty", nameof(packageIdentifier));
            }
            _ = packageVersion ?? throw new ArgumentNullException(nameof(packageVersion));
            _ = sources ?? throw new ArgumentNullException(nameof(sources));

            bool atLeastOneSourceValid = false;
            using CancellationTokenSource linkedCts =
                      CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            List<Task<(PackageSource Source, IEnumerable<NugetPackageMetadata>? FoundPackages)>> tasks =
                sources.Select(source => GetPackageMetadataAsync(source, packageIdentifier, includePrerelease: true, linkedCts.Token)).ToList();
            while (tasks.Any())
            {
                Task<(PackageSource Source, IEnumerable<NugetPackageMetadata>? FoundPackages)> finishedTask =
                    await Task.WhenAny(tasks).ConfigureAwait(false);
                _ = tasks.Remove(finishedTask);
                (PackageSource foundSource, IEnumerable<NugetPackageMetadata>? foundPackages) = await finishedTask.ConfigureAwait(false);
                if (foundPackages == null)
                {
                    continue;
                }
                atLeastOneSourceValid = true;
                NugetPackageMetadata matchedVersion = foundPackages.FirstOrDefault(package => package.Identity.Version == packageVersion);
                if (matchedVersion != null)
                {
                    _nugetLogger.LogDebug($"{packageIdentifier}::{packageVersion} was found in {foundSource.Source}.");
                    linkedCts.Cancel();
                    return (foundSource, matchedVersion);
                }
                else
                {
                    _nugetLogger.LogDebug($"{packageIdentifier}::{packageVersion} is not found in NuGet feed {foundSource.Source}.");
                }
            }
            if (!atLeastOneSourceValid)
            {
                throw new InvalidNuGetSourceException("Failed to load NuGet sources", sources.Select(s => s.Source));
            }
            _nugetLogger.LogWarning(
                string.Format(
                    LocalizableStrings.NuGetApiPackageManager_Warning_PackageNotFound,
                    $"{packageIdentifier}::{packageVersion}",
                    string.Join(", ", sources.Select(source => source.Source))));
            throw new PackageNotFoundException(packageIdentifier, packageVersion, sources.Select(source => source.Source));
        }

        private async Task<(PackageSource Source, IEnumerable<NugetPackageMetadata>? FoundPackages)> GetPackageMetadataAsync(
            PackageSource source,
            string packageIdentifier,
            bool includePrerelease = false,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packageIdentifier))
            {
                throw new ArgumentException($"{nameof(packageIdentifier)} cannot be null or empty", nameof(packageIdentifier));
            }
            _ = source ?? throw new ArgumentNullException(nameof(source));

            _nugetLogger.LogDebug($"Searching for {packageIdentifier} in {source.Source}.");
            try
            {
                SourceRepository repository = SourcesCache.GetOrAdd(source, Repository.Factory.GetCoreV3(source));
                PackageMetadataResource resource = await repository.GetResourceAsync<PackageMetadataResource>(cancellationToken).ConfigureAwait(false);
                IEnumerable<IPackageSearchMetadata> packageMetadata = await resource.GetMetadataAsync(
                    packageIdentifier,
                    includePrerelease: includePrerelease,
                    includeUnlisted: false,
                    _cacheSettings,
                    _nugetLogger,
                    cancellationToken).ConfigureAwait(false);

                if (packageMetadata.Any())
                {
                    _nugetLogger.LogDebug($"Found {packageMetadata.Count()} versions for {packageIdentifier} in NuGet feed {source.Source}.");

                    // extra call is needed because GetMetadataAsync call doesn't include owners and prefixVerified info
                    // https://github.com/NuGet/NuGetGallery/issues/5647
                    var (owners, verified) = await GetPackageAdditionalMetadata(
                         repository,
                         packageIdentifier,
                         includePrerelease,
                         cancellationToken).ConfigureAwait(false);

                    return (source, packageMetadata.Select(pm => new NugetPackageMetadata(pm, owners, verified)));
                }
                else
                {
                    _nugetLogger.LogDebug($"{packageIdentifier} is not found in NuGet feed {source.Source}.");
                }

                return (source, Enumerable.Empty<NugetPackageMetadata>());
            }
            catch (TaskCanceledException)
            {
                //do nothing
                //GetMetadataAsync may cancel the task in case package is found in another feed.
            }
            catch (Exception ex)
            {
                _nugetLogger.LogDebug(string.Format(LocalizableStrings.NuGetApiPackageManager_Error_FailedToReadPackage, source.Source));
                _nugetLogger.LogDebug($"Details: {ex}.");
            }
            return (source, FoundPackages: null);
        }

        private async Task<(string Owners, bool Verified)> GetPackageAdditionalMetadata(
            SourceRepository repository,
            string packageIdentifier,
            bool includePrerelease,
            CancellationToken cancellationToken)
        {
            var nugetSearchClient = await repository.GetResourceAsync<PackageSearchResource>(cancellationToken).ConfigureAwait(false);

            var searchResult = (await nugetSearchClient.SearchAsync(
                packageIdentifier,
                new SearchFilter(includePrerelease),
                skip: 0,
                take: 1,
                _nugetLogger,
                cancellationToken).ConfigureAwait(false)).FirstOrDefault();

            return (searchResult.Owners ?? string.Empty, searchResult.PrefixReserved);
        }

        private IEnumerable<PackageSource> LoadNuGetSources(IEnumerable<string> additionalSources)
        {
            IEnumerable<PackageSource> defaultSources;
            string currentDirectory = string.Empty;
            try
            {
                currentDirectory = Directory.GetCurrentDirectory();
                ISettings settings = global::NuGet.Configuration.Settings.LoadDefaultSettings(currentDirectory);
                PackageSourceProvider packageSourceProvider = new PackageSourceProvider(settings);
                defaultSources = packageSourceProvider.LoadPackageSources().Where(source => source.IsEnabled);
            }
            catch (Exception ex)
            {
                _nugetLogger.LogError(string.Format(LocalizableStrings.NuGetApiPackageManager_Error_FailedToLoadSources, currentDirectory));
                _nugetLogger.LogDebug($"Details: {ex}.");
                throw new InvalidNuGetSourceException($"Failed to load NuGet sources configured for the folder {currentDirectory}", ex);
            }

            if (!additionalSources.Any())
            {
                if (!defaultSources.Any())
                {
                    _nugetLogger.LogError(LocalizableStrings.NuGetApiPackageManager_Error_NoSources);
                    throw new InvalidNuGetSourceException("No NuGet sources are defined or enabled");
                }
                return defaultSources;
            }

            List<PackageSource> customSources = new List<PackageSource>();
            foreach (string source in additionalSources)
            {
                if (string.IsNullOrWhiteSpace(source))
                {
                    continue;
                }
                if (defaultSources.Any(s => s.Source.Equals(source, StringComparison.OrdinalIgnoreCase)))
                {
                    _nugetLogger.LogDebug($"Custom source {source} is already loaded from default configuration.");
                    continue;
                }
                PackageSource packageSource = new PackageSource(source);
                if (packageSource.TrySourceAsUri == null)
                {
                    _nugetLogger.LogWarning(string.Format(LocalizableStrings.NuGetApiPackageManager_Warning_FailedToLoadSource, source));
                    continue;
                }
                customSources.Add(packageSource);
            }

            IEnumerable<PackageSource> retrievedSources = customSources.Concat(defaultSources);
            if (!retrievedSources.Any())
            {
                _nugetLogger.LogError(LocalizableStrings.NuGetApiPackageManager_Error_NoSources);
                throw new InvalidNuGetSourceException("No NuGet sources are defined or enabled");
            }
            return retrievedSources;
        }

        internal class NugetPackageMetadata
        {
            public NugetPackageMetadata(IPackageSearchMetadata metadata, string owners, bool reserved)
            {
                Authors = metadata.Authors;
                Identity = metadata.Identity;
                PrefixReserved = reserved;
                Owners = owners;
                Vulnerabilities = ConvertVulnerabilityMetadata(metadata.Vulnerabilities);
            }

            public string Authors { get; }

            public PackageIdentity Identity { get; }

            public string Owners { get; }

            public bool PrefixReserved { get; }

            public IReadOnlyList<VulnerabilityInfo> Vulnerabilities { get; }

            private IReadOnlyList<VulnerabilityInfo> ConvertVulnerabilityMetadata(IEnumerable<PackageVulnerabilityMetadata>? vulnerabilities)
            {
                if (vulnerabilities is null)
                {
                    return Array.Empty<VulnerabilityInfo>();
                }

                return vulnerabilities.GroupBy(x => x.Severity)
                    .Select(g => new VulnerabilityInfo(
                        g.Key,
                        g.Select(x => x.AdvisoryUrl.AbsoluteUri).ToArray()))
                    .OrderBy(x => x.Severity)
                    .ToList();
            }
        }
    }
}
