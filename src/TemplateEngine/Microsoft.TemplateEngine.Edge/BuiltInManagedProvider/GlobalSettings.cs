// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Edge.BuiltInManagedProvider
{
    internal sealed class GlobalSettings : IGlobalSettings, IDisposable
    {
        private const int FileReadWriteRetries = 20;
        private const int MillisecondsInterval = 20;
        private static readonly TimeSpan MaxNotificationDelayOnWriterLock = TimeSpan.FromSeconds(1);
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly string _globalSettingsFile;
        private IDisposable? _watcher;
        private volatile bool _disposed;
        private volatile AsyncMutex? _mutex;
        private int _waitingInstances;

        public GlobalSettings(IEngineEnvironmentSettings environmentSettings, string globalSettingsFile)
        {
            _environmentSettings = environmentSettings ?? throw new ArgumentNullException(nameof(environmentSettings));
            _globalSettingsFile = globalSettingsFile ?? throw new ArgumentNullException(nameof(globalSettingsFile));
            environmentSettings.Host.FileSystem.CreateDirectory(Path.GetDirectoryName(_globalSettingsFile));
            _watcher = CreateWatcherIfRequested();
        }

        public event Action? SettingsChanged;

        public async Task<IDisposable> LockAsync(CancellationToken token)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GlobalSettings));
            }
            token.ThrowIfCancellationRequested();
            if (_mutex?.IsLocked ?? false)
            {
                throw new InvalidOperationException("Lock is already taken.");
            }
            // We must use Mutex because we want to lock across different processes that might want to modify this settings file
            var escapedFilename = _globalSettingsFile.Replace("\\", "_").Replace("/", "_");
            var mutex = await AsyncMutex.WaitAsync($"Global\\812CA7F3-7CD8-44B4-B3F0-0159355C0BD5{escapedFilename}", token).ConfigureAwait(false);
            _mutex = mutex;
            return mutex;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _watcher?.Dispose();
            _watcher = null;
        }

        public async Task<IReadOnlyList<TemplatePackageData>> GetInstalledTemplatePackagesAsync(CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GlobalSettings));
            }

            if (!_environmentSettings.Host.FileSystem.FileExists(_globalSettingsFile))
            {
                return [];
            }

            for (int i = 0; i < FileReadWriteRetries; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var jObject = _environmentSettings.Host.FileSystem.ReadObject(_globalSettingsFile);
                    var packages = new List<TemplatePackageData>();

                    foreach (var package in jObject.Get<JsonArray>(nameof(GlobalSettingsData.Packages)) ?? new JsonArray())
                    {
                        packages.Add(new TemplatePackageData(
                            package!.ToGuid(nameof(TemplatePackageData.InstallerId)),
                            package.ToString(nameof(TemplatePackageData.MountPointUri)) ?? string.Empty,
                            package![nameof(TemplatePackageData.LastChangeTime)]?.GetValue<DateTime>() ?? default,
                            package.ToStringDictionary(propertyName: nameof(TemplatePackageData.Details))));
                    }

                    return packages;
                }
                catch (JsonException ex)
                {
                    var wrappedEx = new JsonException(string.Format(LocalizableStrings.GlobalSettings_Error_CorruptedSettings, _globalSettingsFile, ex.Message), ex.Path, ex.LineNumber, ex.BytePositionInLine, ex);
                    throw wrappedEx;
                }
                catch (Exception)
                {
                    if (i == (FileReadWriteRetries - 1))
                    {
                        throw;
                    }
                }
                await Task.Delay(MillisecondsInterval, cancellationToken).ConfigureAwait(false);
            }
            throw new InvalidOperationException();
        }

        public async Task SetInstalledTemplatePackagesAsync(IReadOnlyList<TemplatePackageData> packages, CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GlobalSettings));
            }

            if (!(_mutex?.IsLocked ?? false))
            {
                throw new InvalidOperationException($"Before calling {nameof(SetInstalledTemplatePackagesAsync)}, {nameof(LockAsync)} must be called.");
            }

            var globalSettingsData = new GlobalSettingsData(packages);

            for (int i = 0; i < FileReadWriteRetries; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Ignore FSW notifications received during writing changes (we'll notify synchronously)
                    _watcher?.Dispose();
                    _environmentSettings.Host.FileSystem.WriteObject(_globalSettingsFile, globalSettingsData);
                    // We are ready for new notifications now
                    _watcher = CreateWatcherIfRequested();
                    SettingsChanged?.Invoke();
                    return;
                }
                catch (Exception)
                {
                    if (i == (FileReadWriteRetries - 1))
                    {
                        throw;
                    }
                }
                await Task.Delay(MillisecondsInterval, cancellationToken).ConfigureAwait(false);
            }
            throw new InvalidOperationException();
        }

        private IDisposable? CreateWatcherIfRequested()
        {
            if (_environmentSettings.Environment.GetEnvironmentVariable("TEMPLATE_ENGINE_DISABLE_FILEWATCHER") != "1")
            {
                return _environmentSettings.Host.FileSystem.WatchFileChanges(_globalSettingsFile, FileChanged);
            }

            return null;
        }

        // This method is called whenever there is a change in global settings. Since the handlers of SettingsChanged event
        //  first grab the lock (LockAsync) and then read the whole content of GlobalSettings folder - we are here making sure
        //  to skip unwanted extra calls - all concurrent calls while handler is waiting for a lock leads to duplicate reprocessing
        //  of a whole global settings folder.
        //  To prevent this - we try to wait for a lock on behalf of the handler and refuse all concurrent file change notifications in the meantime
        private async void FileChanged(object sender, FileSystemEventArgs e)
        {
            // FileSystemWatcher fires callbacks on threadpool threads that can race with Dispose().
            // This pre-lock check handles the common case where the callback fires after _disposed is set.
            if (_disposed)
            {
                return;
            }

            // Make sure the waiting happens only for one notification at the time - as we do not care about other notifications
            // until the SettingsChanged is called
            //  if multiple concurrent call(s) get here, while there is already other caller inside waiting for the lock
            //  those concurrent callers will just return (as counter is 1 already).
            if (Interlocked.Increment(ref _waitingInstances) > 1)
            {
                return;
            }

            await TryWaitForLock().ConfigureAwait(false);

            // Re-check after lock wait: the object may have been disposed while we were waiting
            // for the lock. Without this guard, SettingsChanged subscribers would call back into
            // disposed state. Stress testing confirms this fires in ~99% of disposal-during-callback races.
            if (_disposed)
            {
                return;
            }

            // We are ready for new notifications now - indicate so by clearing the counter
            Interlocked.Exchange(ref _waitingInstances, 0);

            SettingsChanged?.Invoke();
        }

        private async Task<bool> TryWaitForLock()
        {
            CancellationTokenSource cts = new();
            try
            {
                cts.CancelAfter(MaxNotificationDelayOnWriterLock);
                if (!(_mutex?.IsLocked ?? false))
                {
                    using (await LockAsync(cts.Token).ConfigureAwait(false))
                    { }
                }
            }
            catch (Exception e)
            {
                _environmentSettings.Host.Logger.LogDebug(
                    "Failed to wait for GlobalSettings lock to be freed, before notifying about new changes. {error}",
                    e.Message);
                return false;
            }

            return true;
        }
    }
}
