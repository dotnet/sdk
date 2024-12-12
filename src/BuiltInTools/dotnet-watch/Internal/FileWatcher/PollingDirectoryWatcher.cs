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

        private Dictionary<string, FileMeta> _knownEntities = [];
        private Dictionary<string, FileMeta> _tempDictionary = [];
        private readonly Dictionary<string, ChangeKind> _changes = [];

        private Thread _pollingThread;
        private bool _raiseEvents;

        private volatile bool _disposed;

        public event EventHandler<(string filePath, ChangeKind kind)>? OnFileChange;

#pragma warning disable CS0067 // not used
        public event EventHandler<Exception>? OnError;
#pragma warning restore

        public string WatchedDirectory { get; }

        public PollingDirectoryWatcher(string watchedDirectory)
        {
            Ensure.NotNullOrEmpty(watchedDirectory, nameof(watchedDirectory));

            _watchedDirectory = new DirectoryInfo(watchedDirectory);
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
            _knownEntities.Clear();

            ForeachEntityInDirectory(_watchedDirectory, fileInfo =>
            {
                _knownEntities.Add(fileInfo.FullName, new FileMeta(fileInfo, foundAgain: false));
            });
        }

        private void CheckForChangedFiles()
        {
            _changes.Clear();

            ForeachEntityInDirectory(_watchedDirectory, fileInfo =>
            {
                var fullFilePath = fileInfo.FullName;

                if (!_knownEntities.ContainsKey(fullFilePath))
                {
                    // New file or directory
                    RecordChange(fileInfo, ChangeKind.Add);
                }
                else
                {
                    var fileMeta = _knownEntities[fullFilePath];

                    try
                    {
                        if (!fileMeta.FileInfo.Attributes.HasFlag(FileAttributes.Directory) &&
                            fileMeta.FileInfo.LastWriteTime != fileInfo.LastWriteTime)
                        {
                            // File changed
                            RecordChange(fileInfo, ChangeKind.Update);
                        }

                        _knownEntities[fullFilePath] = new FileMeta(fileMeta.FileInfo, foundAgain: true);
                    }
                    catch (FileNotFoundException)
                    {
                        _knownEntities[fullFilePath] = new FileMeta(fileMeta.FileInfo, foundAgain: false);
                    }
                }

                _tempDictionary.Add(fileInfo.FullName, new FileMeta(fileInfo, foundAgain: false));
            });

            foreach (var file in _knownEntities)
            {
                if (!file.Value.FoundAgain)
                {
                    // File or directory deleted
                    RecordChange(file.Value.FileInfo, ChangeKind.Delete);
                }
            }

            NotifyChanges();

            // Swap the two dictionaries
            (_tempDictionary, _knownEntities) = (_knownEntities, _tempDictionary);
            _tempDictionary.Clear();
        }

        private void RecordChange(FileSystemInfo fileInfo, ChangeKind kind)
        {
            if (_changes.ContainsKey(fileInfo.FullName) ||
                fileInfo.FullName.Equals(_watchedDirectory.FullName, StringComparison.Ordinal))
            {
                return;
            }

            _changes.Add(fileInfo.FullName, kind);

            if (fileInfo is FileInfo { Directory: { } directory })
            {
                RecordChange(directory, ChangeKind.Update);
            }
            else if (fileInfo is DirectoryInfo { Parent: { } parent })
            {
                RecordChange(parent, ChangeKind.Update);
            }
        }

        private static void ForeachEntityInDirectory(DirectoryInfo dirInfo, Action<FileSystemInfo> fileAction)
        {
            if (!dirInfo.Exists)
            {
                return;
            }

            IEnumerable<FileSystemInfo> entities;
            try
            {
                entities = dirInfo.EnumerateFileSystemInfos("*.*");
            }
            // If the directory is deleted after the exists check this will throw and could crash the process
            catch (DirectoryNotFoundException)
            {
                return;
            }

            foreach (var entity in entities)
            {
                fileAction(entity);

                if (entity is DirectoryInfo subdirInfo)
                {
                    ForeachEntityInDirectory(subdirInfo, fileAction);
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

                OnFileChange?.Invoke(this, (path, kind));
            }
        }

        private readonly struct FileMeta(FileSystemInfo fileInfo, bool foundAgain)
        {
            public readonly FileSystemInfo FileInfo = fileInfo;
            public readonly bool FoundAgain = foundAgain;
        }
    }
}
