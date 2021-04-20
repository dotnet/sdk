// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Microsoft.TemplateEngine.Edge.Installers.NuGet
{
    internal class NuGetApiPackageManager : IDownloader, IUpdateChecker
    {
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
            _nugetLogger = new NuGetLogger(settings);
        }

        /// <summary>
        /// Downloads the package from configured NuGet package feeds. NuGet feeds to use are read for current directory, if additional feeds are specified in installation request, they are checked as well.
        /// </summary>
        /// <param name="downloadPath">path to download to.</param>
        /// <param name="identifier">NuGet package identifier.</param>
        /// <param name="version">The version to download. If empty, the latest stable version will be downloaded. If stable version is not availalbe, the latest preview will be downloaded.</param>
        /// <param name="additionalSources">Additional NuGet feeds to use (in addition to default feeds configured for current directory).</param>
        /// <param name="cancellationToken"></param>
        /// <returns><see cref="NuGetPackageInfo"/>containing full path to downloaded package and package details.</returns>
        /// <exception cref="InvalidNuGetSourceException">when sources passed to install request are not valid NuGet sources or failed to read default NuGet configuration.</exception>
        /// <exception cref="DownloadException">when the download of the package failed.</exception>
        /// <exception cref="PackageNotFoundException">when the package cannot be find in default or passed to install request NuGet feeds.</exception>
        public async Task<NuGetPackageInfo> DownloadPackageAsync(string downloadPath, string identifier, string version = null, IEnumerable<string> additionalSources = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException($"{nameof(identifier)} cannot be null or empty", nameof(identifier));
            }
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException($"{nameof(downloadPath)} cannot be null or empty", nameof(downloadPath));
            }

            IEnumerable<PackageSource> packagesSources = LoadNuGetSources(additionalSources?.ToArray() ?? Array.Empty<string>());

            NuGetVersion packageVersion;
            PackageSource source;
            IPackageSearchMetadata packageMetadata;

            if (string.IsNullOrWhiteSpace(version))
            {
                (source, packageMetadata) = await GetLatestVersionInternalAsync(identifier, packagesSources, includePreview: false, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                packageVersion = new NuGetVersion(version);
                (source, packageMetadata) = await GetPackageMetadataAsync(identifier, packageVersion, packagesSources, cancellationToken).ConfigureAwait(false);
            }

            FindPackageByIdResource resource;
            SourceRepository repository = Repository.Factory.GetCoreV3(source);
            try
            {
                resource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _nugetLogger.LogError(string.Format(LocalizableStrings.NuGetApiPackageManager_Error_FailedToLoadSource, source.Source));
                _nugetLogger.LogDebug($"Details: {e.ToString()}.");
                throw new InvalidNuGetSourceException("Failed to load NuGet source", new[] { source.Source }, e);
            }

            string filePath = Path.Combine(downloadPath, packageMetadata.Identity.Id + "." + packageMetadata.Identity.Version + ".nupkg");
            if (_environmentSettings.Host.FileSystem.FileExists(filePath))
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
                        filePath,
                        source.Source,
                        packageMetadata.Identity.Id,
                        packageMetadata.Identity.Version.ToNormalizedString()
                    );
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
                        _nugetLogger.LogDebug($"Details: {ex.ToString()}.");
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
                _nugetLogger.LogDebug($"Details: {e.ToString()}.");
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
                    _nugetLogger.LogDebug($"Details: {ex.ToString()}.");
                }
                throw new DownloadException(packageMetadata.Identity.Id, packageMetadata.Identity.Version.ToNormalizedString(), new[] { source.Source }, e.InnerException);
            }
        }

        /// <summary>
        /// Gets the latest stable version for the package. If the package has preview version installed, returns the latest preview.
        /// Uses NuGet feeds configured for current directory and the source if specified from <paramref name="additionalSource"/>.
        /// </summary>
        /// <param name="identifier">NuGet package identifier.<./param>
        /// <param name="version">current version of NuGet package.</param>
        /// <param name="additionalSource">additional NuGet feeds to check from.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>the latest version for the <paramref name="identifier"/> and indication if installed version is latest</returns>
        /// <exception cref="InvalidNuGetSourceException">when sources passed to install request are not valid NuGet feeds or failed to read default NuGet configuration</exception>
        /// <exception cref="PackageNotFoundException">when the package cannot be find in default or source NuGet feeds</exception>
        public async Task<(string latestVersion, bool isLatestVersion)> GetLatestVersionAsync(string identifier, string version = null, string additionalSource = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException($"{nameof(identifier)} cannot be null or empty", nameof(identifier));
            }

            //if preview version is installed, check for the latest preview version, otherwise for latest stable
            bool previewVersionInstalled = false;
            if (NuGetVersion.TryParse(version, out NuGetVersion currentVersion))
            {
                previewVersionInstalled = currentVersion.IsPrerelease;
            }

            IEnumerable<PackageSource> packageSources = LoadNuGetSources(additionalSource);
            var (_, package) = await GetLatestVersionInternalAsync(identifier, packageSources, previewVersionInstalled, cancellationToken).ConfigureAwait(false);
            bool isLatestVersion = currentVersion != null ? currentVersion >= package.Identity.Version : false;
            return (package.Identity.Version.ToNormalizedString(), isLatestVersion);
        }

        private async Task<(PackageSource, IPackageSearchMetadata)> GetLatestVersionInternalAsync(string packageIdentifier, IEnumerable<PackageSource> packageSources, bool includePreview, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(packageIdentifier))
            {
                throw new ArgumentException($"{nameof(packageIdentifier)} cannot be null or empty", nameof(packageIdentifier));
            }
            _ = packageSources ?? throw new ArgumentNullException(nameof(packageSources));

            (PackageSource source, IEnumerable<IPackageSearchMetadata> foundPackages)[] foundPackagesBySource =
                await Task.WhenAll(
                    packageSources.Select(source => GetPackageMetadataAsync(source, packageIdentifier, includePrerelease: true, cancellationToken)))
                          .ConfigureAwait(false);

            if (!foundPackagesBySource.Any())
            {
                throw new InvalidNuGetSourceException("Failed to load NuGet sources", packageSources.Select(source => source.Source));
            }

            var accumulativeSearchResults = foundPackagesBySource
                .SelectMany(result => result.foundPackages.Select(package => (result.source, package)));

            if (!accumulativeSearchResults.Any())
            {
                _nugetLogger.LogWarning(
                    string.Format(
                        LocalizableStrings.NuGetApiPackageManager_Warning_PackageNotFound,
                        packageIdentifier,
                        string.Join(", ", packageSources.Select(source => source.Source))));
                throw new PackageNotFoundException(packageIdentifier, packageSources.Select(source => source.Source));
            }

            if (!includePreview)
            {
                (PackageSource, IPackageSearchMetadata) latestStableVersion = accumulativeSearchResults.Aggregate(
                    (max, current) =>
                    {
                        if (current.package.Identity.Version.IsPrerelease) return max;
                        if (max == default) return current;
                        return current.package.Identity.Version > max.package.Identity.Version ? current : max;
                    });
                if (latestStableVersion != default)
                {
                    return latestStableVersion;
                }
            }

            (PackageSource, IPackageSearchMetadata) latestVersion = accumulativeSearchResults.Aggregate(
                (max, current) =>
                {
                    if (max == default) return current;
                    return current.package.Identity.Version > max.package.Identity.Version ? current : max;
                });
            return latestVersion;
        }

        private async Task<(PackageSource, IPackageSearchMetadata)> GetPackageMetadataAsync(string packageIdentifier, NuGetVersion packageVersion, IEnumerable<PackageSource> sources, CancellationToken cancellationToken)
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
            var tasks = sources.Select(source => GetPackageMetadataAsync(source, packageIdentifier, includePrerelease: true, linkedCts.Token)).ToList();
            while (tasks.Any())
            {
                var finishedTask = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(finishedTask);
                (PackageSource source, IEnumerable<IPackageSearchMetadata> foundPackages) result = await finishedTask.ConfigureAwait(false);
                if (result.foundPackages == null)
                {
                    continue;
                }
                atLeastOneSourceValid = true;
                IPackageSearchMetadata matchedVersion = result.foundPackages.FirstOrDefault(package => package.Identity.Version == packageVersion);
                if (matchedVersion != null)
                {
                    _nugetLogger.LogDebug($"{packageIdentifier}::{packageVersion} was found in {result.source.Source}.");
                    linkedCts.Cancel();
                    return (result.source, matchedVersion);
                }
                else
                {
                    _nugetLogger.LogDebug($"{packageIdentifier}::{packageVersion} is not found in NuGet feed {result.source.Source}.");
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


        private async Task<(PackageSource source, IEnumerable<IPackageSearchMetadata> foundPackages)> GetPackageMetadataAsync(PackageSource source, string packageIdentifier, bool includePrerelease = false, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packageIdentifier))
            {
                throw new ArgumentException($"{nameof(packageIdentifier)} cannot be null or empty", nameof(packageIdentifier));
            }
            _ = source ?? throw new ArgumentNullException(nameof(source));

            _nugetLogger.LogDebug($"Searching for {packageIdentifier} in {source.Source}.");
            try
            {
                SourceRepository repository = Repository.Factory.GetCoreV3(source);
                PackageMetadataResource resource = await repository.GetResourceAsync<PackageMetadataResource>(cancellationToken).ConfigureAwait(false);
                IEnumerable<IPackageSearchMetadata> foundPackages = await resource.GetMetadataAsync(
                    packageIdentifier,
                    includePrerelease: includePrerelease,
                    includeUnlisted: false,
                    _cacheSettings,
                    _nugetLogger,
                    cancellationToken).ConfigureAwait(false);

                if (foundPackages.Any())
                {
                    _nugetLogger.LogDebug($"Found {foundPackages.Count()} versions for {packageIdentifier} in NuGet feed {source.Source}.");
                }
                else
                {
                    _nugetLogger.LogDebug($"{packageIdentifier} is not found in NuGet feed {source.Source}.");
                }
                return (source, foundPackages);
            }
            catch (Exception ex)
            {
                _nugetLogger.LogError(string.Format(LocalizableStrings.NuGetApiPackageManager_Error_FailedToReadPackage, source.Source));
                _nugetLogger.LogDebug($"Details: {ex.ToString()}.");
            }
            return (source, foundPackages: null);
        }

        private IEnumerable<PackageSource> LoadNuGetSources(params string[] additionalSources)
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
                _nugetLogger.LogDebug($"Details: {ex.ToString()}.");
                throw new InvalidNuGetSourceException($"Failed to load NuGet sources configured for the folder {currentDirectory}", ex);
            }

            if (!additionalSources?.Any() ?? true)
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
    }
}
