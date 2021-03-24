// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Edge.Installers.Folder;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class FolderInstallerTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private EnvironmentSettingsHelper _environmentSettingsHelper;

        public FolderInstallerTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _environmentSettingsHelper = environmentSettingsHelper;
        }

        [Fact]
        public async Task CanInstall_Directory()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            string installPath = _environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);

            FolderInstaller folderInstaller = new FolderInstaller(engineEnvironmentSettings, factory, provider);

            InstallRequest request = new InstallRequest
            {
                Identifier = installPath
            };
            Assert.True(await folderInstaller.CanInstallAsync(request, CancellationToken.None).ConfigureAwait(false));

            InstallResult result = await folderInstaller.InstallAsync(request, CancellationToken.None).ConfigureAwait(false);

            Assert.True(result.Success);
            Assert.Equal(request, result.InstallRequest);
            Assert.Equal(InstallerErrorCode.Success, result.Error);
            result.ErrorMessage.Should().BeNullOrEmpty();

            var source = (FolderManagedTemplatePackage)result.Source;
            source.MountPointUri.Should().Be(installPath);
            source.Version.Should().BeNullOrEmpty();
            source.DisplayName.Should().Be(installPath);
            source.Identifier.Should().Be(installPath);
            source.Installer.Should().Be(folderInstaller);
            source.Provider.Should().Be(provider);
        }

        [Fact]
        public async Task CannotInstall_File()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);

            FolderInstaller folderInstaller = new FolderInstaller(engineEnvironmentSettings, factory, provider);

            InstallRequest request = new InstallRequest
            {
                Identifier = Path.GetTempFileName()
            };
            Assert.False(await folderInstaller.CanInstallAsync(request, CancellationToken.None).ConfigureAwait(false));

            InstallResult result = await folderInstaller.InstallAsync(request, CancellationToken.None).ConfigureAwait(false);

            Assert.False(result.Success);
            Assert.Equal(request, result.InstallRequest);
            Assert.Equal(InstallerErrorCode.PackageNotFound, result.Error);
            result.ErrorMessage.Should().NotBeNullOrEmpty();
            result.Source.Should().BeNull();
        }

        [Fact]
        public async Task CannotInstall_NotExist()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);

            FolderInstaller folderInstaller = new FolderInstaller(engineEnvironmentSettings, factory, provider);

            InstallRequest request = new InstallRequest
            {
                Identifier = "not found"
            };
            Assert.False(await folderInstaller.CanInstallAsync(request, CancellationToken.None).ConfigureAwait(false));

            InstallResult result = await folderInstaller.InstallAsync(request, CancellationToken.None).ConfigureAwait(false);

            Assert.False(result.Success);
            Assert.Equal(request, result.InstallRequest);
            Assert.Equal(InstallerErrorCode.PackageNotFound, result.Error);
            result.ErrorMessage.Should().NotBeNullOrEmpty();
            result.Source.Should().BeNull();
        }

        [Fact]
        public async Task GetLatestVersion_Success()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);

            FolderInstaller folderInstaller = new FolderInstaller(engineEnvironmentSettings, factory, provider);

            FolderManagedTemplatePackage source = new FolderManagedTemplatePackage(engineEnvironmentSettings, folderInstaller, Path.GetRandomFileName());
            IReadOnlyList<CheckUpdateResult> results = await folderInstaller.GetLatestVersionAsync(new[] { source }, CancellationToken.None).ConfigureAwait(false);

            Assert.Single(results);
            CheckUpdateResult result = results.Single();

            Assert.True(result.Success);
            Assert.Equal(source, result.Source);
            Assert.Equal(InstallerErrorCode.Success, result.Error);
            result.ErrorMessage.Should().BeNullOrEmpty();
            result.LatestVersion.Should().BeNullOrEmpty();
            Assert.True(result.IsLatestVersion);
        }

        [Fact]
        public async Task GetLatestVersion_HandleExceptions()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            FolderInstaller folderInstaller = new FolderInstaller(engineEnvironmentSettings, factory, provider);

            _ = await Assert.ThrowsAsync<ArgumentNullException>(() => folderInstaller.GetLatestVersionAsync(null, CancellationToken.None)).ConfigureAwait(false);
        }

        [Fact]
        public async Task Update_Success()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);

            FolderInstaller folderInstaller = new FolderInstaller(engineEnvironmentSettings, factory, provider);

            FolderManagedTemplatePackage source = new FolderManagedTemplatePackage(engineEnvironmentSettings, folderInstaller, Path.GetRandomFileName());
            UpdateRequest updateRequest = new UpdateRequest
            {
                Source = source,
                Version = "1.0.0"
            };

            UpdateResult result = await folderInstaller.UpdateAsync(updateRequest, CancellationToken.None).ConfigureAwait(false);

            Assert.True(result.Success);
            Assert.Equal(updateRequest, result.UpdateRequest);
            Assert.Equal(InstallerErrorCode.Success, result.Error);
            Assert.Equal(source, result.Source);
        }

        [Fact]
        public async Task Update_HandleExceptions()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            FolderInstaller folderInstaller = new FolderInstaller(engineEnvironmentSettings, factory, provider);
            _ = await Assert.ThrowsAsync<ArgumentNullException>(() => folderInstaller.UpdateAsync(null, CancellationToken.None)).ConfigureAwait(false);
        }

        [Fact]
        public async Task CanUninstall_Success()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            string installPath = _environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);

            FolderInstaller folderInstaller = new FolderInstaller(engineEnvironmentSettings, factory, provider);

            InstallRequest request = new InstallRequest
            {
                Identifier = installPath
            };

            InstallResult result = await folderInstaller.InstallAsync(request, CancellationToken.None).ConfigureAwait(false);

            Assert.True(result.Success);
            Assert.Equal(request, result.InstallRequest);
            Assert.Equal(InstallerErrorCode.Success, result.Error);
            result.ErrorMessage.Should().BeNullOrEmpty();

            var source = (FolderManagedTemplatePackage)result.Source;
            source.Should().NotBeNull();
            source.MountPointUri.Should().Be(installPath);
            Directory.Exists(installPath);

            UninstallResult uninstallResult = await folderInstaller.UninstallAsync(source, CancellationToken.None).ConfigureAwait(false);

            Assert.True(uninstallResult.Success);
            Assert.Equal(source, uninstallResult.Source);
            Assert.Equal(InstallerErrorCode.Success, result.Error);
            result.ErrorMessage.Should().BeNullOrEmpty();

            //directory is not removed
            Directory.Exists(installPath);
        }
    }
}
