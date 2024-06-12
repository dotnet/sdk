// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Internal
{
    internal sealed class FileSetWatcher(FileSet fileSet, IReporter reporter) : IDisposable
    {
        private readonly FileWatcher _fileWatcher = new(reporter);

        public async Task<FileItem?> GetChangedFileAsync(Action startedWatching, CancellationToken cancellationToken)
        {
            foreach (var file in fileSet)
            {
                _fileWatcher.WatchDirectory(Path.GetDirectoryName(file.FilePath));
            }

            var tcs = new TaskCompletionSource<FileItem?>(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => tcs.TrySetResult(null));

            void FileChangedCallback(string path, bool newFile)
            {
                if (fileSet.TryGetValue(path, out var fileItem))
                {
                    tcs.TrySetResult(fileItem);
                }
            }

            _fileWatcher.OnFileChange += FileChangedCallback;
            startedWatching();
            var changedFile = await tcs.Task;
            _fileWatcher.OnFileChange -= FileChangedCallback;

            return changedFile;
        }

        public Task<FileItem?> GetChangedFileAsync(CancellationToken cancellationToken)
            => GetChangedFileAsync(() => { }, cancellationToken);

        public void Dispose()
            => _fileWatcher.Dispose();
    }
}
