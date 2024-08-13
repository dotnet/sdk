// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Internal
{
    internal class PollingFileWatcher : IFileSystemWatcher
    {
        // The minimum interval to rerun the scan
        private static readonly TimeSpan _minRunInternal = TimeSpan.FromSeconds(.5);

        private readonly DirectoryInfo _watchedDirectory;

        private Dictionary<string, FileMeta> _knownEntities = new();
        private Dictionary<string, FileMeta> _tempDictionary = new();
        private Dictionary<string, bool> _changes = new();

        private Thread _pollingThread;
        private bool _raiseEvents;

        private bool _disposed;

        public PollingFileWatcher(string watchedDirectory)
        {
            Ensure.NotNullOrEmpty(watchedDirectory, nameof(watchedDirectory));

            _watchedDirectory = new DirectoryInfo(watchedDirectory);
            BasePath = _watchedDirectory.FullName;

            _pollingThread = new Thread(new ThreadStart(PollingLoop))
            {
                IsBackground = true,
                Name = nameof(PollingFileWatcher)
            };

            CreateKnownFilesSnapshot();

            _pollingThread.Start();
        }

        public event EventHandler<(string filePath, bool newFile)>? OnFileChange;

#pragma warning disable CS0067 // not used
        public event EventHandler<Exception>? OnError;
#pragma warning restore

        public string BasePath { get; }

        public bool EnableRaisingEvents
        {
            get => _raiseEvents;
            set
            {
                EnsureNotDisposed();
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

            ForeachEntityInDirectory(_watchedDirectory, f =>
            {
                _knownEntities.Add(f.FullName, new FileMeta(f));
            });
        }

        private void CheckForChangedFiles()
        {
            _changes.Clear();

            ForeachEntityInDirectory(_watchedDirectory, f =>
            {
                var fullFilePath = f.FullName;

                if (!_knownEntities.ContainsKey(fullFilePath))
                {
                    // New file
                    RecordChange(f, isNewFile: true);
                }
                else
                {
                    var fileMeta = _knownEntities[fullFilePath];

                    try
                    {
                        if (fileMeta.FileInfo.LastWriteTime != f.LastWriteTime)
                        {
                            // File changed
                            RecordChange(f, isNewFile: false);
                        }

                        _knownEntities[fullFilePath] = new FileMeta(fileMeta.FileInfo, foundAgain: true);
                    }
                    catch (FileNotFoundException)
                    {
                        _knownEntities[fullFilePath] = new FileMeta(fileMeta.FileInfo, foundAgain: false);
                    }
                }

                _tempDictionary.Add(f.FullName, new FileMeta(f));
            });

            foreach (var file in _knownEntities)
            {
                if (!file.Value.FoundAgain)
                {
                    // File deleted
                    RecordChange(file.Value.FileInfo, isNewFile: false);
                }
            }

            NotifyChanges();

            // Swap the two dictionaries
            (_tempDictionary, _knownEntities) = (_knownEntities, _tempDictionary);
            _tempDictionary.Clear();
        }

        private void RecordChange(FileSystemInfo fileInfo, bool isNewFile)
        {
            if (_changes.ContainsKey(fileInfo.FullName) ||
                fileInfo.FullName.Equals(_watchedDirectory.FullName, StringComparison.Ordinal))
            {
                return;
            }

            _changes.Add(fileInfo.FullName, isNewFile);

            if (fileInfo is FileInfo { Directory: { } directory })
            {
                RecordChange(directory, isNewFile: false);
            }
            else if (fileInfo is DirectoryInfo { Parent: { } parent })
            {
                RecordChange(parent, isNewFile: false);
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
            foreach (var (path, isNewFile) in _changes)
            {
                if (_disposed || !_raiseEvents)
                {
                    break;
                }

                OnFileChange?.Invoke(this, (path, isNewFile));
            }
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PollingFileWatcher));
            }
        }

        public void Dispose()
        {
            EnableRaisingEvents = false;
            _disposed = true;
        }

        private struct FileMeta
        {
            public FileMeta(FileSystemInfo fileInfo, bool foundAgain = false)
            {
                FileInfo = fileInfo;
                FoundAgain = foundAgain;
            }

            public FileSystemInfo FileInfo;

            public bool FoundAgain;
        }
    }
}
