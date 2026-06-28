// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Edge.BuiltInManagedProvider;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    [TestClass]
    public class GlobalSettingsTests
    {
        public TestContext TestContext { get; set; } = null!;

        private static EnvironmentSettingsHelper s_helper = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_helper = new EnvironmentSettingsHelper(NullMessageSink.Instance);

        [ClassCleanup]
        public static void ClassCleanup() => s_helper?.Dispose();

        [TestMethod]
        public async Task TestLocking()
        {
            var envSettings = s_helper.CreateEnvironment();
            var settingsFile = Path.Combine(s_helper.CreateTemporaryFolder(), "settings.json");
            using var globalSettings1 = new GlobalSettings(envSettings, settingsFile);
            using var globalSettings2 = new GlobalSettings(envSettings, settingsFile);
            var disposable = await globalSettings1.LockAsync(TestContext.CancellationToken);
            bool exceptionThrown = false;
            using var cts = new CancellationTokenSource(50);
            try
            {
                await globalSettings2.LockAsync(cts.Token);
            }
            catch (TaskCanceledException)
            {
                exceptionThrown = true;
            }
            Assert.IsTrue(exceptionThrown, nameof(globalSettings2) + " was able to get lock on when it shouldn't");
            disposable.Dispose();
            //Check that we don't time out
            using var cts2 = new CancellationTokenSource(1000);
            using var settingsLock = await globalSettings2.LockAsync(cts2.Token);
        }

        [TestMethod]
        public async Task TestFileWatcher()
        {
            var envSettings = s_helper.CreateEnvironment();
            var settingsFile = Path.Combine(s_helper.CreateTemporaryFolder(), "settings.json");
            using var globalSettings1 = new GlobalSettings(envSettings, settingsFile);
            using var globalSettings2 = new GlobalSettings(envSettings, settingsFile);
            var taskSource = new TaskCompletionSource<TemplatePackageData>();
            globalSettings2.SettingsChanged += () =>
            {
                try
                {
                    var result = globalSettings2.GetInstalledTemplatePackagesAsync(default).GetAwaiter().GetResult();
                    taskSource.TrySetResult(result.Single());
                }
                catch (ObjectDisposedException)
                {
                    // FileSystemWatcher callbacks race with test cleanup disposal.
                    // This handler may fire after globalSettings2 is disposed at end of test.
                }
            };
            var mutex = await globalSettings1.LockAsync(TestContext.CancellationToken);
            var newData = new TemplatePackageData(
                Guid.NewGuid(),
                "Hi",
                DateTime.UtcNow,
                new Dictionary<string, string>() { { "a", "b" } });
            await globalSettings1.SetInstalledTemplatePackagesAsync(new[] { newData }, TestContext.CancellationToken);
            mutex.Dispose();
            var timeoutTask = Task.Delay(10000, TestContext.CancellationToken);
            var firstFinishedTask = await Task.WhenAny(timeoutTask, taskSource.Task);
            Assert.AreEqual(taskSource.Task, firstFinishedTask);

            var newData2 = await taskSource.Task;
            Assert.AreEqual(newData.InstallerId, newData2.InstallerId);
            Assert.AreEqual(newData.MountPointUri, newData2.MountPointUri);
            Assert.AreEqual(newData.Details?["a"], newData2.Details?["a"]);
            Assert.AreEqual(newData.LastChangeTime, newData2.LastChangeTime);
        }

        [TestMethod]
        public async Task TestReadWhileLocked()
        {
            var envSettings = s_helper.CreateEnvironment();
            var settingsFile = Path.Combine(s_helper.CreateTemporaryFolder(), "settings.json");
            using var globalSettings1 = new GlobalSettings(envSettings, settingsFile);

            #region Open1AndPopulateAndSave

            using (await globalSettings1.LockAsync(TestContext.CancellationToken))
            {
                var newData = new TemplatePackageData(
                Guid.NewGuid(),
                "Hi",
                DateTime.UtcNow,
                new Dictionary<string, string>() { { "a", "b" } });
                await globalSettings1.SetInstalledTemplatePackagesAsync(new[] { newData }, TestContext.CancellationToken);
            }

            #endregion Open1AndPopulateAndSave

            #region Open2LoadAndLock

            using var globalSettings2 = new GlobalSettings(envSettings, settingsFile);
            Assert.AreEqual((await globalSettings1.GetInstalledTemplatePackagesAsync(TestContext.CancellationToken))[0].InstallerId, (await globalSettings2.GetInstalledTemplatePackagesAsync(TestContext.CancellationToken))[0].InstallerId);
            var mutex2 = await globalSettings2.LockAsync(TestContext.CancellationToken);

            #endregion Open2LoadAndLock

            #region Open3Load

            using var globalSettings3 = new GlobalSettings(envSettings, settingsFile);
            Assert.AreEqual((await globalSettings1.GetInstalledTemplatePackagesAsync(TestContext.CancellationToken))[0].InstallerId, (await globalSettings3.GetInstalledTemplatePackagesAsync(TestContext.CancellationToken))[0].InstallerId);

            #endregion Open3Load

            mutex2.Dispose();
        }

        [TestMethod]
        public void TestDisablingFilewatcher()
        {
            var envSettings = s_helper.CreateEnvironment(environment: new MockEnvironment(new Dictionary<string, string> { { "TEMPLATE_ENGINE_DISABLE_FILEWATCHER", "1" } }));
            var settingsFile = Path.Combine(s_helper.CreateTemporaryFolder(), "settings.json");
            using var globalSettings1 = new GlobalSettings(envSettings, settingsFile);
            Assert.IsEmpty(((MonitoredFileSystem)envSettings.Host.FileSystem).FilesWatched);

            envSettings = s_helper.CreateEnvironment();
            settingsFile = Path.Combine(s_helper.CreateTemporaryFolder(), "settings.json");
            using var globalSettings2 = new GlobalSettings(envSettings, settingsFile);
            Assert.ContainsSingle(((MonitoredFileSystem)envSettings.Host.FileSystem).FilesWatched);
            Assert.AreEqual(settingsFile, ((MonitoredFileSystem)envSettings.Host.FileSystem).FilesWatched.Single());
        }
    }
}
