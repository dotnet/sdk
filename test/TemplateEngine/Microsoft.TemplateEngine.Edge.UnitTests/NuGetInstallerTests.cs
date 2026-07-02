// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Edge.Installers.NuGet;
using Microsoft.TemplateEngine.Edge.UnitTests.Mocks;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Tests;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    [TestClass]
    public class NuGetInstallerTests : TestBase
    {
        private static PackageManager s_packageManager = null!;
        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
        {
            s_packageManager = new PackageManager();
            s_environmentSettingsHelper = new EnvironmentSettingsHelper();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            s_packageManager?.Dispose();
            s_environmentSettingsHelper?.Dispose();
        }

        public static IEnumerable<object?[]> SerializationData()
        {
            //can read details
            yield return new object[]
            {
                new TemplatePackageData(
                    default,
                    "MountPointUri",
                    default,
                    new Dictionary<string, string>
                     {
                         { "Author", "TestAuthor" },
                         { "NuGetSource", "https://api.nuget.org/v3/index.json" },
                         { "PackageId", "TestPackage" },
                         { "Version", "4.7.0.395" },
                         { "Owners", "test, test2" },
                         { "Reserved", "true" }
                     }),
                "TestPackage", "4.7.0.395", "TestAuthor", "https://api.nuget.org/v3/index.json", false, "true", "test, test2"
            };
            //skips irrelevant details
            yield return new object?[]
            {
                new TemplatePackageData(
                     default,
                     "MountPointUri",
                     default,
                     new Dictionary<string, string>
                     {
                         { "Irrelevant", "not needed" },
                         { "NuGetSource", "https://api.nuget.org/v3/index.json" },
                         { "PackageId", "TestPackage" },
                         { "Version", "4.7.0.395" },
                         { "Owners", "test, test2" },
                         { "Reserved", "false" }
                     }),
                "TestPackage", "4.7.0.395", null, "https://api.nuget.org/v3/index.json", false, "false", "test, test2"
            };
        }

        [TestMethod]
        public async Task CanInstall_LocalPackage()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager();

            NuGetInstaller installer = new NuGetInstaller(factory, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            string package = PackTestTemplatesNuGetPackage(s_packageManager);

            InstallRequest request = new InstallRequest(package);
            Assert.IsTrue(await installer.CanInstallAsync(request, CancellationToken.None));
        }

        [TestMethod]
        public async Task CanInstall_CannotInstallDirectory()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager();

            NuGetInstaller installer = new NuGetInstaller(factory, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            string package = Directory.GetCurrentDirectory();

            InstallRequest request = new InstallRequest(package);
            Assert.IsFalse(await installer.CanInstallAsync(request, CancellationToken.None));
        }

        [TestMethod]
        public async Task CanInstall_CannotInstallFile()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager();

            NuGetInstaller installer = new NuGetInstaller(factory, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            string package = typeof(NuGetInstallerTests).Assembly.Location;

            InstallRequest request = new InstallRequest(package);
            Assert.IsFalse(await installer.CanInstallAsync(request, CancellationToken.None));
        }

        [TestMethod]
        [DataRow("ValidId", "", true)]
        [DataRow("Invalid&%", "", false)]
        [DataRow("ValidId", "1.0.0", true)]
        [DataRow("ValidId", "InvalidVersion", false)]
        [DataRow("Invalid&%", "InvalidVersion", false)]
        public async Task CanInstall_RemotePackage(string identifier, string version, bool result)
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager();

            NuGetInstaller installer = new NuGetInstaller(factory, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            InstallRequest request = new InstallRequest(identifier, version);

            Assert.AreEqual(result, await installer.CanInstallAsync(request, CancellationToken.None));
        }

        [TestMethod]
        public async Task Install_LocalPackage()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatePackageProvider provider = new MockManagedTemplatePackageProvider();
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager();

            NuGetInstaller installer = new NuGetInstaller(factory, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            string package = PackTestTemplatesNuGetPackage(s_packageManager);

            InstallRequest request = new InstallRequest(package);

            InstallResult installResult = await installer.InstallAsync(request, provider, CancellationToken.None);

            Assert.IsTrue(installResult.Success);
            Assert.AreEqual(request, installResult.InstallRequest);
            Assert.AreEqual(InstallerErrorCode.Success, installResult.Error);
            installResult.ErrorMessage.Should().BeNullOrEmpty();

            var source = installResult.TemplatePackage as NuGetManagedTemplatePackage;
            Assert.IsNotNull(source);
            source!.MountPointUri.Should().ContainAll(new[] { installPath, "Microsoft.TemplateEngine.TestTemplates" });
            source.Author.Should().Be("Microsoft");
            source.Owners.Should().BeNull();
            source.Reserved.Should().Be("False");
            source.Version.Should().NotBeNullOrEmpty();
            source.DisplayName.Should().StartWith("Microsoft.TemplateEngine.TestTemplates@");
            source.Identifier.Should().Be("Microsoft.TemplateEngine.TestTemplates");
            source.Installer.Should().Be(installer);
            source.IsLocalPackage.Should().Be(true);
            source.Provider.Should().Be(provider);
        }

        [TestMethod]
        public async Task Install_LocalPackage_CannotInstallUnsupportedRequest()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatePackageProvider provider = new MockManagedTemplatePackageProvider();
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager();

            NuGetInstaller installer = new NuGetInstaller(factory, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            string package = typeof(NuGetInstallerTests).Assembly.Location;

            InstallRequest request = new InstallRequest(package);
            InstallResult installResult = await installer.InstallAsync(request, provider, CancellationToken.None);

            Assert.IsFalse(installResult.Success);
            Assert.AreEqual(request, installResult.InstallRequest);
            Assert.AreEqual(InstallerErrorCode.UnsupportedRequest, installResult.Error);
            installResult.ErrorMessage.Should().NotBeNullOrEmpty();
            installResult.TemplatePackage.Should().BeNull();
        }

        [TestMethod]
        public async Task Install_LocalPackage_CannotInstallSamePackageTwice()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatePackageProvider provider = new MockManagedTemplatePackageProvider();
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager();

            NuGetInstaller installer = new NuGetInstaller(factory, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            string package = PackTestTemplatesNuGetPackage(s_packageManager);

            InstallRequest request = new InstallRequest(package);

            InstallResult installResult = await installer.InstallAsync(request, provider, CancellationToken.None);

            Assert.IsTrue(installResult.Success);
            Assert.AreEqual(request, installResult.InstallRequest);

            installResult = await installer.InstallAsync(request, provider, CancellationToken.None);

            Assert.IsFalse(installResult.Success);
            Assert.AreEqual(request, installResult.InstallRequest);
            Assert.AreEqual(InstallerErrorCode.DownloadFailed, installResult.Error);
            installResult.ErrorMessage.Should().NotBeNullOrEmpty();
            installResult.TemplatePackage.Should().BeNull();
        }

        [TestMethod]
        public async Task Install_RemotePackage()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatePackageProvider provider = new MockManagedTemplatePackageProvider();
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager(s_packageManager, TestPackageProjectPath);

            NuGetInstaller installer = new NuGetInstaller(factory, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            InstallRequest request = new InstallRequest("Microsoft.TemplateEngine.TestTemplates", "1.0.0");

            InstallResult installResult = await installer.InstallAsync(request, provider, CancellationToken.None);

            Assert.IsTrue(installResult.Success);
            Assert.AreEqual(request, installResult.InstallRequest);
            Assert.AreEqual(InstallerErrorCode.Success, installResult.Error);
            installResult.ErrorMessage.Should().BeNullOrEmpty();

            var source = installResult.TemplatePackage as NuGetManagedTemplatePackage;
            Assert.IsNotNull(source);
            source!.MountPointUri.Should().ContainAll(new[] { installPath, "Microsoft.TemplateEngine.TestTemplates" });
            source.Author.Should().Be("Microsoft");
            source.Owners.Should().Be("Microsoft");
            source.Reserved.Should().Be("True");
            source.Version.Should().Be("1.0.0");
            source.DisplayName.Should().Be("Microsoft.TemplateEngine.TestTemplates@1.0.0");
            source.Identifier.Should().Be("Microsoft.TemplateEngine.TestTemplates");
            source.Installer.Should().Be(installer);
            source.IsLocalPackage.Should().Be(false);
            source.Provider.Should().Be(provider);
            source.NuGetSource.Should().Be(MockPackageManager.DefaultFeed);
        }

        [TestMethod]
        [DataRow(nameof(DownloadException), InstallerErrorCode.DownloadFailed)]
        [DataRow(nameof(PackageNotFoundException), InstallerErrorCode.PackageNotFound)]
        [DataRow(nameof(InvalidNuGetSourceException), InstallerErrorCode.InvalidSource)]
        [DataRow(nameof(Exception), InstallerErrorCode.GenericError)]
        public async Task Install_RemotePackage_HandleExceptions(string exception, InstallerErrorCode expectedErrorCode)
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatePackageProvider provider = new MockManagedTemplatePackageProvider();
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager();

            NuGetInstaller installer = new NuGetInstaller(factory, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            InstallRequest request = new InstallRequest(exception);

            InstallResult installResult = await installer.InstallAsync(request, provider, CancellationToken.None);

            Assert.IsFalse(installResult.Success);
            Assert.AreEqual(request, installResult.InstallRequest);
            Assert.AreEqual(expectedErrorCode, installResult.Error);
            installResult.ErrorMessage.Should().NotBeNullOrEmpty();
            installResult.TemplatePackage.Should().BeNull();
        }

        [TestMethod]
        public async Task Install_RemotePackage_HandleVulnerablePackage()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatePackageProvider provider = new MockManagedTemplatePackageProvider();
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager();

            NuGetInstaller installer = new NuGetInstaller(factory, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            InstallRequest request = new InstallRequest(nameof(VulnerablePackageException), "12.0.3");

            InstallResult installResult = await installer.InstallAsync(request, provider, CancellationToken.None);
            Assert.IsFalse(installResult.Success);
            Assert.AreEqual(request, installResult.InstallRequest);
            Assert.AreEqual(InstallerErrorCode.VulnerablePackage, installResult.Error);
            installResult.ErrorMessage.Should().NotBeNullOrEmpty();
            installResult.TemplatePackage.Should().BeNull();
            installResult.Vulnerabilities.Should().NotBeNullOrEmpty();
        }

        [TestMethod]
        public async Task GetLatestVersion_RemotePackage()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatePackageProvider provider = new MockManagedTemplatePackageProvider();
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager(s_packageManager, TestPackageProjectPath);

            NuGetInstaller installer = new NuGetInstaller(factory, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            InstallRequest request = new InstallRequest("Microsoft.TemplateEngine.TestTemplates", "1.0.0");

            InstallResult installResult = await installer.InstallAsync(request, provider, CancellationToken.None);

            Assert.IsTrue(installResult.Success);
            Assert.AreEqual(request, installResult.InstallRequest);

            NuGetManagedTemplatePackage? source = installResult.TemplatePackage as NuGetManagedTemplatePackage;
            Assert.IsNotNull(source);
            IReadOnlyList<CheckUpdateResult> checkUpdateResults = await installer.GetLatestVersionAsync(new[] { source! }, provider, CancellationToken.None);

            Assert.ContainsSingle(checkUpdateResults);
            CheckUpdateResult result = checkUpdateResults.Single();

            Assert.IsTrue(result.Success);
            Assert.AreEqual(source, result.TemplatePackage);
            Assert.AreEqual(InstallerErrorCode.Success, result.Error);
            result.ErrorMessage.Should().BeNullOrEmpty();
            Assert.AreEqual("1.0.0", result.LatestVersion);
            Assert.IsFalse(result.IsLatestVersion);
        }

        [TestMethod]
        public async Task GetLatestVersion_RemotePackageWithVulnerabilities()
        {
            MockManagedTemplatePackageProvider provider = new MockManagedTemplatePackageProvider();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager(s_packageManager, TestPackageProjectPath);

            NuGetInstaller installer = new NuGetInstaller(new MockInstallerFactory(), engineEnvironmentSettings, s_environmentSettingsHelper.CreateTemporaryFolder(), mockPackageManager, mockPackageManager);
            InstallRequest request = new InstallRequest(nameof(VulnerablePackageException), "1.0.0");
            InstallResult installResult = await installer.InstallAsync(request, provider, CancellationToken.None);

            Assert.IsTrue(installResult.Success);
            Assert.AreEqual(request, installResult.InstallRequest);

            NuGetManagedTemplatePackage? source = installResult.TemplatePackage as NuGetManagedTemplatePackage;
            Assert.IsNotNull(source);
            source.Version = "12.0.0";
            IReadOnlyList<CheckUpdateResult> checkUpdateResults = await installer.GetLatestVersionAsync(new[] { source! }, provider, CancellationToken.None);

            Assert.ContainsSingle(checkUpdateResults);
            CheckUpdateResult result = checkUpdateResults.Single();

            Assert.IsFalse(result.Success);
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        [TestMethod]
        [DataRow(nameof(PackageNotFoundException), InstallerErrorCode.PackageNotFound)]
        [DataRow(nameof(InvalidNuGetSourceException), InstallerErrorCode.InvalidSource)]
        [DataRow(nameof(VulnerablePackageException), InstallerErrorCode.VulnerablePackage, "12.0.0")]
        [DataRow(nameof(Exception), InstallerErrorCode.GenericError)]
        public async Task GetLatestVersion_RemotePackage_HandleExceptions(string exception, InstallerErrorCode expectedErrorCode, string version = "1.0.0")
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatePackageProvider provider = new MockManagedTemplatePackageProvider();
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager();

            NuGetInstaller installer = new NuGetInstaller(factory, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            NuGetManagedTemplatePackage source = new NuGetManagedTemplatePackage(engineEnvironmentSettings, installer, provider, installPath, exception)
            {
                Version = version
            };
            IReadOnlyList<CheckUpdateResult> checkUpdateResults = await installer.GetLatestVersionAsync(new[] { source }, provider, CancellationToken.None);

            Assert.ContainsSingle(checkUpdateResults);
            CheckUpdateResult result = checkUpdateResults.Single();

            Assert.IsFalse(result.Success);
            Assert.AreEqual(source, result.TemplatePackage);
            Assert.AreEqual(expectedErrorCode, result.Error);
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        [TestMethod]
        public async Task Uninstall_RemotePackage()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatePackageProvider provider = new MockManagedTemplatePackageProvider();
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager(s_packageManager, TestPackageProjectPath);

            NuGetInstaller installer = new NuGetInstaller(factory, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            InstallRequest request = new InstallRequest("Microsoft.TemplateEngine.TestTemplates", "1.0.0");

            InstallResult installResult = await installer.InstallAsync(request, provider, CancellationToken.None);

            Assert.IsTrue(installResult.Success);
            Assert.AreEqual(request, installResult.InstallRequest);

            var source = installResult.TemplatePackage as NuGetManagedTemplatePackage;
            Assert.IsNotNull(source);
            string mountPoint = source!.MountPointUri;
            Assert.IsTrue(File.Exists(mountPoint));

            UninstallResult result = await installer.UninstallAsync(source, provider, CancellationToken.None);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(source, result.TemplatePackage);
            Assert.AreEqual(InstallerErrorCode.Success, result.Error);
            result.ErrorMessage.Should().BeNullOrEmpty();
            Assert.IsFalse(File.Exists(mountPoint));
        }

        [TestMethod]
        public async Task Update_RemotePackage()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatePackageProvider provider = new MockManagedTemplatePackageProvider();
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager(s_packageManager, TestPackageProjectPath);

            NuGetInstaller installer = new NuGetInstaller(factory, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            InstallRequest request = new InstallRequest("Microsoft.TemplateEngine.TestTemplates", "1.0.0");

            InstallResult installResult = await installer.InstallAsync(request, provider, CancellationToken.None);

            Assert.IsTrue(installResult.Success);
            Assert.AreEqual(request, installResult.InstallRequest);
            Assert.AreEqual(InstallerErrorCode.Success, installResult.Error);
            installResult.ErrorMessage.Should().BeNullOrEmpty();

            var source = installResult.TemplatePackage as NuGetManagedTemplatePackage;
            Assert.IsNotNull(source);
            string oldMountPoint = source!.MountPointUri;
            Assert.IsTrue(File.Exists(oldMountPoint));
            UpdateRequest updateRequest = new UpdateRequest(source, "1.0.1");

            UpdateResult updateResult = await installer.UpdateAsync(updateRequest, provider, CancellationToken.None);
            Assert.IsTrue(updateResult.Success);
            Assert.AreEqual(updateRequest, updateResult.UpdateRequest);
            Assert.AreEqual(InstallerErrorCode.Success, updateResult.Error);
            updateResult.ErrorMessage.Should().BeNullOrEmpty();

            var updatedSource = updateResult.TemplatePackage as NuGetManagedTemplatePackage;
            Assert.IsNotNull(updatedSource);
            updatedSource!.MountPointUri.Should().ContainAll(new[] { installPath, "Microsoft.TemplateEngine.TestTemplates" });
            updatedSource.Author.Should().Be("Microsoft");
            updatedSource.Version.Should().Be("1.0.1");
            updatedSource.DisplayName.Should().Be("Microsoft.TemplateEngine.TestTemplates@1.0.1");
            updatedSource.Identifier.Should().Be("Microsoft.TemplateEngine.TestTemplates");
            updatedSource.Installer.Should().Be(installer);
            updatedSource.IsLocalPackage.Should().Be(false);
            updatedSource.Provider.Should().Be(provider);
            updatedSource.NuGetSource.Should().Be(MockPackageManager.DefaultFeed);
            Assert.IsFalse(File.Exists(oldMountPoint));
            Assert.IsTrue(File.Exists(updatedSource.MountPointUri));
        }

        [TestMethod]
        public async Task Update_CannotUpdateVulnerabilities()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatePackageProvider provider = new MockManagedTemplatePackageProvider();
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager(s_packageManager, TestPackageProjectPath);

            NuGetInstaller installer = new NuGetInstaller(factory, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            InstallRequest request = new InstallRequest(nameof(VulnerablePackageException), "2.0.10");

            InstallResult installResult = await installer.InstallAsync(request, provider, CancellationToken.None);

            Assert.IsTrue(installResult.Success);
            Assert.AreEqual(request, installResult.InstallRequest);
            Assert.AreEqual(InstallerErrorCode.Success, installResult.Error);
            installResult.ErrorMessage.Should().BeNullOrEmpty();

            var source = installResult.TemplatePackage as NuGetManagedTemplatePackage;
            Assert.IsNotNull(source);
            string oldMountPoint = source!.MountPointUri;
            Assert.IsTrue(File.Exists(oldMountPoint));
            UpdateRequest updateRequest = new UpdateRequest(source, "12.0.3");

            UpdateResult updateResult = await installer.UpdateAsync(updateRequest, provider, CancellationToken.None);
            Assert.IsFalse(updateResult.Success);
            Assert.AreEqual(InstallerErrorCode.VulnerablePackage, updateResult.Error);
            updateResult.ErrorMessage.Should().NotBeNullOrEmpty();
            updateResult.Vulnerabilities.Should().NotBeNullOrEmpty();
        }

        [TestMethod]
        [DynamicData(nameof(SerializationData))]
        public void Deserialize(
            TemplatePackageData data,
            string identifier,
            string version,
            string? author,
            string nugetFeed,
            bool local,
            string reserved,
            string owners)
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatePackageProvider provider = new MockManagedTemplatePackageProvider();
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager();
            NuGetInstaller installer = new NuGetInstaller(factory, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);

            NuGetManagedTemplatePackage source = (NuGetManagedTemplatePackage)installer.Deserialize(provider, data);
            source.MountPointUri.Should().Be(data.MountPointUri);
            source.Author.Should().Be(author);
            source.Reserved.Should().Be(reserved);
            source.Owners.Should().Be(owners);
            source.Version.Should().Be(version);
            source.DisplayName.Should().Be($"{identifier}@{version}");
            source.Identifier.Should().Be(identifier);
            source.Installer.Should().Be(installer);
            source.IsLocalPackage.Should().Be(local);
            source.Provider.Should().Be(provider);
            source.NuGetSource.Should().Be(nugetFeed);
        }

        [TestMethod]
        public void Deserialize_ThrowsWhenDetailsDoNotHavePackageID()
        {
            var templateData = new TemplatePackageData(
                default,
                "MountPointUri",
                default,
                new Dictionary<string, string>
                     {
                         { "Irrelevant", "not needed" },
                         { "Version", "4.7.0.395" }
                     });
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatePackageProvider provider = new MockManagedTemplatePackageProvider();
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager();
            NuGetInstaller installer = new NuGetInstaller(factory, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);

            Assert.ThrowsExactly<ArgumentException>(() => installer.Deserialize(provider, templateData));
        }

        [TestMethod]
        public void Deserialize_ThrowsWhenInstallerIdDoesNotMatch()
        {
            var templateData = new TemplatePackageData(
                Guid.NewGuid(),
                "MountPointUri",
                default,
                new Dictionary<string, string>
                {
                    { "Irrelevant", "not needed" },
                    { "Version", "4.7.0.395" }
                });
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatePackageProvider provider = new MockManagedTemplatePackageProvider();
            string installPath = s_environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager();
            NuGetInstaller installer = new NuGetInstaller(factory, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            Assert.ThrowsExactly<ArgumentException>(() => installer.Deserialize(provider, templateData));
        }
    }
}
