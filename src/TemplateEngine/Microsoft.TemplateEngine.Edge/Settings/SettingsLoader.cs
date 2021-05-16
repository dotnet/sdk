// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    internal sealed class SettingsLoader : ISettingsLoader, IDisposable
    {
        internal const string HostTemplateFileConfigBaseName = ".host.json";
        private static object _settingsLock = new object();
        private static object _firstRunLock = new object();
        private readonly SettingsFilePaths _paths;
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly Action<IEngineEnvironmentSettings>? _onFirstRun;
        private readonly Scanner _installScanner;
        private volatile TemplateCache? _userTemplateCache;
        private volatile IMountPointManager? _mountPointManager;
        private volatile ComponentManager? _componentManager;
        private TemplatePackageManager _templatePackagesManager;
        private volatile bool _disposed;
        private bool _loaded;
        private ILogger _logger;

        public SettingsLoader(IEngineEnvironmentSettings environmentSettings, Action<IEngineEnvironmentSettings>? onFirstRun = null)
        {
            _environmentSettings = environmentSettings;
            _paths = new SettingsFilePaths(environmentSettings);
            _templatePackagesManager = new TemplatePackageManager(environmentSettings);
            _onFirstRun = onFirstRun;
            _installScanner = new Scanner(environmentSettings);
            _logger = environmentSettings.Host.LoggerFactory.CreateLogger<SettingsLoader>();
        }

        public IComponentManager Components
        {
            get
            {
                var local = _componentManager;
                if (local != null)
                {
                    return local;
                }

                lock (_settingsLock)
                {
                    if (_componentManager == null)
                    {
                        EnsureLoaded();
                    }
                    //EnsureLoaded sets _componentManager
                    return _componentManager!;
                }
            }
        }

        public IEngineEnvironmentSettings EnvironmentSettings => _environmentSettings;

        public ITemplatePackageManager TemplatePackagesManager => _templatePackagesManager;

        private IMountPointManager MountPointManager
        {
            get
            {
                var local = _mountPointManager;
                if (local != null)
                {
                    return local;
                }

                lock (_settingsLock)
                {
                    if (_mountPointManager == null)
                    {
                        EnsureLoaded();
                    }
                    //EnsureLoaded sets _userSettings
                    return _mountPointManager!;
                }
            }
        }

        public void Save()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SettingsLoader));
            }
            EnsureLoaded();
            _componentManager!.Save();
        }

        public Task RebuildTemplateCacheAsync(CancellationToken token)
        {
            return UpdateTemplateCacheAsync(true);
        }

        public ITemplate? LoadTemplate(ITemplateInfo info, string? baselineName)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SettingsLoader));
            }

            IGenerator? generator;
            if (!Components.TryGetComponent(info.GeneratorId, out generator))
            {
                return null;
            }

            IMountPoint mountPoint;
            if (!MountPointManager.TryDemandMountPoint(info.MountPointUri, out mountPoint))
            {
                return null;
            }
            IFileSystemInfo config = mountPoint.FileSystemInfo(info.ConfigPlace);

            IFileSystemInfo? localeConfig = null;
            if (!string.IsNullOrEmpty(info.LocaleConfigPlace)
                    && !string.IsNullOrEmpty(info.MountPointUri))
            {
                IMountPoint localeMountPoint;
                if (!MountPointManager.TryDemandMountPoint(info.MountPointUri, out localeMountPoint))
                {
                    // TODO: decide if we should proceed without loc info, instead of bailing.
                    return null;
                }

                localeConfig = localeMountPoint.FileSystemInfo(info.LocaleConfigPlace);
            }

            IFile? hostTemplateConfigFile = FindBestHostTemplateConfigFile(config);

            ITemplate template;
            using (Timing.Over(_logger, $"Template from config {config.MountPoint.MountPointUri}{config.FullPath}"))
            {
                //! because I know TryDemandMountPoint returns non-null when returns true
                if (generator!.TryGetTemplateFromConfigInfo(config, out template, localeConfig, hostTemplateConfigFile, baselineName))
                {
                    return template;
                }
                else
                {
                    //TODO: Log the failure to read the template info
                }
            }

            return null;
        }

        public IFile? FindBestHostTemplateConfigFile(IFileSystemInfo config)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SettingsLoader));
            }

            IDictionary<string, IFile> allHostFilesForTemplate = new Dictionary<string, IFile>();

            foreach (IFile hostFile in config.Parent.EnumerateFiles($"*{HostTemplateFileConfigBaseName}", SearchOption.TopDirectoryOnly))
            {
                allHostFilesForTemplate.Add(hostFile.Name, hostFile);
            }

            string preferredHostFileName = string.Concat(_environmentSettings.Host.HostIdentifier, HostTemplateFileConfigBaseName);
            if (allHostFilesForTemplate.TryGetValue(preferredHostFileName, out IFile preferredHostFile))
            {
                return preferredHostFile;
            }

            foreach (string fallbackHostName in _environmentSettings.Host.FallbackHostTemplateConfigNames)
            {
                string fallbackHostFileName = string.Concat(fallbackHostName, HostTemplateFileConfigBaseName);

                if (allHostFilesForTemplate.TryGetValue(fallbackHostFileName, out IFile fallbackHostFile))
                {
                    return fallbackHostFile;
                }
            }

            return null;
        }

        public async Task<IReadOnlyList<ITemplateInfo>> GetTemplatesAsync(CancellationToken token)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SettingsLoader));
            }
            var userTemplateCache = await UpdateTemplateCacheAsync(false).ConfigureAwait(false);
            return userTemplateCache.TemplateInfo;
        }

        public async Task<IReadOnlyList<ITemplateMatchInfo>> GetTemplatesAsync(Func<ITemplateMatchInfo, bool> matchFilter, IEnumerable<Func<ITemplateInfo, MatchInfo?>> filters, CancellationToken token = default)
        {
            IReadOnlyList<ITemplateInfo> templates = await GetTemplatesAsync(token).ConfigureAwait(false);
            //TemplateListFilter.GetTemplateMatchInfo code should be moved to this method eventually, when no longer needed.
#pragma warning disable CS0618 // Type or member is obsolete.
            return TemplateListFilter.GetTemplateMatchInfo(templates, matchFilter, filters.ToArray()).ToList();
#pragma warning restore CS0618 // Type or member is obsolete
        }

        public void AddProbingPath(string probeIn)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SettingsLoader));
            }
            EnsureLoaded();
            _componentManager!.AddProbingPath(probeIn);
        }

        public bool TryGetMountPoint(string mountPointUri, out IMountPoint mountPoint)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SettingsLoader));
            }
            return MountPointManager.TryDemandMountPoint(mountPointUri, out mountPoint);
        }

        public void ResetHostSettings()
        {
            lock (_settingsLock)
            {
                _paths.Delete(_paths.BaseDir);
                _loaded = false;
                _componentManager = null;
                _mountPointManager = null;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _templatePackagesManager.Dispose();
        }

        private void EnsureFirstRun()
        {
            lock (_firstRunLock)
            {
                if (!_paths.Exists(_paths.BaseDir) || !_paths.Exists(_paths.FirstRunCookie))
                {
                    try
                    {
                        _onFirstRun?.Invoke(EnvironmentSettings);
                        _paths.WriteAllText(_paths.FirstRunCookie, "");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Failed to initialize the host, unable to complete first run actions");
                        _logger.LogDebug($"Details: {ex.ToString()}");
                        throw new EngineInitializationException("Failed to initialize the host, unable to complete first run actions", "First run", ex);
                    }
                }
            }
        }

        /// <summary>
        /// Loads the settings, should be called under locking <see cref="_settingsLock"/>.
        /// </summary>
        private void EnsureLoaded()
        {
            lock (_settingsLock)
            {
                if (_loaded)
                {
                    return;
                }

                using (Timing.Over(_logger, "Init Component manager"))
                {
                    _componentManager = new ComponentManager(_environmentSettings);
                }

                using (Timing.Over(_logger, "Init MountPoint manager"))
                {
                    _mountPointManager = new MountPointManager(_environmentSettings, _componentManager);
                }

                _loaded = true;
                EnsureFirstRun();
            }
        }

        private async Task<TemplateCache> UpdateTemplateCacheAsync(bool needsRebuild)
        {
            // Kick off gathering template packages, so parsing cache can happen in parallel.
            Task<IReadOnlyList<ITemplatePackage>> getTemplatePackagesTask = _templatePackagesManager.GetTemplatePackagesAsync(needsRebuild);

            if (!(_userTemplateCache is TemplateCache cache))
            {
                cache = new TemplateCache(JObject.Parse(_paths.ReadAllText(_paths.TemplateCacheFile, "{}")));
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

            // Check that some mountpoint wasn't removed...
            if (!needsRebuild && mountPoints.Keys.Count != cache.MountPointsInfo.Count)
            {
                needsRebuild = true;
            }

            // Cool, looks like everything is up to date, exit
            if (!needsRebuild)
            {
                return cache;
            }

            var scanResults = new ScanResult[allTemplatePackages.Count];
            Parallel.For(0, allTemplatePackages.Count, (int index) =>
            {
                try
                {
                    var scanResult = _installScanner.Scan(allTemplatePackages[index].MountPointUri);
                    scanResults[index] = scanResult;
                }
                catch (Exception ex)
                {
                    scanResults[index] = ScanResult.Empty;
                    _logger.LogWarning($"Failed to scan \"{allTemplatePackages[index].MountPointUri}\":{Environment.NewLine}{ex}");
                }
            });

            cache = new TemplateCache(
                scanResults,
                mountPoints
                );
            JObject serialized = JObject.FromObject(cache);
            _paths.WriteAllText(_paths.TemplateCacheFile, serialized.ToString());
            return _userTemplateCache = cache;
        }
    }
}
