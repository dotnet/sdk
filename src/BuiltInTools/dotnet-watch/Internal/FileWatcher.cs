// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch
{
    internal sealed class FileWatcher(IReporter reporter) : IDisposable
    {
        // Directory watcher for each watched directory
        private readonly Dictionary<string, IDirectoryWatcher> _watchers = [];

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

            foreach (var (_, watcher) in _watchers)
            {
                watcher.OnFileChange -= WatcherChangedHandler;
                watcher.OnError -= WatcherErrorHandler;
                watcher.Dispose();
            }
        }

        public bool WatchingDirectories
            => _watchers.Count > 0;

        public void WatchContainingDirectories(IEnumerable<string> filePaths)
            => WatchDirectories(filePaths.Select(path => Path.GetDirectoryName(path)!));

        public void WatchDirectories(IEnumerable<string> directories)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            foreach (var dir in directories)
            {
                var directory = EnsureTrailingSlash(dir);

                var alreadyWatched = _watchers
                    .Where(d => directory.StartsWith(d.Key))
                    .Any();

                if (alreadyWatched)
                {
                    continue;
                }

                var redundantWatchers = _watchers
                    .Where(d => d.Key.StartsWith(directory))
                    .Select(d => d.Key)
                    .ToList();

                foreach (var watcher in redundantWatchers)
                {
                    DisposeWatcher(watcher);
                }

                var newWatcher = FileWatcherFactory.CreateWatcher(directory);
                if (newWatcher is EventBasedDirectoryWatcher eventBasedWatcher)
                {
                    eventBasedWatcher.Logger = message => reporter.Verbose(message);
                }

                newWatcher.OnFileChange += WatcherChangedHandler;
                newWatcher.OnError += WatcherErrorHandler;
                newWatcher.EnableRaisingEvents = true;

                _watchers.Add(directory, newWatcher);
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

        private void DisposeWatcher(string directory)
        {
            var watcher = _watchers[directory];
            _watchers.Remove(directory);

            watcher.EnableRaisingEvents = false;
            watcher.OnFileChange -= WatcherChangedHandler;
            watcher.OnError -= WatcherErrorHandler;

            watcher.Dispose();
        }

        private static string EnsureTrailingSlash(string path)
            => (path is [.., var last] && last != Path.DirectorySeparatorChar) ? path + Path.DirectorySeparatorChar : path;

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

            watcher.WatchDirectories([Path.GetDirectoryName(filePath)!]);

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
