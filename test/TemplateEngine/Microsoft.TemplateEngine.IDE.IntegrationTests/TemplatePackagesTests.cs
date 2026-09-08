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
    [TestClass]
    public class TemplatePackagesTests : BootstrapperTestBase
    {
        private static PackageManager s_packageManager = null!;
        private const string ValidPackageJsonFile = /*lang=json,strict*/ """
{
    "Packages": [
        {
            "Details": {
                "PackageId": "Microsoft.DotNet.Common.ProjectTemplates.5.0",
                "Author": "Microsoft",
                "NuGetSource": "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json",
                "Version": "5.0.0"
            },
            "InstallerId": "015dcbac-b4a5-49ea-94a6-061616eb60e2",
            "LastChangeTime": "2023-04-13T15:17:16.4866397Z",
            "MountPointUri": "packages\\Microsoft.DotNet.Common.ProjectTemplates.5.0.5.0.0.nupkg"
        },
        {
            "Details": {
                "PackageId": "Microsoft.DotNet.Web.ProjectTemplates.10.0",
                "Author": "Microsoft",
                "NuGetSource": "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json",
                "Version": "10.0.0"
            },
            "InstallerId": "015dcbac-b4a5-49ea-94a6-061616eb60e2",
            "LastChangeTime": "2023-06-01T11:32:14.867341Z",
            "MountPointUri": "packages\\Microsoft.DotNet.Web.ProjectTemplates.10.0.10.0.0.nupkg"
        }
    ]
}
""";
        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_packageManager = new PackageManager();

        [ClassCleanup]
        public static void ClassCleanup() => s_packageManager?.Dispose();

        [TestMethod]
        public async Task CanInstall_LocalNuGetPackage()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string packageLocation = await s_packageManager.GetNuGetPackage("Microsoft.DotNet.Common.ProjectTemplates.5.0");

            InstallRequest installRequest = new InstallRequest(Path.GetFullPath(packageLocation));

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.ContainsSingle(result);
            Assert.IsTrue(result[0].Success);
            Assert.AreEqual(InstallerErrorCode.Success, result[0].Error);
            result[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.AreEqual(installRequest, result[0].InstallRequest);

            IManagedTemplatePackage? source = result[0].TemplatePackage;
            Assert.IsNotNull(source);
            Assert.AreEqual("Microsoft.DotNet.Common.ProjectTemplates.5.0", source!.Identifier);
            Assert.AreEqual("Global Settings", source.Provider.Factory.DisplayName);
            Assert.AreEqual("NuGet", source.Installer.Factory.Name);
            Assert.AreEqual("Microsoft", source.GetDetails()["Author"]);
            source.Version.Should().NotBeNullOrEmpty();

            IReadOnlyList<IManagedTemplatePackage> managedTemplatesPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None);

            Assert.ContainsSingle(managedTemplatesPackages);
            managedTemplatesPackages[0].Should().BeEquivalentTo(source);

            IReadOnlyList<ITemplatePackage> templatePackages = await bootstrapper.GetTemplatePackagesAsync(CancellationToken.None);

            Assert.ContainsSingle(templatePackages);
            Assert.IsInstanceOfType<IManagedTemplatePackage>(templatePackages[0]);
            templatePackages[0].Should().BeEquivalentTo((ITemplatePackage)source);
        }

        [TestMethod]
        public async Task CanInstall_RemoteNuGetPackage()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            InstallRequest installRequest = new InstallRequest(
                "Microsoft.Android.Templates",
                "36.1.69",
                details: new Dictionary<string, string>
                {
                    { InstallerConstants.NuGetSourcesKey, "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json" }
                });

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.ContainsSingle(result);
            Assert.IsTrue(result[0].Success);
            Assert.AreEqual(InstallerErrorCode.Success, result[0].Error);
            Assert.IsTrue(string.IsNullOrEmpty(result[0].ErrorMessage));
            Assert.AreEqual(installRequest, result[0].InstallRequest);

            IManagedTemplatePackage? source = result[0].TemplatePackage;
            Assert.IsNotNull(source);
            Assert.AreEqual("Microsoft.Android.Templates", source!.Identifier);
            Assert.AreEqual("Global Settings", source.Provider.Factory.DisplayName);
            Assert.AreEqual("NuGet", source.Installer.Factory.Name);
            source.GetDetails()["Author"].Should().NotBeNullOrEmpty();
            source.GetDetails()["NuGetSource"].Should().NotBeNullOrEmpty();
            Assert.AreEqual("36.1.69", source.Version);

            IReadOnlyList<IManagedTemplatePackage> managedTemplatesPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None);

            Assert.ContainsSingle(managedTemplatesPackages);
            managedTemplatesPackages[0].Should().BeEquivalentTo(source);

            IReadOnlyList<ITemplatePackage> templatePackages = await bootstrapper.GetTemplatePackagesAsync(CancellationToken.None);

            Assert.ContainsSingle(templatePackages);
            Assert.IsInstanceOfType<IManagedTemplatePackage>(templatePackages[0]);
            templatePackages[0].Should().BeEquivalentTo((ITemplatePackage)source);
        }

        [TestMethod]
        public async Task CanInstall_Folder()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("TemplateWithSourceName");

            InstallRequest installRequest = new InstallRequest(Path.GetFullPath(templateLocation));

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.ContainsSingle(result);
            Assert.IsTrue(result[0].Success);
            Assert.AreEqual(InstallerErrorCode.Success, result[0].Error);
            result[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.AreEqual(installRequest, result[0].InstallRequest);

            IManagedTemplatePackage? source = result[0].TemplatePackage;
            Assert.IsNotNull(source);
            Assert.AreEqual(Path.GetFullPath(templateLocation), source!.Identifier);
            Assert.AreEqual("Global Settings", source.Provider.Factory.DisplayName);
            Assert.AreEqual("Folder", source.Installer.Factory.Name);
            source.Version.Should().BeNullOrEmpty();

            IReadOnlyList<IManagedTemplatePackage> managedTemplatesPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None);

            Assert.ContainsSingle(managedTemplatesPackages);
            managedTemplatesPackages[0].Should().BeEquivalentTo(source);

            IReadOnlyList<ITemplatePackage> templatePackages = await bootstrapper.GetTemplatePackagesAsync(CancellationToken.None);

            Assert.ContainsSingle(templatePackages);
            Assert.IsInstanceOfType<IManagedTemplatePackage>(templatePackages[0]);
            templatePackages[0].Should().BeEquivalentTo((ITemplatePackage)source);
        }

        [TestMethod]
        public async Task CanCheckForLatestVersion_NuGetPackage()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            InstallRequest installRequest = new InstallRequest("Microsoft.DotNet.Common.ProjectTemplates.5.0", "5.0.0");

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.ContainsSingle(result);
            Assert.IsTrue(result[0].Success);
            IManagedTemplatePackage? source = result[0].TemplatePackage;
            Assert.IsNotNull(source);
            IReadOnlyList<CheckUpdateResult> checkUpdateResults = await bootstrapper.GetLatestVersionsAsync(new[] { source! }, CancellationToken.None);

            Assert.ContainsSingle(checkUpdateResults);
            Assert.IsTrue(checkUpdateResults[0].Success);
            Assert.AreEqual(InstallerErrorCode.Success, checkUpdateResults[0].Error);
            Assert.IsTrue(string.IsNullOrEmpty(checkUpdateResults[0].ErrorMessage));
            Assert.AreEqual(source, checkUpdateResults[0].TemplatePackage);
            Assert.IsFalse(checkUpdateResults[0].IsLatestVersion);
            Assert.AreNotEqual("5.0.0", checkUpdateResults[0].LatestVersion);
        }

        [TestMethod]
        public async Task CanCheckForLatestVersion_Folder()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("TemplateWithSourceName");

            InstallRequest installRequest = new InstallRequest(Path.GetFullPath(templateLocation));

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.ContainsSingle(result);
            Assert.IsTrue(result[0].Success);
            IManagedTemplatePackage? source = result[0].TemplatePackage;
            Assert.IsNotNull(source);
            IReadOnlyList<CheckUpdateResult> checkUpdateResults = await bootstrapper.GetLatestVersionsAsync(new[] { source! }, CancellationToken.None);

            Assert.ContainsSingle(checkUpdateResults);
            Assert.IsTrue(checkUpdateResults[0].Success);
            Assert.AreEqual(InstallerErrorCode.Success, checkUpdateResults[0].Error);
            Assert.IsTrue(string.IsNullOrEmpty(checkUpdateResults[0].ErrorMessage));
            Assert.AreEqual(source, checkUpdateResults[0].TemplatePackage);
            Assert.IsTrue(checkUpdateResults[0].IsLatestVersion);
        }

        [TestMethod]
        public async Task CanUpdateAllPackagesMetadataOnGetLatestVersion()
        {
            using Bootstrapper bootstrapper = GetBootstrapper(packageJsonContent: ValidPackageJsonFile);
            var installedPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None);

            // implicitly populates packages metadata
            await bootstrapper.GetLatestVersionsAsync(installedPackages, CancellationToken.None);

            var updatedPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None);

            Assert.HasCount(2, updatedPackages);
            var commonPackage = updatedPackages[0];
            Assert.AreEqual("5.0.0", commonPackage.Version);
            var commonPackageDetails = commonPackage.GetDetails();
            // dotnet-public feed may not return Owners metadata
            commonPackageDetails.TryGetValue("Owners", out var commonOwners);
            // Owners may be empty or populated depending on feed behavior
            Assert.IsFalse(bool.Parse(commonPackageDetails["Reserved"]));

            var webPackage = updatedPackages[1];
            Assert.AreEqual("10.0.0", webPackage.Version);
            var webPackageDetails = webPackage.GetDetails();
            // dotnet-public feed may not return Owners metadata
            webPackageDetails.TryGetValue("Owners", out var webOwners);
            Assert.IsFalse(bool.Parse(webPackageDetails["Reserved"]));
        }

        [TestMethod]
        public async Task CanUpdateSpecifiedPackageMetadataOnGetLatestVersion()
        {
            using Bootstrapper bootstrapper = GetBootstrapper(packageJsonContent: ValidPackageJsonFile);
            var installedPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None);
            var webTemplatePackage = installedPackages.FirstOrDefault(ip => ip.Identifier == "Microsoft.DotNet.Web.ProjectTemplates.10.0");

            // implicitly populates package metadata
            await bootstrapper.GetLatestVersionsAsync(new[] { webTemplatePackage! }, CancellationToken.None);

            var updatedPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None);
            Assert.HasCount(2, updatedPackages);
            var commonPackageDetails = updatedPackages[0].GetDetails();
            Assert.IsFalse(commonPackageDetails.TryGetValue("Owners", out var _));
            Assert.IsFalse(bool.Parse(commonPackageDetails["Reserved"]));

            // the specified package has updated metadata
            var webPackageDetails = updatedPackages[1].GetDetails();
            // dotnet-public feed may not return Owners for Microsoft packages
            webPackageDetails.TryGetValue("Owners", out var webOwners);
            Assert.IsFalse(bool.Parse(webPackageDetails["Reserved"]));
        }

        [TestMethod]
        public async Task CanUpdate_NuGetPackage()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            InstallRequest installRequest = new InstallRequest("Microsoft.DotNet.Common.ProjectTemplates.5.0", "5.0.0");

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.ContainsSingle(result);
            Assert.IsTrue(result[0].Success);
            IManagedTemplatePackage? source = result[0].TemplatePackage;
            Assert.IsNotNull(source);
            UpdateRequest updateRequest = new UpdateRequest(source!, "5.0.1");

            IReadOnlyList<UpdateResult> updateResults = await bootstrapper.UpdateTemplatePackagesAsync(new[] { updateRequest }, CancellationToken.None);

            Assert.ContainsSingle(updateResults);
            Assert.IsTrue(updateResults[0].Success);
            Assert.AreEqual(InstallerErrorCode.Success, updateResults[0].Error);
            Assert.IsTrue(string.IsNullOrEmpty(updateResults[0].ErrorMessage));
            Assert.AreEqual(updateRequest, updateResults[0].UpdateRequest);

            IManagedTemplatePackage? updatedSource = updateResults[0].TemplatePackage;
            Assert.IsNotNull(updatedSource);
            Assert.AreEqual("Global Settings", updatedSource!.Provider.Factory.DisplayName);
            Assert.AreEqual("NuGet", updatedSource.Installer.Factory.Name);
            Assert.AreEqual("5.0.1", updatedSource.Version);

            IReadOnlyList<IManagedTemplatePackage> managedTemplatesPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None);

            Assert.ContainsSingle(managedTemplatesPackages);
            managedTemplatesPackages[0].Should().BeEquivalentTo(updatedSource);

            IReadOnlyList<ITemplatePackage> templatePackages = await bootstrapper.GetTemplatePackagesAsync(CancellationToken.None);

            Assert.ContainsSingle(templatePackages);
            Assert.IsInstanceOfType<IManagedTemplatePackage>(templatePackages[0]);
            templatePackages[0].Should().BeEquivalentTo((ITemplatePackage)updatedSource);
        }

        [TestMethod]
        public async Task CanUninstall_NuGetPackage()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            InstallRequest installRequest = new InstallRequest("Microsoft.DotNet.Common.ProjectTemplates.5.0", "5.0.0");

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.ContainsSingle(result);
            Assert.IsTrue(result[0].Success);
            IManagedTemplatePackage? source = result[0].TemplatePackage;
            Assert.IsNotNull(source);
            IReadOnlyList<IManagedTemplatePackage> managedTemplatesPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None);

            Assert.ContainsSingle(managedTemplatesPackages);
            managedTemplatesPackages[0].Should().BeEquivalentTo(source);

            IReadOnlyList<UninstallResult> uninstallResults = await bootstrapper.UninstallTemplatePackagesAsync(new[] { source! }, CancellationToken.None);

            Assert.ContainsSingle(uninstallResults);
            Assert.IsTrue(uninstallResults[0].Success);
            Assert.AreEqual(InstallerErrorCode.Success, uninstallResults[0].Error);
            uninstallResults[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.AreEqual(source, uninstallResults[0].TemplatePackage);

            IReadOnlyList<ITemplatePackage> templatePackages = await bootstrapper.GetTemplatePackagesAsync(CancellationToken.None);

            Assert.IsEmpty(templatePackages);
        }

        [TestMethod]
        public async Task CanUninstall_Folder()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("TemplateWithSourceName");

            InstallRequest installRequest = new InstallRequest(Path.GetFullPath(templateLocation));

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.ContainsSingle(result);
            Assert.IsTrue(result[0].Success);
            IManagedTemplatePackage? source = result[0].TemplatePackage;
            Assert.IsNotNull(source);
            Assert.AreEqual(templateLocation, source!.MountPointUri);

            IReadOnlyList<IManagedTemplatePackage> managedTemplatesPackages = await bootstrapper.GetManagedTemplatePackagesAsync(CancellationToken.None);

            Assert.ContainsSingle(managedTemplatesPackages);
            managedTemplatesPackages[0].Should().BeEquivalentTo(source);

            IReadOnlyList<UninstallResult> uninstallResults = await bootstrapper.UninstallTemplatePackagesAsync(new[] { source }, CancellationToken.None);

            Assert.ContainsSingle(uninstallResults);
            Assert.IsTrue(uninstallResults[0].Success);
            Assert.AreEqual(InstallerErrorCode.Success, uninstallResults[0].Error);
            uninstallResults[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.AreEqual(source, uninstallResults[0].TemplatePackage);

            IReadOnlyList<ITemplatePackage> templatePackages = await bootstrapper.GetTemplatePackagesAsync(CancellationToken.None);

            Assert.IsEmpty(templatePackages);

            Directory.Exists(templateLocation);
        }

        [TestMethod]
        public async Task CanReInstallPackage_WhenForceIsSpecified()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string packageLocation = await s_packageManager.GetNuGetPackage("Microsoft.DotNet.Common.ProjectTemplates.5.0");

            InstallRequest installRequest = new InstallRequest(Path.GetFullPath(packageLocation));

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.ContainsSingle(result);
            Assert.IsTrue(result[0].Success);
            Assert.AreEqual(InstallerErrorCode.Success, result[0].Error);
            result[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.AreEqual(installRequest, result[0].InstallRequest);

            InstallRequest repeatedInstallRequest = new InstallRequest(Path.GetFullPath(packageLocation), force: true);

            result = await bootstrapper.InstallTemplatePackagesAsync(new[] { repeatedInstallRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.ContainsSingle(result);
            Assert.IsTrue(result[0].Success);
            Assert.AreEqual(InstallerErrorCode.Success, result[0].Error);
            result[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.AreEqual(repeatedInstallRequest, result[0].InstallRequest);

            InstallRequest repeatedInstallRequestWithoutForce = new InstallRequest(Path.GetFullPath(packageLocation));

            result = await bootstrapper.InstallTemplatePackagesAsync(new[] { repeatedInstallRequestWithoutForce }, InstallationScope.Global, CancellationToken.None);

            Assert.ContainsSingle(result);
            Assert.IsFalse(result[0].Success);
            Assert.AreEqual(InstallerErrorCode.DownloadFailed, result[0].Error);
        }

        [TestMethod]
        public async Task CanReInstallRemotePackage_WhenForceIsSpecified()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            InstallRequest installRequest = new InstallRequest("Microsoft.DotNet.Common.ProjectTemplates.5.0", version: "5.0.0");

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.ContainsSingle(result);
            Assert.IsTrue(result[0].Success);
            Assert.AreEqual(InstallerErrorCode.Success, result[0].Error);
            Assert.IsTrue(string.IsNullOrEmpty(result[0].ErrorMessage));
            Assert.AreEqual(installRequest, result[0].InstallRequest);

            InstallRequest repeatedInstallRequest = new InstallRequest("Microsoft.DotNet.Common.ProjectTemplates.5.0", version: "5.0.0", force: true);

            result = await bootstrapper.InstallTemplatePackagesAsync(new[] { repeatedInstallRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.ContainsSingle(result);
            Assert.IsTrue(result[0].Success);
            Assert.AreEqual(InstallerErrorCode.Success, result[0].Error);
            result[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.AreEqual(repeatedInstallRequest, result[0].InstallRequest);

            InstallRequest repeatedInstallRequestWithoutForce = new InstallRequest("Microsoft.DotNet.Common.ProjectTemplates.5.0", version: "5.0.0");

            result = await bootstrapper.InstallTemplatePackagesAsync(new[] { repeatedInstallRequestWithoutForce }, InstallationScope.Global, CancellationToken.None);

            Assert.ContainsSingle(result);
            Assert.IsFalse(result[0].Success);
            Assert.AreEqual(InstallerErrorCode.AlreadyInstalled, result[0].Error);
        }

        [TestMethod]
        public async Task CanReInstallFolder_WhenForceIsSpecified()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("TemplateWithSourceName");

            InstallRequest installRequest = new InstallRequest(Path.GetFullPath(templateLocation));

            IReadOnlyList<InstallResult> result = await bootstrapper.InstallTemplatePackagesAsync(new[] { installRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.ContainsSingle(result);
            Assert.IsTrue(result[0].Success);
            Assert.AreEqual(InstallerErrorCode.Success, result[0].Error);
            result[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.AreEqual(installRequest, result[0].InstallRequest);

            InstallRequest repeatedInstallRequest = new InstallRequest(Path.GetFullPath(templateLocation), force: true);

            result = await bootstrapper.InstallTemplatePackagesAsync(new[] { repeatedInstallRequest }, InstallationScope.Global, CancellationToken.None);

            Assert.ContainsSingle(result);
            Assert.IsTrue(result[0].Success);
            Assert.AreEqual(InstallerErrorCode.Success, result[0].Error);
            result[0].ErrorMessage.Should().BeNullOrEmpty();
            Assert.AreEqual(repeatedInstallRequest, result[0].InstallRequest);

            InstallRequest repeatedInstallRequestWithoutForce = new InstallRequest(Path.GetFullPath(templateLocation));

            result = await bootstrapper.InstallTemplatePackagesAsync(new[] { repeatedInstallRequestWithoutForce }, InstallationScope.Global, CancellationToken.None);

            Assert.ContainsSingle(result);
            Assert.IsFalse(result[0].Success);
            Assert.AreEqual(InstallerErrorCode.AlreadyInstalled, result[0].Error);
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
