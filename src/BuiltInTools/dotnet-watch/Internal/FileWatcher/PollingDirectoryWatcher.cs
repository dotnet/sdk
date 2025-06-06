// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Watch
{
    internal sealed class PollingDirectoryWatcher : IDirectoryWatcher
    {
        // The minimum interval to rerun the scan
        private static readonly TimeSpan _minRunInternal = TimeSpan.FromSeconds(.5);

        private readonly DirectoryInfo _watchedDirectory;
        private readonly bool _includeSubdirectories;

        private Dictionary<string, DateTime> _currentSnapshot = new(PathUtilities.OSSpecificPathComparer);

        // The following are sets that are used to calculate new snapshot and cleared on eached use (pooled):
        private Dictionary<string, DateTime> _snapshotBuilder = new(PathUtilities.OSSpecificPathComparer);
        private readonly Dictionary<string, ChangeKind> _changesBuilder = new(PathUtilities.OSSpecificPathComparer);

        private Thread _pollingThread;
        private bool _raiseEvents;

        private volatile bool _disposed;

        public event EventHandler<ChangedPath>? OnFileChange;

#pragma warning disable CS0067 // not used
        public event EventHandler<Exception>? OnError;
#pragma warning restore

        public string WatchedDirectory { get; }

        public PollingDirectoryWatcher(string watchedDirectory, bool includeSubdirectories)
        {
            _watchedDirectory = new DirectoryInfo(watchedDirectory);
            _includeSubdirectories = includeSubdirectories;
            WatchedDirectory = _watchedDirectory.FullName;

            _pollingThread = new Thread(new ThreadStart(PollingLoop))
            {
                IsBackground = true,
                Name = nameof(PollingDirectoryWatcher)
            };

            CaptureInitialSnapshot();

            _pollingThread.Start();
        }

        public void Dispose()
        {
            EnableRaisingEvents = false;
            _disposed = true;
        }

        public bool EnableRaisingEvents
        {
            get => _raiseEvents;
            set
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _raiseEvents = value;
            }
        }

        private void PollingLoop()
        {
            var stopwatch = Stopwatch.StartNew();
            stopwatch.Start();

            while (!_disposed)
            {
                if (stopwatch.Elapsed < _minRunInternal)
                {
                    // Don't run too often
                    // The min wait time here can be double
                    // the value of the variable (FYI)
                    Thread.Sleep(_minRunInternal);
                }

                stopwatch.Reset();

                if (!_raiseEvents)
                {
                    continue;
                }

                CheckForChangedFiles();
            }

            stopwatch.Stop();
        }

        private void CaptureInitialSnapshot()
        {
            Debug.Assert(_currentSnapshot.Count == 0);

            ForeachEntityInDirectory(_watchedDirectory, (filePath, writeTime) =>
            {
                _currentSnapshot.Add(filePath, writeTime);
            });
        }

        private void CheckForChangedFiles()
        {
            Debug.Assert(_changesBuilder.Count == 0);
            Debug.Assert(_snapshotBuilder.Count == 0);

            ForeachEntityInDirectory(_watchedDirectory, (filePath, currentWriteTime) =>
            {
                if (!_currentSnapshot.TryGetValue(filePath, out var snapshotWriteTime))
                {
                    _changesBuilder.TryAdd(filePath, ChangeKind.Add);
                }
                else if (snapshotWriteTime != currentWriteTime)
                {
                    _changesBuilder.TryAdd(filePath, ChangeKind.Update);
                }

                _snapshotBuilder.Add(filePath, currentWriteTime);
            });

            foreach (var (filePath, _) in _currentSnapshot)
            {
                if (!_snapshotBuilder.ContainsKey(filePath))
                {
                    _changesBuilder.TryAdd(filePath, ChangeKind.Delete);
                }
            }

            NotifyChanges(_changesBuilder);

            // Swap the two dictionaries
            (_snapshotBuilder, _currentSnapshot) = (_currentSnapshot, _snapshotBuilder);

            _changesBuilder.Clear();
            _snapshotBuilder.Clear();
        }

        private void ForeachEntityInDirectory(DirectoryInfo dirInfo, Action<string, DateTime> fileAction)
        {
            if (!dirInfo.Exists)
            {
                return;
            }

            IEnumerable<FileSystemInfo> entities;
            try
            {
                entities = dirInfo.EnumerateFileSystemInfos("*.*", SearchOption.TopDirectoryOnly);
            }
            // If the directory is deleted after the exists check this will throw and could crash the process
            catch (DirectoryNotFoundException)
            {
                return;
            }

            foreach (var entity in entities)
            {
                if (entity is DirectoryInfo subdirInfo)
                {
                    if (_includeSubdirectories)
                    {
                        ForeachEntityInDirectory(subdirInfo, fileAction);
                    }
                }
                else
                {
                    string filePath;
                    DateTime currentWriteTime;
                    try
                    {
                        filePath = entity.FullName;
                        currentWriteTime = entity.LastWriteTimeUtc;
                    }
                    catch (FileNotFoundException)
                    {
                        continue;
                    }

                    fileAction(filePath, currentWriteTime);
                }
            }
        }

        private void NotifyChanges(Dictionary<string, ChangeKind> changes)
        {
            foreach (var (path, kind) in changes)
            {
                if (_disposed || !_raiseEvents)
                {
                    break;
                }

                OnFileChange?.Invoke(this, new ChangedPath(path, kind));
            }
        }
    }
}
