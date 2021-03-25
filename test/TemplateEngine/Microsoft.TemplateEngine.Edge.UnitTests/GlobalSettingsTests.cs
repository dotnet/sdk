// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Edge.BuiltInManagedProvider;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class GlobalSettingsTests : IDisposable
    {
        EnvironmentSettingsHelper _helper = new EnvironmentSettingsHelper();

        [Fact]
        public async Task TestLocking()
        {
            var envSettings = _helper.CreateEnvironment();
            var settingsFile = Path.Combine(_helper.CreateTemporaryFolder(), "settings.json");
            using var globalSettings1 = new GlobalSettings(envSettings, settingsFile);
            using var globalSettings2 = new GlobalSettings(envSettings, settingsFile);
            var disposable = await globalSettings1.LockAsync(default).ConfigureAwait(false);
            bool exceptionThrown = false;
            using var cts = new CancellationTokenSource(50);
            try
            {
                await globalSettings2.LockAsync(cts.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                exceptionThrown = true;
            }
            Assert.True(exceptionThrown, nameof(globalSettings2) + " was able to get lock on when it shouldn't");
            disposable.Dispose();
            //Check that we don't time out
            using var cts2 = new CancellationTokenSource(1000);
            using var _ = await globalSettings2.LockAsync(cts2.Token).ConfigureAwait(false);
        }


        [Fact]
        public async Task TestFilwatcher()
        {
            var envSettings = _helper.CreateEnvironment();
            var settingsFile = Path.Combine(_helper.CreateTemporaryFolder(), "settings.json");
            using var globalSettings1 = new GlobalSettings(envSettings, settingsFile);
            using var globalSettings2 = new GlobalSettings(envSettings, settingsFile);
            var taskSource = new TaskCompletionSource<TemplatePackageData>();
            globalSettings2.SettingsChanged += async () => taskSource.TrySetResult((await globalSettings2.GetInstalledTemplatesPackagesAsync(default).ConfigureAwait(false)).Single());
            var mutex = await globalSettings1.LockAsync(default).ConfigureAwait(false);
            var newData = new TemplatePackageData()
            {
                InstallerId = Guid.NewGuid(),
                MountPointUri = "Hi",
                Details = new Dictionary<string, string>() { { "a", "b" } },
                LastChangeTime = DateTime.UtcNow
            };
            await globalSettings1.SetInstalledTemplatesPackagesAsync(new[] { newData }, default).ConfigureAwait(false);
            mutex.Dispose();
            var timeoutTask = Task.Delay(1000);
            var firstFinishedTask = await Task.WhenAny(timeoutTask, taskSource.Task).ConfigureAwait(false);
            Assert.Equal(taskSource.Task, firstFinishedTask);

            var newData2 = taskSource.Task.Result;
            Assert.Equal(newData.InstallerId, newData2.InstallerId);
            Assert.Equal(newData.MountPointUri, newData2.MountPointUri);
            Assert.Equal(newData.Details["a"], newData2.Details["a"]);
            Assert.Equal(newData.LastChangeTime, newData2.LastChangeTime);
        }


        [Fact]
        public async Task TestReadWhileLocked()
        {
            var envSettings = _helper.CreateEnvironment();
            var settingsFile = Path.Combine(_helper.CreateTemporaryFolder(), "settings.json");
            using var globalSettings1 = new GlobalSettings(envSettings, settingsFile);

            #region Open1AndPopulateAndSave
            using (await globalSettings1.LockAsync(default).ConfigureAwait(false))
            {
                var newData = new TemplatePackageData()
                {
                    InstallerId = Guid.NewGuid(),
                    MountPointUri = "Hi",
                    Details = new Dictionary<string, string>() { { "a", "b" } },
                    LastChangeTime = DateTime.UtcNow
                };
                await globalSettings1.SetInstalledTemplatesPackagesAsync(new[] { newData }, default).ConfigureAwait(false);
            }
            #endregion

            #region Open2LoadAndLock
            using var globalSettings2 = new GlobalSettings(envSettings, settingsFile);
            Assert.Equal((await globalSettings1.GetInstalledTemplatesPackagesAsync(default).ConfigureAwait(false))[0].InstallerId, (await globalSettings2.GetInstalledTemplatesPackagesAsync(default).ConfigureAwait(false))[0].InstallerId);
            var mutex2 = await globalSettings2.LockAsync(default).ConfigureAwait(false);
            #endregion

            #region Open3Load
            using var globalSettings3 = new GlobalSettings(envSettings, settingsFile);
            Assert.Equal((await globalSettings1.GetInstalledTemplatesPackagesAsync(default).ConfigureAwait(false))[0].InstallerId, (await globalSettings3.GetInstalledTemplatesPackagesAsync(default).ConfigureAwait(false))[0].InstallerId);
            #endregion

            mutex2.Dispose();
        }

        public void Dispose()
        {
            _helper.Dispose();
        }
    }
}
