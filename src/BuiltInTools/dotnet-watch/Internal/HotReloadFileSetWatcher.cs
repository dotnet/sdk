// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Internal
{
    internal sealed class HotReloadFileSetWatcher(IReadOnlyDictionary<string, FileItem> fileSet, DateTime buildCompletionTime, IReporter reporter, TestFlags testFlags) : IDisposable
    {
        private static readonly TimeSpan s_debounceInterval = TimeSpan.FromMilliseconds(50);
        private static readonly DateTime s_fileNotExistFileTime = DateTime.FromFileTime(0);

        private readonly FileWatcher _fileWatcher = new(fileSet, reporter);
        private readonly object _changedFilesLock = new();
        private readonly ConcurrentDictionary<string, ChangedFile> _changedFiles = new(StringComparer.Ordinal);

        private TaskCompletionSource<ChangedFile[]?>? _tcs;
        private bool _initialized;
        private bool _disposed;

        public void Dispose()
        {
            _disposed = true;
            _fileWatcher.Dispose();
        }

        public void UpdateBuildCompletionTime(DateTime value)
        {
            lock (_changedFilesLock)
            {
                buildCompletionTime = value;
                _changedFiles.Clear();
            }
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            _fileWatcher.StartWatching();
            _fileWatcher.OnFileChange += FileChangedCallback;

            var waitingForChanges = MessageDescriptor.WaitingForChanges;
            if (testFlags.HasFlag(TestFlags.ElevateWaitingForChangesMessageSeverity))
            {
                waitingForChanges = waitingForChanges with { Severity = MessageSeverity.Output };
            }

            reporter.Report(waitingForChanges);

            Task.Factory.StartNew(async () =>
            {
                // Debounce / polling loop
                while (!_disposed)
                {
                    await Task.Delay(s_debounceInterval);
                    if (_changedFiles.IsEmpty)
                    {
                        continue;
                    }

                    var tcs = Interlocked.Exchange(ref _tcs, null!);
                    if (tcs is null)
                    {
                        continue;
                    }

                    ChangedFile[] changedFiles;
                    lock (_changedFilesLock)
                    {
                        changedFiles = _changedFiles.Values.ToArray();
                        _changedFiles.Clear();
                    }

                    if (changedFiles is [])
                    {
                        continue;
                    }

                    tcs.TrySetResult(changedFiles);
                }

            }, default, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            void FileChangedCallback(string path, ChangeKind kind)
            {
                // only handle file changes:
                if (Directory.Exists(path))
                {
                    return;
                }

                if (kind != ChangeKind.Delete)
                {
                    try
                    {
                        // Do not report changes to files that happened during build:
                        var creationTime = File.GetCreationTimeUtc(path);
                        var writeTime = File.GetLastWriteTimeUtc(path);

                        if (creationTime == s_fileNotExistFileTime || writeTime == s_fileNotExistFileTime)
                        {
                            // file might have been deleted since we received the event
                            kind = ChangeKind.Delete;
                        }
                        else if (creationTime.Ticks < buildCompletionTime.Ticks && writeTime.Ticks < buildCompletionTime.Ticks)
                        {
                            reporter.Verbose(
                                $"Ignoring file change during build: {kind} '{path}' " +
                                $"(created {FormatTimestamp(creationTime)} and written {FormatTimestamp(writeTime)} before {FormatTimestamp(buildCompletionTime)}).");

                            return;
                        }
                        else if (writeTime > creationTime)
                        {
                            reporter.Verbose($"File change: {kind} '{path}' (written {FormatTimestamp(writeTime)} after {FormatTimestamp(buildCompletionTime)}).");
                        }
                        else
                        {
                            reporter.Verbose($"File change: {kind} '{path}' (created {FormatTimestamp(creationTime)} after {FormatTimestamp(buildCompletionTime)}).");
                        }
                    }
                    catch (Exception e)
                    {
                        reporter.Verbose($"Ignoring file '{path}' due to access error: {e.Message}.");
                        return;
                    }
                }

                if (kind == ChangeKind.Delete)
                {
                    reporter.Verbose($"File '{path}' deleted after {FormatTimestamp(buildCompletionTime)}.");
                }

                if (kind == ChangeKind.Add)
                {
                    lock (_changedFilesLock)
                    {
                        _changedFiles.TryAdd(path, new ChangedFile(new FileItem { FilePath = path }, kind));
                    }
                }
                else if (fileSet.TryGetValue(path, out var fileItem))
                {
                    lock (_changedFilesLock)
                    {
                        _changedFiles.TryAdd(path, new ChangedFile(fileItem, kind));
                    }
                }
            }
        }

        public Task<ChangedFile[]?> GetChangedFilesAsync(CancellationToken cancellationToken, bool forceWaitForNewUpdate = false)
        {
            EnsureInitialized();

            var tcs = _tcs;
            if (!forceWaitForNewUpdate && tcs is not null)
            {
                return tcs.Task;
            }

            _tcs = tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => tcs.TrySetResult(null));
            return tcs.Task;
        }

        internal static string FormatTimestamp(DateTime time)
            => time.ToString("HH:mm:ss.fffffff");
    }
}
