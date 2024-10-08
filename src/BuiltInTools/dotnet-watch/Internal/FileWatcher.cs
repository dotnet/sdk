// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Internal
{
    internal sealed class FileWatcher(IReadOnlyDictionary<string, FileItem> fileSet, IReporter reporter) : IDisposable
    {
        private readonly Dictionary<string, IFileSystemWatcher> _watchers = [];

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

        public void StartWatching()
        {
            EnsureNotDisposed();

            foreach (var (filePath, _) in fileSet)
            {
                var directory = EnsureTrailingSlash(Path.GetDirectoryName(filePath)!);

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
            if (sender is IFileSystemWatcher watcher)
            {
                reporter.Warn($"The file watcher observing '{watcher.BasePath}' encountered an error: {error.Message}");
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

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(FileWatcher));
            }
        }

        private static string EnsureTrailingSlash(string path)
            => (path is [.., var last] && last != Path.DirectorySeparatorChar) ? path + Path.DirectorySeparatorChar : path;

        public async Task<ChangedFile?> GetChangedFileAsync(Action? startedWatching, CancellationToken cancellationToken)
        {
            StartWatching();

            var fileChangedSource = new TaskCompletionSource<ChangedFile?>(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => fileChangedSource.TrySetResult(null));

            void FileChangedCallback(string path, ChangeKind kind)
            {
                if (fileSet.TryGetValue(path, out var fileItem))
                {
                    fileChangedSource.TrySetResult(new ChangedFile(fileItem, kind));
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

        public static async ValueTask WaitForFileChangeAsync(string path, IReporter reporter, CancellationToken cancellationToken)
        {
            var fileSet = new Dictionary<string, FileItem>() { { path, new FileItem { FilePath = path } } };

            using var watcher = new FileWatcher(fileSet, reporter);
            await watcher.GetChangedFileAsync(startedWatching: null, cancellationToken);

            reporter.Output($"File changed: {path}");
        }
    }
}
