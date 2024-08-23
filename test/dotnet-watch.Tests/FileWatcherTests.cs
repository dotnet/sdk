// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Testing;
using Microsoft.DotNet.Watcher.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    public class FileWatcherTests
    {
        public FileWatcherTests(ITestOutputHelper output)
        {
            _output = output;
            _testAssetManager = new TestAssetsManager(output);
        }

        private readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
        private readonly TimeSpan NegativeTimeout = TimeSpan.FromSeconds(5);
        private readonly ITestOutputHelper _output;
        private readonly TestAssetsManager _testAssetManager;

        private async Task TestOperation(
            string dir,
            (string, bool)[] expectedChanges,
            bool usePolling,
            Action operation)
        {
            // On Unix the native file watcher may surface events from
            // the recent past. Delay to avoid those.
            // On Unix the file write time is in 1s increments;
            // if we don't wait, there's a chance that the polling
            // watcher will not detect the change
            await Task.Delay(1250);

            using var watcher = FileWatcherFactory.CreateWatcher(dir, usePolling);
            if (watcher is DotnetFileWatcher dotnetWatcher)
            {
                dotnetWatcher.Logger = m => _output.WriteLine(m);
            }

            var changedEv = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var filesChanged = new HashSet<(string, bool)>();

            var testFileFullPath = Path.Combine(dir, "foo");

            EventHandler<(string path, bool newFile)> handler = null;
            handler = (_, f) =>
            {
                filesChanged.Add(f);

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
            AssertEx.SequenceEqual(expectedChanges, filesChanged.Order());
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
                ? new[]
                {
                    (testFileFullPath, false),
                    (testFileFullPath, true),
                }
                : new[]
                {
                    (testFileFullPath, true),
                },
                usePolling,
                () => File.WriteAllText(testFileFullPath, string.Empty));
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
                expectedChanges: [(testFileFullPath, false)],
                usePolling,
                () => File.WriteAllText(testFileFullPath, string.Empty));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task MoveFile(bool usePolling)
        {
            var dir = _testAssetManager.CreateTestDirectory(identifier: usePolling.ToString()).Path;
            var srcFile = Path.Combine(dir, "foo");
            var dstFile = Path.Combine(dir, "foo2");

            File.WriteAllText(srcFile, string.Empty);

            await TestOperation(
                dir,
                expectedChanges: RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && !usePolling
                ? new[]
                {
                    (srcFile, false),
                    (srcFile, true),
                    (dstFile, true),
                }
                : new[]
                {
                    (srcFile, false),
                    (dstFile, true),
                },
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
                expectedChanges: [
                    (subdir, false),
                    (testFileFullPath, false)
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
                expectedChanges: [(testFileFullPath, false)],
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

        private async Task AssertFileChangeRaisesEvent(string directory, IFileSystemWatcher watcher)
        {
            var changedEv = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var expectedPath = Path.Combine(directory, Path.GetRandomFileName());
            EventHandler<(string, bool)> handler = (_, f) =>
            {
                _output.WriteLine("File changed: " + f);
                try
                {
                    if (string.Equals(f.Item1, expectedPath, StringComparison.OrdinalIgnoreCase))
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
                expectedChanges: [
                    (subdir, false),
                    (f1, false),
                    (f2, false),
                    (f3, false),
                ],
                usePolling: true,
                () => Directory.Delete(subdir, recursive: true));
        }
    }
}
