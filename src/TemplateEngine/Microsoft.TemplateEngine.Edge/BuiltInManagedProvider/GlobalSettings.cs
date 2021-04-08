// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Edge.Settings;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Edge.BuiltInManagedProvider
{
    internal sealed class GlobalSettings : IGlobalSettings, IDisposable
    {
        private const int FileReadWriteRetries = 5;
        private readonly Paths _paths;
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly string _globalSettingsFile;
        private IDisposable? _watcher;
        private volatile bool _disposed;
        private volatile AsyncMutex? _mutex;

        public GlobalSettings(IEngineEnvironmentSettings environmentSettings, string globalSettingsFile)
        {
            _environmentSettings = environmentSettings ?? throw new ArgumentNullException(nameof(environmentSettings));
            _globalSettingsFile = globalSettingsFile ?? throw new ArgumentNullException(nameof(globalSettingsFile));
            _paths = new Paths(environmentSettings);
            environmentSettings.Host.FileSystem.CreateDirectory(Path.GetDirectoryName(_globalSettingsFile));
            _watcher = environmentSettings.Host.FileSystem.WatchFileChanges(_globalSettingsFile, FileChanged);
        }

        private void FileChanged(object sender, FileSystemEventArgs e)
        {
            SettingsChanged?.Invoke();
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
                return Array.Empty<TemplatePackageData>();
            }

            for (int i = 0; i < FileReadWriteRetries; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    string? textFileContent = _paths.ReadAllText(_globalSettingsFile, "{}");
                    GlobalSettingsData? data = JsonConvert.DeserializeObject<GlobalSettingsData>(textFileContent);
                    return data.Packages ?? Array.Empty<TemplatePackageData>();
                }
                catch (Exception)
                {
                    if (i == (FileReadWriteRetries - 1))
                    {
                        throw;
                    }
                }
                await Task.Delay(20, cancellationToken).ConfigureAwait(false);
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

            string? serializedText = JsonConvert.SerializeObject(new GlobalSettingsData()
            {
                Packages = packages
            });

            for (int i = 0; i < FileReadWriteRetries; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    _paths.WriteAllText(_globalSettingsFile, serializedText);
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
                await Task.Delay(20, cancellationToken).ConfigureAwait(false);
            }
            throw new InvalidOperationException();
        }
    }
}
