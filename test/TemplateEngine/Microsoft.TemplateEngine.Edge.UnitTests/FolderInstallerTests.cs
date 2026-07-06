// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Edge.Installers.Folder;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    [TestClass]
    public class FolderInstallerTests
    {
        public TestContext TestContext { get; set; } = null!;

        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_environmentSettingsHelper = new EnvironmentSettingsHelper();

        [ClassCleanup]
        public static void ClassCleanup() => s_environmentSettingsHelper?.Dispose();

        [TestMethod]
        public async Task CanInstall_Directory()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatePackageProvider provider = new MockManagedTemplatePackageProvider();
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);

            FolderInstaller folderInstaller = new FolderInstaller(engineEnvironmentSettings, factory);

            InstallRequest request = new InstallRequest(installPath);

            Assert.IsTrue(await folderInstaller.CanInstallAsync(request, CancellationToken.None));

            InstallResult result = await folderInstaller.InstallAsync(request, provider, CancellationToken.None);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(request, result.InstallRequest);
            Assert.AreEqual(InstallerErrorCode.Success, result.Error);
            result.ErrorMessage.Should().BeNullOrEmpty();

            var source = result.TemplatePackage as FolderManagedTemplatePackage;
            Assert.IsNotNull(source);
            source!.MountPointUri.Should().Be(installPath);
            source.Version.Should().BeNullOrEmpty();
            source.DisplayName.Should().Be(installPath);
            source.Identifier.Should().Be(installPath);
            source.Installer.Should().Be(folderInstaller);
            source.Provider.Should().Be(provider);
        }

        [TestMethod]
        public async Task CannotInstall_File()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatePackageProvider provider = new MockManagedTemplatePackageProvider();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);

            FolderInstaller folderInstaller = new FolderInstaller(engineEnvironmentSettings, factory);

            InstallRequest request = new InstallRequest(Path.GetTempFileName());
            Assert.IsFalse(await folderInstaller.CanInstallAsync(request, CancellationToken.None));

            InstallResult result = await folderInstaller.InstallAsync(request, provider, CancellationToken.None);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(request, result.InstallRequest);
            Assert.AreEqual(InstallerErrorCode.PackageNotFound, result.Error);
            result.ErrorMessage.Should().NotBeNullOrEmpty();
            result.TemplatePackage.Should().BeNull();
        }

        [TestMethod]
        public async Task CannotInstall_NotExist()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatePackageProvider provider = new MockManagedTemplatePackageProvider();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);

            FolderInstaller folderInstaller = new FolderInstaller(engineEnvironmentSettings, factory);

            InstallRequest request = new InstallRequest("not found");
            Assert.IsFalse(await folderInstaller.CanInstallAsync(request, CancellationToken.None));

            InstallResult result = await folderInstaller.InstallAsync(request, provider, CancellationToken.None);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(request, result.InstallRequest);
            Assert.AreEqual(InstallerErrorCode.PackageNotFound, result.Error);
            result.ErrorMessage.Should().NotBeNullOrEmpty();
            result.TemplatePackage.Should().BeNull();
        }

        [TestMethod]
        public async Task GetLatestVersion_Success()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatePackageProvider provider = new MockManagedTemplatePackageProvider();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);

            FolderInstaller folderInstaller = new FolderInstaller(engineEnvironmentSettings, factory);

            FolderManagedTemplatePackage source = new FolderManagedTemplatePackage(engineEnvironmentSettings, folderInstaller, provider, Path.GetRandomFileName(), DateTime.UtcNow);
            IReadOnlyList<CheckUpdateResult> results = await folderInstaller.GetLatestVersionAsync(new[] { source }, provider, CancellationToken.None);

            Assert.ContainsSingle(results);
            CheckUpdateResult result = results.Single();

            Assert.IsTrue(result.Success);
            Assert.AreEqual(source, result.TemplatePackage);
            Assert.AreEqual(InstallerErrorCode.Success, result.Error);
            result.ErrorMessage.Should().BeNullOrEmpty();
            result.LatestVersion.Should().BeNullOrEmpty();
            Assert.IsTrue(result.IsLatestVersion);
        }

        [TestMethod]
        public async Task GetLatestVersion_HandleExceptions()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatePackageProvider provider = new MockManagedTemplatePackageProvider();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            FolderInstaller folderInstaller = new FolderInstaller(engineEnvironmentSettings, factory);

            _ = await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => folderInstaller.GetLatestVersionAsync(null!, provider, CancellationToken.None));
        }

        [TestMethod]
        public async Task Update_Success()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatePackageProvider provider = new MockManagedTemplatePackageProvider();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);

            FolderInstaller folderInstaller = new FolderInstaller(engineEnvironmentSettings, factory);

            FolderManagedTemplatePackage source = new FolderManagedTemplatePackage(engineEnvironmentSettings, folderInstaller, provider, Path.GetRandomFileName(), DateTime.UtcNow);
            //add a delay so update updates last changed time
            await Task.Delay(100, TestContext.CancellationToken);
            UpdateRequest updateRequest = new UpdateRequest(source, "1.0.0");
            UpdateResult result = await folderInstaller.UpdateAsync(updateRequest, provider, TestContext.CancellationToken);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(updateRequest, result.UpdateRequest);
            Assert.AreEqual(InstallerErrorCode.Success, result.Error);
            Assert.AreEqual(source.MountPointUri, result.TemplatePackage?.MountPointUri);
            Assert.AreNotEqual(source.LastChangeTime, result.TemplatePackage?.LastChangeTime);
        }

        [TestMethod]
        public async Task Update_HandleExceptions()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatePackageProvider provider = new MockManagedTemplatePackageProvider();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            FolderInstaller folderInstaller = new FolderInstaller(engineEnvironmentSettings, factory);
            _ = await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => folderInstaller.UpdateAsync(null!, provider, CancellationToken.None));
        }

        [TestMethod]
        public async Task CanUninstall_Success()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatePackageProvider provider = new MockManagedTemplatePackageProvider();
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);

            FolderInstaller folderInstaller = new FolderInstaller(engineEnvironmentSettings, factory);

            InstallRequest request = new InstallRequest(installPath);

            InstallResult result = await folderInstaller.InstallAsync(request, provider, CancellationToken.None);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(request, result.InstallRequest);
            Assert.AreEqual(InstallerErrorCode.Success, result.Error);
            result.ErrorMessage.Should().BeNullOrEmpty();

            var source = result.TemplatePackage as FolderManagedTemplatePackage;
            source.Should().NotBeNull();
            source!.MountPointUri.Should().Be(installPath);
            Directory.Exists(installPath);

            UninstallResult uninstallResult = await folderInstaller.UninstallAsync(source, provider, CancellationToken.None);

            Assert.IsTrue(uninstallResult.Success);
            Assert.AreEqual(source, uninstallResult.TemplatePackage);
            Assert.AreEqual(InstallerErrorCode.Success, uninstallResult.Error);
            uninstallResult.ErrorMessage.Should().BeNullOrEmpty();

            //directory is not removed
            Directory.Exists(installPath);
        }
    }
}
