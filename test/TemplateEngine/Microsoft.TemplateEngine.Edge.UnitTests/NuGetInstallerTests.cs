// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Edge.Installers.NuGet;
using Microsoft.TemplateEngine.Edge.UnitTests.Mocks;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class NuGetInstallerTests : IClassFixture<PackageManager>, IClassFixture<EnvironmentSettingsHelper>
    {
        private PackageManager _packageManager;
        private EnvironmentSettingsHelper _environmentSettingsHelper;

        public NuGetInstallerTests(PackageManager packageManager, EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _packageManager = packageManager;
            _environmentSettingsHelper = environmentSettingsHelper;
        }

        [Fact]
        public async Task CanInstall_LocalPackage()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            string installPath = _environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager();

            NuGetInstaller installer = new NuGetInstaller(factory, provider, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            string package = _packageManager.PackTestTemplatesNuGetPackage();

            InstallRequest request = new InstallRequest(package);
            Assert.True(await installer.CanInstallAsync(request, CancellationToken.None).ConfigureAwait(false));
        }

        [Fact]
        public async Task CanInstall_CannotInstallDirectory()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            string installPath = _environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager();

            NuGetInstaller installer = new NuGetInstaller(factory, provider, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            string package = Directory.GetCurrentDirectory();

            InstallRequest request = new InstallRequest(package);
            Assert.False(await installer.CanInstallAsync(request, CancellationToken.None).ConfigureAwait(false));
        }

        [Fact]
        public async Task CanInstall_CannotInstallFile()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            string installPath = _environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager();

            NuGetInstaller installer = new NuGetInstaller(factory, provider, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            string package = typeof(NuGetInstallerTests).GetTypeInfo().Assembly.Location;

            InstallRequest request = new InstallRequest(package);
            Assert.False(await installer.CanInstallAsync(request, CancellationToken.None).ConfigureAwait(false));
        }

        [Theory]
        [InlineData("ValidId", "", true)]
        [InlineData("Invalid&%", "", false)]
        [InlineData("ValidId", "1.0.0", true)]
        [InlineData("ValidId", "InvalidVersion", false)]
        [InlineData("Invalid&%", "InvalidVersion", false)]
        public async Task CanInstall_RemotePackage(string identifier, string version, bool result)
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            string installPath = _environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager();

            NuGetInstaller installer = new NuGetInstaller(factory, provider, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            InstallRequest request = new InstallRequest(identifier, version);

            Assert.Equal(result, await installer.CanInstallAsync(request, CancellationToken.None).ConfigureAwait(false));
        }

        [Fact]
        public async Task Install_LocalPackage()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            string installPath = _environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager();

            NuGetInstaller installer = new NuGetInstaller(factory, provider, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            string package = _packageManager.PackTestTemplatesNuGetPackage();

            InstallRequest request = new InstallRequest
            (package);

            InstallResult installResult = await installer.InstallAsync(request, CancellationToken.None).ConfigureAwait(false);

            Assert.True(installResult.Success);
            Assert.Equal(request, installResult.InstallRequest);
            Assert.Equal(InstallerErrorCode.Success, installResult.Error);
            installResult.ErrorMessage.Should().BeNullOrEmpty();

            var source = (NuGetManagedTemplatePackage)installResult.TemplatePackage;
            source.MountPointUri.Should().ContainAll(new[] { installPath, "Microsoft.TemplateEngine.TestTemplates" });
            source.Author.Should().Be("Microsoft");
            source.Version.Should().NotBeNullOrEmpty();
            source.DisplayName.Should().StartWith("Microsoft.TemplateEngine.TestTemplates::");
            source.Identifier.Should().Be("Microsoft.TemplateEngine.TestTemplates");
            source.Installer.Should().Be(installer);
            source.LocalPackage.Should().Be(true);
            source.Provider.Should().Be(provider);
        }

        [Fact]
        public async Task Install_LocalPackage_CannotInstallUnsupportedRequest()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            string installPath = _environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager();

            NuGetInstaller installer = new NuGetInstaller(factory, provider, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            string package = typeof(NuGetInstallerTests).GetTypeInfo().Assembly.Location;

            InstallRequest request = new InstallRequest
            (package);
            InstallResult installResult = await installer.InstallAsync(request, CancellationToken.None).ConfigureAwait(false);

            Assert.False(installResult.Success);
            Assert.Equal(request, installResult.InstallRequest);
            Assert.Equal(InstallerErrorCode.UnsupportedRequest, installResult.Error);
            installResult.ErrorMessage.Should().NotBeNullOrEmpty();
            installResult.TemplatePackage.Should().BeNull();
        }

        [Fact]
        public async Task Install_LocalPackage_CannotInstallSamePackageTwice()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            string installPath = _environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager();

            NuGetInstaller installer = new NuGetInstaller(factory, provider, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            string package = _packageManager.PackTestTemplatesNuGetPackage();

            InstallRequest request = new InstallRequest
            (package);

            InstallResult installResult = await installer.InstallAsync(request, CancellationToken.None).ConfigureAwait(false);

            Assert.True(installResult.Success);
            Assert.Equal(request, installResult.InstallRequest);

            installResult = await installer.InstallAsync(request, CancellationToken.None).ConfigureAwait(false);

            Assert.False(installResult.Success);
            Assert.Equal(request, installResult.InstallRequest);
            Assert.Equal(InstallerErrorCode.DownloadFailed, installResult.Error);
            installResult.ErrorMessage.Should().NotBeNullOrEmpty();
            installResult.TemplatePackage.Should().BeNull();
        }

        [Fact]
        public async Task Install_RemotePackage()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            string installPath = _environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager(_packageManager);

            NuGetInstaller installer = new NuGetInstaller(factory, provider, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            InstallRequest request = new InstallRequest("Microsoft.TemplateEngine.TestTemplates", "1.0.0");

            InstallResult installResult = await installer.InstallAsync(request, CancellationToken.None).ConfigureAwait(false);

            Assert.True(installResult.Success);
            Assert.Equal(request, installResult.InstallRequest);
            Assert.Equal(InstallerErrorCode.Success, installResult.Error);
            installResult.ErrorMessage.Should().BeNullOrEmpty();

            var source = (NuGetManagedTemplatePackage)installResult.TemplatePackage;
            source.MountPointUri.Should().ContainAll(new[] { installPath, "Microsoft.TemplateEngine.TestTemplates" });
            source.Author.Should().Be("Microsoft");
            source.Version.Should().Be("1.0.0");
            source.DisplayName.Should().Be("Microsoft.TemplateEngine.TestTemplates::1.0.0");
            source.Identifier.Should().Be("Microsoft.TemplateEngine.TestTemplates");
            source.Installer.Should().Be(installer);
            source.LocalPackage.Should().Be(false);
            source.Provider.Should().Be(provider);
            source.NuGetSource.Should().Be(MockPackageManager.DefaultFeed);
        }

        [Theory]
        [InlineData(nameof(DownloadException), InstallerErrorCode.DownloadFailed)]
        [InlineData(nameof(PackageNotFoundException), InstallerErrorCode.PackageNotFound)]
        [InlineData(nameof(InvalidNuGetSourceException), InstallerErrorCode.InvalidSource)]
        [InlineData(nameof(Exception), InstallerErrorCode.GenericError)]
        public async Task Install_RemotePackage_HandleExceptions(string exception, InstallerErrorCode expectedErrorCode)
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            string installPath = _environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager();

            NuGetInstaller installer = new NuGetInstaller(factory, provider, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            InstallRequest request = new InstallRequest(exception);

            InstallResult installResult = await installer.InstallAsync(request, CancellationToken.None).ConfigureAwait(false);

            Assert.False(installResult.Success);
            Assert.Equal(request, installResult.InstallRequest);
            Assert.Equal(expectedErrorCode, installResult.Error);
            installResult.ErrorMessage.Should().NotBeNullOrEmpty();
            installResult.TemplatePackage.Should().BeNull();
        }

        [Fact]
        public async Task GetLatestVersion_RemotePackage()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            string installPath = _environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager(_packageManager);

            NuGetInstaller installer = new NuGetInstaller(factory, provider, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            InstallRequest request = new InstallRequest("Microsoft.TemplateEngine.TestTemplates", "1.0.0");

            InstallResult installResult = await installer.InstallAsync(request, CancellationToken.None).ConfigureAwait(false);

            Assert.True(installResult.Success);
            Assert.Equal(request, installResult.InstallRequest);

            var source = (NuGetManagedTemplatePackage)installResult.TemplatePackage;
            IReadOnlyList<CheckUpdateResult> checkUpdateResults = await installer.GetLatestVersionAsync(new[] { source }, CancellationToken.None).ConfigureAwait(false);

            Assert.Single(checkUpdateResults);
            CheckUpdateResult result = checkUpdateResults.Single();

            Assert.True(result.Success);
            Assert.Equal(source, result.TemplatePackage);
            Assert.Equal(InstallerErrorCode.Success, result.Error);
            result.ErrorMessage.Should().BeNullOrEmpty();
            Assert.Equal("1.0.0", result.LatestVersion);
            Assert.False(result.IsLatestVersion);
        }

        [Theory]
        [InlineData(nameof(PackageNotFoundException), InstallerErrorCode.PackageNotFound)]
        [InlineData(nameof(InvalidNuGetSourceException), InstallerErrorCode.InvalidSource)]
        [InlineData(nameof(Exception), InstallerErrorCode.GenericError)]
        public async Task GetLatestVersion_RemotePackage_HandleExceptions(string exception, InstallerErrorCode expectedErrorCode)
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            string installPath = _environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager();

            NuGetInstaller installer = new NuGetInstaller(factory, provider, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);

            Dictionary<string, string> sourceDetails = new Dictionary<string, string>();
            sourceDetails[NuGetManagedTemplatePackage.PackageIdKey] = exception;
            NuGetManagedTemplatePackage source = new NuGetManagedTemplatePackage(engineEnvironmentSettings, installer, installPath, sourceDetails);
            IReadOnlyList<CheckUpdateResult> checkUpdateResults = await installer.GetLatestVersionAsync(new[] { source }, CancellationToken.None).ConfigureAwait(false);

            Assert.Single(checkUpdateResults);
            CheckUpdateResult result = checkUpdateResults.Single();

            Assert.False(result.Success);
            Assert.Equal(source, result.TemplatePackage);
            Assert.Equal(expectedErrorCode, result.Error);
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Uninstall_RemotePackage()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            string installPath = _environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager(_packageManager);

            NuGetInstaller installer = new NuGetInstaller(factory, provider, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            InstallRequest request = new InstallRequest("Microsoft.TemplateEngine.TestTemplates", "1.0.0");

            InstallResult installResult = await installer.InstallAsync(request, CancellationToken.None).ConfigureAwait(false);

            Assert.True(installResult.Success);
            Assert.Equal(request, installResult.InstallRequest);

            var source = (NuGetManagedTemplatePackage)installResult.TemplatePackage;
            string mountPoint = source.MountPointUri;
            Assert.True(File.Exists(mountPoint));

            UninstallResult result = await installer.UninstallAsync(source, CancellationToken.None).ConfigureAwait(false);

            Assert.True(result.Success);
            Assert.Equal(source, result.TemplatePackage);
            Assert.Equal(InstallerErrorCode.Success, result.Error);
            result.ErrorMessage.Should().BeNullOrEmpty();
            Assert.False(File.Exists(mountPoint));
        }

        [Fact]
        public async Task Update_RemotePackage()
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            string installPath = _environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager(_packageManager);

            NuGetInstaller installer = new NuGetInstaller(factory, provider, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);
            InstallRequest request = new InstallRequest("Microsoft.TemplateEngine.TestTemplates", "1.0.0");

            InstallResult installResult = await installer.InstallAsync(request, CancellationToken.None).ConfigureAwait(false);

            Assert.True(installResult.Success);
            Assert.Equal(request, installResult.InstallRequest);
            Assert.Equal(InstallerErrorCode.Success, installResult.Error);
            installResult.ErrorMessage.Should().BeNullOrEmpty();

            var source = (NuGetManagedTemplatePackage)installResult.TemplatePackage;
            string oldMountPoint = source.MountPointUri;
            Assert.True(File.Exists(oldMountPoint));
            UpdateRequest updateRequest = new UpdateRequest(source, "1.0.1");

            UpdateResult updateResult = await installer.UpdateAsync( updateRequest, CancellationToken.None).ConfigureAwait(false);
            Assert.True(updateResult.Success);
            Assert.Equal(updateRequest, updateResult.UpdateRequest);
            Assert.Equal(InstallerErrorCode.Success, updateResult.Error);
            updateResult.ErrorMessage.Should().BeNullOrEmpty();

            var updatedSource = (NuGetManagedTemplatePackage)updateResult.TemplatePackage;

            updatedSource.MountPointUri.Should().ContainAll(new[] { installPath, "Microsoft.TemplateEngine.TestTemplates" });
            updatedSource.Author.Should().Be("Microsoft");
            updatedSource.Version.Should().Be("1.0.1");
            updatedSource.DisplayName.Should().Be("Microsoft.TemplateEngine.TestTemplates::1.0.1");
            updatedSource.Identifier.Should().Be("Microsoft.TemplateEngine.TestTemplates");
            updatedSource.Installer.Should().Be(installer);
            updatedSource.LocalPackage.Should().Be(false);
            updatedSource.Provider.Should().Be(provider);
            updatedSource.NuGetSource.Should().Be(MockPackageManager.DefaultFeed);
            Assert.False(File.Exists(oldMountPoint));
            Assert.True(File.Exists(updatedSource.MountPointUri));
        }

        public static IEnumerable<object[]> SerializationData()
        {
            //can read details
            yield return new object[] {
                new TemplatePackageData()
                {
                     MountPointUri = "MountPointUri",
                     Details = new Dictionary<string, string>
                     {
                         { "Author", "TestAuthor" },
                         { "NuGetSource", "https://api.nuget.org/v3/index.json" },
                         { "PackageId", "TestPackage" },
                         { "Version", "4.7.0.395" }
                     }
                },
                "TestPackage", "4.7.0.395", "TestAuthor", "https://api.nuget.org/v3/index.json", false
            };
            //skips irrelevant details
            yield return new object[] {
                new TemplatePackageData()
                {
                     MountPointUri = "MountPointUri",
                     Details = new Dictionary<string, string>
                     {
                         { "Irrelevant", "not nedeed" },
                         { "NuGetSource", "https://api.nuget.org/v3/index.json" },
                         { "PackageId", "TestPackage" },
                         { "Version", "4.7.0.395" }
                     }
                },
                "TestPackage", "4.7.0.395", null, "https://api.nuget.org/v3/index.json", false
            };
        }

        [Theory]
        [MemberData(nameof(SerializationData))]
        public void Deserialize(TemplatePackageData data, string identifier, string version, string author, string nugetFeed, bool local)
        {
            MockInstallerFactory factory = new MockInstallerFactory();
            MockManagedTemplatesPackageProvider provider = new MockManagedTemplatesPackageProvider();
            string installPath = _environmentSettingsHelper.CreateTemporaryFolder();
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            MockPackageManager mockPackageManager = new MockPackageManager();
            NuGetInstaller installer = new NuGetInstaller(factory, provider, engineEnvironmentSettings, installPath, mockPackageManager, mockPackageManager);

            NuGetManagedTemplatePackage source = (NuGetManagedTemplatePackage)installer.Deserialize(provider, data);
            source.MountPointUri.Should().Be(data.MountPointUri);
            source.Author.Should().Be(author);
            source.Version.Should().Be(version);
            source.DisplayName.Should().Be($"{identifier}::{version}");
            source.Identifier.Should().Be(identifier);
            source.Installer.Should().Be(installer);
            source.LocalPackage.Should().Be(local);
            source.Provider.Should().Be(provider);
            source.NuGetSource.Should().Be(nugetFeed);
        }
    }
}
