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
        private const int MaxLoadAttempts = 20;
        private static object _settingsLock = new object();
        private static object _firstRunLock = new object();
        private readonly SettingsFilePaths _paths;
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly Action<IEngineEnvironmentSettings>? _onFirstRun;
        private SettingsStore? _userSettings;
        private TemplateCache _userTemplateCache;
        private IMountPointManager? _mountPointManager;
        private IComponentManager? _componentManager;
        private bool _templatesLoaded;
        private TemplatePackageManager _templatePackagesManager;
        private volatile bool _disposed;
        private bool _loaded;

        public SettingsLoader(IEngineEnvironmentSettings environmentSettings, Action<IEngineEnvironmentSettings>? onFirstRun = null)
        {
            _environmentSettings = environmentSettings;
            _paths = new SettingsFilePaths(environmentSettings);
            _userTemplateCache = new TemplateCache(environmentSettings);
            _templatePackagesManager = new TemplatePackageManager(environmentSettings);
            _onFirstRun = onFirstRun;
        }

        public IComponentManager Components
        {
            get
            {
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

        private SettingsStore UserSettings
        {
            get
            {
                lock (_settingsLock)
                {
                    if (_userSettings == null)
                    {
                        EnsureLoaded();
                    }
                    //EnsureLoaded sets _userSettings
                    return _userSettings!;
                }
            }
        }

        public void Save()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SettingsLoader));
            }

            JObject serialized = JObject.FromObject(UserSettings);
            _paths.WriteAllText(_paths.SettingsFile, serialized.ToString());
        }

        public Task RebuildCacheAsync(CancellationToken token)
        {
            return RebuildCacheFromSettingsIfNotCurrent(true);
        }

        public ITemplate? LoadTemplate(ITemplateInfo info, string baselineName)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SettingsLoader));
            }

            IGenerator generator;
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
            using (Timing.Over(_environmentSettings.Host, "Template from config"))
            {
                if (generator.TryGetTemplateFromConfigInfo(config, out template, localeConfig, hostTemplateConfigFile, baselineName))
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
            await RebuildCacheFromSettingsIfNotCurrent(false).ConfigureAwait(false);
            return _userTemplateCache.TemplateInfo;
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

            const int maxAttempts = 10;
            int attemptCount = 0;
            bool successfulWrite = false;

            while (!successfulWrite && attemptCount++ < maxAttempts)
            {
                if (!UserSettings.ProbingPaths.Add(probeIn))
                {
                    return;
                }

                try
                {
                    Save();
                    successfulWrite = true;
                }
                catch
                {
                    Task.Delay(10).Wait();
                }
            }
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
                _userSettings = null;
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
                        _environmentSettings.Host.OnCriticalError(null, "Failed to initialize the host, unable to complete first run actions", "null", 0);
                        _environmentSettings.Host.LogDiagnosticMessage($"Details: {ex.ToString()}", "First run");
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

                string? userSettings = null;
                using (Timing.Over(_environmentSettings.Host, "Read settings"))
                {
                    for (int i = 0; i < MaxLoadAttempts; ++i)
                    {
                        try
                        {
                            userSettings = _paths.ReadAllText(_paths.SettingsFile, "{}");
                            break;
                        }
                        catch (IOException)
                        {
                            if (i == MaxLoadAttempts - 1)
                            {
                                throw;
                            }

                            Task.Delay(2).Wait();
                        }
                    }
                }

                JObject parsed;
                using (Timing.Over(_environmentSettings.Host, "Parse settings"))
                {
                    try
                    {
                        parsed = JObject.Parse(userSettings);
                    }
                    catch (Exception ex)
                    {
                        throw new EngineInitializationException("Error parsing the user settings file", "Settings File", ex);
                    }
                }

                using (Timing.Over(_environmentSettings.Host, "Deserialize user settings"))
                {
                    _userSettings = new SettingsStore(parsed);
                }

                using (Timing.Over(_environmentSettings.Host, "Init probing paths"))
                {
                    if (_userSettings.ProbingPaths.Count == 0)
                    {
                        _userSettings.ProbingPaths.Add(_paths.Content);
                    }
                }

                using (Timing.Over(_environmentSettings.Host, "Init Component manager"))
                {
                    _componentManager = new ComponentManager(this, _userSettings);
                }

                using (Timing.Over(_environmentSettings.Host, "Init MountPoint manager"))
                {
                    _mountPointManager = new MountPointManager(_environmentSettings, _componentManager);
                }

                using (Timing.Over(_environmentSettings.Host, "Demand template load"))
                {
                    EnsureTemplatesLoaded();
                }
                _loaded = true;
                EnsureFirstRun();
            }
        }

        // Loads from the template cache
        private void EnsureTemplatesLoaded()
        {
            if (_templatesLoaded)
            {
                return;
            }

            string userTemplateCache;

            if (_paths.Exists(_paths.TemplateCacheFile))
            {
                using (Timing.Over(_environmentSettings.Host, "Read template cache"))
                {
                    userTemplateCache = _paths.ReadAllText(_paths.TemplateCacheFile, "{}");
                }
            }
            else
            {
                userTemplateCache = "{}";
            }

            JObject parsed;
            using (Timing.Over(_environmentSettings.Host, "Parse template cache"))
            {
                parsed = JObject.Parse(userTemplateCache);
            }

            using (Timing.Over(_environmentSettings.Host, "Init template cache"))
            {
                _userTemplateCache = new TemplateCache(_environmentSettings, parsed);
            }

            _templatesLoaded = true;
        }

        private async Task RebuildCacheFromSettingsIfNotCurrent(bool forceRebuild)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SettingsLoader));
            }

            EnsureTemplatesLoaded();
            forceRebuild |= _userTemplateCache.Locale != CultureInfo.CurrentUICulture.Name;
            if (_userTemplateCache.Version != TemplateInfo.CurrentVersion)
            {
                _environmentSettings.Host.LogDiagnosticMessage(
                        $"Template cache file version is {_userTemplateCache.Version}, but template engine is {TemplateInfo.CurrentVersion}, rebuilding cache.",
                        "Debug");
                forceRebuild = true;
            }

            var placesThatNeedScanning = new HashSet<string>();
            var allTemplatePackages = await _templatePackagesManager.GetTemplatePackagesAsync(forceRebuild).ConfigureAwait(false);
            var mountPoints = new Dictionary<string, DateTime>();

            if (forceRebuild)
            {
                _userTemplateCache = new TemplateCache(_environmentSettings);
                foreach (var source in allTemplatePackages)
                {
                    mountPoints[source.MountPointUri] = source.LastChangeTime;
                    placesThatNeedScanning.Add(source.MountPointUri);
                }
            }
            else
            {
                foreach (var source in allTemplatePackages)
                {
                    mountPoints[source.MountPointUri] = source.LastChangeTime;

                    if (_userTemplateCache.MountPointsInfo.TryGetValue(source.MountPointUri, out var cachedLastChangeTime))
                    {
                        if (source.LastChangeTime > cachedLastChangeTime)
                        {
                            placesThatNeedScanning.Add(source.MountPointUri);
                        }
                    }
                    else
                    {
                        placesThatNeedScanning.Add(source.MountPointUri);
                    }
                }
            }

            foreach (var place in mountPoints.Where(k => placesThatNeedScanning.Contains(k.Key)).OrderBy(k => k.Value))
            {
                try
                {
                    _userTemplateCache.Scan(place.Key);
                }
                catch (Exception ex)
                {
                    _environmentSettings.Host.OnNonCriticalError(null, $"Failed to scan \"{place}\":{Environment.NewLine}{ex}", null, 0);
                }
            }

            // When writing the template caches, we need the existing cache version to read the existing caches for before updating.
            // so don't update it until after the template caches are written.
            _userTemplateCache.WriteTemplateCaches(mountPoints);
        }
    }
}
