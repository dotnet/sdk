// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.DotNet.Watch.UnitTests
{
    public class FileWatcherTests(ITestOutputHelper output)
    {
        private readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
        private readonly TimeSpan NegativeTimeout = TimeSpan.FromSeconds(5);
        private readonly TestAssetsManager _testAssetManager = new TestAssetsManager(output);

        private async Task TestOperation(
            string dir,
            ChangedPath[] expectedChanges,
            bool usePolling,
            bool watchSubdirectories,
            Action operation,
            ImmutableHashSet<string> watchedFileNames = null)
        {
            using var watcher = DirectoryWatcher.Create(dir, watchedFileNames ?? [], usePolling, includeSubdirectories: watchSubdirectories);
            if (watcher is EventBasedDirectoryWatcher dotnetWatcher)
            {
                dotnetWatcher.Logger = m => output.WriteLine(m);
            }

            var operationCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var filesChanged = new HashSet<ChangedPath>();

            EventHandler<ChangedPath> handler = null;
            handler = (_, f) =>
            {
                if (filesChanged.Add(f))
                {
                    output.WriteLine($"Observed new {f.Kind}: '{f.Path}' ({filesChanged.Count} out of {expectedChanges.Length})");
                }
                else
                {
                    output.WriteLine($"Already seen {f.Kind}: '{f.Path}'");
                }

                if (filesChanged.Count == expectedChanges.Length)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.OnFileChange -= handler;
                    operationCompletionSource.TrySetResult();
                }
            };

            watcher.OnFileChange += handler;
            watcher.EnableRaisingEvents = true;

            if (usePolling)
            {
                // On Unix the file write time is in 1s increments;
                // if we don't wait, there's a chance that the polling
                // watcher will not detect the change
                await Task.Delay(1000);
            }

            operation();

            var task = operationCompletionSource.Task;
            await (Debugger.IsAttached ? task : task.TimeoutAfter(DefaultTimeout));

            AssertEx.SequenceEqual(expectedChanges, filesChanged.OrderBy(x => x.Path));
        }

        private sealed class TestFileWatcher(IReporter reporter)
            : FileWatcher(reporter, TestOptions.GetEnvironmentOptions())
        {
            public IReadOnlyDictionary<string, DirectoryWatcher> DirectoryTreeWatchers => _directoryTreeWatchers;
            public IReadOnlyDictionary<string, DirectoryWatcher> DirectoryWatchers => _directoryWatchers;

            protected override DirectoryWatcher CreateDirectoryWatcher(string directory, ImmutableHashSet<string> fileNames, bool includeSubdirectories)
                => new TestDirectoryWatcher(directory, fileNames, includeSubdirectories);
        }

        private sealed class TestDirectoryWatcher(string watchedDirectory, ImmutableHashSet<string> watchedFileNames, bool includeSubdirectories)
            : DirectoryWatcher(watchedDirectory, watchedFileNames, includeSubdirectories)
        {
            public override bool EnableRaisingEvents { get; set; }
            public override void Dispose() { }
        }

        private static IEnumerable<string> Inspect(IReadOnlyDictionary<string, DirectoryWatcher> watchers)
            => watchers.OrderBy(w => w.Key).Select(w => $"{w.Key.TrimEnd('\\', '/')}: [{string.Join(',', w.Value.WatchedFileNames.Order())}]");

        [Fact]
        public void DirectoryWatcherMerging()
        {
            var watcher = new TestFileWatcher(NullReporter.Singleton);
            string root = TestContext.Current.TestExecutionDirectory;

            var dirA = Path.Combine(root, "A");
            var dirB = Path.Combine(root, "B");
            var dirBC = Path.Combine(dirB, "C");
            var a1 = Path.Combine(dirA, "a1");
            var a2 = Path.Combine(dirA, "a2");
            var a3 = Path.Combine(dirA, "a3");
            var a4 = Path.Combine(dirA, "a4");
            var b1 = Path.Combine(dirB, "b1");
            var b2 = Path.Combine(dirB, "b2");
            var bc1 = Path.Combine(dirBC, "bc1");

            watcher.WatchFiles([a1, a2]);

            AssertEx.Empty(watcher.DirectoryTreeWatchers);
            AssertEx.SequenceEqual([$"{dirA}: [a1,a2]"], Inspect(watcher.DirectoryWatchers));

            watcher.WatchFiles([a1, a2, a3]);
            AssertEx.Empty(watcher.DirectoryTreeWatchers);
            AssertEx.SequenceEqual([$"{dirA}: [a1,a2,a3]"], Inspect(watcher.DirectoryWatchers));

            watcher.WatchContainingDirectories([a1, a2], includeSubdirectories: false);
            AssertEx.Empty(watcher.DirectoryTreeWatchers);
            AssertEx.SequenceEqual([$"{dirA}: []"], Inspect(watcher.DirectoryWatchers));

            watcher.WatchFiles([a4]);
            AssertEx.Empty(watcher.DirectoryTreeWatchers);
            AssertEx.SequenceEqual([$"{dirA}: []"], Inspect(watcher.DirectoryWatchers));

            watcher.WatchFiles([b1, bc1]);
            AssertEx.Empty(watcher.DirectoryTreeWatchers);
            AssertEx.SequenceEqual(
            [
                $"{dirA}: []",
                $"{dirB}: [b1]",
                $"{dirBC}: [bc1]",
            ], Inspect(watcher.DirectoryWatchers));

            watcher.WatchContainingDirectories([b2], includeSubdirectories: true);
            AssertEx.SequenceEqual([$"{dirB}: []"], Inspect(watcher.DirectoryTreeWatchers));
            AssertEx.SequenceEqual([$"{dirA}: []"], Inspect(watcher.DirectoryWatchers));

            watcher.WatchFiles([a1, b1, bc1]);
            watcher.WatchContainingDirectories([bc1], includeSubdirectories: true);
            AssertEx.SequenceEqual([$"{dirB}: []"], Inspect(watcher.DirectoryTreeWatchers));
            AssertEx.SequenceEqual([$"{dirA}: []"], Inspect(watcher.DirectoryWatchers));
        }

        [Theory]
        [CombinatorialData]
        public async Task NewFile(bool usePolling)
        {
            var dir = _testAssetManager.CreateTestDirectory(identifier: usePolling.ToString()).Path;

            var file = Path.Combine(dir, "file");

            await TestOperation(
                dir,
                expectedChanges: !RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || usePolling
                ?
                [
                    new(file, ChangeKind.Add),
                ]
                :
                [
                    new(file, ChangeKind.Update),
                    new(file, ChangeKind.Add),
                ],
                usePolling,
                watchSubdirectories: true,
                () => File.WriteAllText(file, string.Empty));
        }

        [Theory]
        [CombinatorialData]
        public async Task NewFileInNewDirectory(bool usePolling, bool nested)
        {
            if (!usePolling && !(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)))
            {
                // Skip test on Unix:
                // https://github.com/dotnet/runtime/issues/116351
                return;
            }

            var dir = _testAssetManager.CreateTestDirectory(identifier: usePolling.ToString()).Path;

            var dir1 = Path.Combine(dir, "dir1");
            var dir2 = nested ? Path.Combine(dir1, "dir2") : dir1;
            var fileInSubdir = Path.Combine(dir2, "file_in_subdir");

            await TestOperation(
                dir,
                expectedChanges: !RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || usePolling
                ?
                [
                    new(fileInSubdir, ChangeKind.Add),
                ]
                :
                [
                    new(fileInSubdir, ChangeKind.Update),
                    new(fileInSubdir, ChangeKind.Add),
                ],
                usePolling,
                watchSubdirectories: true,
                () =>
                {
                    Directory.CreateDirectory(dir1);

                    if (nested)
                    {
                        Directory.CreateDirectory(dir2);
                    }

                    File.WriteAllText(fileInSubdir, string.Empty);
                });
        }

        [Theory]
        [CombinatorialData]
        public async Task ChangeFile(bool usePolling)
        {
            var dir = _testAssetManager.CreateTestDirectory(identifier: usePolling.ToString()).Path;

            var file = Path.Combine(dir, "file");
            File.WriteAllText(file, string.Empty);

            await TestOperation(
                dir,
                expectedChanges: [new(file, ChangeKind.Update)],
                usePolling,
                watchSubdirectories: true,
                () => File.WriteAllText(file, string.Empty));
        }

        [Theory]
        [CombinatorialData]
        public async Task MoveFile(bool usePolling)
        {
            var dir = _testAssetManager.CreateTestDirectory(identifier: usePolling.ToString()).Path;
            var srcFile = Path.Combine(dir, "file");
            var dstFile = Path.Combine(dir, "file2");

            File.WriteAllText(srcFile, string.Empty);

            await TestOperation(
                dir,
                expectedChanges: RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && !usePolling ?
                [
                    // On OSX events from before we started observing are reported as well.
                    new(srcFile, ChangeKind.Update),
                    new(srcFile, ChangeKind.Add),
                    new(srcFile, ChangeKind.Delete),
                    new(dstFile, ChangeKind.Add),
                ]
                :
                [
                    new(srcFile, ChangeKind.Delete),
                    new(dstFile, ChangeKind.Add),
                ],
                usePolling,
                watchSubdirectories: true,
                () => File.Move(srcFile, dstFile));
        }

        [Theory]
        [CombinatorialData]
        public async Task FileInSubdirectory(bool usePolling, bool watchSubdirectories)
        {
            var dir = _testAssetManager.CreateTestDirectory(identifier: $"{usePolling}{watchSubdirectories}").Path;

            var subdir = Path.Combine(dir, "subdir");
            Directory.CreateDirectory(subdir);

            var fileInDir = Path.Combine(dir, "file_in_dir");
            File.WriteAllText(fileInDir, string.Empty);

            var fileInSubdir = Path.Combine(subdir, "file_in_subdir");
            File.WriteAllText(fileInSubdir, string.Empty);

            ChangedPath[] expectedChanges;

            if (watchSubdirectories)
            {
                expectedChanges = !RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || usePolling ?
                [
                    new(fileInDir, ChangeKind.Update),
                    new(fileInSubdir, ChangeKind.Update)
                ]
                :
                [
                    new(fileInDir, ChangeKind.Update),
                    new(fileInDir, ChangeKind.Add),
                    new(fileInSubdir, ChangeKind.Update),
                    new(fileInSubdir, ChangeKind.Add),
                ];
            }
            else
            {
                expectedChanges =
                [
                    new(fileInDir, ChangeKind.Update),
                ];
            }

            await TestOperation(
                dir,
                expectedChanges,
                usePolling,
                watchSubdirectories,
                () =>
                {
                    File.WriteAllText(fileInSubdir, string.Empty);
                    File.WriteAllText(fileInDir, string.Empty);
                });
        }

        [Theory]
        [CombinatorialData]
        public async Task NoNotificationIfDisabled(bool usePolling)
        {
            var dir = _testAssetManager.CreateTestDirectory(identifier: usePolling.ToString()).Path;

            using var watcher = DirectoryWatcher.Create(dir, watchedFileNames: [], usePolling, includeSubdirectories: true);

            var changedEv = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            watcher.OnFileChange += (_, f) => changedEv.TrySetResult(0);

            // Disable
            watcher.EnableRaisingEvents = false;

            var testFileFullPath = Path.Combine(dir, "foo");

            if (usePolling)
            {
                // On Unix the file write time is in 1s increments;
                // if we don't wait, there's a chance that the polling
                // watcher will not detect the change
                await Task.Delay(1000);
            }
            File.WriteAllText(testFileFullPath, string.Empty);

            await Assert.ThrowsAsync<TimeoutException>(() => changedEv.Task.TimeoutAfter(NegativeTimeout));
        }

        [Theory]
        [CombinatorialData]
        public async Task DisposedNoEvents(bool usePolling)
        {
            var dir = _testAssetManager.CreateTestDirectory(identifier: usePolling.ToString()).Path;
            var changedEv = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using (var watcher = DirectoryWatcher.Create(dir, watchedFileNames: [], usePolling, includeSubdirectories: true))
            {
                watcher.OnFileChange += (_, f) => changedEv.TrySetResult();
                watcher.EnableRaisingEvents = true;
            }

            var file = Path.Combine(dir, "file");

            if (usePolling)
            {
                // On Unix the file write time is in 1s increments;
                // if we don't wait, there's a chance that the polling
                // watcher will not detect the change
                await Task.Delay(1000);
            }
            File.WriteAllText(file, string.Empty);

            await Assert.ThrowsAsync<TimeoutException>(() => changedEv.Task.TimeoutAfter(NegativeTimeout));
        }

        [Theory]
        [CombinatorialData]
        public async Task MultipleFiles(bool usePolling)
        {
            var dir = _testAssetManager.CreateTestDirectory(identifier: usePolling.ToString()).Path;

            var file1 = Path.Combine(dir, "a1");
            var file2 = Path.Combine(dir, "a2");
            var file3 = Path.Combine(dir, "a3");
            var file4 = Path.Combine(dir, "a4");

            File.WriteAllText(file1, string.Empty);
            File.WriteAllText(file2, string.Empty);
            File.WriteAllText(file3, string.Empty);
            File.WriteAllText(file4, string.Empty);

            await TestOperation(
                dir,
                expectedChanges: [new(file3, ChangeKind.Update)],
                usePolling,
                watchSubdirectories: true,
                () =>
                {
                    File.WriteAllText(file2, string.Empty);
                    File.WriteAllText(file3, string.Empty);
                },
                watchedFileNames: ["a3"]);
        }

        [Theory]
        [CombinatorialData]
        public async Task MultipleTriggers(bool usePolling)
        {
            var dir = _testAssetManager.CreateTestDirectory(identifier: usePolling.ToString()).Path;

            using var watcher = DirectoryWatcher.Create(dir, watchedFileNames: [], usePolling, includeSubdirectories: true);

            watcher.EnableRaisingEvents = true;

            for (var i = 0; i < 5; i++)
            {
                await AssertFileChangeRaisesEvent(dir, watcher);
            }

            watcher.EnableRaisingEvents = false;
        }

        private async Task AssertFileChangeRaisesEvent(string directory, DirectoryWatcher watcher)
        {
            var changedEv = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var expectedPath = Path.Combine(directory, Path.GetRandomFileName());
            EventHandler<ChangedPath> handler = (_, f) =>
            {
                output.WriteLine("File changed: " + f);
                try
                {
                    if (string.Equals(f.Path, expectedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        changedEv.TrySetResult(0);
                    }
                }
                catch (ObjectDisposedException)
                {
                    // There's a known race condition here:
                    // even though we tell the watcher to stop raising events and we unsubscribe the handler
                    // there might be in-flight events that will still process. Since we dispose the reset
                    // event, this code will fail if the handler executes after Dispose happens.
                }
            };

            File.AppendAllText(expectedPath, " ");

            watcher.OnFileChange += handler;
            try
            {
                // On Unix the file write time is in 1s increments;
                // if we don't wait, there's a chance that the polling
                // watcher will not detect the change
                await Task.Delay(1000);
                File.AppendAllText(expectedPath, " ");
                await changedEv.Task.TimeoutAfter(DefaultTimeout);
            }
            finally
            {
                watcher.OnFileChange -= handler;
            }
        }

        [Theory]
        [CombinatorialData]
        public async Task DeleteSubfolder(bool usePolling)
        {
            var dir = _testAssetManager.CreateTestDirectory(usePolling.ToString()).Path;

            var subdir = Path.Combine(dir, "subdir");
            Directory.CreateDirectory(subdir);

            var f1 = Path.Combine(subdir, "foo1");
            var f2 = Path.Combine(subdir, "foo2");
            var f3 = Path.Combine(subdir, "foo3");

            File.WriteAllText(f1, string.Empty);
            File.WriteAllText(f2, string.Empty);
            File.WriteAllText(f3, string.Empty);

            await TestOperation(
                dir,
                expectedChanges: RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && !usePolling ?
                [
                    new(f1, ChangeKind.Update),
                    new(f1, ChangeKind.Add),
                    new(f1, ChangeKind.Delete),
                    new(f2, ChangeKind.Update),
                    new(f2, ChangeKind.Add),
                    new(f2, ChangeKind.Delete),
                    new(f3, ChangeKind.Update),
                    new(f3, ChangeKind.Add),
                    new(f3, ChangeKind.Delete),
                ]
                :
                [
                    new(f1, ChangeKind.Delete),
                    new(f2, ChangeKind.Delete),
                    new(f3, ChangeKind.Delete),
                ],
                usePolling,
                watchSubdirectories: true,
                () => Directory.Delete(subdir, recursive: true));
        }
    }
}
