// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.IDE.IntegrationTests
{
    public class TemplatePackagesTests : BootstrapperTestBase, IClassFixture<PackageManager>
    {
        private readonly PackageManager _packageManager;
        private const string ValidPackageJsonFile = /*lang=json,strict*/ """
{
    "Packages": [
        {
            "Details": {
                "PackageId": "Sln",
                "Author": "Enrico Sada",
                "NuGetSource": "https://api.nuget.org/v3/index.json",
                "Version": "0.2.0"
            },
            "InstallerId": "015dcbac-b4a5-49ea-94a6-061616eb60e2",
            "LastChangeTime": "2023-04-13T15:17:16.4866397Z",
            "MountPointUri": "packages\\Sln.0.3.0.nupkg"
        },
        {
            "Details": {
                "PackageId": "Boxed.Templates",
                "Author": "Muhammad Rehan Saeed (RehanSaeed.com)",
                "NuGetSource": "https://api.nuget.org/v3/index.json",
                "Version": "7.4.0"
            },
            "InstallerId": "015dcbac-b4a5-49ea-94a6-061616eb60e2",
            "LastChangeTime": "2023-06-01T11:32:14.867341Z",
            "MountPointUri": "packages\\Boxed.Templates.7.4.0.nupkg"
        }
    ]
}
""";

        public TemplatePackagesTests(PackageManager packageManager)
        {
            _packageManager = packageManager;
        }

        [Fact]
        internal async Task CanInstall_LocalNuGetPackage()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string packageLocation = await _packageManager.GetNuGetPackage("Microsoft.DotNet.Common.ProjectTemplates.5.0");

            InstallRequest installRequest = new InstallRequest(Path.GetFullPath(packageLocation));

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.Single(result);
            Assert.True(result[0].Success);
            Assert.Equal(InstallerErrorCode.Success, result[0].Error);
            result[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.Equal(installRequest, result[0].InstallRequest);

            IManagedTemplatePackage? source = result[0].TemplatePackage;
            Assert.NotNull(source);
            Assert.Equal("Microsoft.DotNet.Common.ProjectTemplates.5.0", source!.Identifier);
            Assert.Equal("Global Settings", source.Provider.Factory.DisplayName);
            Assert.Equal("NuGet", source.Installer.Factory.Name);
            Assert.Equal("Microsoft", source.GetDetails()["Author"]);
            source.Version.Should().NotBeNullOrEmpty();

            IReadOnlyList<IManagedTemplatePackage> managedTemplatesPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None);

            Assert.Single(managedTemplatesPackages);
            managedTemplatesPackages[0].Should().BeEquivalentTo(source);

            IReadOnlyList<ITemplatePackage> templatePackages = await bootstrapper.GetTemplatePackagesAsync(CancellationToken.None);

            Assert.Single(templatePackages);
            Assert.IsAssignableFrom<IManagedTemplatePackage>(templatePackages[0]);
            templatePackages[0].Should().BeEquivalentTo((ITemplatePackage)source);
        }

        [Fact]
        internal async Task CanInstall_RemoteNuGetPackage()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            InstallRequest installRequest = new InstallRequest(
                "Take.Blip.Client.Templates",
                "0.5.135",
                details: new Dictionary<string, string>
                {
                    { InstallerConstants.NuGetSourcesKey, "https://api.nuget.org/v3/index.json" }
                });

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.Single(result);
            Assert.True(result[0].Success);
            Assert.Equal(InstallerErrorCode.Success, result[0].Error);
            Assert.True(string.IsNullOrEmpty(result[0].ErrorMessage));
            Assert.Equal(installRequest, result[0].InstallRequest);

            IManagedTemplatePackage? source = result[0].TemplatePackage;
            Assert.NotNull(source);
            Assert.Equal("Take.Blip.Client.Templates", source!.Identifier);
            Assert.Equal("Global Settings", source.Provider.Factory.DisplayName);
            Assert.Equal("NuGet", source.Installer.Factory.Name);
            source.GetDetails()["Author"].Should().NotBeNullOrEmpty();
            Assert.Equal("https://api.nuget.org/v3/index.json", source.GetDetails()["NuGetSource"]);
            Assert.Equal("0.5.135", source.Version);

            IReadOnlyList<IManagedTemplatePackage> managedTemplatesPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None);

            Assert.Single(managedTemplatesPackages);
            managedTemplatesPackages[0].Should().BeEquivalentTo(source);

            IReadOnlyList<ITemplatePackage> templatePackages = await bootstrapper.GetTemplatePackagesAsync(CancellationToken.None);

            Assert.Single(templatePackages);
            Assert.IsAssignableFrom<IManagedTemplatePackage>(templatePackages[0]);
            templatePackages[0].Should().BeEquivalentTo((ITemplatePackage)source);
        }

        [Fact]
        internal async Task CanInstall_Folder()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("TemplateWithSourceName");

            InstallRequest installRequest = new InstallRequest(Path.GetFullPath(templateLocation));

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.Single(result);
            Assert.True(result[0].Success);
            Assert.Equal(InstallerErrorCode.Success, result[0].Error);
            result[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.Equal(installRequest, result[0].InstallRequest);

            IManagedTemplatePackage? source = result[0].TemplatePackage;
            Assert.NotNull(source);
            Assert.Equal(Path.GetFullPath(templateLocation), source!.Identifier);
            Assert.Equal("Global Settings", source.Provider.Factory.DisplayName);
            Assert.Equal("Folder", source.Installer.Factory.Name);
            source.Version.Should().BeNullOrEmpty();

            IReadOnlyList<IManagedTemplatePackage> managedTemplatesPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None);

            Assert.Single(managedTemplatesPackages);
            managedTemplatesPackages[0].Should().BeEquivalentTo(source);

            IReadOnlyList<ITemplatePackage> templatePackages = await bootstrapper.GetTemplatePackagesAsync(CancellationToken.None);

            Assert.Single(templatePackages);
            Assert.IsAssignableFrom<IManagedTemplatePackage>(templatePackages[0]);
            templatePackages[0].Should().BeEquivalentTo((ITemplatePackage)source);
        }

        [Fact]
        internal async Task CanCheckForLatestVersion_NuGetPackage()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            InstallRequest installRequest = new InstallRequest("Microsoft.DotNet.Common.ProjectTemplates.5.0", "5.0.0");

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.Single(result);
            Assert.True(result[0].Success);
            IManagedTemplatePackage? source = result[0].TemplatePackage;
            Assert.NotNull(source);
            IReadOnlyList<CheckUpdateResult> checkUpdateResults = await bootstrapper.GetLatestVersionsAsync(new[] { source! }, CancellationToken.None);

            Assert.Single(checkUpdateResults);
            Assert.True(checkUpdateResults[0].Success);
            Assert.Equal(InstallerErrorCode.Success, checkUpdateResults[0].Error);
            Assert.True(string.IsNullOrEmpty(checkUpdateResults[0].ErrorMessage));
            Assert.Equal(source, checkUpdateResults[0].TemplatePackage);
            Assert.False(checkUpdateResults[0].IsLatestVersion);
            Assert.NotEqual("5.0.0", checkUpdateResults[0].LatestVersion);
        }

        [Fact]
        internal async Task CanCheckForLatestVersion_Folder()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("TemplateWithSourceName");

            InstallRequest installRequest = new InstallRequest(Path.GetFullPath(templateLocation));

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.Single(result);
            Assert.True(result[0].Success);
            IManagedTemplatePackage? source = result[0].TemplatePackage;
            Assert.NotNull(source);
            IReadOnlyList<CheckUpdateResult> checkUpdateResults = await bootstrapper.GetLatestVersionsAsync(new[] { source! }, CancellationToken.None);

            Assert.Single(checkUpdateResults);
            Assert.True(checkUpdateResults[0].Success);
            Assert.Equal(InstallerErrorCode.Success, checkUpdateResults[0].Error);
            Assert.True(string.IsNullOrEmpty(checkUpdateResults[0].ErrorMessage));
            Assert.Equal(source, checkUpdateResults[0].TemplatePackage);
            Assert.True(checkUpdateResults[0].IsLatestVersion);
        }

        [Fact]
        internal async Task CanUpdateAllPackagesMetadataOnGetLatestVersion()
        {
            using Bootstrapper bootstrapper = GetBootstrapper(packageJsonContent: ValidPackageJsonFile);
            var installedPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None);

            // implicitly populates packages metadata
            IReadOnlyList<CheckUpdateResult> checkUpdateResults = await bootstrapper
                .GetLatestVersionsAsync(installedPackages, CancellationToken.None);

            var updatedPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None);

            Assert.Equal(2, updatedPackages.Count);
            var slnPackage = updatedPackages[0];
            Assert.Equal("0.2.0", slnPackage.Version);
            var slnPackageDetails = slnPackage.GetDetails();
            Assert.Equal("enricosada", slnPackageDetails["Owners"]);
            Assert.False(bool.Parse(slnPackageDetails["Reserved"]));

            var boxPackage = updatedPackages[1];
            Assert.Equal("7.4.0", boxPackage.Version);
            var boxPackageDetails = boxPackage.GetDetails();
            Assert.Equal("BlackLight", boxPackageDetails["Owners"]);
            Assert.True(bool.Parse(boxPackageDetails["Reserved"]));
        }

        [Fact]
        internal async Task CanUpdateSpecifiedPackageMetadataOnGetLatestVersion()
        {
            using Bootstrapper bootstrapper = GetBootstrapper(packageJsonContent: ValidPackageJsonFile);
            var installedPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None);
            var boxedTemplatePackage = installedPackages.FirstOrDefault(ip => ip.Identifier == "Boxed.Templates");

            // implicitly populates package metadata
            IReadOnlyList<CheckUpdateResult> checkUpdateResults = await bootstrapper
                .GetLatestVersionsAsync(new[] { boxedTemplatePackage! }, CancellationToken.None);

            var updatedPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None);
            Assert.Equal(2, updatedPackages.Count);
            var slnPackageDetails = updatedPackages[0].GetDetails();
            Assert.False(slnPackageDetails.TryGetValue("Owners", out var _));
            Assert.False(bool.Parse(slnPackageDetails["Reserved"]));

            // the specified package has updated metadata
            var boxPackageDetails = updatedPackages[1].GetDetails();
            Assert.Equal("BlackLight", boxPackageDetails["Owners"]);
            Assert.True(bool.Parse(boxPackageDetails["Reserved"]));
        }

        [Fact]
        internal async Task CanUpdate_NuGetPackage()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            InstallRequest installRequest = new InstallRequest("Microsoft.DotNet.Common.ProjectTemplates.5.0", "5.0.0");

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.Single(result);
            Assert.True(result[0].Success);
            IManagedTemplatePackage? source = result[0].TemplatePackage;
            Assert.NotNull(source);
            UpdateRequest updateRequest = new UpdateRequest(source!, "5.0.1");

            IReadOnlyList<UpdateResult> updateResults = await bootstrapper.UpdateTemplatePackagesAsync(new[] { updateRequest }, CancellationToken.None);

            Assert.Single(updateResults);
            Assert.True(updateResults[0].Success);
            Assert.Equal(InstallerErrorCode.Success, updateResults[0].Error);
            Assert.True(string.IsNullOrEmpty(updateResults[0].ErrorMessage));
            Assert.Equal(updateRequest, updateResults[0].UpdateRequest);

            IManagedTemplatePackage? updatedSource = updateResults[0].TemplatePackage;
            Assert.NotNull(updatedSource);
            Assert.Equal("Global Settings", updatedSource!.Provider.Factory.DisplayName);
            Assert.Equal("NuGet", updatedSource.Installer.Factory.Name);
            Assert.Equal("5.0.1", updatedSource.Version);

            IReadOnlyList<IManagedTemplatePackage> managedTemplatesPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None);

            Assert.Single(managedTemplatesPackages);
            managedTemplatesPackages[0].Should().BeEquivalentTo(updatedSource);

            IReadOnlyList<ITemplatePackage> templatePackages = await bootstrapper.GetTemplatePackagesAsync(CancellationToken.None);

            Assert.Single(templatePackages);
            Assert.IsAssignableFrom<IManagedTemplatePackage>(templatePackages[0]);
            templatePackages[0].Should().BeEquivalentTo((ITemplatePackage)updatedSource);
        }

        [Fact]
        internal async Task CanUninstall_NuGetPackage()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            InstallRequest installRequest = new InstallRequest("Microsoft.DotNet.Common.ProjectTemplates.5.0", "5.0.0");

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.Single(result);
            Assert.True(result[0].Success);
            IManagedTemplatePackage? source = result[0].TemplatePackage;
            Assert.NotNull(source);
            IReadOnlyList<IManagedTemplatePackage> managedTemplatesPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None);

            Assert.Single(managedTemplatesPackages);
            managedTemplatesPackages[0].Should().BeEquivalentTo(source);

            IReadOnlyList<UninstallResult> uninstallResults = await bootstrapper.UninstallTemplatePackagesAsync(new[] { source! }, CancellationToken.None);

            Assert.Single(uninstallResults);
            Assert.True(uninstallResults[0].Success);
            Assert.Equal(InstallerErrorCode.Success, uninstallResults[0].Error);
            uninstallResults[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.Equal(source, uninstallResults[0].TemplatePackage);

            IReadOnlyList<ITemplatePackage> templatePackages = await bootstrapper.GetTemplatePackagesAsync(CancellationToken.None);

            Assert.Empty(templatePackages);
        }

        [Fact]
        internal async Task CanUninstall_Folder()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("TemplateWithSourceName");

            InstallRequest installRequest = new InstallRequest(Path.GetFullPath(templateLocation));

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.Single(result);
            Assert.True(result[0].Success);
            IManagedTemplatePackage? source = result[0].TemplatePackage;
            Assert.NotNull(source);
            Assert.Equal(templateLocation, source!.MountPointUri);

            IReadOnlyList<IManagedTemplatePackage> managedTemplatesPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None);

            Assert.Single(managedTemplatesPackages);
            managedTemplatesPackages[0].Should().BeEquivalentTo(source);

            IReadOnlyList<UninstallResult> uninstallResults = await bootstrapper.UninstallTemplatePackagesAsync(new[] { source }, CancellationToken.None);

            Assert.Single(uninstallResults);
            Assert.True(uninstallResults[0].Success);
            Assert.Equal(InstallerErrorCode.Success, uninstallResults[0].Error);
            uninstallResults[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.Equal(source, uninstallResults[0].TemplatePackage);

            IReadOnlyList<ITemplatePackage> templatePackages = await bootstrapper.GetTemplatePackagesAsync(CancellationToken.None);

            Assert.Empty(templatePackages);

            Directory.Exists(templateLocation);
        }

        [Fact]
        internal async Task CanReInstallPackage_WhenForceIsSpecified()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string packageLocation = await _packageManager.GetNuGetPackage("Microsoft.DotNet.Common.ProjectTemplates.5.0");

            InstallRequest installRequest = new InstallRequest(Path.GetFullPath(packageLocation));

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.Single(result);
            Assert.True(result[0].Success);
            Assert.Equal(InstallerErrorCode.Success, result[0].Error);
            result[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.Equal(installRequest, result[0].InstallRequest);

            InstallRequest repeatedInstallRequest = new InstallRequest(Path.GetFullPath(packageLocation), force: true);

            result = await bootstrapper.InstallTemplatePackagesAsync(new[] { repeatedInstallRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.Single(result);
            Assert.True(result[0].Success);
            Assert.Equal(InstallerErrorCode.Success, result[0].Error);
            result[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.Equal(repeatedInstallRequest, result[0].InstallRequest);

            InstallRequest repeatedInstallRequestWithoutForce = new InstallRequest(Path.GetFullPath(packageLocation));

            result = await bootstrapper.InstallTemplatePackagesAsync(new[] { repeatedInstallRequestWithoutForce }, InstallationScope.Global, CancellationToken.None);

            Assert.Single(result);
            Assert.False(result[0].Success);
            Assert.Equal(InstallerErrorCode.DownloadFailed, result[0].Error);
        }

        [Fact]
        internal async Task CanReInstallRemotePackage_WhenForceIsSpecified()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            InstallRequest installRequest = new InstallRequest("Microsoft.DotNet.Common.ProjectTemplates.5.0", version: "5.0.0");

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.Single(result);
            Assert.True(result[0].Success);
            Assert.Equal(InstallerErrorCode.Success, result[0].Error);
            Assert.True(string.IsNullOrEmpty(result[0].ErrorMessage));
            Assert.Equal(installRequest, result[0].InstallRequest);

            InstallRequest repeatedInstallRequest = new InstallRequest("Microsoft.DotNet.Common.ProjectTemplates.5.0", version: "5.0.0", force: true);

            result = await bootstrapper.InstallTemplatePackagesAsync(new[] { repeatedInstallRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.Single(result);
            Assert.True(result[0].Success);
            Assert.Equal(InstallerErrorCode.Success, result[0].Error);
            result[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.Equal(repeatedInstallRequest, result[0].InstallRequest);

            InstallRequest repeatedInstallRequestWithoutForce = new InstallRequest("Microsoft.DotNet.Common.ProjectTemplates.5.0", version: "5.0.0");

            result = await bootstrapper.InstallTemplatePackagesAsync(new[] { repeatedInstallRequestWithoutForce }, InstallationScope.Global, CancellationToken.None);

            Assert.Single(result);
            Assert.False(result[0].Success);
            Assert.Equal(InstallerErrorCode.AlreadyInstalled, result[0].Error);
        }

        [Fact]
        internal async Task CanReInstallFolder_WhenForceIsSpecified()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("TemplateWithSourceName");

            InstallRequest installRequest = new InstallRequest(Path.GetFullPath(templateLocation));

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.Single(result);
            Assert.True(result[0].Success);
            Assert.Equal(InstallerErrorCode.Success, result[0].Error);
            result[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.Equal(installRequest, result[0].InstallRequest);

            InstallRequest repeatedInstallRequest = new InstallRequest(Path.GetFullPath(templateLocation), force: true);

            result = await bootstrapper.InstallTemplatePackagesAsync(new[] { repeatedInstallRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.Single(result);
            Assert.True(result[0].Success);
            Assert.Equal(InstallerErrorCode.Success, result[0].Error);
            result[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.Equal(repeatedInstallRequest, result[0].InstallRequest);

            InstallRequest repeatedInstallRequestWithoutForce = new InstallRequest(Path.GetFullPath(templateLocation));

            result = await bootstrapper.InstallTemplatePackagesAsync(new[] { repeatedInstallRequestWithoutForce }, InstallationScope.Global, CancellationToken.None);

            Assert.Single(result);
            Assert.False(result[0].Success);
            Assert.Equal(InstallerErrorCode.AlreadyInstalled, result[0].Error);
        }

        internal class MountPointFactoryMock : IMountPointFactory
        {
            public Guid Id => Guid.Empty;

            public bool TryMount(IEngineEnvironmentSettings environmentSettings, IMountPoint? parent, string mountPointUri, out IMountPoint? mountPoint)
            {
                mountPoint = new MockMountPoint(environmentSettings);
                return true;
            }
        }
    }
}
