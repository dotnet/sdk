// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Internal
{
    internal class DotnetFileWatcher : IFileSystemWatcher
    {
        internal Action<string>? Logger { get; set;  }

        private volatile bool _disposed;

        private readonly Func<string, FileSystemWatcher> _watcherFactory;

        private FileSystemWatcher? _fileSystemWatcher;

        private readonly object _createLock = new();

        public DotnetFileWatcher(string watchedDirectory)
            : this(watchedDirectory, DefaultWatcherFactory)
        {
        }

        internal DotnetFileWatcher(string watchedDirectory, Func<string, FileSystemWatcher> fileSystemWatcherFactory)
        {
            Ensure.NotNull(fileSystemWatcherFactory, nameof(fileSystemWatcherFactory));
            Ensure.NotNullOrEmpty(watchedDirectory, nameof(watchedDirectory));

            BasePath = watchedDirectory;
            _watcherFactory = fileSystemWatcherFactory;
            CreateFileSystemWatcher();
        }

        public event EventHandler<(string filePath, bool newFile)>? OnFileChange;

        public event EventHandler<Exception>? OnError;

        public string BasePath { get; }

        private static FileSystemWatcher DefaultWatcherFactory(string watchedDirectory)
        {
            Ensure.NotNullOrEmpty(watchedDirectory, nameof(watchedDirectory));

            return new FileSystemWatcher(watchedDirectory);
        }

        private void WatcherErrorHandler(object sender, ErrorEventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            Logger?.Invoke("Error");

            var exception = e.GetException();

            Logger?.Invoke(exception.ToString());

            // Win32Exception may be triggered when setting EnableRaisingEvents on a file system type
            // that is not supported, such as a network share. Don't attempt to recreate the watcher
            // in this case as it will cause a StackOverflowException
            if (!(exception is Win32Exception))
            {
                // Recreate the watcher if it is a recoverable error.
                CreateFileSystemWatcher();
            }

            OnError?.Invoke(this, exception);
        }

        private void WatcherRenameHandler(object sender, RenamedEventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            Logger?.Invoke("Rename");

            NotifyChange(e.OldFullPath, newFile: false);
            NotifyChange(e.FullPath, newFile: true);

            if (Directory.Exists(e.FullPath))
            {
                foreach (var newLocation in Directory.EnumerateFileSystemEntries(e.FullPath, "*", SearchOption.AllDirectories))
                {
                    // Calculated previous path of this moved item.
                    var oldLocation = Path.Combine(e.OldFullPath, newLocation.Substring(e.FullPath.Length + 1));
                    NotifyChange(oldLocation, newFile: false);
                    NotifyChange(newLocation, newFile: true);
                }
            }
        }

        private void WatcherChangeHandler(object sender, FileSystemEventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            Logger?.Invoke("Change");
            NotifyChange(e.FullPath, newFile: false);
        }

        private void WatcherAddedHandler(object sender, FileSystemEventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            Logger?.Invoke("Added");
            NotifyChange(e.FullPath, newFile: true);
        }

        private void NotifyChange(string fullPath, bool newFile)
        {
            // Only report file changes
            OnFileChange?.Invoke(this, (fullPath, newFile));
        }

        private void CreateFileSystemWatcher()
        {
            lock (_createLock)
            {
                bool enableEvents = false;

                if (_fileSystemWatcher != null)
                {
                    enableEvents = _fileSystemWatcher.EnableRaisingEvents;

                    DisposeInnerWatcher();
                }

                _fileSystemWatcher = _watcherFactory(BasePath);
                _fileSystemWatcher.IncludeSubdirectories = true;

                _fileSystemWatcher.Created += WatcherAddedHandler;
                _fileSystemWatcher.Deleted += WatcherChangeHandler;
                _fileSystemWatcher.Changed += WatcherChangeHandler;
                _fileSystemWatcher.Renamed += WatcherRenameHandler;
                _fileSystemWatcher.Error += WatcherErrorHandler;

                _fileSystemWatcher.EnableRaisingEvents = enableEvents;
            }
        }

        private void DisposeInnerWatcher()
        {
            if ( _fileSystemWatcher != null )
            {
                _fileSystemWatcher.EnableRaisingEvents = false;

                _fileSystemWatcher.Created -= WatcherAddedHandler;
                _fileSystemWatcher.Deleted -= WatcherChangeHandler;
                _fileSystemWatcher.Changed -= WatcherChangeHandler;
                _fileSystemWatcher.Renamed -= WatcherRenameHandler;
                _fileSystemWatcher.Error -= WatcherErrorHandler;

                _fileSystemWatcher.Dispose();
            }
        }

        public bool EnableRaisingEvents
        {
            get => _fileSystemWatcher!.EnableRaisingEvents;
            set => _fileSystemWatcher!.EnableRaisingEvents = value;
        }

        public void Dispose()
        {
            _disposed = true;
            DisposeInnerWatcher();
        }
    }
}
