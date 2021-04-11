// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.NuGetPackageDownloader
{
    internal class NuGetPackageDownloader : INuGetPackageDownloader
    {
        private readonly ILogger _logger;
        private readonly DirectoryPath _packageInstallDir;

        public NuGetPackageDownloader(DirectoryPath packageInstallDir, ILogger logger = null)
        {
            _packageInstallDir = packageInstallDir;
            _logger = logger ?? new NullLogger();
        }

        private readonly SourceCacheContext _cacheSettings = new SourceCacheContext()
        {
            NoCache = true, DirectDownload = true
        };

        public async Task<string> DownloadPackageAsync(
            PackageId packageId,
            NuGetVersion packageVersion,
            PackageSourceLocation packageSourceLocation = null)
        {
            var cancellationToken = CancellationToken.None;
            var cache = new SourceCacheContext() {DirectDownload = true, NoCache = true};

            IPackageSearchMetadata packageMetadata;

            IEnumerable<PackageSource> packagesSources = LoadNuGetSources(packageSourceLocation);
            PackageSource source;

            if (packageVersion is null)
            {
                (source, packageMetadata) = await GetLatestVersionInternalAsync(packageId.ToString(), packagesSources,
                    includePreview: false, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                packageVersion = new NuGetVersion(packageVersion);
                (source, packageMetadata) =
                    await GetPackageMetadataAsync(packageId.ToString(), packageVersion, packagesSources,
                        cancellationToken).ConfigureAwait(false);
            }

            FindPackageByIdResource resource = null;
            SourceRepository repository = Repository.Factory.GetCoreV3(source);

            try
            {
                resource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                //_nugetLogger.LogError(string.Format(LocalizableStrings.NuGetApiPackageManager_Error_FailedToLoadSource, source.Source));
                //_nugetLogger.LogDebug($"Details: {e.ToString()}.");
                //throw new NotImplementedException("Failed to load NuGet source", new[] { source.Source }, e);
            }

            if (resource == null)
            {
                throw new NotImplementedException();
            }

            var nupkgPath = Path.Combine(_packageInstallDir.Value, packageId.ToString(),
                packageVersion.ToNormalizedString(),
                $"{packageId}.{packageVersion.ToNormalizedString()}.nupkg");
            Directory.CreateDirectory(Path.GetDirectoryName(nupkgPath));
            using var destinationStream = File.Create(nupkgPath);
            var success = await resource.CopyNupkgToStreamAsync(
                id: packageId.ToString(),
                version: packageVersion,
                destination: destinationStream,
                cacheContext: cache,
                logger: _logger,
                cancellationToken: cancellationToken);

            if (!success)
            {
                throw new Exception($"Downloading {packageId} version {packageVersion.ToNormalizedString()} failed");
            }

            return nupkgPath;
        }

        public async Task<IEnumerable<string>> ExtractPackageAsync(string packagePath, string targetFolder)
        {
            await using var packageStream = File.OpenRead(packagePath);
            var packageReader = new PackageFolderReader(targetFolder);
            var packageExtractionContext = new PackageExtractionContext(
                PackageSaveMode.Defaultv3,
                XmlDocFileSaveMode.None,
                clientPolicyContext: null,
                logger: _logger);
            var packagePathResolver = new NuGetPackagePathResolver(targetFolder);
            var cancellationToken = CancellationToken.None;

            return await PackageExtractor.ExtractPackageAsync(
                source: targetFolder,
                packageStream: packageStream,
                packagePathResolver: packagePathResolver,
                packageExtractionContext: packageExtractionContext,
                token: cancellationToken);
        }

        private IEnumerable<PackageSource> LoadNuGetSources(PackageSourceLocation packageSourceLocation = null)
        {
            IEnumerable<PackageSource> defaultSources = new List<PackageSource>();
            string currentDirectory = string.Empty;
            try
            {
                currentDirectory = Directory.GetCurrentDirectory();
                ISettings settings;
                if (packageSourceLocation.NugetConfig.HasValue)
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

                PackageSourceProvider packageSourceProvider = new PackageSourceProvider(settings);
                defaultSources = packageSourceProvider.LoadPackageSources().Where(source => source.IsEnabled);
            }
            catch (Exception ex)
            {
                _logger.LogError(string.Format("Failed to load NuGet sources configured for the folder {0}",
                    currentDirectory));
                _logger.LogDebug($"Details: {ex.ToString()}.");
                // TODO error handling 
                //throw new InvalidNuGetSourceException($"Failed to load NuGet sources configured for the folder {currentDirectory}", ex);
            }

            if (!packageSourceLocation?.OverrideSourceFeeds.Any() ?? true)
            {
                if (!defaultSources.Any())
                {
                    // TODO error handling 
                    // _nugetLogger.LogError(LocalizableStrings.NuGetApiPackageManager_Error_NoSources);
                    throw new NuGetPackageInstallerException("No NuGet sources are defined or enabled");
                }

                return defaultSources;
            }

            List<PackageSource> customSources = new List<PackageSource>();
            foreach (string source in packageSourceLocation?.OverrideSourceFeeds)
            {
                if (string.IsNullOrWhiteSpace(source))
                {
                    continue;
                }

                PackageSource packageSource = new PackageSource(source);
                if (packageSource.TrySourceAsUri == null)
                {
                    //_nugetLogger.LogWarning(string.Format(LocalizableStrings.NuGetApiPackageManager_Warning_FailedToLoadSource, source));
                    continue;
                }

                customSources.Add(packageSource);
            }

            IEnumerable<PackageSource> retrievedSources;
            if (packageSourceLocation != null && packageSourceLocation.OverrideSourceFeeds.Any())
            {
                retrievedSources = customSources;
            }
            else
            {
                retrievedSources = defaultSources;
            }

            if (!retrievedSources.Any())
            {
                // _nugetLogger.LogError(LocalizableStrings.NuGetApiPackageManager_Error_NoSources);
                throw new NotImplementedException("No NuGet sources are defined or enabled");
            }

            return retrievedSources;
        }

        private async Task<(PackageSource, IPackageSearchMetadata)> GetLatestVersionInternalAsync(
            string packageIdentifier, IEnumerable<PackageSource> packageSources, bool includePreview,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(packageIdentifier))
            {
                throw new ArgumentException($"{nameof(packageIdentifier)} cannot be null or empty",
                    nameof(packageIdentifier));
            }

            _ = packageSources ?? throw new ArgumentNullException(nameof(packageSources));

            (PackageSource source, IEnumerable<IPackageSearchMetadata> foundPackages)[] foundPackagesBySource =
                await Task.WhenAll(
                        packageSources.Select(source => GetPackageMetadataAsync(source, packageIdentifier,
                            includePrerelease: true, cancellationToken)))
                    .ConfigureAwait(false);

            if (!foundPackagesBySource.Any())
            {
                //throw new NotImplementedException("Failed to load NuGet sources", packageSources.Select(source => source.Source));
            }

            var accumulativeSearchResults = foundPackagesBySource
                .SelectMany(result => result.foundPackages.Select(package => (result.source, package)));

            if (!accumulativeSearchResults.Any())
            {
                // _logger.LogWarning(
                //     string.Format(
                //         LocalizableStrings.NuGetApiPackageManager_Warning_PackageNotFound,
                //         packageIdentifier,
                //         string.Join(", ", packageSources.Select(source => source.Source))));
                //throw new NotImplementedException(packageIdentifier, packageSources.Select(source => source.Source));
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

        private async Task<(PackageSource, IPackageSearchMetadata)> GetPackageMetadataAsync(string packageIdentifier,
            NuGetVersion packageVersion, IEnumerable<PackageSource> sources, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(packageIdentifier))
            {
                throw new ArgumentException($"{nameof(packageIdentifier)} cannot be null or empty",
                    nameof(packageIdentifier));
            }

            _ = packageVersion ?? throw new ArgumentNullException(nameof(packageVersion));
            _ = sources ?? throw new ArgumentNullException(nameof(sources));

            bool atLeastOneSourceValid = false;
            using CancellationTokenSource linkedCts =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var tasks = sources.Select(source =>
                GetPackageMetadataAsync(source, packageIdentifier, includePrerelease: true, linkedCts.Token)).ToList();
            while (tasks.Any())
            {
                var finishedTask = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(finishedTask);
                (PackageSource source, IEnumerable<IPackageSearchMetadata> foundPackages) result =
                    await finishedTask.ConfigureAwait(false);
                if (result.foundPackages == null)
                {
                    continue;
                }

                atLeastOneSourceValid = true;
                IPackageSearchMetadata matchedVersion =
                    result.foundPackages.FirstOrDefault(package => package.Identity.Version == packageVersion);
                if (matchedVersion != null)
                {
                    _logger.LogDebug($"{packageIdentifier}::{packageVersion} was found in {result.source.Source}.");
                    linkedCts.Cancel();
                    return (result.source, matchedVersion);
                }
                else
                {
                    _logger.LogDebug(
                        $"{packageIdentifier}::{packageVersion} is not found in NuGet feed {result.source.Source}.");
                }
            }

            if (!atLeastOneSourceValid)
            {
                throw new NuGetPackageInstallerException(string.Format("Failed to load NuGet sources {0}",
                    string.Join(";", sources.Select(s => s.Source))));
            }

            // _logger.LogWarning(
            //     string.Format(
            //         LocalizableStrings.NuGetApiPackageManager_Warning_PackageNotFound,
            //         $"{packageIdentifier}::{packageVersion}",
            //         string.Join(", ", sources.Select(source => source.Source))));
            // throw new PackageNotFoundException(packageIdentifier, packageVersion, sources.Select(source => source.Source));

            throw new NuGetPackageInstallerException(string.Format("{0} is not found in NuGet feeds {1}",
                $"{packageIdentifier}::{packageVersion}", string.Join(";", sources.Select(s => s.Source))));
        }

        private async Task<(PackageSource source, IEnumerable<IPackageSearchMetadata> foundPackages)>
            GetPackageMetadataAsync(PackageSource source, string packageIdentifier, bool includePrerelease = false,
                CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packageIdentifier))
            {
                throw new ArgumentException($"{nameof(packageIdentifier)} cannot be null or empty",
                    nameof(packageIdentifier));
            }

            _ = source ?? throw new ArgumentNullException(nameof(source));

            _logger.LogDebug($"Searching for {packageIdentifier} in {source.Source}.");
            try
            {
                SourceRepository repository = Repository.Factory.GetCoreV3(source);
                PackageMetadataResource resource = await repository
                    .GetResourceAsync<PackageMetadataResource>(cancellationToken).ConfigureAwait(false);
                IEnumerable<IPackageSearchMetadata> foundPackages = await resource.GetMetadataAsync(
                    packageIdentifier,
                    includePrerelease: includePrerelease,
                    includeUnlisted: false,
                    _cacheSettings,
                    _logger,
                    cancellationToken).ConfigureAwait(false);

                if (foundPackages.Any())
                {
                    _logger.LogDebug(
                        $"Found {foundPackages.Count()} versions for {packageIdentifier} in NuGet feed {source.Source}.");
                }
                else
                {
                    _logger.LogDebug($"{packageIdentifier} is not found in NuGet feed {source.Source}.");
                }

                return (source, foundPackages);
            }
            catch (Exception ex)
            {
                // _logger.LogError(string.Format(LocalizableStrings.NuGetApiPackageManager_Error_FailedToReadPackage, source.Source));
                _logger.LogDebug($"Details: {ex.ToString()}.");
            }

            return (source, foundPackages: null);
        }
    }
}
