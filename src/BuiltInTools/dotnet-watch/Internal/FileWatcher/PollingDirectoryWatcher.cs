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

        private Dictionary<string, FileMeta> _knownFiles = new(PathUtilities.OSSpecificPathComparer);
        private Dictionary<string, FileMeta> _tempDictionary = new(PathUtilities.OSSpecificPathComparer);
        private readonly Dictionary<string, ChangeKind> _changes = new(PathUtilities.OSSpecificPathComparer);

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

            CreateKnownFilesSnapshot();

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

        private void CreateKnownFilesSnapshot()
        {
            _knownFiles.Clear();

            ForeachEntityInDirectory(_watchedDirectory, fileInfo =>
            {
                _knownFiles.Add(fileInfo.FullName, new FileMeta(fileInfo, foundAgain: false));
            });
        }

        private void CheckForChangedFiles()
        {
            _changes.Clear();

            ForeachEntityInDirectory(_watchedDirectory, fileInfo =>
            {
                var fullFilePath = fileInfo.FullName;

                if (_knownFiles.TryGetValue(fullFilePath, out var fileMeta))
                {
                    try
                    {
                        if (fileMeta.FileInfo.LastWriteTime != fileInfo.LastWriteTime)
                        {
                            // File changed
                            _changes.TryAdd(fullFilePath, ChangeKind.Update);
                        }

                        _knownFiles[fullFilePath] = new FileMeta(fileMeta.FileInfo, foundAgain: true);
                    }
                    catch (FileNotFoundException)
                    {
                        _knownFiles[fullFilePath] = new FileMeta(fileMeta.FileInfo, foundAgain: false);
                    }
                }
                else
                {
                    // File added
                    _changes.TryAdd(fullFilePath, ChangeKind.Add);
                }

                _tempDictionary.Add(fileInfo.FullName, new FileMeta(fileInfo, foundAgain: false));
            });

            foreach (var (fullPath, fileMeta) in _knownFiles)
            {
                if (!fileMeta.FoundAgain)
                {
                    // File deleted
                    _changes.TryAdd(fullPath, ChangeKind.Delete);
                }
            }

            NotifyChanges();

            // Swap the two dictionaries
            (_tempDictionary, _knownFiles) = (_knownFiles, _tempDictionary);
            _tempDictionary.Clear();
        }

        private void ForeachEntityInDirectory(DirectoryInfo dirInfo, Action<FileSystemInfo> fileAction)
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
                    fileAction(entity);
                }
            }
        }

        private void NotifyChanges()
        {
            foreach (var (path, kind) in _changes)
            {
                if (_disposed || !_raiseEvents)
                {
                    break;
                }

                OnFileChange?.Invoke(this, new ChangedPath(path, kind));
            }
        }

        private readonly struct FileMeta(FileSystemInfo fileInfo, bool foundAgain)
        {
            public readonly FileSystemInfo FileInfo = fileInfo;
            public readonly bool FoundAgain = foundAgain;
        }
    }
}
