// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch
{
    internal sealed class FileWatcher(IReporter reporter) : IDisposable
    {
        // Directory watcher for each watched directory
        private readonly Dictionary<string, IDirectoryWatcher> _watchers = [];

        private bool _disposed;
        public event Action<string, ChangeKind>? OnFileChange;

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

        private void WatcherChangedHandler(object? sender, (string changedPath, ChangeKind kind) args)
        {
            OnFileChange?.Invoke(args.changedPath, args.kind);
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

        public Task<ChangedFile?> WaitForFileChangeAsync(Action? startedWatching, CancellationToken cancellationToken)
           => WaitForFileChangeAsync(
               changeFilter: (path, kind) => new ChangedFile(new FileItem() { FilePath = path, ContainingProjectPaths = [] }, kind),
               startedWatching,
               cancellationToken);

        public Task<ChangedFile?> WaitForFileChangeAsync(IReadOnlyDictionary<string, FileItem> fileSet, Action? startedWatching, CancellationToken cancellationToken)
            => WaitForFileChangeAsync(
                changeFilter: (path, kind) => fileSet.TryGetValue(path, out var fileItem) ? new ChangedFile(fileItem, kind) : null,
                startedWatching,
                cancellationToken);

        public async Task<ChangedFile?> WaitForFileChangeAsync(Func<string, ChangeKind, ChangedFile?> changeFilter, Action? startedWatching, CancellationToken cancellationToken)
        {
            var fileChangedSource = new TaskCompletionSource<ChangedFile?>(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => fileChangedSource.TrySetResult(null));

            void FileChangedCallback(string path, ChangeKind kind)
            {
                if (changeFilter(path, kind) is { } changedFile)
                {
                    fileChangedSource.TrySetResult(changedFile);
                }
            }

            ChangedFile? changedFile;

            OnFileChange += FileChangedCallback;
            try
            {
                startedWatching?.Invoke();
                changedFile = await fileChangedSource.Task;
            }
            finally
            {
                OnFileChange -= FileChangedCallback;
            }

            return changedFile;
        }

        public static async ValueTask WaitForFileChangeAsync(string filePath, IReporter reporter, Action? startedWatching, CancellationToken cancellationToken)
        {
            using var watcher = new FileWatcher(reporter);

            watcher.WatchDirectories([Path.GetDirectoryName(filePath)!]);

            var fileChange = await watcher.WaitForFileChangeAsync(
                changeFilter: (path, kind) => path == filePath ? new ChangedFile(new FileItem { FilePath = path, ContainingProjectPaths = [] }, kind) : null,
                startedWatching,
                cancellationToken);

            if (fileChange != null)
            {
                reporter.Output($"File changed: {filePath}");
            }
        }
    }
}
