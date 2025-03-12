// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Edge.BuiltInManagedProvider;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    /// <summary>
    /// Manages all <see cref="ITemplatePackageProvider"/>s available to the host.
    /// Use this class to get all template packages and templates installed.
    /// </summary>
    public class TemplatePackageManager : IDisposable
    {
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly SettingsFilePaths _paths;
        private readonly ILogger _logger;
        private readonly Scanner _installScanner;
        private volatile TemplateCache? _userTemplateCache;
        private Dictionary<ITemplatePackageProvider, Task<IReadOnlyList<ITemplatePackage>>>? _cachedSources;

        /// <summary>
        /// Creates the instance.
        /// </summary>
        /// <param name="environmentSettings">template engine environment settings.</param>
        public TemplatePackageManager(IEngineEnvironmentSettings environmentSettings)
        {
            _environmentSettings = environmentSettings;
            _logger = environmentSettings.Host.LoggerFactory.CreateLogger<TemplatePackageManager>();
            _paths = new SettingsFilePaths(environmentSettings);
            _installScanner = new Scanner(environmentSettings);
        }

        /// <summary>
        /// Triggered every time when the list of <see cref="ITemplatePackage"/>s changes, this is triggered by <see cref="ITemplatePackageProvider.TemplatePackagesChanged"/>.
        /// </summary>
        public event Action? TemplatePackagesChanged;

        /// <summary>
        /// Returns <see cref="IManagedTemplatePackageProvider"/> with specified name.
        /// </summary>
        /// <param name="name">Name from <see cref="ITemplatePackageProviderFactory.DisplayName"/>.</param>
        /// <returns></returns>
        /// <remarks>For default built-in providers use <see cref="GetBuiltInManagedProvider"/> method instead.</remarks>
        public IManagedTemplatePackageProvider GetManagedProvider(string name)
        {
            EnsureProvidersLoaded();
            return _cachedSources!.Keys.OfType<IManagedTemplatePackageProvider>().FirstOrDefault(p => p.Factory.DisplayName == name);
        }

        /// <summary>
        /// Returns <see cref="IManagedTemplatePackageProvider"/> with specified <see cref="Guid"/>.
        /// </summary>
        /// <param name="id"><see cref="Guid"/> from <see cref="IIdentifiedComponent.Id"/> of <see cref="ITemplatePackageProviderFactory"/>.</param>
        /// <returns></returns>
        /// <remarks>For default built-in providers use <see cref="GetBuiltInManagedProvider"/> method instead.</remarks>
        public IManagedTemplatePackageProvider GetManagedProvider(Guid id)
        {
            EnsureProvidersLoaded();
            return _cachedSources!.Keys.OfType<IManagedTemplatePackageProvider>().FirstOrDefault(p => p.Factory.Id == id);
        }

        /// <summary>
        /// Same as <see cref="GetTemplatePackagesAsync"/> but filters only <see cref="IManagedTemplatePackage"/> packages.
        /// </summary>
        /// <param name="force">Useful when <see cref="IManagedTemplatePackage"/> doesn't trigger <see cref="ITemplatePackageProvider.TemplatePackagesChanged"/> event.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the asynchronous operation.</param>
        /// <returns>The list of <see cref="IManagedTemplatePackage"/>.</returns>
        public async Task<IReadOnlyList<IManagedTemplatePackage>> GetManagedTemplatePackagesAsync(bool force, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureProvidersLoaded();
            return (await GetTemplatePackagesAsync(force, cancellationToken).ConfigureAwait(false)).OfType<IManagedTemplatePackage>().ToList();
        }

        /// <summary>
        /// Returns combined list of <see cref="ITemplatePackage"/>s that all <see cref="ITemplatePackageProvider"/>s and <see cref="IManagedTemplatePackageProvider"/>s return.
        /// <see cref="TemplatePackageManager"/> caches the responses from <see cref="ITemplatePackageProvider"/>s, to get non-cached response <paramref name="force"/> should be set to true.
        /// Note that specifying <paramref name="force"/> will only return responses from already loaded providers. To reload providers, instantiate new instance of the <see cref="TemplatePackageManager"/>.
        /// </summary>
        /// <param name="force">Useful when <see cref="ITemplatePackageProvider"/> doesn't trigger <see cref="ITemplatePackageProvider.TemplatePackagesChanged"/> event.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the asynchronous operation.</param>
        /// <returns>The list of <see cref="ITemplatePackage"/>s.</returns>
        public async Task<IReadOnlyList<ITemplatePackage>> GetTemplatePackagesAsync(bool force, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureProvidersLoaded();
            if (force)
            {
                foreach (var provider in _cachedSources!.Keys.ToList())
                {
                    _cachedSources[provider] = Task.Run(() => provider.GetAllTemplatePackagesAsync(default));
                }
            }

            var sources = new List<ITemplatePackage>();
            foreach (KeyValuePair<ITemplatePackageProvider, Task<IReadOnlyList<ITemplatePackage>>> source in _cachedSources.OrderBy((p) => (p.Key.Factory as IPrioritizedComponent)?.Priority ?? 0))
            {
                try
                {
                    sources.AddRange(await source.Value.ConfigureAwait(false));
                }
                catch (Exception ex)
                {
                    _logger.LogError(LocalizableStrings.TemplatePackageManager_Error_FailedToGetTemplatePackages, source.Key.Factory.DisplayName, ex.Message);
                    _logger.LogDebug("Details: {0}", ex);
                }
            }

            return sources;
        }

        public void Dispose()
        {
            if (_cachedSources == null)
            {
                return;
            }
            foreach (var provider in _cachedSources.Keys.OfType<IDisposable>())
            {
                provider.Dispose();
            }
        }

        /// <summary>
        /// Returns built-in <see cref="IManagedTemplatePackageProvider"/> of specified <see cref="InstallationScope"/>.
        /// </summary>
        /// <param name="scope">scope managed by built-in provider.</param>
        /// <returns><see cref="IManagedTemplatePackageProvider"/> which manages packages of <paramref name="scope"/>.</returns>
        public IManagedTemplatePackageProvider GetBuiltInManagedProvider(InstallationScope scope = InstallationScope.Global)
        {
            switch (scope)
            {
                case InstallationScope.Global:
                    return GetManagedProvider(GlobalSettingsTemplatePackageProviderFactory.FactoryId);
                default:
                    break;
            }
            return GetManagedProvider(GlobalSettingsTemplatePackageProviderFactory.FactoryId);
        }

        /// <summary>
        /// Gets all templates based on current settings.
        /// </summary>
        /// <remarks>
        /// This call is cached. And can be invalidated by <see cref="RebuildTemplateCacheAsync"/>.
        /// </remarks>
        public async Task<IReadOnlyList<ITemplateInfo>> GetTemplatesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var userTemplateCache = await UpdateTemplateCacheAsync(false, cancellationToken).ConfigureAwait(false);
            return userTemplateCache.TemplateInfo;
        }

        /// <summary>
        /// Gets the templates filtered using <paramref name="filters"/> and <paramref name="matchFilter"/>.
        /// </summary>
        /// <param name="matchFilter">The criteria for <see cref="ITemplateMatchInfo"/> to be included to result collection.</param>
        /// <param name="filters">The list of filters to be applied to templates.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the asynchronous operation.</param>
        /// <returns>The filtered list of templates with match information.</returns>
        /// <example>
        /// <c>GetTemplatesAsync(WellKnownSearchFilters.MatchesAllCriteria, new [] { WellKnownSearchFilters.NameFilter("myname") }</c> - returns the templates which name or short name contains "myname". <br/>
        /// <c>GetTemplatesAsync(TemplateListFilter.MatchesAtLeastOneCriteria, new [] { WellKnownSearchFilters.NameFilter("myname"), WellKnownSearchFilters.NameFilter("othername") })</c> - returns the templates which name or short name contains "myname" or "othername".<br/>
        /// </example>
        public async Task<IReadOnlyList<ITemplateMatchInfo>> GetTemplatesAsync(Func<ITemplateMatchInfo, bool> matchFilter, IEnumerable<Func<ITemplateInfo, MatchInfo?>> filters, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<ITemplateInfo> templates = await GetTemplatesAsync(cancellationToken).ConfigureAwait(false);
            //TemplateListFilter.GetTemplateMatchInfo code should be moved to this method eventually, when no longer needed.
#pragma warning disable CS0618 // Type or member is obsolete.
            return TemplateListFilter.GetTemplateMatchInfo(templates, matchFilter, filters.ToArray()).ToList();
#pragma warning restore CS0618 // Type or member is obsolete
        }

        /// <summary>
        /// Deletes templates cache and rebuilds it.
        /// Useful if user suspects cache is corrupted and wants to rebuild it.
        /// </summary>
        public Task RebuildTemplateCacheAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return UpdateTemplateCacheAsync(true, token);
        }

        /// <summary>
        /// Helper method that returns <see cref="ITemplatePackage"/> that contains <paramref name="template"/>.
        /// </summary>
        public async Task<ITemplatePackage> GetTemplatePackageAsync(ITemplateInfo template, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<ITemplatePackage> templatePackages = await GetTemplatePackagesAsync(false, cancellationToken).ConfigureAwait(false);
            return templatePackages.Single(s => s.MountPointUri == template.MountPointUri);
        }

        /// <summary>
        /// Returns all <see cref="ITemplateInfo"/> contained by <paramref name="templatePackage"/>.
        /// </summary>
        /// <param name="templatePackage">The template package to get template from.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the asynchronous operation.</param>
        /// <returns>The enumerator to templates of the <paramref name="templatePackage"/>.</returns>
        public async Task<IEnumerable<ITemplateInfo>> GetTemplatesAsync(ITemplatePackage templatePackage, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var allTemplates = await GetTemplatesAsync(cancellationToken).ConfigureAwait(false);
            return allTemplates.Where(t => t.MountPointUri == templatePackage.MountPointUri);
        }

        /// <summary>
        /// Returns managed template package <see cref="IManagedTemplatePackage"/> matching <paramref name="packageIdentifier"/> and containing templates <see cref="ITemplateInfo"/>.
        /// </summary>
        /// <param name="packageIdentifier">The template package identifier.</param>
        /// <param name="packageVersion">The template package version, if null package version is not checked.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the asynchronous operation.</param>
        /// <returns>The managed template package and the containing templates.</returns>
        /// <exception cref="InvalidOperationException"> Throws an exception when package <paramref name="packageIdentifier"/>.</exception>
        public async Task<(IManagedTemplatePackage? Package, IEnumerable<ITemplateInfo>? Templates)> GetManagedTemplatePackageAsync(string packageIdentifier, string? packageVersion = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var templatePackages = await GetManagedTemplatePackagesAsync(false, cancellationToken).ConfigureAwait(false);
            var foundPackage = templatePackages
                .FirstOrDefault(tp =>
                {
                    if (tp?.Identifier == packageIdentifier)
                    {
                        return string.IsNullOrWhiteSpace(packageVersion) || tp.Version == packageVersion;
                    }
                    return false;
                });

            if (foundPackage != null)
            {
                var templates = await GetTemplatesAsync(foundPackage, cancellationToken).ConfigureAwait(false);

                return (foundPackage, templates);
            }

            throw new InvalidOperationException(string.Format(LocalizableStrings.TemplatePackageManager_Error_FailedToFindPackage, packageIdentifier));
        }

        private void EnsureProvidersLoaded()
        {
            if (_cachedSources != null)
            {
                return;
            }

            _cachedSources = new Dictionary<ITemplatePackageProvider, Task<IReadOnlyList<ITemplatePackage>>>();
            var providers = _environmentSettings.Components.OfType<ITemplatePackageProviderFactory>().Select(f => f.CreateProvider(_environmentSettings));
            foreach (var provider in providers)
            {
                provider.TemplatePackagesChanged += () =>
                {
                    _cachedSources[provider] = provider.GetAllTemplatePackagesAsync(default);
                    TemplatePackagesChanged?.Invoke();
                };
                _cachedSources[provider] = Task.Run(() => provider.GetAllTemplatePackagesAsync(default));
            }
        }

        private async Task<TemplateCache> UpdateTemplateCacheAsync(bool needsRebuild, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Kick off gathering template packages, so parsing cache can happen in parallel.
            Task<IReadOnlyList<ITemplatePackage>> getTemplatePackagesTask = GetTemplatePackagesAsync(needsRebuild, cancellationToken);
            if (_userTemplateCache is not TemplateCache cache)
            {
                try
                {
                    _userTemplateCache = cache = new TemplateCache(_environmentSettings.Host.FileSystem.ReadObject(_paths.TemplateCacheFile));
                }
                catch (FileNotFoundException)
                {
                    // Don't log this, it's expected, we just don't want to do File.Exists...
                    cache = new TemplateCache(null);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to load templatecache.json.");
                    cache = new TemplateCache(null);
                }
            }

            if (cache.Version == null)
            {
                // Null version means, parsing cache failed.
                needsRebuild = true;
            }

            if (!needsRebuild && cache.Version != TemplateInfo.CurrentVersion)
            {
                _logger.LogDebug($"Template cache file version is {cache.Version}, but template engine is {TemplateInfo.CurrentVersion}, rebuilding cache.");
                needsRebuild = true;
            }

            if (!needsRebuild && cache.Locale != CultureInfo.CurrentUICulture.Name)
            {
                _logger.LogDebug($"Template cache locale is {cache.Locale}, but CurrentUICulture is {CultureInfo.CurrentUICulture.Name}, rebuilding cache.");
                needsRebuild = true;
            }

            var allTemplatePackages = await getTemplatePackagesTask.ConfigureAwait(false);

            var mountPoints = new Dictionary<string, DateTime>();

            foreach (var package in allTemplatePackages)
            {
                mountPoints[package.MountPointUri] = package.LastChangeTime;

                // We can stop comparing, but we need to keep looping to fill mountPoints
                if (!needsRebuild)
                {
                    if (cache.MountPointsInfo.TryGetValue(package.MountPointUri, out var cachedLastChangeTime))
                    {
                        if (package.LastChangeTime > cachedLastChangeTime)
                        {
                            needsRebuild = true;
                        }
                    }
                    else
                    {
                        needsRebuild = true;
                    }
                }
            }
            cancellationToken.ThrowIfCancellationRequested();

            // Check that some mountpoint wasn't removed...
            if (!needsRebuild && !mountPoints.Keys.OrderBy(mp => mp).SequenceEqual(cache.MountPointsInfo.Keys.OrderBy(mp => mp)))
            {
                needsRebuild = true;
            }

            // Cool, looks like everything is up to date, exit
            if (!needsRebuild)
            {
                return cache;
            }

            var scanResults = new ScanResult?[allTemplatePackages.Count];
            Parallel.For(0, allTemplatePackages.Count, async (index) =>
            {
                try
                {
                    var scanResult = await _installScanner.ScanAsync(allTemplatePackages[index].MountPointUri, cancellationToken).ConfigureAwait(false);
                    scanResults[index] = scanResult;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(LocalizableStrings.TemplatePackageManager_Error_FailedToScan, allTemplatePackages[index].MountPointUri, ex.Message);
                    _logger.LogDebug($"Stack trace: {ex.StackTrace}");
                }
            });
            cancellationToken.ThrowIfCancellationRequested();
            cache = new TemplateCache(allTemplatePackages, scanResults, mountPoints, _environmentSettings);
            foreach (var scanResult in scanResults)
            {
                scanResult?.Dispose();
            }
            _userTemplateCache = cache;

            try
            {
                _environmentSettings.Host.FileSystem.WriteObject(_paths.TemplateCacheFile, cache);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(LocalizableStrings.TemplatePackageManager_Error_FailedToStoreCache, ex.Message);
                _logger.LogDebug($"Stack trace: {ex.StackTrace}");
            }

            return cache;
        }
    }
}
