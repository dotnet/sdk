﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Internal
{
    public class FileSetWatcher : IDisposable
    {
        private readonly FileWatcher _fileWatcher;
        private readonly FileSet _fileSet;

        public FileSetWatcher(FileSet fileSet, IReporter reporter)
        {
            Ensure.NotNull(fileSet, nameof(fileSet));

            _fileSet = fileSet;
            _fileWatcher = new FileWatcher(reporter);
        }

        public bool WatchForNewFiles { get; init; }

        public async Task<FileItem?> GetChangedFileAsync(CancellationToken cancellationToken, Action startedWatching)
        {
            foreach (var file in _fileSet)
            {
                _fileWatcher.WatchDirectory(Path.GetDirectoryName(file.FilePath));
            }

            var tcs = new TaskCompletionSource<FileItem?>(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => tcs.TrySetResult(null));

            void FileChangedCallback(string path, bool newFile)
            {
                if (_fileSet.TryGetValue(path, out var fileItem))
                {
                    tcs.TrySetResult(fileItem);
                }
                else if (WatchForNewFiles && newFile)
                {
                    tcs.TrySetResult(new FileItem { FilePath = path, IsNewFile = newFile });
                }
            }

            _fileWatcher.OnFileChange += FileChangedCallback;
            startedWatching();
            var changedFile = await tcs.Task;
            _fileWatcher.OnFileChange -= FileChangedCallback;

            return changedFile;
        }

        public Task<FileItem?> GetChangedFileAsync(CancellationToken cancellationToken)
        {
            return GetChangedFileAsync(cancellationToken, () => { });
        }

        public void Dispose()
        {
            _fileWatcher.Dispose();
        }
    }
}
