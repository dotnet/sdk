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

            FolderInstaller folderInstaller = new FolderInstaller(engineEnvironmentSettings, factory);

            InstallRequest request = new InstallRequest(installPath);

            Assert.True(await folderInstaller.CanInstallAsync(request, CancellationToken.None).ConfigureAwait(false));

            InstallResult result = await folderInstaller.InstallAsync(request, provider, CancellationToken.None).ConfigureAwait(false);

            Assert.True(result.Success);
            Assert.Equal(request, result.InstallRequest);
            Assert.Equal(InstallerErrorCode.Success, result.Error);
            result.ErrorMessage.Should().BeNullOrEmpty();

            var source = (FolderManagedTemplatePackage)result.TemplatePackage;
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

            FolderInstaller folderInstaller = new FolderInstaller(engineEnvironmentSettings, factory);

            InstallRequest request = new InstallRequest(Path.GetTempFileName());
            Assert.False(await folderInstaller.CanInstallAsync(request, CancellationToken.None).ConfigureAwait(false));

            InstallResult result = await folderInstaller.InstallAsync(request, provider, CancellationToken.None).ConfigureAwait(false);

            Assert.False(result.Success);
            Assert.Equal(request, result.InstallRequest);
            Assert.Equal(InstallerErrorCode.PackageNotFound, result.Error);
            result.ErrorMessage.Should().NotBeNullOrEmpty();
            result.TemplatePackage.Should().BeNull();
        }

        [Fact]
        public async Task CannotInstall_NotExist()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);

            FolderInstaller folderInstaller = new FolderInstaller(engineEnvironmentSettings, factory);

            InstallRequest request = new InstallRequest("not found");
            Assert.False(await folderInstaller.CanInstallAsync(request, CancellationToken.None).ConfigureAwait(false));

            InstallResult result = await folderInstaller.InstallAsync(request, provider, CancellationToken.None).ConfigureAwait(false);

            Assert.False(result.Success);
            Assert.Equal(request, result.InstallRequest);
            Assert.Equal(InstallerErrorCode.PackageNotFound, result.Error);
            result.ErrorMessage.Should().NotBeNullOrEmpty();
            result.TemplatePackage.Should().BeNull();
        }

        [Fact]
        public async Task GetLatestVersion_Success()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);

            FolderInstaller folderInstaller = new FolderInstaller(engineEnvironmentSettings, factory);

            FolderManagedTemplatePackage source = new FolderManagedTemplatePackage(engineEnvironmentSettings, folderInstaller, provider, Path.GetRandomFileName());
            IReadOnlyList<CheckUpdateResult> results = await folderInstaller.GetLatestVersionAsync(new[] { source }, provider, CancellationToken.None).ConfigureAwait(false);

            Assert.Single(results);
            CheckUpdateResult result = results.Single();

            Assert.True(result.Success);
            Assert.Equal(source, result.TemplatePackage);
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
            FolderInstaller folderInstaller = new FolderInstaller(engineEnvironmentSettings, factory);

            _ = await Assert.ThrowsAsync<ArgumentNullException>(() => folderInstaller.GetLatestVersionAsync(null, provider, CancellationToken.None)).ConfigureAwait(false);
        }

        [Fact]
        public async Task Update_Success()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);

            FolderInstaller folderInstaller = new FolderInstaller(engineEnvironmentSettings, factory);

            FolderManagedTemplatePackage source = new FolderManagedTemplatePackage(engineEnvironmentSettings, folderInstaller, provider, Path.GetRandomFileName());
            UpdateRequest updateRequest = new UpdateRequest(source, "1.0.0");
            UpdateResult result = await folderInstaller.UpdateAsync(updateRequest, provider, CancellationToken.None).ConfigureAwait(false);

            Assert.True(result.Success);
            Assert.Equal(updateRequest, result.UpdateRequest);
            Assert.Equal(InstallerErrorCode.Success, result.Error);
            Assert.Equal(source, result.TemplatePackage);
        }

        [Fact]
        public async Task Update_HandleExceptions()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            FolderInstaller folderInstaller = new FolderInstaller(engineEnvironmentSettings, factory);
            _ = await Assert.ThrowsAsync<ArgumentNullException>(() => folderInstaller.UpdateAsync(null, provider, CancellationToken.None)).ConfigureAwait(false);
        }

        [Fact]
        public async Task CanUninstall_Success()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            string installPath = _environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);

            FolderInstaller folderInstaller = new FolderInstaller(engineEnvironmentSettings, factory);

            InstallRequest request = new InstallRequest(installPath);

            InstallResult result = await folderInstaller.InstallAsync(request, provider, CancellationToken.None).ConfigureAwait(false);

            Assert.True(result.Success);
            Assert.Equal(request, result.InstallRequest);
            Assert.Equal(InstallerErrorCode.Success, result.Error);
            result.ErrorMessage.Should().BeNullOrEmpty();

            var source = (FolderManagedTemplatePackage)result.TemplatePackage;
            source.Should().NotBeNull();
            source.MountPointUri.Should().Be(installPath);
            Directory.Exists(installPath);

            UninstallResult uninstallResult = await folderInstaller.UninstallAsync(source, provider, CancellationToken.None).ConfigureAwait(false);

            Assert.True(uninstallResult.Success);
            Assert.Equal(source, uninstallResult.TemplatePackage);
            Assert.Equal(InstallerErrorCode.Success, result.Error);
            result.ErrorMessage.Should().BeNullOrEmpty();

            //directory is not removed
            Directory.Exists(installPath);
        }
    }
}
