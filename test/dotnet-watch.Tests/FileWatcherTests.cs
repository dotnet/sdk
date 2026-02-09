// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
            Action operation)
        {
            using var watcher = FileWatcherFactory.CreateWatcher(dir, usePolling);
            if (watcher is EventBasedDirectoryWatcher dotnetWatcher)
            {
                dotnetWatcher.Logger = m => output.WriteLine(m);
            }

            var changedEv = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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
                    changedEv.TrySetResult();
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

            await changedEv.Task.TimeoutAfter(DefaultTimeout);
            AssertEx.SequenceEqual(expectedChanges, filesChanged.Order(Comparer<ChangedPath>.Create((x, y) => (x.Path, x.Kind).CompareTo((y.Path, y.Kind)))));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task NewFile(bool usePolling)
        {
            var dir = _testAssetManager.CreateTestDirectory(identifier: usePolling.ToString()).Path;

            var testFileFullPath = Path.Combine(dir, "foo");

            await TestOperation(
                dir,
                expectedChanges: !RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !usePolling
                ?
                [
                    new(testFileFullPath, ChangeKind.Update),
                    new(testFileFullPath, ChangeKind.Add),
                ]
                :
                [
                    new(testFileFullPath, ChangeKind.Add),
                ],
                usePolling,
                () => File.WriteAllText(testFileFullPath, string.Empty));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task NewFileInNewDirectory(bool usePolling)
        {
            var dir = _testAssetManager.CreateTestDirectory(identifier: usePolling.ToString()).Path;

            var newDir = Path.Combine(dir, "Dir");
            var newFile = Path.Combine(newDir, "foo");

            await TestOperation(
                dir,
                expectedChanges: RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && !usePolling
                ?
                [
                    new(newDir, ChangeKind.Add),
                    new(newFile, ChangeKind.Update),
                    new(newFile, ChangeKind.Add),
                ]
                :
                [
                    new(newDir, ChangeKind.Add),
                ],
                usePolling,
                () =>
                {
                    Directory.CreateDirectory(newDir);
                    File.WriteAllText(newFile, string.Empty);
                });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ChangeFile(bool usePolling)
        {
            var dir = _testAssetManager.CreateTestDirectory(identifier: usePolling.ToString()).Path;

            var testFileFullPath = Path.Combine(dir, "foo");
            File.WriteAllText(testFileFullPath, string.Empty);

            await TestOperation(
                dir,
                expectedChanges: [new(testFileFullPath, ChangeKind.Update)],
                usePolling,
                () => File.WriteAllText(testFileFullPath, string.Empty));
        }

        [Theory]
        [CombinatorialData]
        public async Task MoveFile(bool usePolling)
        {
            var dir = _testAssetManager.CreateTestDirectory(identifier: usePolling.ToString()).Path;
            var srcFile = Path.Combine(dir, "foo");
            var dstFile = Path.Combine(dir, "foo2");

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
                () => File.Move(srcFile, dstFile));

        }

        [Fact]
        public async Task FileInSubdirectory()
        {
            var dir = _testAssetManager.CreateTestDirectory().Path;

            var subdir = Path.Combine(dir, "subdir");
            Directory.CreateDirectory(subdir);

            var testFileFullPath = Path.Combine(subdir, "foo");
            File.WriteAllText(testFileFullPath, string.Empty);

            await TestOperation(
                dir,
                expectedChanges:
                [
                    new(subdir, ChangeKind.Update),
                    new(testFileFullPath, ChangeKind.Update)
                ],
                usePolling: true,
                () => File.WriteAllText(testFileFullPath, string.Empty));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task NoNotificationIfDisabled(bool usePolling)
        {
            var dir = _testAssetManager.CreateTestDirectory(identifier: usePolling.ToString()).Path;

            using var watcher = FileWatcherFactory.CreateWatcher(dir, usePolling);

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
        [InlineData(true)]
        [InlineData(false)]
        public async Task DisposedNoEvents(bool usePolling)
        {
            var dir = _testAssetManager.CreateTestDirectory(identifier: usePolling.ToString()).Path;
            var changedEv = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using (var watcher = FileWatcherFactory.CreateWatcher(dir, usePolling))
            {
                watcher.OnFileChange += (_, f) => changedEv.TrySetResult();
                watcher.EnableRaisingEvents = true;
            }

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
        [InlineData(true)]
        [InlineData(false)]
        public async Task MultipleFiles(bool usePolling)
        {
            var dir = _testAssetManager.CreateTestDirectory(identifier: usePolling.ToString()).Path;

            File.WriteAllText(Path.Combine(dir, "foo1"), string.Empty);
            File.WriteAllText(Path.Combine(dir, "foo2"), string.Empty);
            File.WriteAllText(Path.Combine(dir, "foo3"), string.Empty);
            File.WriteAllText(Path.Combine(dir, "foo4"), string.Empty);

            // On Unix the native file watcher may surface events from
            // the recent past. Delay to avoid those.
            // On Unix the file write time is in 1s increments;
            // if we don't wait, there's a chance that the polling
            // watcher will not detect the change
            await Task.Delay(1250);

            var testFileFullPath = Path.Combine(dir, "foo3");

            await TestOperation(
                dir,
                expectedChanges: [new(testFileFullPath, ChangeKind.Update)],
                usePolling: true,
                () => File.WriteAllText(testFileFullPath, string.Empty));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task MultipleTriggers(bool usePolling)
        {
            var dir = _testAssetManager.CreateTestDirectory(identifier: usePolling.ToString()).Path;

            using var watcher = FileWatcherFactory.CreateWatcher(dir, usePolling);

            watcher.EnableRaisingEvents = true;

            for (var i = 0; i < 5; i++)
            {
                await AssertFileChangeRaisesEvent(dir, watcher);
            }

            watcher.EnableRaisingEvents = false;
        }

        private async Task AssertFileChangeRaisesEvent(string directory, IDirectoryWatcher watcher)
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
        [InlineData(true)]
        [InlineData(false)]
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
                expectedChanges: usePolling ?
                [
                    new(subdir, ChangeKind.Delete),
                    new(f1, ChangeKind.Delete),
                    new(f2, ChangeKind.Delete),
                    new(f3, ChangeKind.Delete),
                ]
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
                [
                    new(subdir, ChangeKind.Add),
                    new(subdir, ChangeKind.Delete),
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
                : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
                [
                    new(subdir, ChangeKind.Update),
                    new(subdir, ChangeKind.Delete),
                    new(f1, ChangeKind.Delete),
                    new(f2, ChangeKind.Delete),
                    new(f3, ChangeKind.Delete),
                ]
                :
                [
                    new(subdir, ChangeKind.Delete),
                    new(f1, ChangeKind.Delete),
                    new(f2, ChangeKind.Delete),
                    new(f3, ChangeKind.Delete),
                ],
                usePolling,
                () => Directory.Delete(subdir, recursive: true));
        }
    }
}
