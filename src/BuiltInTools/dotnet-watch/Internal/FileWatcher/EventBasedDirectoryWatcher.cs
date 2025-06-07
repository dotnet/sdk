// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;

namespace Microsoft.DotNet.Watch
{
    internal sealed class EventBasedDirectoryWatcher : IDirectoryWatcher
    {
        public event EventHandler<ChangedPath>? OnFileChange;
        public event EventHandler<Exception>? OnError;

        public string WatchedDirectory { get; }
        public bool IncludeSubdirectories { get; }
        public Action<string>? Logger { get; set; }

        private volatile bool _disposed;
        private FileSystemWatcher? _fileSystemWatcher;
        private readonly Lock _createLock = new();

        internal EventBasedDirectoryWatcher(string watchedDirectory, bool includeSubdirectories)
        {
            WatchedDirectory = watchedDirectory;
            IncludeSubdirectories = includeSubdirectories;

            CreateFileSystemWatcher();
        }

        public void Dispose()
        {
            _disposed = true;
            DisposeInnerWatcher();
        }

        private void WatcherErrorHandler(object sender, ErrorEventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            Logger?.Invoke("[FW] Error");

            var exception = e.GetException();

            Logger?.Invoke(exception.ToString());

            // Win32Exception may be triggered when setting EnableRaisingEvents on a file system type
            // that is not supported, such as a network share. Don't attempt to recreate the watcher
            // in this case as it will cause a StackOverflowException
            if (exception is not Win32Exception)
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

            Logger?.Invoke($"[FW] Renamed '{e.OldFullPath}' to '{e.FullPath}'.");

            if (Directory.Exists(e.FullPath))
            {
                foreach (var newLocation in Directory.EnumerateFiles(e.FullPath, "*", SearchOption.AllDirectories))
                {
                    // Calculated previous path of this moved item.
                    var oldLocation = Path.Combine(e.OldFullPath, newLocation.Substring(e.FullPath.Length + 1));
                    NotifyChange(oldLocation, ChangeKind.Delete);
                    NotifyChange(newLocation, ChangeKind.Add);
                }
            }
            else
            {
                NotifyChange(e.OldFullPath, ChangeKind.Delete);
                NotifyChange(e.FullPath, ChangeKind.Add);
            }
        }

        private void WatcherDeletedHandler(object sender, FileSystemEventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            var isDir = Directory.Exists(e.FullPath);

            Logger?.Invoke($"[FW] Deleted '{e.FullPath}'.");

            // ignore directory changes:
            if (isDir)
            {
                return;
            }

            NotifyChange(e.FullPath, ChangeKind.Delete);
        }

        private void WatcherChangeHandler(object sender, FileSystemEventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            var isDir = Directory.Exists(e.FullPath);

            Logger?.Invoke($"[FW] Updated '{e.FullPath}'.");

            // ignore directory changes:
            if (isDir)
            {
                return;
            }

            NotifyChange(e.FullPath, ChangeKind.Update);
        }

        private void WatcherAddedHandler(object sender, FileSystemEventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            var isDir = Directory.Exists(e.FullPath);

            Logger?.Invoke($"[FW] Added '{e.FullPath}'.");

            if (isDir)
            {
                return;
            }

            NotifyChange(e.FullPath, ChangeKind.Add);
        }

        private void NotifyChange(string fullPath, ChangeKind kind)
        {
            // Only report file changes
            OnFileChange?.Invoke(this, new ChangedPath(fullPath, kind));
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

                _fileSystemWatcher = new FileSystemWatcher(WatchedDirectory)
                {
                    IncludeSubdirectories = IncludeSubdirectories
                };

                _fileSystemWatcher.Created += WatcherAddedHandler;
                _fileSystemWatcher.Deleted += WatcherDeletedHandler;
                _fileSystemWatcher.Changed += WatcherChangeHandler;
                _fileSystemWatcher.Renamed += WatcherRenameHandler;
                _fileSystemWatcher.Error += WatcherErrorHandler;

                _fileSystemWatcher.EnableRaisingEvents = enableEvents;
            }
        }

        private void DisposeInnerWatcher()
        {
            if (_fileSystemWatcher != null)
            {
                _fileSystemWatcher.EnableRaisingEvents = false;

                _fileSystemWatcher.Created -= WatcherAddedHandler;
                _fileSystemWatcher.Deleted -= WatcherDeletedHandler;
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
    }
}
