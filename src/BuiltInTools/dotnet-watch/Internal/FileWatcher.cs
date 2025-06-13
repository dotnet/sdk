// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch
{
    internal sealed class FileWatcher(IReporter reporter) : IDisposable
    {
        // Directory watcher for each watched directory tree.
        // Keyed by full path to the root directory with a trailing directory separator.
        private readonly Dictionary<string, IDirectoryWatcher> _directoryTreeWatchers = new(PathUtilities.OSSpecificPathComparer);

        // Directory watcher for each watched directory (non-recursive).
        // Keyed by full path to the root directory with a trailing directory separator.
        private readonly Dictionary<string, IDirectoryWatcher> _directoryWatchers = new(PathUtilities.OSSpecificPathComparer);

        private bool _disposed;
        public event Action<ChangedPath>? OnFileChange;

        public bool SuppressEvents { get; set; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (var (_, watcher) in _directoryTreeWatchers)
            {
                watcher.OnFileChange -= WatcherChangedHandler;
                watcher.OnError -= WatcherErrorHandler;
                watcher.Dispose();
            }
        }

        public bool WatchingDirectories
            => _directoryTreeWatchers.Count > 0 || _directoryWatchers.Count > 0;

        public void WatchContainingDirectories(IEnumerable<string> filePaths, bool includeSubdirectories)
            => WatchDirectories(filePaths.Select(path => Path.GetDirectoryName(path)!), includeSubdirectories);

        public void WatchDirectories(IEnumerable<string> directories, bool includeSubdirectories)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            foreach (var dir in directories.Distinct())
            {
                var directory = PathUtilities.EnsureTrailingSlash(PathUtilities.NormalizeDirectorySeparators(dir));

                // the directory is watched by active directory watcher:
                if (!includeSubdirectories && _directoryWatchers.ContainsKey(directory))
                {
                    continue;
                }

                // the directory is a root or subdirectory of active directory tree watcher:
                var alreadyWatched = _directoryTreeWatchers.Any(d => directory.StartsWith(d.Key, PathUtilities.OSSpecificPathComparison));
                if (alreadyWatched)
                {
                    continue;
                }

                var newWatcher = FileWatcherFactory.CreateWatcher(directory, includeSubdirectories);
                if (newWatcher is EventBasedDirectoryWatcher eventBasedWatcher)
                {
                    eventBasedWatcher.Logger = message => reporter.Verbose(message);
                }

                newWatcher.OnFileChange += WatcherChangedHandler;
                newWatcher.OnError += WatcherErrorHandler;
                newWatcher.EnableRaisingEvents = true;

                // watchers that are now redundant (covered by the new directory watcher):
                if (includeSubdirectories)
                {
                    var watchersToRemove = _directoryTreeWatchers
                        .Where(d => d.Key.StartsWith(directory, PathUtilities.OSSpecificPathComparison))
                        .ToList();

                    foreach (var (watchedDirectory, watcher) in watchersToRemove)
                    {
                        _directoryTreeWatchers.Remove(watchedDirectory);

                        watcher.EnableRaisingEvents = false;
                        watcher.OnFileChange -= WatcherChangedHandler;
                        watcher.OnError -= WatcherErrorHandler;

                        watcher.Dispose();
                    }

                    _directoryTreeWatchers.Add(directory, newWatcher);
                }
                else
                {
                    _directoryWatchers.Add(directory, newWatcher);
                }
            }
        }

        private void WatcherErrorHandler(object? sender, Exception error)
        {
            if (sender is IDirectoryWatcher watcher)
            {
                reporter.Warn($"The file watcher observing '{watcher.WatchedDirectory}' encountered an error: {error.Message}");
            }
        }

        private void WatcherChangedHandler(object? sender, ChangedPath change)
        {
            if (!SuppressEvents)
            {
                OnFileChange?.Invoke(change);
            }
        }

        public async Task<ChangedFile?> WaitForFileChangeAsync(IReadOnlyDictionary<string, FileItem> fileSet, Action? startedWatching, CancellationToken cancellationToken)
        {
            var changedPath = await WaitForFileChangeAsync(
                acceptChange: change => fileSet.ContainsKey(change.Path),
                startedWatching,
                cancellationToken);

            return changedPath.HasValue ? new ChangedFile(fileSet[changedPath.Value.Path], changedPath.Value.Kind) : null;
        }

        public async Task<ChangedPath?> WaitForFileChangeAsync(Predicate<ChangedPath> acceptChange, Action? startedWatching, CancellationToken cancellationToken)
        {
            var fileChangedSource = new TaskCompletionSource<ChangedPath?>(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => fileChangedSource.TrySetResult(null));

            void FileChangedCallback(ChangedPath change)
            {
                if (acceptChange(change))
                {
                    fileChangedSource.TrySetResult(change);
                }
            }

            ChangedPath? change;

            OnFileChange += FileChangedCallback;
            try
            {
                startedWatching?.Invoke();
                change = await fileChangedSource.Task;
            }
            finally
            {
                OnFileChange -= FileChangedCallback;
            }

            return change;
        }

        public static async ValueTask WaitForFileChangeAsync(string filePath, IReporter reporter, Action? startedWatching, CancellationToken cancellationToken)
        {
            using var watcher = new FileWatcher(reporter);

            watcher.WatchContainingDirectories([filePath], includeSubdirectories: false);

            var fileChange = await watcher.WaitForFileChangeAsync(
                acceptChange: change => change.Path == filePath,
                startedWatching,
                cancellationToken);

            if (fileChange != null)
            {
                reporter.Output($"File changed: {filePath}");
            }
        }
    }
}
